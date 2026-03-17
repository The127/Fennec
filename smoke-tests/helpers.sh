#!/usr/bin/env bash
set -euo pipefail

# --- Host configuration ---
LOCAL=${LOCAL:-http://localhost:8310}
MINI=${MINI:-http://192.168.1.41:8310}

# --- Colors ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# --- Mac Mini notification ---
MINI_SSH=${MINI_SSH:-p-mini}
notify_mini() {
    ssh "$MINI_SSH" "osascript -e 'display notification \"$1\" with title \"Fennec Test\"'" 2>/dev/null &
}

# --- Logging ---
log()  { echo -e "${CYAN}[$(date +%H:%M:%S)]${NC} $*"; }
pass() { echo -e "${GREEN}[PASS]${NC} $*"; notify_mini "✅ $*"; }
fail() { echo -e "${RED}[FAIL]${NC} $*"; notify_mini "❌ $*"; return 1; }
warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
step() { echo -e "\n${CYAN}--- $* ---${NC}"; notify_mini "$*"; }

# --- HTTP helpers ---
get()  { curl -sf "$1" 2>/dev/null; }
post() {
    local url="$1"
    local data="${2:-}"
    if [[ -z "$data" ]]; then
        data='{}'
    fi
    local response
    response=$(curl -s -w "\n%{http_code}" -X POST "$url" -d "$data" 2>/dev/null)
    local body http_code
    http_code=$(echo "$response" | tail -1)
    body=$(echo "$response" | sed '$d')
    if (( http_code >= 400 )); then
        warn "POST $url → $http_code: $body"
        return 1
    fi
    echo "$body"
}

# --- Polling ---
# poll <description> <command> [timeout_seconds]
poll() {
    local desc="$1" cmd="$2" timeout="${3:-30}"
    local elapsed=0
    log "Waiting: $desc (timeout ${timeout}s)"
    while ! eval "$cmd" >/dev/null 2>&1; do
        sleep 0.5
        elapsed=$((elapsed + 1))
        if (( elapsed >= timeout * 2 )); then
            fail "Timed out: $desc"
            return 1
        fi
    done
    log "Ready: $desc"
}

# --- State queries ---
auth_state()  { get "$1/auth/state"; }
voice_state() { get "$1/voice/state"; }
user_id()     { auth_state "$1" | python3 -c "import sys,json; print(json.load(sys.stdin)['userId'])"; }

is_logged_in() {
    auth_state "$1" | python3 -c "import sys,json; assert json.load(sys.stdin).get('isLoggedIn')"
}

is_voice_connected() {
    voice_state "$1" | python3 -c "import sys,json; assert json.load(sys.stdin)['isConnected']"
}

is_screen_sharing() {
    voice_state "$1" | python3 -c "import sys,json; assert json.load(sys.stdin)['isScreenSharing']"
}

is_not_screen_sharing() {
    voice_state "$1" | python3 -c "import sys,json; assert not json.load(sys.stdin)['isScreenSharing']"
}

screen_sharer_count() {
    voice_state "$1" | python3 -c "import sys,json; print(len(json.load(sys.stdin)['activeScreenSharers']))"
}

assert_sharer_count() {
    local host="$1" expected="$2"
    local actual
    actual=$(screen_sharer_count "$host")
    if [[ "$actual" != "$expected" ]]; then
        fail "Expected $expected active sharers on $host, got $actual"
        return 1
    fi
}

all_peers_connected() {
    voice_state "$1" | python3 -c "
import sys,json
s=json.load(sys.stdin)
peers=s.get('peerStates',{})
assert len(peers)>0 and all(v=='connected' for v in peers.values()), f'peers: {peers}'
"
}

peer_count() {
    voice_state "$1" | python3 -c "import sys,json; print(len(json.load(sys.stdin).get('peerStates',{})))"
}

# Check if a specific user is in activeScreenSharers
has_sharer() {
    local host="$1" sharer_id="$2"
    voice_state "$host" | python3 -c "
import sys,json
s=json.load(sys.stdin)
assert any(sh['userId']=='$sharer_id' for sh in s['activeScreenSharers']), 'sharer not found'
"
}

# --- Metrics helpers ---
is_receiving_frames() {
    local host="$1" user_id="$2"
    get "$host/screen-share/receiving/$user_id" | python3 -c "
import sys,json; d=json.load(sys.stdin); assert d['framesReceived']>0, f'not receiving: {d}'
"
}

get_metrics() {
    get "$1/screen-share/metrics/$2"
}

sender_fps() {
    local host="$1" user_id="$2"
    get_metrics "$host" "$user_id" | python3 -c "import sys,json; print(json.load(sys.stdin)['sentFps'])"
}

receiver_fps() {
    local host="$1" user_id="$2"
    get_metrics "$host" "$user_id" | python3 -c "import sys,json; print(json.load(sys.stdin)['receiveFps'])"
}

frames_received() {
    local host="$1" user_id="$2"
    get "$host/screen-share/receiving/$user_id" | python3 -c "import sys,json; print(json.load(sys.stdin)['framesReceived'])"
}

# --- Setup / teardown ---
restart_services() {
    step "Restarting test services"
    systemctl --user restart fennec-test-app@local fennec-test-app-mac-mini
    poll "local healthy" "get $LOCAL/health"
    poll "mini healthy" "get $MINI/health"
    poll "local logged in" "is_logged_in $LOCAL" 30
    poll "mini logged in" "is_logged_in $MINI" 60
    poll "local voice connected" "is_voice_connected $LOCAL" 30
    poll "mini voice connected" "is_voice_connected $MINI" 30
    poll "local peers connected" "all_peers_connected $LOCAL" 30
    poll "mini peers connected" "all_peers_connected $MINI" 30
}

# Wait for peers to stabilize after a renegotiation
wait_peers_reconnected() {
    local host="$1" label="${2:-$1}"
    poll "$label peers reconnected" "all_peers_connected $host" 30
}

# --- Test runner ---
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

EXIT_ON_FAIL=${EXIT_ON_FAIL:-1}
PAUSE=${PAUSE:-0}
_TEST_FAILED=0

# Call this from test functions to signal failure
assert() {
    if ! "$@"; then
        warn "ASSERT FAILED: $*"
        _TEST_FAILED=1
        return 1
    fi
}

run_test() {
    local name="$1"
    shift
    TESTS_RUN=$((TESTS_RUN + 1))
    _TEST_FAILED=0
    step "TEST: $name"
    # Wait for clean peer state before each test — check twice with a gap to ensure stability
    poll "pre-test peers settling" "all_peers_connected $LOCAL" 45 || true
    sleep 2
    poll "pre-test peers stable" "all_peers_connected $LOCAL" 15 || true
    if (( PAUSE > 0 )); then sleep "$PAUSE"; fi
    # Call the test function; it uses assert() to flag failures
    "$@" || _TEST_FAILED=1
    if (( _TEST_FAILED == 0 )); then
        pass "$name"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        fail "$name" || true
        TESTS_FAILED=$((TESTS_FAILED + 1))
        # Clean up screen shares between failed tests
        post "$LOCAL/screen-share/stop" 2>/dev/null || true
        post "$MINI/screen-share/stop" 2>/dev/null || true
        sleep 1
        if (( EXIT_ON_FAIL )); then
            print_summary || true
            exit 1
        fi
    fi
}

print_summary() {
    echo ""
    echo "=============================="
    echo -e " ${CYAN}Tests run:${NC}    $TESTS_RUN"
    echo -e " ${GREEN}Passed:${NC}       $TESTS_PASSED"
    if (( TESTS_FAILED > 0 )); then
        echo -e " ${RED}Failed:${NC}       $TESTS_FAILED"
    else
        echo -e " Failed:       0"
    fi
    echo "=============================="
    (( TESTS_FAILED == 0 ))
}
