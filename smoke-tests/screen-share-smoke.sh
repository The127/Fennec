#!/usr/bin/env bash
# Screen share smoke tests for the dual-instance test setup.
# Usage: ./smoke-tests/screen-share-smoke.sh [--no-restart] [test-name]
set -euo pipefail
cd "$(dirname "$0")"
source helpers.sh

# ============================================================
#  Test: basic start/stop
# ============================================================
test_start_stop() {
    assert post "$LOCAL/screen-share/start"
    assert poll "local sharing" "is_screen_sharing $LOCAL" 10
    sleep 1
    assert assert_sharer_count "$MINI" 1

    assert post "$LOCAL/screen-share/stop"
    assert poll "local stopped" "is_not_screen_sharing $LOCAL" 10
    sleep 1
    assert assert_sharer_count "$MINI" 0
}

# ============================================================
#  Test: start, watch, verify frames, stop
# ============================================================
test_single_share_watch_frames() {
    assert post "$LOCAL/screen-share/start"
    assert poll "local sharing" "is_screen_sharing $LOCAL" 10
    assert poll "mini sees sharer" "has_sharer $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "mini receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "local stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: dual share, sequential watch (local first)
# ============================================================
test_dual_share_local_watches_first() {
    assert post "$LOCAL/screen-share/start"
    assert post "$MINI/screen-share/start"
    assert poll "local sharing" "is_screen_sharing $LOCAL" 10
    assert poll "mini sharing" "is_screen_sharing $MINI" 10
    assert poll "local sees 2 sharers" "assert_sharer_count $LOCAL 2" 15
    assert poll "mini sees 2 sharers" "assert_sharer_count $MINI 2" 15
    assert poll "peers connected" "all_peers_connected $LOCAL" 15

    assert post "$LOCAL/screen-share/watch/$MINI_USER"
    assert wait_peers_reconnected "$LOCAL" "local"

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"

    assert poll "local receiving" "is_receiving_frames $LOCAL $MINI_USER" 10
    assert poll "mini receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$LOCAL/screen-share/unwatch/$MINI_USER"
    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert post "$MINI/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: dual share, sequential watch (mini first)
# ============================================================
test_dual_share_mini_watches_first() {
    assert post "$LOCAL/screen-share/start"
    assert post "$MINI/screen-share/start"
    assert poll "both sharing" "assert_sharer_count $LOCAL 2" 15
    assert poll "peers connected" "all_peers_connected $LOCAL" 15

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"

    assert post "$LOCAL/screen-share/watch/$MINI_USER"
    assert wait_peers_reconnected "$LOCAL" "local"

    assert poll "local receiving" "is_receiving_frames $LOCAL $MINI_USER" 10
    assert poll "mini receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$LOCAL/screen-share/unwatch/$MINI_USER"
    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert post "$MINI/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: change resolution mid-share
# ============================================================
test_change_resolution_mid_share() {
    assert post "$LOCAL/screen-share/start" '{"resolution":"1080p","bitrateKbps":1500,"frameRate":30}'
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "receiving at 1080p" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$LOCAL/screen-share/update" '{"resolution":"720p","bitrateKbps":1000,"frameRate":30}'
    sleep 3
    assert is_receiving_frames "$MINI" "$LOCAL_USER"
    log "still receiving after 720p switch"

    assert post "$LOCAL/screen-share/update" '{"resolution":"480p","bitrateKbps":500,"frameRate":30}'
    sleep 3
    assert is_receiving_frames "$MINI" "$LOCAL_USER"
    log "still receiving after 480p switch"

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: change bitrate mid-share
# ============================================================
test_change_bitrate_mid_share() {
    assert post "$LOCAL/screen-share/start" '{"bitrateKbps":1500}'
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$LOCAL/screen-share/update" '{"resolution":"1080p","bitrateKbps":300,"frameRate":30}'
    sleep 3
    assert is_receiving_frames "$MINI" "$LOCAL_USER"
    log "receiving at 300 kbps"

    assert post "$LOCAL/screen-share/update" '{"resolution":"1080p","bitrateKbps":3000,"frameRate":30}'
    sleep 3
    assert is_receiving_frames "$MINI" "$LOCAL_USER"
    log "receiving at 3000 kbps"

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: change frame rate mid-share
# ============================================================
test_change_fps_mid_share() {
    assert post "$LOCAL/screen-share/start" '{"frameRate":30}'
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$LOCAL/screen-share/update" '{"resolution":"1080p","bitrateKbps":1500,"frameRate":10}'
    sleep 3
    assert is_receiving_frames "$MINI" "$LOCAL_USER"
    log "receiving at 10 fps"

    assert post "$LOCAL/screen-share/update" '{"resolution":"1080p","bitrateKbps":1500,"frameRate":30}'
    sleep 3
    assert is_receiving_frames "$MINI" "$LOCAL_USER"
    log "receiving at 30 fps"

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: change capture target mid-share
# ============================================================
test_change_target_mid_share() {
    assert post "$LOCAL/screen-share/start"
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    # Switch target (stop+start internally)
    assert post "$LOCAL/screen-share/change-target"
    assert poll "sharing again" "is_screen_sharing $LOCAL" 15

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "receiving after change" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: sharer stops while being watched
# ============================================================
test_sharer_stops_while_watched() {
    assert post "$LOCAL/screen-share/start"
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$LOCAL/screen-share/stop"
    assert poll "local stopped" "is_not_screen_sharing $LOCAL" 10
    sleep 1
    assert assert_sharer_count "$MINI" 0
    log "mini cleaned up after sharer stopped"
}

# ============================================================
#  Test: watcher unwatches then re-watches
# ============================================================
test_unwatch_rewatch() {
    assert post "$LOCAL/screen-share/start"
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "first watch receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    sleep 2

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "re-watch receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: stop and restart share
# ============================================================
test_stop_restart_share() {
    assert post "$LOCAL/screen-share/start"
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "first session receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
    sleep 1

    assert post "$LOCAL/screen-share/start"
    assert poll "sharing again" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "second session receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: dual share, one stops, other continues
# ============================================================
test_dual_share_one_stops() {
    assert post "$LOCAL/screen-share/start"
    assert post "$MINI/screen-share/start"
    assert poll "both sharing" "assert_sharer_count $LOCAL 2" 15
    assert poll "peers connected" "all_peers_connected $LOCAL" 15

    assert post "$LOCAL/screen-share/watch/$MINI_USER"
    assert wait_peers_reconnected "$LOCAL" "local"
    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"

    assert poll "local receiving" "is_receiving_frames $LOCAL $MINI_USER" 10
    assert poll "mini receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$LOCAL/screen-share/stop"
    assert poll "local stopped" "is_not_screen_sharing $LOCAL" 10
    sleep 2
    assert assert_sharer_count "$MINI" 1
    assert is_receiving_frames "$LOCAL" "$MINI_USER"
    log "local still receiving from mini after local stopped sharing"

    assert post "$LOCAL/screen-share/unwatch/$MINI_USER"
    assert post "$MINI/screen-share/stop"
    assert poll "mini stopped" "is_not_screen_sharing $MINI" 10
}

# ============================================================
#  Test: rapid resolution cycling
# ============================================================
test_rapid_resolution_cycling() {
    assert post "$LOCAL/screen-share/start" '{"resolution":"1080p","bitrateKbps":1500,"frameRate":30}'
    assert poll "sharing" "is_screen_sharing $LOCAL" 10

    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    for res in 720p 480p 1080p 480p 720p 1080p; do
        assert post "$LOCAL/screen-share/update" "{\"resolution\":\"$res\",\"bitrateKbps\":1500,\"frameRate\":30}"
        sleep 1
    done

    sleep 3
    assert is_receiving_frames "$MINI" "$LOCAL_USER"
    log "still receiving after rapid resolution cycling"

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Test: cross-platform direction (mini shares, local watches)
# ============================================================
test_mini_shares_local_watches() {
    assert post "$MINI/screen-share/start"
    assert poll "mini sharing" "is_screen_sharing $MINI" 10
    assert poll "local sees sharer" "has_sharer $LOCAL $MINI_USER" 10

    assert post "$LOCAL/screen-share/watch/$MINI_USER"
    assert wait_peers_reconnected "$LOCAL" "local"
    assert poll "local receiving" "is_receiving_frames $LOCAL $MINI_USER" 10

    assert post "$LOCAL/screen-share/unwatch/$MINI_USER"
    assert post "$MINI/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $MINI" 10
}

# ============================================================
#  Test: sharer starts before second peer joins voice
#  (kris shares, mac mini joins call after, mini should watch)
# ============================================================
test_late_joiner_watches_existing_share() {
    # Stop MINI entirely so LOCAL is alone
    systemctl --user stop fennec-test-app-mac-mini
    sleep 2

    # LOCAL starts sharing while alone in voice
    assert post "$LOCAL/screen-share/start"
    assert poll "local sharing" "is_screen_sharing $LOCAL" 10

    # Now bring MINI back up — it will auto-login and auto-join voice
    systemctl --user start fennec-test-app-mac-mini
    poll "mini healthy" "get $MINI/health" 60
    poll "mini logged in" "is_logged_in $MINI" 60
    poll "mini voice connected" "is_voice_connected $MINI" 30
    assert poll "mini peers connected" "all_peers_connected $MINI" 30

    # MINI should see LOCAL as a sharer
    assert poll "mini sees sharer" "has_sharer $MINI $LOCAL_USER" 10

    # Give UI time to populate ActiveScreenShares from voice state
    sleep 3

    # MINI watches LOCAL's existing share
    assert post "$MINI/screen-share/watch/$LOCAL_USER"
    assert wait_peers_reconnected "$MINI" "mini"
    assert poll "mini receiving" "is_receiving_frames $MINI $LOCAL_USER" 10

    assert post "$MINI/screen-share/unwatch/$LOCAL_USER"
    assert post "$LOCAL/screen-share/stop"
    assert poll "stopped" "is_not_screen_sharing $LOCAL" 10
}

# ============================================================
#  Main
# ============================================================
if [[ "${1:-}" == "--no-restart" ]]; then
    shift
    log "Skipping service restart"
    poll "local healthy" "get $LOCAL/health" 5
    poll "mini healthy" "get $MINI/health" 5
    poll "local voice connected" "is_voice_connected $LOCAL" 5
    poll "mini voice connected" "is_voice_connected $MINI" 5
else
    restart_services
fi

LOCAL_USER=$(user_id "$LOCAL")
MINI_USER=$(user_id "$MINI")
log "Local user: $LOCAL_USER"
log "Mini user:  $MINI_USER"

# Clean slate
post "$LOCAL/screen-share/stop" 2>/dev/null || true
post "$MINI/screen-share/stop" 2>/dev/null || true
sleep 1

if [[ -n "${1:-}" ]]; then
    run_test "$1" "$1"
else
    run_test "start/stop"                       test_start_stop
    run_test "single share + watch + frames"    test_single_share_watch_frames
    run_test "dual share (local watches first)" test_dual_share_local_watches_first
    run_test "dual share (mini watches first)"  test_dual_share_mini_watches_first
    run_test "change resolution mid-share"      test_change_resolution_mid_share
    run_test "change bitrate mid-share"         test_change_bitrate_mid_share
    run_test "change fps mid-share"             test_change_fps_mid_share
    run_test "change target mid-share"          test_change_target_mid_share
    run_test "sharer stops while watched"       test_sharer_stops_while_watched
    run_test "unwatch then re-watch"            test_unwatch_rewatch
    run_test "stop and restart share"           test_stop_restart_share
    run_test "dual share, one stops"            test_dual_share_one_stops
    run_test "rapid resolution cycling"         test_rapid_resolution_cycling
    run_test "mini shares, local watches"       test_mini_shares_local_watches
    run_test "late joiner watches existing share" test_late_joiner_watches_existing_share
fi

print_summary
