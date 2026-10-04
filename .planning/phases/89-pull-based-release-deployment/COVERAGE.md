# API Coverage — GitHub (Releases, provenance and repository settings) and Sigstore

> Full coverage by default. Opt-outs are explicit, reasoned decisions.
> Scope: the GitHub and Sigstore surface relevant to releasing and pulling builds. The phase
> touches no other external API. Endpoints are grouped by who calls them: the unauthenticated
> server installer, the release workflow (job token), and read-only operator tooling.

| capability | decision | reason |
|---|---|---|
| server: GET /repos/{owner}/{repo}/releases/latest (unauthenticated) | INTEGRATE | |
| server: GET release asset (github.com/{owner}/{repo}/releases/download/...) | INTEGRATE | |
| server: GET /repos/{owner}/{repo}/compare/{base}...{head} | INTEGRATE | |
| server: Sigstore TUF trust root fetched by gh attestation verify | INTEGRATE | |
| server: GET /repos/{owner}/{repo}/releases (list all releases) | OPT-OUT | not needed — releases/latest plus a local semver comparison against the active release decides what to install |
| server: GET /repos/{owner}/{repo}/releases/tags/{tag} | OPT-OUT | not needed — a manual install builds the three asset URLs from the tag directly |
| server: GET /repos/{owner}/{repo}/attestations/{subject_digest} | OPT-OUT | explicitly out of scope — the Sigstore bundle ships as a release asset and is verified locally by gh; a second attestation source would add a trust path without adding assurance |
| server: any authenticated GitHub API call (token on the App CT) | OPT-OUT | explicitly out of scope — the phase goal is that GitHub holds no credential relationship with the production box; 12 unauthenticated polls an hour fit the 60/hour limit |
| server: webhooks / repository_dispatch notifications to the CT | OPT-OUT | explicitly out of scope — a push into the box is what this phase removes; the server pulls |
| server: gh attestation verify --custom-trusted-root (fully offline trust root) | OPT-OUT | not needed — a pinned trust root silently breaks on Sigstore key rotation; an unreachable service already refuses the install |
| workflow: actions/attest-build-provenance (create attestation) | INTEGRATE | |
| workflow: gh release create --draft --verify-tag | INTEGRATE | |
| workflow: gh release upload --clobber (drafts only) | INTEGRATE | |
| workflow: gh release view --json isDraft | INTEGRATE | |
| workflow: gh release download (publish job) | INTEGRATE | |
| workflow: gh attestation verify (publish-job re-verification) | INTEGRATE | |
| workflow: gh release edit --draft=false --latest | INTEGRATE | |
| workflow: delete release or release assets | OPT-OUT | not needed — a published release is never overwritten (the draft step refuses) and drafts are refreshed with --clobber |
| repository setting: immutable releases | OPT-OUT | not needed yet — publish-time re-verification plus server-side checksum and attestation already pin the bytes; revisit if releases are ever edited after publish |
| operator: GET /repos/{owner}/{repo} (gh repo view) | INTEGRATE | |
| operator: GET /repos/{owner}/{repo}/environments/deploy (+ policies) | INTEGRATE | |
| operator: GET /repos/{owner}/{repo}/rulesets and /rulesets/{id} | INTEGRATE | |
| operator: GET /repos/{owner}/{repo}/actions/runners | INTEGRATE | |
| operator: GET workflow runs and PR checks (gh run view, gh pr checks) | INTEGRATE | |
| operator: PUT/POST environments, deployment policies and rulesets | OPT-OUT | explicitly out of scope for automation — creating the deploy environment and ruleset is an operator handover step; docs/releasing.md gives the commands |
| operator: DELETE /repos/{owner}/{repo}/actions/runners/{id} | OPT-OUT | explicitly out of scope for automation — runner deregistration is an operator handover step, verified afterwards read-only |
