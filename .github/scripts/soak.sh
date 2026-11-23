#!/usr/bin/env bash
# The soak (Plan.md §10, criterion 1): runs the specs project <runs> times in a row with
# Lamina:Repeat=<copies>, each run starting its own system under test, and stops at the first
# run that fails.
#
#   soak.sh <project> <runs> <copies>
#
# Each run writes its results and logs (see run-xunit.sh) to $TEST_RESULTS/run-<n>. A run that
# passes has its folder deleted, so after a failure the folder left behind is the failing run's,
# with the Docker containers' state and logs and DCP's session folders added. Writes "N/<runs>
# passed" and a line per run to $GITHUB_STEP_SUMMARY (when set) and the console. Exits 0 only
# if every run passed. Expects the project to be built in Release already.
set -uo pipefail

project=$1
runs=$2
copies=$3
results=${TEST_RESULTS:?TEST_RESULTS must name the results directory}
summary=${GITHUB_STEP_SUMMARY:-/dev/null}
scripts=$(cd "$(dirname "$0")" && pwd)

collect_failure_logs() {
  local dir=$1 started=$2
  mkdir -p "$dir/containers"
  docker ps -a > "$dir/containers/ps.txt" 2>&1
  for container in $(docker ps -aq); do
    name=$(docker inspect --format '{{.Name}}' "$container" | tr -d /)
    docker logs "$container" > "$dir/containers/$name.log" 2>&1
  done
  # Sut.Aspire's DCP keeps each service's console output in a session folder, which outlives
  # the run. Copy only the folders this run created.
  mkdir -p "$dir/dcp"
  find "${TMPDIR:-/tmp}" -maxdepth 1 -name 'aspire-dcp*' -newer "$started" \
    -exec cp -r {} "$dir/dcp/" \; 2>/dev/null || true
}

passed=0
lines=()
for ((n = 1; n <= runs; n++)); do
  dir="$results/run-$n"
  echo "::group::Run $n of $runs"
  mkdir -p "$dir"
  touch "$dir/.started"
  started=$SECONDS
  status=0
  TEST_RESULTS=$dir Lamina__Repeat=$copies \
    bash "$scripts/run-xunit.sh" "$project" commerce || status=$?
  if [ "$status" -eq 0 ]; then
    python3 "$scripts/check-results.py" repeat "$dir/commerce.xml" "$copies" || status=$?
  fi
  took=$((SECONDS - started))
  echo "::endgroup::"

  if [ "$status" -ne 0 ]; then
    collect_failure_logs "$dir" "$dir/.started"
    lines+=("- Run $n: **failed** after ${took} s. Its results and logs are in the artifact, under \`run-$n\`.")
    echo "::error::Soak run $n of $runs failed. Stopping."
    break
  fi
  passed=$((passed + 1))
  lines+=("- Run $n: passed in ${took} s.")
  rm -rf "$dir"
done

{
  echo "## $passed/$runs passed"
  echo
  echo "Each run: the specs in \`$project\` with \`Lamina__Repeat=$copies\` through Sut.Aspire."
  echo
  printf '%s\n' "${lines[@]}"
} | tee -a "$summary"

[ "$passed" -eq "$runs" ]
