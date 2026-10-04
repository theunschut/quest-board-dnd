#!/usr/bin/env bash
# Static structural guard over the release workflow and every workflow's runner selection.
# Prints PASS/FAIL per assertion and exits non-zero if any assertion fails.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

python3 - <<'PY'
import glob
import os
import re
import sys

import yaml

failures = 0


def check(desc, ok):
    global failures
    if ok:
        print("PASS: " + desc)
    else:
        print("FAIL: " + desc)
        failures += 1


path = ".github/workflows/release.yml"
check("release.yml exists", os.path.isfile(path))
if not os.path.isfile(path):
    sys.exit(1)

with open(path, encoding="utf-8") as fh:
    d = yaml.safe_load(fh)

# PyYAML parses the bare key `on` as boolean True.
on = d.get("on", d.get(True))
jobs = d.get("jobs", {})
build = jobs.get("build", {})
publish = jobs.get("publish", {})
build_steps = build.get("steps", [])
publish_steps = publish.get("steps", [])

check("only trigger is push of strict version tags",
      isinstance(on, dict) and list(on.keys()) == ["push"]
      and on["push"] == {"tags": ["v[0-9]+.[0-9]+.[0-9]+"]})
check("workflow-level permissions are empty", d.get("permissions") == {})
check("concurrency does not cancel in progress",
      d.get("concurrency", {}).get("cancel-in-progress") is False)
check("jobs are exactly build and publish", sorted(jobs.keys()) == ["build", "publish"])
check("both jobs run on ubuntu-24.04",
      build.get("runs-on") == "ubuntu-24.04" and publish.get("runs-on") == "ubuntu-24.04")
check("build permissions are exactly contents/id-token/attestations write",
      build.get("permissions") == {"contents": "write", "id-token": "write", "attestations": "write"})
check("publish permissions are exactly contents write",
      publish.get("permissions") == {"contents": "write"})
check("publish uses the deploy environment", publish.get("environment") == "deploy")
check("publish needs build", publish.get("needs") == "build")

all_steps = build_steps + publish_steps
uses = [s["uses"] for s in all_steps if "uses" in s]
check("every uses: is pinned to a 40-hex SHA",
      len(uses) > 0 and all(re.search(r"@[0-9a-f]{40}(\s|$)", u) for u in uses))
checkouts = [s for s in all_steps if str(s.get("uses", "")).startswith("actions/checkout@")]
check("every checkout sets persist-credentials false",
      len(checkouts) > 0 and all(s.get("with", {}).get("persist-credentials") is False for s in checkouts))
check("no run: contains a ${{ expression",
      all("${{" not in s["run"] for s in all_steps if "run" in s))


def index_of(pred):
    for i, s in enumerate(build_steps):
        if pred(s):
            return i
    return -1


test_idx = index_of(lambda s: "dotnet test" in s.get("run", ""))
package_idx = index_of(lambda s: "package-release.sh" in s.get("run", ""))
attest_idx = index_of(lambda s: str(s.get("uses", "")).startswith("actions/attest-build-provenance@"))
check("dotnet test step is present", test_idx >= 0)
check("dotnet test runs before the attest step", 0 <= test_idx < attest_idx)
check("packaging runs before the attest step", 0 <= package_idx < attest_idx)

test_step = build_steps[test_idx] if test_idx >= 0 else {}
check("test step requires the SQL migrator tests",
      str(test_step.get("env", {}).get("QUESTBOARD_MIGRATOR_TEST_REQUIRED")) == "1")

draft = next((s for s in build_steps if "gh release create" in s.get("run", "")), {})
draft_run = draft.get("run", "")
check("draft step creates a draft and verifies the tag",
      "--draft" in draft_run and "--verify-tag" in draft_run)
check("draft step refuses to overwrite a published release",
      "already exists and is published; refusing to overwrite it" in draft_run)

publish_runs = "\n".join(s.get("run", "") for s in publish_steps)
for needle in ["sha256sum -c", "--deny-self-hosted-runners", "--signer-workflow",
               "--source-ref", "--draft=false", "--latest"]:
    check("publish contains " + needle, needle in publish_runs)

check("binary-release.yml does not exist", not os.path.exists(".github/workflows/binary-release.yml"))


def runs_on_values(value):
    if isinstance(value, str):
        return [value]
    if isinstance(value, list):
        return [str(v) for v in value]
    if isinstance(value, dict):
        return [str(v) for v in value.values()]
    return []


offenders = []
for wf in sorted(glob.glob(".github/workflows/*.yml") + glob.glob(".github/workflows/*.yaml")):
    with open(wf, encoding="utf-8") as fh:
        doc = yaml.safe_load(fh) or {}
    for name, job in (doc.get("jobs") or {}).items():
        values = runs_on_values((job or {}).get("runs-on"))
        if any("self-hosted" in v for v in values):
            offenders.append(wf + ":" + name)
check("no workflow job targets a self-hosted runner" + (" (" + ", ".join(offenders) + ")" if offenders else ""),
      not offenders)

sys.exit(1 if failures else 0)
PY
