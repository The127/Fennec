#!/usr/bin/env bash
set -euo pipefail

SSH_KEY=${SSH_KEY:-/ssh/id_ed25519}
MINI_HOST=${MINI_HOST:-192.168.1.29}
MINI_USER=${MINI_USER:-kristianjakobsen}
SOURCE_DIR=${SOURCE_DIR:-/src}

SSH_OPTS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o ConnectTimeout=10 -i $SSH_KEY"

log() { echo "[$(date +%H:%M:%S)] $*"; }

ssh_mini() {
    ssh $SSH_OPTS "${MINI_USER}@${MINI_HOST}" "$@"
}

wait_for_mini() {
    log "Waiting for Mac Mini to be reachable..."
    local attempts=0
    while ! ssh_mini true 2>/dev/null; do
        attempts=$((attempts + 1))
        if (( attempts >= 60 )); then
            log "Mac Mini unreachable after 5 minutes, retrying..."
            attempts=0
        fi
        sleep 5
    done
    log "Mac Mini is reachable"
}

# Ensure a virtual display exists via BetterDisplay.
# Creates and connects a 16:9 virtual screen if none exists.
ensure_display() {
    log "Ensuring BetterDisplay virtual screen exists..."
    ssh_mini 'export PATH="/opt/homebrew/bin:$PATH" && \
        open -a BetterDisplay 2>/dev/null || true && sleep 2 && \
        existing=$(betterdisplaycli get -type=VirtualScreen -identifiers 2>/dev/null | grep -c displayID || echo 0) && \
        if [ "$existing" -eq 0 ]; then \
            betterdisplaycli create -type=VirtualScreen -name="Fennec Test" -aspectWidth=16 -aspectHeight=9 2>/dev/null; \
        fi && \
        betterdisplaycli set -type=VirtualScreen -connected=on 2>/dev/null || true'
    log "Virtual display ready"
}

stop_app() {
    log "Stopping app on Mac Mini..."
    ssh_mini 'pkill -INT -f "Fennec.App.Desktop" 2>/dev/null || true' 2>/dev/null || true
    # Wait up to 15s for the process to exit, then force-kill
    local i=0
    while ssh_mini 'pgrep -f "Fennec.App.Desktop" >/dev/null 2>&1' 2>/dev/null && (( i < 15 )); do
        sleep 1; i=$((i+1))
    done
    ssh_mini 'pkill -9 -f "Fennec.App.Desktop" 2>/dev/null || true' 2>/dev/null || true
    sleep 1
}

clear_app_data() {
    log "Clearing cached app data on Mac Mini..."
    ssh_mini 'rm -rf ~/Library/Application\ Support/FennecApp' 2>/dev/null || true
}

start_app() {
    log "Syncing source to Mac Mini..."
    rsync -az --delete \
        --exclude .git/ --exclude bin/ --exclude obj/ \
        -e "ssh $SSH_OPTS" \
        "${SOURCE_DIR}/" "${MINI_USER}@${MINI_HOST}:~/fennec/"

    log "Building on Mac Mini..."
    ssh_mini 'export PATH="/opt/homebrew/bin:$HOME/.dotnet:$PATH" && cd ~/fennec && just build-native-macos && dotnet build Fennec.App.Desktop/Fennec.App.Desktop.csproj'

    log "Starting app on Mac Mini..."
    ssh_mini "FENNEC_NO_UPDATE=${FENNEC_NO_UPDATE:-1} \
        FENNEC_SKIP_TLS_VERIFY=1 \
        FENNEC_AUTO_LOGIN=${FENNEC_AUTO_LOGIN} \
        FENNEC_AUTO_LOGIN_PASSWORD=${FENNEC_AUTO_LOGIN_PASSWORD} \
        FENNEC_AUTO_JOIN_SERVER=${FENNEC_AUTO_JOIN_SERVER} \
        FENNEC_AUTO_JOIN_CHANNEL=${FENNEC_AUTO_JOIN_CHANNEL} \
        caffeinate -d -i bash -c 'export PATH=\$HOME/.dotnet:\$PATH && cd ~/fennec && dotnet run --project Fennec.App.Desktop 2>&1'" &
    APP_PID=$!
}

setup_vnc_tunnel() {
    log "Starting websockify in launcher (0.0.0.0:6081 → ${MINI_HOST}:5900)..."
    pkill -f "websockify.*6081" 2>/dev/null || true
    websockify 0.0.0.0:6081 "${MINI_HOST}:5900" &
    WEBSOCKIFY_PID=$!
    log "VNC tunnel ready on :6081"
}

configure_mac_mini() {
    log "Configuring Mac Mini display/lock settings..."
    ssh_mini '
        defaults write com.apple.screensaver idleTime -int 0
        defaults write com.apple.screensaver askForPassword -int 0
    '
    log "Display/lock settings applied"
}

trap 'stop_app; kill ${WEBSOCKIFY_PID:-} 2>/dev/null || true; exit 0' SIGTERM SIGINT

# Wait for Mac Mini to come online
wait_for_mini
configure_mac_mini
ensure_display

# Clear stale sessions so auto-login uses seeded users
stop_app
clear_app_data

# Set up VNC tunnel for dashboard access
setup_vnc_tunnel

# Initial start
start_app

# Monitor and restart on crash
while true; do
    wait $APP_PID || true
    log "App process exited, restarting in 5s..."
    sleep 5
    wait_for_mini
    start_app
done
