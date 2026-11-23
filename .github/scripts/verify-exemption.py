"""Says whether Verify's maintenance-fee exemption needs renewing soon (#49).

  verify-exemption.py <Directory.Build.props> [today as yyyy-MM-dd]

Reads Verify_SponsorshipExemptionUntil (yyyy-MM, the last covered month) and writes
until=, expires= (the first day after the covered month), days_left= and due= to
$GITHUB_OUTPUT when set, or to stdout. due is true from RENEW_WITHIN_DAYS before expiry,
which gives the monthly schedule two runs to raise it. Fails if the property is missing or
not yyyy-MM, so a broken check is noticed rather than staying quiet.
"""

import datetime
import os
import re
import sys
import xml.etree.ElementTree as ET

PROPERTY = "Verify_SponsorshipExemptionUntil"
RENEW_WITHIN_DAYS = 62


def fail(message):
    print(f"::error::{message}")
    sys.exit(1)


def main(args):
    if len(args) not in (1, 2):
        fail("usage: verify-exemption.py <Directory.Build.props> [yyyy-MM-dd]")

    values = [e.text or "" for e in ET.parse(args[0]).getroot().iter(PROPERTY)]
    if len(values) != 1:
        fail(f"expected one {PROPERTY} in {args[0]}, found {len(values)}")
    until = values[0].strip()
    match = re.fullmatch(r"(\d{4})-(\d{2})", until)
    if not match or not 1 <= int(match[2]) <= 12:
        fail(f"{PROPERTY} is '{until}', not yyyy-MM")

    year, month = int(match[1]), int(match[2])
    expires = datetime.date(year + month // 12, month % 12 + 1, 1)
    today = datetime.date.fromisoformat(args[1]) if len(args) == 2 else datetime.date.today()
    days_left = (expires - today).days
    due = days_left <= RENEW_WITHIN_DAYS

    lines = [f"until={until}", f"expires={expires}", f"days_left={days_left}", f"due={str(due).lower()}"]
    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
    print("\n".join(lines))
    if days_left <= 0:
        print(f"::error::Verify's OpenSource exemption ran out on {expires}; builds fail with SC021.")
    elif due:
        print(f"::warning::Verify's OpenSource exemption runs out on {expires} ({days_left} days).")


if __name__ == "__main__":
    main(sys.argv[1:])
