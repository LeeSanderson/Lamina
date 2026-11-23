#!/usr/bin/env bash
# Runs an xUnit v3 test project through its own runner (dotnet run), not dotnet test: under
# Microsoft.Testing.Platform, dotnet test drops xUnit diagnostic messages, which carry Lamina's
# run-start line (provider, endpoints, time to ready) and teardown line.
#
#   run-xunit.sh <project> <name> [runner args...]
#
# Writes to $TEST_RESULTS: <name>.trx and <name>.xml (results), <name>.log (the runner's
# output, also shown on the console) and <name>.host.log (everything logged through
# Microsoft.Extensions.Logging, including the Aspire resource logs Sut.Aspire forwards).
# The logs go to stderr so they can't interleave with the runner's lines; under
# Lamina:Repeat=50 they run to hundreds of thousands of lines. Services an Aspire AppHost
# starts inherit the setting too, so their resource log lines are forwarded at error level
# ("fail:"); the level says which stream a line came from, not that anything failed.
# Exits with the runner's exit code. Expects the project to be built in Release already.
set -uo pipefail

project=$1
name=$2
shift 2
results=${TEST_RESULTS:?TEST_RESULTS must name the results directory}
mkdir -p "$results"

Logging__Console__LogToStandardErrorThreshold=Trace \
  dotnet run --project "$project" -c Release --no-build -- \
  -noColor -result-trx "$results/$name.trx" -result-xml "$results/$name.xml" "$@" \
  2> "$results/$name.host.log" \
  | tee "$results/$name.log"
status=${PIPESTATUS[0]}

if [ "$status" -ne 0 ]; then
  echo "::group::Last 200 lines of $name.host.log"
  tail -n 200 "$results/$name.host.log"
  echo "::endgroup::"
fi
exit "$status"
