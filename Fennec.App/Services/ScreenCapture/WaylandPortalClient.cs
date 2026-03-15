using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Fennec.App.Services.ScreenCapture;

/// <summary>
/// Runs a Python3 helper that drives the XDG Desktop Portal ScreenCast flow and then
/// captures frames from the selected PipeWire node via GStreamer (pipewiresrc → appsink).
///
/// Protocol:
///   stderr — human-readable status lines ("READY &lt;node_id&gt;", "ERROR: ...")
///   stdout — binary frame stream: for each frame, [uint32 width LE][uint32 height LE][RGBA bytes]
///
/// Requires: python3-dbus, python3-gobject, gstreamer1-plugin-pipewire (all default on Silverblue).
/// </summary>
public class WaylandPortalClient : IAsyncDisposable
{
    private const string PortalScript = """
import dbus
import dbus.mainloop.glib
import gi
gi.require_version('Gst', '1.0')
from gi.repository import GLib, Gst
import sys
import struct
import random
import string

def tok():
    return ''.join(random.choices(string.ascii_lowercase + string.digits, k=16))

dbus.mainloop.glib.DBusGMainLoop(set_as_default=True)
Gst.init(None)
bus_conn = dbus.SessionBus()
loop = GLib.MainLoop()

portal_obj = bus_conn.get_object('org.freedesktop.portal.Desktop', '/org/freedesktop/portal/desktop')
sc = dbus.Interface(portal_obj, 'org.freedesktop.portal.ScreenCast')
sender = bus_conn.get_unique_name().lstrip(':').replace('.', '_')

def wait(handle_token, cb):
    path = f'/org/freedesktop/portal/desktop/request/{sender}/{handle_token}'
    def on_resp(code, results):
        if code != 0:
            print(f'ERROR: portal response {code}', file=sys.stderr, flush=True)
            loop.quit()
            return
        cb(results)
    bus_conn.add_signal_receiver(on_resp, 'Response', 'org.freedesktop.portal.Request', path=path)

session_handle = [None]

def step1(_=None):
    t = tok()
    wait(t, step2)
    sc.CreateSession({'handle_token': dbus.String(t), 'session_handle_token': dbus.String(tok())})

def step2(r):
    session_handle[0] = str(r['session_handle'])
    t = tok()
    wait(t, step3)
    sc.SelectSources(session_handle[0], {
        'handle_token': dbus.String(t),
        'types': dbus.UInt32(1),
        'multiple': dbus.Boolean(False),
        'cursor_mode': dbus.UInt32(2),
    })

def step3(_):
    t = tok()
    wait(t, step4)
    sc.Start(session_handle[0], '', {'handle_token': dbus.String(t)})

def step4(r):
    streams = r.get('streams', [])
    if not streams:
        print('ERROR: no streams returned by portal', file=sys.stderr, flush=True)
        loop.quit()
        return
    node_id = int(streams[0][0])
    # OpenPipeWireRemote gives an authenticated FD to the PipeWire daemon;
    # without it pipewiresrc connects unauthenticated and gets black frames.
    pw_fd_obj = sc.OpenPipeWireRemote(session_handle[0], {})
    pw_fd = pw_fd_obj.take()
    print(f'READY {node_id}', file=sys.stderr, flush=True)
    start_capture(node_id, pw_fd)

def start_capture(node_id, pw_fd):
    pipeline = Gst.parse_launch(
        f'pipewiresrc fd={pw_fd} path={node_id} ! '
        f'videoconvert ! '
        f'video/x-raw,format=RGBA ! '
        f'appsink name=sink emit-signals=true max-buffers=1 drop=true sync=false'
    )

    sink = pipeline.get_by_name('sink')

    out = sys.stdout.buffer

    def on_new_sample(appsink):
        sample = appsink.emit('pull-sample')
        if not sample:
            return Gst.FlowReturn.OK
        buf = sample.get_buffer()
        caps = sample.get_caps()
        s = caps.get_structure(0)
        w = s.get_int('width')[1]
        h = s.get_int('height')[1]
        ok, mapinfo = buf.map(Gst.MapFlags.READ)
        if not ok:
            return Gst.FlowReturn.OK
        try:
            out.write(struct.pack('<II', w, h))
            out.write(bytes(mapinfo.data))
            out.flush()
        finally:
            buf.unmap(mapinfo)
        return Gst.FlowReturn.OK

    sink.connect('new-sample', on_new_sample)
    pipeline.set_state(Gst.State.PLAYING)

GLib.idle_add(step1)
loop.run()
""";

    private readonly ILogger<WaylandPortalClient> _logger;
    public Process? Process { get; private set; }

    public WaylandPortalClient(ILogger<WaylandPortalClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Writes the script, starts the helper process, waits until the portal picker completes
    /// and GStreamer starts, then returns the node_id.  The process keeps running after this
    /// call — it must stay alive to stream frames and hold the portal session open.
    /// </summary>
    public async Task<uint> StartSessionAsync(CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("python3", "-")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start portal helper");

        // Feed script via stdin then close it so Python sees EOF on the script stream
        await Process.StandardInput.WriteAsync(PortalScript);
        Process.StandardInput.Close();

        // Read stderr until "READY <node_id>" — stdout is reserved for binary frame data
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await Process.StandardError.ReadLineAsync(ct);
            if (line == null)
            {
                var err = await Process.StandardError.ReadToEndAsync(ct);
                throw new InvalidOperationException($"Portal script exited: {err.Trim()}");
            }
            _logger.LogDebug("Portal: {Line}", line);
            if (line.StartsWith("ERROR:"))
                throw new InvalidOperationException($"Portal: {line["ERROR:".Length..].Trim()}");
            if (line.StartsWith("READY "))
            {
                var nodeId = uint.Parse(line["READY ".Length..].Trim());
                _logger.LogInformation("Portal: stream node_id = {NodeId}", nodeId);
                return nodeId;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Process != null && !Process.HasExited)
        {
            try { Process.Kill(); await Process.WaitForExitAsync(); } catch { }
        }
        Process?.Dispose();
        Process = null;
    }
}
