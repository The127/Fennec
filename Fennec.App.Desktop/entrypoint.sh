#!/bin/sh
set -e

# Trust any extra CA certs mounted at /extra-certs/
if [ -d /extra-certs ]; then
    for cert in /extra-certs/*.crt; do
        [ -f "$cert" ] && cp "$cert" /usr/local/share/ca-certificates/
    done
    update-ca-certificates 2>/dev/null || true
fi

# Start virtual framebuffer (no auth required)
Xvfb :99 -screen 0 1920x1080x24 -nolisten tcp -ac &
sleep 1

export DISPLAY=:99

# Start VNC server (no password, listen on all interfaces)
x11vnc -display :99 -forever -nopw -listen 0.0.0.0 -rfbport 5900 -shared &

# Start noVNC web client (serves on port 6080)
websockify --web /usr/share/novnc 6080 localhost:5900 &

exec dotnet Fennec.App.Desktop.dll "$@"
