# shellcheck shell=bash
# Sourced by the offline tests. Puts stand-ins for the commands that manage
# the host (systemctl, systemd-run, pkexec, apt-get) first on PATH, so no test
# can ever restart a service, trigger an authentication prompt or install a
# package on the machine it runs on, including from a subprocess. Every call
# is recorded and fails.
#
# Usage: host_guard_install DIR          (DIR must be a directory the test cleans up)
#        host_guard_calls                (prints the recorded calls, one per line)
#        host_guard_mark_test_root DIR   (lets the dispatcher use DIR as its root)

host_guard_install() {
  local dir="$1"
  HOST_GUARD_LOG="${dir}/host-guard-calls.log"
  : > "$HOST_GUARD_LOG"
  local cmd
  for cmd in systemctl systemd-run pkexec apt-get; do
    cat > "${dir}/${cmd}" <<EOF
#!/bin/sh
printf '%s\n' "${cmd} \$*" >> "${HOST_GUARD_LOG}"
printf 'refused in tests: %s\n' "${cmd} \$*" >&2
exit 1
EOF
    chmod +x "${dir}/${cmd}"
  done
  export HOST_GUARD_LOG
  export PATH="${dir}:${PATH}"
}

# Marks DIR as a test tree for the dispatcher. The dispatcher honours
# QUESTBOARD_DEPLOY_ROOT only for a directory that holds this marker and is
# owned, like the marker, by the user running it, so a variable that leaks in
# from elsewhere cannot relocate a real installer run.
host_guard_mark_test_root() {
  : > "${1}/.questboard-test-root"
}

host_guard_calls() {
  cat "${HOST_GUARD_LOG:-/dev/null}" 2> /dev/null || true
}
