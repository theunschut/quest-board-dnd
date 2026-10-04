# Cutting a release

How to turn a merged change into a release the server installs, what the release workflow
refuses, how to check a published release from a workstation, and the one-time GitHub settings the
pipeline depends on. What the server does after a release is published (pull, verify, install,
roll back) is in [deploy.md](deploy.md).

## What a release is

A release is a tag of the form `vX.Y.Z` (for example `v5.4.0`) with three files attached:

| Asset | What it is |
|---|---|
| `questboard-vX.Y.Z.zip` | The published web app, the database migrator, the installer files and a manifest |
| `questboard-vX.Y.Z.zip.sha256` | A checksum of the zip, so a corrupt download is caught early |
| `questboard-vX.Y.Z.zip.sigstore.json` | A signed build-provenance bundle: which workflow built the zip, from which commit and tag |

The workflow that makes them is `.github/workflows/release.yml`. It builds on GitHub-hosted
runners only. The signed provenance names that workflow file, and the server checks for it, so the
file keeps its name.

## Cutting a release

1. Merge the change to `main`.
2. Tag the commit on `main` with `vX.Y.Z` and push the tag. A lightweight tag is fine.

   ```bash
   git checkout main
   git pull
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

3. The `release` workflow starts. It validates the tag, builds, runs every test (including the
   migrator tests against a real SQL Server container and the installer's script tests), packages
   the release, checks the packaged zip, attests it and creates a **draft** release with the three
   assets. A draft is not visible to the server.
4. Open the draft release on GitHub and write the release title and notes, for example
   `v5.4.0 - Rescheduled game nights update in your calendar`. The workflow gives the draft a
   placeholder title and notes; replace them before approving.

   ```bash
   gh release edit vX.Y.Z --title "vX.Y.Z - <summary>" --notes-file notes.md
   ```

5. On the workflow run, approve the `deploy` environment (Review deployments). This is the only
   manual gate.
6. The `publish` job downloads the draft again, re-checks the checksum and the attestation, and
   publishes the release as the latest one.
7. The App CT's timer sees the new release on its next poll and installs it, normally within about
   five minutes. The outcome arrives by mail. See [deploy.md](deploy.md).

## What the workflow refuses

The workflow stops before building, or before publishing, when:

- **The tag is not exactly `vX.Y.Z`.** No leading zeros, no `-rc.1`, no `+build` suffix, no missing
  or extra component.
- **The tag's commit is not on `main`.** Every release must come from a commit that went through
  `main`.
- **The tag does not point at the commit that triggered the run.**
- **The release already exists and is published.** A published release is never overwritten; only
  a draft is updated.
- **Any test fails.** The zip is built from the same build that passed the full test run, so an
  attested zip is always one that passed.
- **The checksum or the attestation does not match when publishing.** The publish job re-verifies
  before it makes anything public.

## Release assets

Besides the three files above, a release lists GitHub's automatic source archives. The server
ignores those. It downloads exactly `questboard-vX.Y.Z.zip`, `.zip.sha256` and
`.zip.sigstore.json`, and a release missing any of them is refused.

Releases older than the pull-based deploy have no checksum or bundle and can never be installed
by the server.

## Verifying a published release from a workstation

You need the GitHub CLI (`gh`), `curl`, `python3` and `sha256sum`, and no login or token. Run the
script from a checkout of this repository, because it sources the installer's own verification
code from `deploy/lib/`:

```bash
build/verify-published-release.sh vX.Y.Z
```

It downloads the three assets, checks the checksum, verifies the attestation exactly as the server
will (this repository, the `release.yml` workflow, the `refs/tags/vX.Y.Z` ref, hosted runners
only), confirms the attested commit is on `main`, and confirms that a copy with one byte changed
is refused. On success it prints `all checks passed for vX.Y.Z`, followed by the last line,
`sha256 questboard-vX.Y.Z.zip <hex>`: that is the hash to compare against when you carry the zip to the server for the first install (see
[deploy.md](deploy.md)).

Verification contacts GitHub and the Sigstore trust service, so it needs network access. It never
installs anything and is meant to be run on a workstation, not on the server.

## One-time GitHub settings

Two controls cannot live in the repository and must be created once, before the first release. The
commands use the repository `theunschut/quest-board-dnd`.

### The `deploy` environment

The publish job runs in an environment called `deploy`. The environment requires the operator to
approve, and only accepts deployments from release tags, never from a branch.

Look up your numeric user id, then create the environment with yourself as the required reviewer.
Self-review stays allowed, because with one operator nobody else exists to approve.

```bash
gh api /users/<your-github-username> --jq .id

gh api --method PUT /repos/theunschut/quest-board-dnd/environments/deploy \
  -F 'reviewers[][type]=User' \
  -F 'reviewers[][id]=<your-user-id>' \
  -F 'prevent_self_review=false' \
  -F 'deployment_branch_policy[protected_branches]=false' \
  -F 'deployment_branch_policy[custom_branch_policies]=true'

gh api --method POST /repos/theunschut/quest-board-dnd/environments/deploy/deployment-branch-policies \
  -f name='v*.*.*' \
  -f type='tag'
```

### The tag ruleset

Only the repository admin role may create, move or delete a release tag.

```bash
gh api --method POST /repos/theunschut/quest-board-dnd/rulesets \
  -f name='release tags' \
  -f target='tag' \
  -f enforcement='active' \
  -f 'conditions[ref_name][include][]=refs/tags/v*' \
  -f 'rules[][type]=creation' \
  -f 'rules[][type]=update' \
  -f 'rules[][type]=deletion' \
  -f 'bypass_actors[][actor_type]=RepositoryRole' \
  -F 'bypass_actors[][actor_id]=5'
```

Repository role 5 is the built-in admin role. With this ruleset, anyone who is not an admin cannot
push a `v*` tag, so they cannot start a release.

### Checking the settings

```bash
build/check-github-settings.sh
```

It only reads settings, and needs `gh` logged in as the repository owner. It prints one `PASS` or
`FAIL` line for each of: the active `v*` tag ruleset restricted to the admin role, a required
reviewer on the `deploy` environment, the `v*.*.*` tag deployment policy with no branch policy,
and no registered self-hosted runner. It exits non-zero if any fail. It fails while any
self-hosted runner is still registered, so it also confirms the old push-based deploy has been
retired.

## The Docker image

`.github/workflows/docker-publish.yml` is separate and unchanged. It publishes the container image
to GHCR on every tag push, regardless of the `deploy` approval, so an image can appear for a tag
that is never published as a release. That image is the self-host path; the production server does
not use it.

## When a release does not install

Read the outcome mail and the journal on the server (`journalctl -u questboard-deploy-poll.service`).
The outcomes and what each means are in [deploy.md](deploy.md).

- If the release is faulty, fix forward: merge the fix and cut a new tag. The server installs it
  because it is newer.
- If the cause was on the server (a full disk, an unreachable database) and the release itself is
  fine, fix the cause and run `questboard-deploy install vX.Y.Z` on the server. The server skips a
  release it has already rejected until you do this or a newer tag appears. An outage of the
  verification services is not a rejection: nothing is remembered and the next poll retries it.
