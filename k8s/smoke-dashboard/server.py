import asyncio
import copy
import json
import os
import re
import struct
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from pathlib import Path

import httpx
import websockets
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from fastapi import FastAPI, WebSocket
from fastapi.responses import HTMLResponse, StreamingResponse
from fastapi.staticfiles import StaticFiles

NAMESPACE = "fennec-test"
K8S_BASE = "https://kubernetes.default.svc"
SA_DIR = Path("/var/run/secrets/kubernetes.io/serviceaccount")

def _vnc_des(pw: str, challenge: bytes) -> bytes:
    key = (pw + "\x00" * 8)[:8].encode("latin-1")
    key = bytes(int(f"{b:08b}"[::-1], 2) for b in key)
    cipher = Cipher(algorithms.TripleDES(key * 3), modes.ECB(), backend=default_backend())
    enc = cipher.encryptor()
    return enc.update(challenge[:8]) + enc.finalize() + \
           Cipher(algorithms.TripleDES(key * 3), modes.ECB(), backend=default_backend()).encryptor().update(challenge[8:16]) + \
           Cipher(algorithms.TripleDES(key * 3), modes.ECB(), backend=default_backend()).encryptor().finalize()


class _VncProxy:
    """Single persistent upstream VNC connection multiplexed across browser sessions.

    macOS 15 locks the screen on every VNC client disconnect, not just the last.
    We maintain ONE upstream TCP session through websockify that never drops.
    Each browser connects to the proxy which handles the VNC handshake locally,
    then routes RFB messages through the persistent upstream.
    """

    def __init__(self) -> None:
        self._upstream: websockets.WebSocketClientProtocol | None = None
        self._server_init: bytes | None = None
        self._to_upstream: asyncio.Queue[bytes] = asyncio.Queue()
        self._downstream_ws: WebSocket | None = None

    async def run(self) -> None:
        password = os.environ.get("MINI_VNC_PASSWORD", "")
        while True:
            try:
                async with websockets.connect(
                    "ws://mac-launcher:6081",
                    subprotocols=["binary"],
                    ping_interval=None,
                ) as ws:
                    await asyncio.wait_for(ws.recv(), timeout=10)  # version
                    await ws.send(b"RFB 003.889\n")

                    sec = await asyncio.wait_for(ws.recv(), timeout=10)
                    n = sec[0]
                    types = list(sec[1 : 1 + n])

                    if 2 in types:
                        await ws.send(bytes([2]))
                        challenge = await asyncio.wait_for(ws.recv(), timeout=10)
                        await ws.send(_vnc_des(password, challenge))
                        result = await asyncio.wait_for(ws.recv(), timeout=10)
                        if struct.unpack(">I", result[:4])[0] != 0:
                            raise RuntimeError("VNC auth failed")
                    elif 1 in types:
                        await ws.send(bytes([1]))
                    else:
                        raise RuntimeError(f"no supported VNC security types: {types}")

                    await ws.send(b"\x01")  # shared
                    self._server_init = await asyncio.wait_for(ws.recv(), timeout=10)
                    self._upstream = ws
                    print("[vnc-proxy] upstream ready", flush=True)

                    await asyncio.gather(
                        self._read_upstream(ws),
                        self._write_upstream(ws),
                    )
            except Exception as e:
                print(f"[vnc-proxy] upstream error: {e}, reconnecting in 5s", flush=True)
            finally:
                self._upstream = None
                self._server_init = None
            await asyncio.sleep(5)

    async def _read_upstream(self, ws: websockets.WebSocketClientProtocol) -> None:
        count = 0
        try:
            async for msg in ws:
                count += 1
                print(f"[vnc-proxy] upstream recv #{count}: {len(msg)} bytes hex={msg[:32].hex()} downstream={'yes' if self._downstream_ws else 'no'}", flush=True)
                downstream = self._downstream_ws
                if downstream is not None:
                    try:
                        await downstream.send_bytes(msg)
                    except Exception:
                        pass
        except Exception as e:
            print(f"[vnc-proxy] _read_upstream error after {count} msgs: {e}", flush=True)
        finally:
            print(f"[vnc-proxy] _read_upstream done after {count} msgs", flush=True)

    async def _write_upstream(self, ws: websockets.WebSocketClientProtocol) -> None:
        count = 0
        while True:
            msg = await self._to_upstream.get()
            count += 1
            print(f"[vnc-proxy] upstream send #{count}: {len(msg)} bytes, type=0x{msg[0]:02x}", flush=True)
            try:
                await ws.send(msg)
            except Exception as e:
                print(f"[vnc-proxy] _write_upstream error after {count} msgs: {e}", flush=True)
                break

    async def serve(self, browser_ws: WebSocket) -> None:
        raw = browser_ws.headers.get("sec-websocket-protocol", "")
        offered = [s.strip() for s in raw.split(",") if s.strip()]
        subprotocol = "binary" if "binary" in offered else (offered[0] if offered else None)
        await browser_ws.accept(subprotocol=subprotocol)

        if self._server_init is None:
            await browser_ws.close()
            return

        # Fake VNC handshake — browser talks to the proxy, not Mac Mini directly.
        # We authenticate with Mac Mini once on the upstream; browsers get no-auth.
        try:
            await browser_ws.send_bytes(b"RFB 003.008\n")
            cv = await browser_ws.receive_bytes()
            print(f"[vnc-proxy] client version: {cv!r}", flush=True)
            await browser_ws.send_bytes(bytes([1, 1]))  # 1 security type: None
            cc = await browser_ws.receive_bytes()
            print(f"[vnc-proxy] client sec choice: {cc!r}", flush=True)
            await browser_ws.send_bytes(b"\x00\x00\x00\x00")  # auth OK
            ci = await browser_ws.receive_bytes()
            print(f"[vnc-proxy] client init: {ci!r}", flush=True)
            await browser_ws.send_bytes(self._server_init)
            print(f"[vnc-proxy] sent ServerInit ({len(self._server_init)} bytes)", flush=True)
        except Exception as e:
            print(f"[vnc-proxy] handshake error: {e}", flush=True)
            return

        # Drain stale upstream requests from the previous session so Mac Mini
        # doesn't receive stale incremental FramebufferUpdateRequests.
        drained = 0
        try:
            while True:
                self._to_upstream.get_nowait()
                drained += 1
        except asyncio.QueueEmpty:
            pass
        if drained:
            print(f"[vnc-proxy] drained {drained} stale upstream messages", flush=True)

        # Inject a full-frame request so Mac Mini sends fresh pixels immediately.
        w, h = struct.unpack(">HH", self._server_init[:4])
        await self._to_upstream.put(struct.pack(">BBHHHH", 3, 0, 0, 0, w, h))
        print(f"[vnc-proxy] injected FBU request {w}x{h}", flush=True)

        self._downstream_ws = browser_ws
        try:
            count = 0
            async for msg in browser_ws.iter_bytes():
                count += 1
                if len(msg) >= 4 and msg[0] == 0x02:  # SetEncodings — force Raw only
                    # ZRLE (type 16) uses a stateful zlib stream per upstream connection.
                    # New browsers cannot decode mid-stream ZRLE data. Force Raw (type 0)
                    # so Mac Mini sends uncompressed pixels any browser can decode.
                    msg = bytes([2, 0, 0, 1, 0, 0, 0, 0])  # SetEncodings, 1 type: Raw
                    print("[vnc-proxy] SetEncodings intercepted → Raw only", flush=True)
                await self._to_upstream.put(msg)
        except Exception as e:
            print(f"[vnc-proxy] browser→upstream error: {e}", flush=True)
        finally:
            if self._downstream_ws is browser_ws:
                self._downstream_ws = None
            try:
                while True:
                    self._to_upstream.get_nowait()
            except asyncio.QueueEmpty:
                pass
            try:
                await browser_ws.close()
            except Exception:
                pass


_proxy = _VncProxy()


@asynccontextmanager
async def lifespan(app: FastAPI):
    asyncio.create_task(_proxy.run())
    yield


app = FastAPI(lifespan=lifespan)

# ── Run history ────────────────────────────────────────────────────────────────

_run_history: list[dict] = []  # newest first


def _start_run(pod: str) -> dict:
    run = {
        "id": pod,
        "startedAt": datetime.now(timezone.utc).isoformat(),
        "tests": {},   # name → "pass"|"fail"|"running"
        "logs": [],    # raw log lines
        "status": "running",
    }
    _run_history.insert(0, run)
    return run


@app.get("/api/runs")
async def list_runs():
    return [
        {k: v for k, v in r.items() if k != "logs"}
        for r in _run_history
    ]


@app.get("/api/runs/{run_id:path}")
async def get_run(run_id: str):
    for r in _run_history:
        if r["id"] == run_id:
            return r
    from fastapi.responses import JSONResponse
    return JSONResponse({"error": "not found"}, status_code=404)


def _k8s_token() -> str:
    return (SA_DIR / "token").read_text()


def _k8s_ca() -> str:
    return str(SA_DIR / "ca.crt")


def _k8s_headers() -> dict:
    return {"Authorization": f"Bearer {_k8s_token()}"}


# ── Config ─────────────────────────────────────────────────────────────────────

@app.get("/api/config")
async def config():
    return {
        "miniVncPassword": os.environ.get("MINI_VNC_PASSWORD", ""),
        "miniVncUsername": os.environ.get("MINI_VNC_USERNAME", ""),
    }


@app.get("/api/vnc-test")
async def vnc_test():
    password = os.environ.get("MINI_VNC_PASSWORD", "")

    steps = []
    try:
        async with websockets.connect("ws://mac-launcher:6081", subprotocols=["binary"], ping_interval=None) as ws:
            intro = await asyncio.wait_for(ws.recv(), timeout=5)
            steps.append(f"version: {intro.decode().strip()}")
            await ws.send(b"RFB 003.889\n")

            sec = await asyncio.wait_for(ws.recv(), timeout=5)
            n = sec[0]; types = list(sec[1:1+n])
            steps.append(f"security types: {types}")

            if 2 not in types:
                return {"steps": steps, "error": "type 2 not offered"}
            await ws.send(bytes([2]))

            challenge = await asyncio.wait_for(ws.recv(), timeout=5)
            steps.append(f"challenge: {len(challenge)} bytes")

            resp = _vnc_des(password, challenge)
            await ws.send(resp)

            result = await asyncio.wait_for(ws.recv(), timeout=5)
            code = struct.unpack(">I", result[:4])[0]
            steps.append(f"auth result code: {code} ({'OK' if code == 0 else 'FAIL'})")

            if code != 0:
                reason = result[4:].decode(errors="replace") if len(result) > 4 else ""
                return {"steps": steps, "error": f"auth failed: {reason}"}

            await ws.send(b"\x01")
            init = await asyncio.wait_for(ws.recv(), timeout=5)
            w, h = struct.unpack(">HH", init[:4])
            steps.append(f"ServerInit: {w}x{h} framebuffer")
            return {"steps": steps, "ok": True, "framebuffer": f"{w}x{h}"}

    except Exception as e:
        steps.append(f"exception: {type(e).__name__}: {e}")
        return {"steps": steps, "error": str(e)}

@app.get("/api/local/{path:path}")
async def proxy_local(path: str):
    async with httpx.AsyncClient(timeout=10) as client:
        resp = await client.get(f"http://fennec-app-local:8310/{path}")
    return resp.json()


@app.get("/api/mini/{path:path}")
async def proxy_mini(path: str):
    async with httpx.AsyncClient(timeout=10) as client:
        resp = await client.get(f"http://fennec-app-mini:8310/{path}")
    return resp.json()


# ── SSE: smoke test pod log streaming ─────────────────────────────────────────

_ANSI = re.compile(r"\x1b\[[0-9;]*m")


def _parse_line(line: str) -> dict:
    clean = _ANSI.sub("", line)
    if "--- TEST:" in clean:
        name = clean.split("--- TEST:")[1].replace("---", "").strip()
        return {"type": "test_start", "name": name, "raw": line}
    if "[PASS]" in clean:
        return {"type": "test_result", "result": "pass", "name": clean.split("[PASS]")[1].strip(), "raw": line}
    if "[FAIL]" in clean:
        return {"type": "test_result", "result": "fail", "name": clean.split("[FAIL]")[1].strip(), "raw": line}
    return {"type": "log", "raw": line}


async def _smoke_pods() -> list[str]:
    url = f"{K8S_BASE}/api/v1/namespaces/{NAMESPACE}/pods?labelSelector=app%3Dsmoke-test"
    async with httpx.AsyncClient(verify=_k8s_ca()) as client:
        resp = await client.get(url, headers=_k8s_headers())
    return [p["metadata"]["name"] for p in resp.json().get("items", [])]


async def _wait_for_pod_running(pod: str, timeout: int = 120):
    """Poll until pod has at least one container in running or terminated state."""
    url = f"{K8S_BASE}/api/v1/namespaces/{NAMESPACE}/pods/{pod}"
    deadline = asyncio.get_event_loop().time() + timeout
    async with httpx.AsyncClient(verify=_k8s_ca()) as client:
        while asyncio.get_event_loop().time() < deadline:
            resp = await client.get(url, headers=_k8s_headers())
            data = resp.json()
            for cs in data.get("status", {}).get("containerStatuses", []):
                if "running" in cs.get("state", {}) or "terminated" in cs.get("state", {}):
                    return True
            await asyncio.sleep(2)
    return False


async def _stream_logs(pod: str):
    url = f"{K8S_BASE}/api/v1/namespaces/{NAMESPACE}/pods/{pod}/log?follow=true"
    async with httpx.AsyncClient(verify=_k8s_ca(), timeout=None) as client:
        async with client.stream("GET", url, headers=_k8s_headers()) as resp:
            async for line in resp.aiter_lines():
                yield line


def _sse(data: dict) -> str:
    return f"data: {json.dumps(data)}\n\n"


@app.get("/events")
async def sse_events():
    async def stream():
        seen: set[str] = set()

        # Drain any pods that already exist at connect time so we don't
        # re-stream completed runs from before this browser session.
        try:
            for p in await _smoke_pods():
                seen.add(p)
            if seen:
                yield _sse({"type": "log", "raw": f"Watching for new runs (skipped {len(seen)} existing pod(s))..."})
        except Exception:
            pass

        while True:
            try:
                pods = await _smoke_pods()
                new = [p for p in pods if p not in seen]
                if new:
                    pod = new[-1]
                    seen.add(pod)
                    yield _sse({"type": "log", "raw": f"Waiting for {pod} to start..."})
                    ready = await _wait_for_pod_running(pod)
                    if not ready:
                        yield _sse({"type": "log", "raw": f"[timeout] {pod} never reached running state"})
                        continue
                    run = _start_run(pod)
                    yield _sse({"type": "run_start", "runId": pod, "startedAt": run["startedAt"], "raw": f"Streaming logs from {pod}..."})
                    async for line in _stream_logs(pod):
                        evt = _parse_line(line)
                        run["logs"].append(line)
                        if evt["type"] == "test_result":
                            run["tests"][evt["name"]] = evt["result"]
                        elif evt["type"] == "test_start":
                            run["tests"].setdefault(evt["name"], "running")
                        yield _sse({**evt, "runId": pod})
                    run["status"] = "complete"
                    yield _sse({"type": "done", "runId": pod, "raw": "--- run complete ---"})
                else:
                    # No new pod — send SSE comment as keepalive so the
                    # connection stays open without triggering onmessage.
                    yield ": keepalive\n\n"
                    await asyncio.sleep(3)
            except Exception as e:
                yield _sse({"type": "log", "raw": f"[dashboard error] {e}"})
                await asyncio.sleep(5)

    return StreamingResponse(
        stream(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


# ── Job management ─────────────────────────────────────────────────────────────

_JOB_BASE = {
    "apiVersion": "batch/v1",
    "kind": "Job",
    "metadata": {"name": "smoke-test", "namespace": NAMESPACE},
    "spec": {
        "backoffLimit": 0,
        "template": {
            "metadata": {"labels": {"app": "smoke-test"}},
            "spec": {
                "restartPolicy": "Never",
                "containers": [{
                    "name": "test-runner",
                    "image": "fennec-test-runner:latest",
                    "imagePullPolicy": "Never",
                    "env": [
                        {"name": "LOCAL", "value": "http://fennec-app-local:8310"},
                        {"name": "MINI", "value": "http://fennec-app-mini:8310"},
                        {"name": "RESTART_MODE", "value": "k8s"},
                        {"name": "EXIT_ON_FAIL", "value": "0"},
                    ],
                }],
            },
        },
    },
}


async def _delete_job():
    url = f"{K8S_BASE}/apis/batch/v1/namespaces/{NAMESPACE}/jobs/smoke-test?propagationPolicy=Foreground"
    async with httpx.AsyncClient(verify=_k8s_ca()) as client:
        await client.delete(url, headers=_k8s_headers())
    await asyncio.sleep(1)


async def _create_job(job: dict) -> int:
    url = f"{K8S_BASE}/apis/batch/v1/namespaces/{NAMESPACE}/jobs"
    async with httpx.AsyncClient(verify=_k8s_ca()) as client:
        resp = await client.post(
            url,
            headers={**_k8s_headers(), "Content-Type": "application/json"},
            json=job,
        )
    return resp.status_code


@app.post("/api/run")
async def run_all():
    await _delete_job()
    status = await _create_job(copy.deepcopy(_JOB_BASE))
    return {"status": status}


@app.post("/api/run/{test_name}")
async def run_single(test_name: str):
    await _delete_job()
    job = copy.deepcopy(_JOB_BASE)
    container = job["spec"]["template"]["spec"]["containers"][0]
    # Pass test name as CMD arg (script accepts positional arg after --no-restart)
    container["command"] = ["bash", "screen-share-smoke.sh", "--no-restart", test_name]
    status = await _create_job(job)
    return {"status": status}


@app.delete("/api/run")
async def cancel_run():
    await _delete_job()
    return {"status": "deleted"}


_UNLOCK_SCRIPT = r"""
import ctypes, ctypes.util, time, sys

cg = ctypes.CDLL('/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics')
cg.CGEventCreateKeyboardEvent.restype = ctypes.c_void_p
cg.CGEventPost.argtypes = [ctypes.c_uint32, ctypes.c_void_p]
cg.CGEventKeyboardSetUnicodeString.argtypes = [ctypes.c_void_p, ctypes.c_ulong, ctypes.POINTER(ctypes.c_uint16)]
cg.CFRelease.argtypes = [ctypes.c_void_p]

HID_TAP = 0  # kCGHIDEventTap — reaches lock screen from root

def post_unicode(ch, down):
    buf = ctypes.c_uint16(ord(ch))
    evt = cg.CGEventCreateKeyboardEvent(None, 0, down)
    cg.CGEventKeyboardSetUnicodeString(evt, 1, ctypes.byref(buf))
    cg.CGEventPost(HID_TAP, evt)
    cg.CFRelease(evt)

def post_keycode(code, down):
    evt = cg.CGEventCreateKeyboardEvent(None, code, down)
    cg.CGEventPost(HID_TAP, evt)
    cg.CFRelease(evt)

password = sys.argv[1]
time.sleep(0.3)
for ch in password:
    post_unicode(ch, True);  time.sleep(0.05)
    post_unicode(ch, False); time.sleep(0.05)
post_keycode(36, True);  time.sleep(0.05)  # Return
post_keycode(36, False)
"""


@app.post("/api/unlock-mini")
async def unlock_mini():
    """SSH into Mac Mini and send password via CoreGraphics HID events.

    VNC keyboard input is blocked by macOS 15 on the lock screen.
    CGEventPost with kCGHIDEventTap (run as root via sudo) bypasses that.
    """
    host = os.environ.get("MINI_HOST", "")
    user = os.environ.get("MINI_USER", "")
    password = os.environ.get("MINI_VNC_PASSWORD", "")
    if not host or not user or not password:
        return {"error": "MINI_HOST / MINI_USER / MINI_VNC_PASSWORD not set"}

    ssh_opts = [
        "-i", "/ssh/id_ed25519",
        "-o", "StrictHostKeyChecking=no",
        "-o", "UserKnownHostsFile=/dev/null",
        "-o", "ConnectTimeout=10",
    ]
    # Write script to a temp file on Mac Mini via stdin, then run as root.
    # We pass the password as a CLI arg so it never appears in the script text.
    remote_cmd = (
        f"python3 -c {__import__('shlex').quote('exec(open(\"/tmp/_unlock.py\").read())')} {__import__('shlex').quote(password)}"
    )
    # Step 1: upload script
    upload = await asyncio.create_subprocess_exec(
        "ssh", *ssh_opts, f"{user}@{host}",
        f"cat > /tmp/_unlock.py",
        stdin=asyncio.subprocess.PIPE,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
    )
    await upload.communicate(input=_UNLOCK_SCRIPT.encode())

    # Step 2: run as root (NOPASSWD sudo on Mac Mini)
    run = await asyncio.create_subprocess_exec(
        "ssh", *ssh_opts, f"{user}@{host}",
        f"sudo python3 /tmp/_unlock.py {__import__('shlex').quote(password)}",
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
    )
    stdout, stderr = await asyncio.wait_for(run.communicate(), timeout=15)
    return {
        "ok": run.returncode == 0,
        "returncode": run.returncode,
        "stderr": stderr.decode()[:500],
    }


# ── noVNC WebSocket proxy ──────────────────────────────────────────────────────

async def _ws_proxy(client_ws: WebSocket, target_url: str, filter_sec_types: set | None = None):
    # Negotiate subprotocol: forward whatever the client offers, prefer "binary"
    raw = client_ws.headers.get("sec-websocket-protocol", "")
    offered = [s.strip() for s in raw.split(",") if s.strip()]
    subprotocol = "binary" if "binary" in offered else (offered[0] if offered else None)
    await client_ws.accept(subprotocol=subprotocol)
    try:
        async with websockets.connect(
            target_url,
            subprotocols=[subprotocol] if subprotocol else [],
            ping_interval=None,
        ) as server_ws:

            async def to_server():
                try:
                    async for msg in client_ws.iter_bytes():
                        await server_ws.send(msg)
                except Exception:
                    pass
                finally:
                    await server_ws.close()

            server_msg_index = 0

            async def to_client():
                nonlocal server_msg_index
                try:
                    async for msg in server_ws:
                        if isinstance(msg, bytes) and filter_sec_types and server_msg_index == 1:
                            # Second server message is the security-types list.
                            # Keep only the types in filter_sec_types.
                            n = msg[0]
                            kept = [t for t in msg[1:1 + n] if t in filter_sec_types]
                            msg = bytes([len(kept)] + kept) if kept else msg
                        server_msg_index += 1
                        if isinstance(msg, bytes):
                            await client_ws.send_bytes(msg)
                        else:
                            await client_ws.send_text(msg)
                except Exception:
                    pass

            await asyncio.gather(to_server(), to_client())
    except Exception:
        pass
    finally:
        try:
            await client_ws.close()
        except Exception:
            pass


@app.websocket("/ws/local")
async def novnc_local(ws: WebSocket):
    await _ws_proxy(ws, "ws://fennec-app-local:6080")


@app.websocket("/ws/mini")
async def novnc_mini(ws: WebSocket):
    # Route through the persistent VNC proxy so the upstream connection to Mac Mini
    # never drops — macOS 15 locks the screen on every VNC disconnect, not just the last.
    await _proxy.serve(ws)


# ── Static files (registered last so WS routes take priority) ─────────────────

app.mount("/novnc", StaticFiles(directory="static/novnc"), name="novnc")
app.mount("/static", StaticFiles(directory="static"), name="static")


@app.get("/")
async def root():
    return HTMLResponse(Path("static/index.html").read_text())
