#!/bin/zsh
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
PACKAGE_DIR="${ROOT_DIR}/Assets/Plugins/NativeKit/MacOS/HelperArtifacts/package"
HELPER_LABEL="com.unitool.roothelper"
INSTALLED_HELPER="/Library/PrivilegedHelperTools/com.unitool.roothelper"
INSTALLED_PLIST="/Library/LaunchDaemons/com.unitool.roothelper.plist"
TRUST_FILE="/Users/Shared/UniTool/helper/trust.json"
LOG_FILE="/Users/Shared/UniTool/logs/helper.log"

TEST_RESTART=0
if [[ "${1:-}" == "--test-restart" ]]; then
  TEST_RESTART=1
fi

pass() { echo "[PASS] $1"; }
fail() { echo "[FAIL] $1"; }
info() { echo "[INFO] $1"; }

TOTAL=0
FAILED=0

check_file_exists() {
  local path="$1"
  local name="$2"
  TOTAL=$((TOTAL + 1))
  if [[ -e "$path" ]]; then
    pass "${name}: ${path}"
  else
    fail "${name} missing: ${path}"
    FAILED=$((FAILED + 1))
  fi
}

check_package() {
  info "Checking package artifacts..."
  check_file_exists "${PACKAGE_DIR}/com.unitool.roothelper" "package helper"
  check_file_exists "${PACKAGE_DIR}/com.unitool.roothelper.plist" "package plist"
  check_file_exists "${PACKAGE_DIR}/install_helper.sh" "package install script"
  check_file_exists "${PACKAGE_DIR}/uninstall_helper.sh" "package uninstall script"
  check_file_exists "${PACKAGE_DIR}/refresh_trust.sh" "package refresh script"
}

check_installed() {
  info "Checking installed system files..."
  check_file_exists "${INSTALLED_HELPER}" "installed helper"
  check_file_exists "${INSTALLED_PLIST}" "installed plist"
  check_file_exists "${TRUST_FILE}" "trust file"

  TOTAL=$((TOTAL + 1))
  if [[ -e "${LOG_FILE}" ]]; then
    pass "helper log: ${LOG_FILE}"
  else
    info "helper log not found yet: ${LOG_FILE}"
  fi
}

check_launchctl() {
  info "Checking launchctl service state..."
  TOTAL=$((TOTAL + 1))

  local output
  if ! output="$(launchctl print system/${HELPER_LABEL} 2>&1)"; then
    fail "launchctl print system/${HELPER_LABEL} failed"
    echo "${output}" | sed -n '1,6p'
    FAILED=$((FAILED + 1))
    return
  fi

  if echo "${output}" | grep -q "label = ${HELPER_LABEL}"; then
    pass "launchd service is registered"
  else
    info "launchd output did not include label (continuing)"
  fi

  if echo "${output}" | grep -q "state = running"; then
    pass "launchd service is running"
  else
    info "launchd service currently not running (on-demand idle is acceptable)"
  fi

  local pid
  pid="$(echo "${output}" | awk '/pid =/{print $3; exit}')"
  if [[ -n "${pid}" ]]; then
    info "current helper pid: ${pid}"
  fi
}

check_restart() {
  if [[ ${TEST_RESTART} -ne 1 ]]; then
    return
  fi

  info "Checking auto restart (--test-restart)..."
  TOTAL=$((TOTAL + 1))

  local before
  local after
  before="$(launchctl print system/${HELPER_LABEL} 2>/dev/null | awk '/pid =/{print $3; exit}')"

  if [[ -z "${before}" ]]; then
    info "No running pid found, trying to kickstart helper..."
    if ! sudo launchctl kickstart -k "system/${HELPER_LABEL}" >/dev/null 2>&1; then
      fail "kickstart failed, please run with admin privileges"
      FAILED=$((FAILED + 1))
      return
    fi
    sleep 1
    before="$(launchctl print system/${HELPER_LABEL} 2>/dev/null | awk '/pid =/{print $3; exit}')"
  fi

  if [[ -z "${before}" ]]; then
    info "cannot get helper pid for restart check (service may be on-demand idle)"
    info "skipping restart test because helper did not start after kickstart"
    return
  fi

  if ! sudo kill -9 "${before}" >/dev/null 2>&1; then
    fail "sudo kill failed, please run with admin privileges"
    FAILED=$((FAILED + 1))
    return
  fi

  sleep 2
  after="$(launchctl print system/${HELPER_LABEL} 2>/dev/null | awk '/pid =/{print $3; exit}')"
  if [[ -n "${after}" && "${after}" != "${before}" ]]; then
    pass "helper auto-restarted: ${before} -> ${after}"
  else
    fail "helper auto-restart check failed"
    FAILED=$((FAILED + 1))
  fi
}

echo "== UniTool macOS Root Helper Acceptance Check =="
check_package
check_installed
check_launchctl
check_restart

echo ""
echo "Total checks: ${TOTAL}, Failed: ${FAILED}"
if [[ ${FAILED} -eq 0 ]]; then
  echo "Result: PASS"
  exit 0
fi

echo "Result: FAIL"
exit 1
