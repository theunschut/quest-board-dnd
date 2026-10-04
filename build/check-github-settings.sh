#!/usr/bin/env bash
# Read-only check of the GitHub-side controls the release pipeline depends on.
#
# Run it on a workstation that has the repository owner's gh login. It only reads settings,
# through gh api with its built-in --jq filter, so no system jq is needed. It never changes
# anything and is never installed on the server.
#
# Prints one PASS or FAIL line per control and exits 1 if any control fails:
#   1. the v* tag ruleset is active and only the admin repository role can bypass it
#   2. the deploy environment has at least one required reviewer
#   3. the deploy environment only deploys from v*.*.* tags (no branch policy)
#   4. no self-hosted runner is registered
set -euo pipefail

overall=0

pass() { printf 'PASS: %s\n' "$1"; }

fail() {
  printf 'FAIL: %s\n' "$1"
  overall=1
}

repo="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"

# Repository role id 5 is the built-in admin role.
admin_role_id=5

check_tag_ruleset() {
  local label="v* tag ruleset is active, guards creation, update and deletion, and only the admin role can bypass it"
  local ids
  if ! ids="$(gh api "repos/$repo/rulesets" --jq '.[] | select(.target == "tag") | .id' 2>/dev/null)"; then
    fail "$label (could not list rulesets)"
    return
  fi
  if [ -z "$ids" ]; then
    fail "$label (no ruleset targets tags)"
    return
  fi

  local filter='
    .enforcement == "active"
    and ((.conditions.ref_name.include // []) | any(. == "refs/tags/v*"))
    and ((.rules // []) | any(.type == "creation"))
    and ((.rules // []) | any(.type == "update"))
    and ((.rules // []) | any(.type == "deletion"))
    and ((.bypass_actors // []) | length) == 1
    and (.bypass_actors[0].actor_type == "RepositoryRole")
    and (.bypass_actors[0].actor_id == '"$admin_role_id"')'

  local id verdict
  for id in $ids; do
    verdict="$(gh api "repos/$repo/rulesets/$id" --jq "$filter" 2>/dev/null)" || continue
    if [ "$verdict" = "true" ]; then
      pass "$label"
      return
    fi
  done
  fail "$label"
}

check_deploy_environment_reviewer() {
  local label="deploy environment requires at least one reviewer"
  local verdict
  if ! verdict="$(gh api "repos/$repo/environments/deploy" \
    --jq '[(.protection_rules // [])[] | select(.type == "required_reviewers") | ((.reviewers // []) | length)] | any(. >= 1)' 2>/dev/null)"; then
    fail "$label (environment 'deploy' not found)"
    return
  fi
  if [ "$verdict" = "true" ]; then pass "$label"; else fail "$label"; fi
}

check_deploy_environment_tag_policy() {
  local label="deploy environment uses a v*.*.* tag deployment policy and no branch policy"
  local custom verdict
  if ! custom="$(gh api "repos/$repo/environments/deploy" \
    --jq '.deployment_branch_policy.custom_branch_policies == true' 2>/dev/null)"; then
    fail "$label (environment 'deploy' not found)"
    return
  fi
  if [ "$custom" != "true" ]; then
    fail "$label (custom deployment policies are not enabled)"
    return
  fi
  if ! verdict="$(gh api "repos/$repo/environments/deploy/deployment-branch-policies" \
    --jq '(.branch_policies // []) | (any(.type == "tag" and .name == "v*.*.*")) and (any(.type == "branch") | not)' 2>/dev/null)"; then
    fail "$label (could not read deployment policies)"
    return
  fi
  if [ "$verdict" = "true" ]; then pass "$label"; else fail "$label"; fi
}

check_no_registered_runners() {
  local label="no self-hosted runner is registered"
  local total
  if ! total="$(gh api "repos/$repo/actions/runners" --jq '.total_count' 2>/dev/null)"; then
    fail "$label (could not read registered runners)"
    return
  fi
  if [ "$total" = "0" ]; then pass "$label"; else fail "$label ($total registered)"; fi
}

check_tag_ruleset
check_deploy_environment_reviewer
check_deploy_environment_tag_policy
check_no_registered_runners

exit "$overall"
