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
check("both jobs run on ubuntu-26.04",
      build.get("runs-on") == "ubuntu-26.04" and publish.get("runs-on") == "ubuntu-26.04")
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
floating = []
for wf in sorted(glob.glob(".github/workflows/*.yml") + glob.glob(".github/workflows/*.yaml")):
    with open(wf, encoding="utf-8") as fh:
        doc = yaml.safe_load(fh) or {}
    for name, job in (doc.get("jobs") or {}).items():
        values = runs_on_values((job or {}).get("runs-on"))
        if any("self-hosted" in v for v in values):
            offenders.append(wf + ":" + name)
        # A "-latest" label moves to a new OS on GitHub's schedule, so a job's environment could
        # change with no commit in this repository. Every job names its OS version instead.
        if any(v.endswith("-latest") for v in values):
            floating.append(wf + ":" + name)
check("no workflow job targets a self-hosted runner" + (" (" + ", ".join(offenders) + ")" if offenders else ""),
      not offenders)
check("no workflow job floats on a -latest runner label" + (" (" + ", ".join(floating) + ")" if floating else ""),
      not floating)

# --- dotnet.yml: required jobs, pinning and the scripts both workflows call ---

import shlex

dotnet_path = ".github/workflows/dotnet.yml"
check("dotnet.yml exists", os.path.isfile(dotnet_path))
dotnet_jobs = {}
if os.path.isfile(dotnet_path):
    with open(dotnet_path, encoding="utf-8") as fh:
        dotnet_jobs = (yaml.safe_load(fh) or {}).get("jobs", {}) or {}

required_jobs = ["build", "migrator-sql", "deploy-scripts", "workflow-lint", "network-verify"]
missing_jobs = [j for j in required_jobs if j not in dotnet_jobs]
check("dotnet.yml has the jobs " + ", ".join(required_jobs)
      + (" (missing: " + ", ".join(missing_jobs) + ")" if missing_jobs else ""),
      not missing_jobs)

# The original build job keeps its tag-pinned actions, so only the later jobs are held to SHA pins.
pinned_jobs = ["migrator-sql", "deploy-scripts", "workflow-lint", "network-verify"]
unpinned = []
for name in pinned_jobs:
    for step in (dotnet_jobs.get(name) or {}).get("steps", []) or []:
        ref = step.get("uses")
        if ref and not re.search(r"@[0-9a-f]{40}(\s|$)", ref):
            unpinned.append(name + ": " + ref)
check("every uses: in migrator-sql and the new jobs is pinned to a 40-hex SHA"
      + (" (" + ", ".join(unpinned) + ")" if unpinned else ""), not unpinned)

new_jobs = ["deploy-scripts", "workflow-lint", "network-verify"]
check("new jobs run on ubuntu-26.04 with contents: read only",
      all((dotnet_jobs.get(n) or {}).get("runs-on") == "ubuntu-26.04"
          and (dotnet_jobs.get(n) or {}).get("permissions") == {"contents": "read"}
          for n in new_jobs))
new_checkouts = [s for n in new_jobs for s in (dotnet_jobs.get(n) or {}).get("steps", []) or []
                 if str(s.get("uses", "")).startswith("actions/checkout@")]
check("new jobs check out without persisting credentials",
      len(new_checkouts) == len(new_jobs)
      and all(s.get("with", {}).get("persist-credentials") is False for s in new_checkouts))

workflow_runs = []
for wf in sorted(glob.glob(".github/workflows/*.yml") + glob.glob(".github/workflows/*.yaml")):
    with open(wf, encoding="utf-8") as fh:
        doc = yaml.safe_load(fh) or {}
    for jname, job in (doc.get("jobs") or {}).items():
        for step in (job or {}).get("steps", []) or []:
            if "run" in step:
                workflow_runs.append((wf, jname, step["run"]))

check("no run: in dotnet.yml contains a ${{ expression",
      all("${{" not in run for wf, _, run in workflow_runs if wf.endswith("dotnet.yml")))

# Every docker run image reference must carry a sha256 digest.
bad_images = []
option_values = {"-v", "-w", "-e", "-u", "--entrypoint", "--network", "--user", "--platform", "--name", "--workdir"}
for wf, jname, run in workflow_runs:
    joined = re.sub(r"\\\n", " ", run)
    for line in joined.splitlines():
        if "docker run" not in line:
            continue
        tokens = shlex.split(line[line.index("docker run") + len("docker run"):])
        i = 0
        image = None
        while i < len(tokens):
            tok = tokens[i]
            if tok in option_values:
                i += 2
                continue
            if tok.startswith("-"):
                i += 1
                continue
            image = tok
            break
        if image is None or not re.search(r"@sha256:[0-9a-f]{64}$", image):
            bad_images.append(wf + ":" + jname + ": " + str(image))
check("every docker run image reference is pinned by sha256 digest"
      + (" (" + ", ".join(bad_images) + ")" if bad_images else ""), not bad_images)

# Every repository script a run: step names must exist; glob tokens are skipped.
missing_scripts = []
for wf, jname, run in workflow_runs:
    for token in re.findall(r"(?<![A-Za-z0-9_./-])(?:build|deploy)/[^\s\"'`;|&()<>]*", run):
        if "*" in token:
            continue
        if token.endswith(".sh") or token == "deploy/bin/questboard-deploy":
            if not os.path.isfile(token):
                missing_scripts.append(wf + ":" + jname + ": " + token)
check("every repository script named in a run: step exists"
      + (" (" + ", ".join(missing_scripts) + ")" if missing_scripts else ""), not missing_scripts)

sys.exit(1 if failures else 0)
PY
