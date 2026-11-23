"""Checks an xUnit.net v2+ XML result file written by a CI test step (-result-xml).

  check-results.py repeat <results.xml> <copies>
      Fails unless at least one test ran, none failed, and every test that ran is a
      '[copy k/<copies>]' of its spec (so Lamina:Repeat really expanded the run).

  check-results.py broken <results.xml> <test method>
      Fails unless exactly that one test ran and failed, and its message holds the
      last-observed state, the test ID and an alias (Plan.md §10, criterion 2).
"""

import re
import sys
import xml.etree.ElementTree as ET


def fail(message):
    print(f"::error::{message}")
    sys.exit(1)


def tests_that_ran(path):
    return [t for t in ET.parse(path).getroot().iter("test") if t.get("result") != "NotRun"]


def check_repeat(path, copies):
    ran = tests_that_ran(path)
    if not ran:
        fail(f"No spec ran ({path}).")
    failed = [t.get("name") for t in ran if t.get("result") != "Pass"]
    if failed:
        fail(f"{len(failed)} of {len(ran)} spec copies did not pass, first: {failed[0]}")
    not_copies = [t.get("name") for t in ran if not re.search(rf" \[copy \d+/{copies}\]$", t.get("name"))]
    if not_copies:
        fail(f"Expected every spec as '[copy k/{copies}]', but '{not_copies[0]}' is not.")
    print(f"{len(ran)} spec copies passed, each spec repeated {copies} times.")


def check_broken(path, method):
    ran = tests_that_ran(path)
    if [t.get("name") for t in ran] != [method]:
        fail(f"Expected only '{method}' to run, but ran: {[t.get('name') for t in ran]}")
    test = ran[0]
    if test.get("result") != "Fail":
        fail(f"'{method}' should fail on purpose, but its result was {test.get('result')}.")

    message = test.findtext("failure/message") or ""
    footer = re.search(r"Test (\w+) on '[^']+' · aliases: (.+)", message)
    missing = []
    if "Last observed" not in message:
        missing.append("the last-observed state")
    if footer is None:
        missing.append("the test ID")
    elif not re.search(rf"\S+ -> \S+-{footer.group(1)}\b", footer.group(2)):
        missing.append("an alias")
    if missing:
        fail(f"The Broken spec's message lacks {', '.join(missing)}:\n{message}")
    print(f"The Broken spec failed as intended, with the last-observed state, test ID {footer.group(1)} and its aliases.")


if __name__ == "__main__":
    if len(sys.argv) != 4 or sys.argv[1] not in ("repeat", "broken"):
        fail(__doc__)
    {"repeat": check_repeat, "broken": check_broken}[sys.argv[1]](sys.argv[2], sys.argv[3])
