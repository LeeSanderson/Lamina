// Throwaway measurement for issue #54 (Kafka probe mechanics). Not product code.
// Usage: dotnet run -- <bootstrap> <section...>   sections: create assign end subscribe times stamps headers retention
using System.Diagnostics;
using System.Text;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var bootstrap = args[0];
var sections = args.Skip(1).ToHashSet();
var trials = 10;

using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrap }).Build();
using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrap, LingerMs = 0 }).Build();

async Task<string> CreateTopic(int partitions = 3, Dictionary<string, string>? configs = null)
{
    var name = "t-" + Guid.NewGuid().ToString("N")[..8];
    var sw = Stopwatch.StartNew();
    await admin.CreateTopicsAsync([new TopicSpecification { Name = name, NumPartitions = partitions, ReplicationFactor = 1, Configs = configs }]);
    Log($"create-topic {name} p={partitions} {sw.ElapsedMilliseconds} ms");
    // Wait until a fresh consumer can query every partition's watermarks.
    sw.Restart(); var attempts = 0;
    using (var probe = new ConsumerBuilder<Ignore, Ignore>(new ConsumerConfig { BootstrapServers = bootstrap, GroupId = "wait-" + Guid.NewGuid().ToString("N") }).Build())
    {
        while (true)
        {
            attempts++;
            try { for (var p = 0; p < partitions; p++) probe.QueryWatermarkOffsets(new TopicPartition(name, p), TimeSpan.FromSeconds(5)); break; }
            catch (KafkaException) when (sw.Elapsed < TimeSpan.FromSeconds(30)) { await Task.Delay(20); }
        }
    }
    Log($"  watermarks queryable after {sw.ElapsedMilliseconds} ms ({attempts} attempts)");
    return name;
}

static void Log(string s) => Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} {s}");

static double Median(List<double> xs) { xs.Sort(); return xs[xs.Count / 2]; }
static string Stats(List<double> xs) => $"min {xs.Min():F0} / median {Median([.. xs]):F0} / max {xs.Max():F0} ms (n={xs.Count})";

ConsumerConfig ConsumerConfig(string? group = null) => new()
{
    BootstrapServers = bootstrap,
    GroupId = group ?? "lamina-probe-" + Guid.NewGuid().ToString("N"), // required by Confluent.Kafka even for Assign

    EnableAutoCommit = false,
    AutoOffsetReset = AutoOffsetReset.Latest,
    FetchWaitMaxMs = 100,
};

ConsumeResult<string, string>? ConsumeUntil(IConsumer<string, string> c, string key, TimeSpan timeout)
{
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < timeout)
    {
        var r = c.Consume(TimeSpan.FromMilliseconds(50));
        if (r?.Message?.Key == key) return r;
    }
    return null;
}

if (sections.Contains("create"))
{
    for (var i = 0; i < 3; i++) await CreateTopic();
    // Producer auto-create: produce to a topic that doesn't exist.
    var auto = "auto-" + Guid.NewGuid().ToString("N")[..8];
    var sw = Stopwatch.StartNew();
    var dr = await producer.ProduceAsync(auto, new Message<string, string> { Key = "k", Value = "v" });
    Log($"auto-create by produce: {dr.Status} partition={dr.Partition} {sw.ElapsedMilliseconds} ms");
    var md = admin.GetMetadata(auto, TimeSpan.FromSeconds(5));
    Log($"auto-created topic partitions: {md.Topics[0].Partitions.Count}");
    // Consumer default allow.auto.create.topics=false: assign to a missing topic.
    var missing = "missing-" + Guid.NewGuid().ToString("N")[..8];
    using var c = new ConsumerBuilder<string, string>(ConsumerConfig()).SetErrorHandler((_, e) => Log($"consumer error: {e.Code} {e.Reason}")).Build();
    c.Subscribe(missing);
    try { c.Consume(TimeSpan.FromSeconds(3)); } catch (ConsumeException e) { Log($"subscribe missing topic -> ConsumeException {e.Error.Code}"); }
    catch (Exception e) { Log($"subscribe missing (no group) -> {e.GetType().Name}: {e.Message}"); }
    var exists = admin.GetMetadata(TimeSpan.FromSeconds(5)).Topics.Any(t => t.Topic == missing);
    Log($"missing topic created by consumer: {exists}");
}

if (sections.Contains("assign"))
{
    // Per-spec probe: no group.id, snapshot end offsets, assign explicitly, produce, time to first message.
    var topic = await CreateTopic();
    var build = new List<double>(); var snap = new List<double>(); var first = new List<double>(); var total = new List<double>();
    for (var i = 0; i < trials; i++)
    {
        var sw = Stopwatch.StartNew();
        using var c = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
        build.Add(sw.Elapsed.TotalMilliseconds);
        var t1 = sw.Elapsed;
        var parts = Enumerable.Range(0, 3).Select(p => new TopicPartition(topic, p)).ToList();
        var starts = parts.Select(tp => new TopicPartitionOffset(tp, c.QueryWatermarkOffsets(tp, TimeSpan.FromSeconds(5)).High)).ToList();
        snap.Add((sw.Elapsed - t1).TotalMilliseconds);
        c.Assign(starts);
        var key = Guid.NewGuid().ToString();
        var t2 = sw.Elapsed;
        await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "x" });
        var r = ConsumeUntil(c, key, TimeSpan.FromSeconds(10));
        first.Add((sw.Elapsed - t2).TotalMilliseconds);
        total.Add(sw.Elapsed.TotalMilliseconds);
        if (r is null) Log("assign: MISSED");
        c.Close();
    }
    Log($"assign: build {Stats(build)}");
    Log($"assign: watermark snapshot (3 partitions) {Stats(snap)}");
    Log($"assign: produce->consumed {Stats(first)}");
    Log($"assign: total new-consumer->message {Stats(total)}");

    // Long-lived consumer: steady-state produce->consume latency.
    using var lc = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
    lc.Assign(Enumerable.Range(0, 3).Select(p => new TopicPartitionOffset(topic, p, Offset.End)));
    lc.Consume(TimeSpan.FromSeconds(1));
    var steady = new List<double>();
    for (var i = 0; i < trials; i++)
    {
        var key = Guid.NewGuid().ToString(); var sw = Stopwatch.StartNew();
        await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "x" });
        ConsumeUntil(lc, key, TimeSpan.FromSeconds(10)); steady.Add(sw.Elapsed.TotalMilliseconds);
    }
    Log($"assign long-lived: produce->consumed {Stats(steady)}");
}

if (sections.Contains("end"))
{
    // Race: assign at Offset.End (resolved lazily) then produce at once. Is the message missed?
    var topic = await CreateTopic(1);
    var missed = 0;
    for (var i = 0; i < 20; i++)
    {
        using var c = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
        c.Assign(new TopicPartitionOffset(topic, 0, Offset.End));
        var key = Guid.NewGuid().ToString();
        await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "x" });
        if (ConsumeUntil(c, key, TimeSpan.FromSeconds(3)) is null) missed++;
        c.Close();
    }
    Log($"Offset.End then immediate produce: missed {missed}/20");
}

if (sections.Contains("subscribe"))
{
    foreach (var protocol in new[] { GroupProtocol.Classic, GroupProtocol.Consumer })
    {
        var topic = await CreateTopic();
        var assigned = new List<double>(); var first = new List<double>(); var missedEarly = 0;
        for (var i = 0; i < 5; i++)
        {
            var cfg = ConsumerConfig("g-" + Guid.NewGuid().ToString("N"));
            cfg.GroupProtocol = protocol;
            if (protocol == GroupProtocol.Classic) { cfg.SessionTimeoutMs = 6000; cfg.HeartbeatIntervalMs = 500; }
            var pin = Environment.GetEnvironmentVariable("PIN") == "1";
            Dictionary<int, long> snapshot;
            using (var q = new ConsumerBuilder<Ignore, Ignore>(ConsumerConfig()).Build()) snapshot = Enumerable.Range(0, 3).ToDictionary(p => p, p => q.QueryWatermarkOffsets(new TopicPartition(topic, p), TimeSpan.FromSeconds(5)).High.Value);
            var sw = Stopwatch.StartNew(); TimeSpan? at = null;
            using var c = new ConsumerBuilder<string, string>(cfg)
                .SetPartitionsAssignedHandler((_, ps) => { if (ps.Count > 0) at ??= sw.Elapsed; return ps.Select(tp => pin ? new TopicPartitionOffset(tp, snapshot[tp.Partition.Value]) : new TopicPartitionOffset(tp, Offset.Unset)); })
                .Build();
            c.Subscribe(topic);
            // Produce a message straight after Subscribe: with auto.offset.reset=latest it is lost if it lands before the first fetch position.
            var early = Guid.NewGuid().ToString();
            await producer.ProduceAsync(topic, new Message<string, string> { Key = early, Value = "x" });
            while (at is null && sw.Elapsed < TimeSpan.FromSeconds(30)) c.Consume(TimeSpan.FromMilliseconds(50));
            assigned.Add(at?.TotalMilliseconds ?? double.NaN);
            var key = Guid.NewGuid().ToString(); var t2 = sw.Elapsed;
            await producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "x" });
            var sawEarly = false; ConsumeResult<string, string>? r = null; var sw2 = Stopwatch.StartNew();
            while (sw2.Elapsed < TimeSpan.FromSeconds(10)) { var x = c.Consume(TimeSpan.FromMilliseconds(50)); if (x?.Message.Key == early) sawEarly = true; if (x?.Message.Key == key) { r = x; break; } }
            if (!sawEarly) missedEarly++;
            first.Add((sw.Elapsed - t2).TotalMilliseconds);
            c.Close();
        }
        Log($"subscribe {protocol}: subscribe->assigned {Stats(assigned)}");
        Log($"subscribe {protocol}: produce->consumed after assignment {Stats(first)}");
        Log($"subscribe {protocol}: message produced right after Subscribe missed {missedEarly}/5");
    }
}

if (sections.Contains("times"))
{
    var topic = await CreateTopic(3);
    await producer.ProduceAsync(topic, new Message<string, string> { Key = "a", Value = "x" });
    var runStart = DateTime.UtcNow; await Task.Delay(50);
    await producer.ProduceAsync(new TopicPartition(topic, 0), new Message<string, string> { Key = "b", Value = "x" });
    // Backdated record after run start, on partition 0.
    await producer.ProduceAsync(new TopicPartition(topic, 0), new Message<string, string> { Key = "old", Value = "x", Timestamp = new Timestamp(DateTime.UtcNow.AddDays(-730)) });
    using var c = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
    var sw = Stopwatch.StartNew();
    var offs = c.OffsetsForTimes(Enumerable.Range(0, 3).Select(p => new TopicPartitionTimestamp(topic, p, new Timestamp(runStart))), TimeSpan.FromSeconds(5));
    Log($"OffsetsForTimes (3 partitions) {sw.ElapsedMilliseconds} ms: {string.Join(", ", offs.Select(o => $"p{o.Partition.Value}={o.Offset}"))}");
    sw.Restart();
    var marks = Enumerable.Range(0, 3).Select(p => c.QueryWatermarkOffsets(new TopicPartition(topic, p), TimeSpan.FromSeconds(5)).High.Value).ToList();
    Log($"QueryWatermarkOffsets x3 {sw.ElapsedMilliseconds} ms: {string.Join(", ", marks)}");
    c.Assign(offs.Select(o => o.Offset == Offset.End ? new TopicPartitionOffset(o.TopicPartition, Offset.End) : o));
    var seen = new List<string>(); var sw2 = Stopwatch.StartNew();
    while (sw2.Elapsed < TimeSpan.FromSeconds(3)) { var r = c.Consume(TimeSpan.FromMilliseconds(100)); if (r?.Message != null) seen.Add(r.Message.Key); }
    Log($"consumed from OffsetsForTimes(runStart): [{string.Join(",", seen)}]  (backdated 'old' after run start included? {seen.Contains("old")})");
}

if (sections.Contains("stamps"))
{
    var topic = await CreateTopic(1);
    foreach (var (label, ts) in new[] { ("-730d", DateTime.UtcNow.AddDays(-730)), ("+30m", DateTime.UtcNow.AddMinutes(30)), ("+2h", DateTime.UtcNow.AddHours(2)), ("none", (DateTime?)null) }.Select(x => (x.Item1, (DateTime?)x.Item2)))
    {
        var m = new Message<string, string> { Key = label, Value = "x", Headers = [new Header("occurred-at", Encoding.UTF8.GetBytes((ts ?? DateTime.UtcNow).ToString("O")))] };
        if (ts is { } t) m.Timestamp = new Timestamp(t);
        try { var dr = await producer.ProduceAsync(topic, m); Log($"produce ts {label}: {dr.Status} stored {dr.Timestamp.Type} {dr.Timestamp.UtcDateTime:O}"); }
        catch (ProduceException<string, string> e) { Log($"produce ts {label}: REJECTED {e.Error.Code} '{e.Error.Reason}'"); }
    }
    try { await producer.ProduceAsync(topic, new Message<string, string> { Key = "lat", Value = "x", Timestamp = new Timestamp(DateTime.UtcNow, TimestampType.LogAppendTime) }); }
    catch (Exception e) { Log($"produce with TimestampType.LogAppendTime: {e.GetType().Name}: {e.Message}"); }
    // LogAppendTime topic: broker overwrites the timestamp.
    var lat = await CreateTopic(1, new() { ["message.timestamp.type"] = "LogAppendTime" });
    var d2 = await producer.ProduceAsync(lat, new Message<string, string> { Key = "k", Value = "x", Timestamp = new Timestamp(DateTime.UtcNow.AddDays(-730)) });
    Log($"LogAppendTime topic, -730d produce: stored {d2.Timestamp.Type} {d2.Timestamp.UtcDateTime:O}");
    using var c = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
    c.Assign(new TopicPartitionOffset(lat, 0, Offset.Beginning));
    var r = c.Consume(TimeSpan.FromSeconds(5));
    Log($"LogAppendTime topic consumed: {r?.Message.Timestamp.Type} {r?.Message.Timestamp.UtcDateTime:O}");
}

if (sections.Contains("retention"))
{
    // Broker started with log.retention.check.interval.ms=5000. Default retention.ms (7 days).
    var onlyOld = await CreateTopic(1);
    var mixed = await CreateTopic(1);
    await producer.ProduceAsync(onlyOld, new Message<string, string> { Key = "old", Value = "x", Timestamp = new Timestamp(DateTime.UtcNow.AddDays(-730)) });
    await producer.ProduceAsync(mixed, new Message<string, string> { Key = "old", Value = "x", Timestamp = new Timestamp(DateTime.UtcNow.AddDays(-730)) });
    await producer.ProduceAsync(mixed, new Message<string, string> { Key = "now", Value = "x" });
    var noRet = await CreateTopic(1, new() { ["retention.ms"] = "-1" });
    await producer.ProduceAsync(noRet, new Message<string, string> { Key = "old", Value = "x", Timestamp = new Timestamp(DateTime.UtcNow.AddDays(-730)) });
    using var c = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
    for (var s = 0; s <= 30; s += 5)
    {
        string W(string t) { var w = c.QueryWatermarkOffsets(new TopicPartition(t, 0), TimeSpan.FromSeconds(5)); return $"[{w.Low},{w.High})"; }
        Log($"t+{s}s backdated-only {W(onlyOld)}  mixed {W(mixed)}  backdated-only retention.ms=-1 {W(noRet)}");
        await Task.Delay(5000);
    }
}

if (sections.Contains("headers"))
{
    var topic = await CreateTopic(1);
    // Aspire producer with OpenTelemetry tracing, as in a SUT service.
    var hb = Host.CreateApplicationBuilder();
    hb.Configuration["ConnectionStrings:kafka"] = bootstrap;
    hb.AddKafkaProducer<string, string>("kafka");
    hb.Services.AddOpenTelemetry().WithTracing(t => t.AddSource("Bench"));
    using var host = hb.Build();
    await host.StartAsync();
    var aspire = host.Services.GetRequiredService<IProducer<string, string>>();
    var source = new ActivitySource("Bench");

    async Task Send(string key, Action setup)
    {
        await Task.Run(async () =>
        {
            setup();
            await aspire.ProduceAsync(topic, new Message<string, string> { Key = key, Value = "x" });
        });
    }
    await Send("activity-baggage-only", () => { var a = source.StartActivity("req"); a?.AddBaggage("lamina.test", "A1"); });
    await Send("otel-baggage-current", () => { source.StartActivity("req"); Baggage.SetBaggage("lamina.test", "B2"); });
    await Send("otel-baggage-encoded", () => { source.StartActivity("req"); Baggage.SetBaggage("lamina.test", "C 3,x=y"); Baggage.SetBaggage("lamina.offset", "-P400D"); });
    await Send("no-activity-no-baggage", () => { });
    // A test-side producer writing both spellings and two baggage headers.
    await producer.ProduceAsync(topic, new Message<string, string> { Key = "test-side", Value = "x", Headers = [
        new Header("baggage", Encoding.UTF8.GetBytes("other=1")),
        new Header("baggage", Encoding.UTF8.GetBytes("lamina.test = D4 , lamina.offset = P30D")) ] });

    var w3c = DistributedContextPropagator.CreateW3CPropagator();
    using var c = new ConsumerBuilder<string, string>(ConsumerConfig()).Build();
    c.Assign(new TopicPartitionOffset(topic, 0, Offset.Beginning));
    for (var i = 0; i < 5; i++)
    {
        var r = c.Consume(TimeSpan.FromSeconds(5));
        if (r is null) break;
        var hs = r.Message.Headers.Select(h => $"{h.Key}: {Encoding.UTF8.GetString(h.GetValueBytes())}");
        // Getter: join every baggage header with ',' (W3C allows several headers to be combined that way).
        var parsed = w3c.ExtractBaggage(r.Message.Headers, static (carrier, field, out value, out values) =>
        {
            values = null;
            var all = ((Headers)carrier!).Where(h => h.Key == field).Select(h => Encoding.UTF8.GetString(h.GetValueBytes())).ToList();
            value = all.Count == 0 ? null : string.Join(",", all);
        });
        Log($"{r.Message.Key}: headers [{string.Join(" | ", hs)}] => parsed [{string.Join(", ", parsed?.Select(p => $"{p.Key}='{p.Value}'") ?? [])}]");
    }
    await host.StopAsync();
}
