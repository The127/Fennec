// ── Run history ────────────────────────────────────────────────────────────────

const _runs = [];     // [{id, startedAt, tests: Map, logs: [], status}], newest first
let _liveId = null;   // run currently executing
let _viewId = null;   // run currently displayed in the UI

function _findRun(id) { return _runs.find(r => r.id === id); }

function _addRun(id, startedAt) {
  const run = { id, startedAt, tests: new Map(), logs: [], status: 'running' };
  _runs.unshift(run);
  renderRunList();
  return run;
}

function _viewRun(id) {
  _viewId = id;
  const run = _findRun(id);
  if (!run) return;
  tests.clear();
  for (const [k, v] of run.tests) tests.set(k, v);
  allLogs.length = 0;
  allLogs.push(...run.logs);
  activeFilter = null;
  currentTest = null;
  renderTestList();
  renderLogs();
  updateLogHeader();
  renderRunList();
}

function _runDotClass(run) {
  if (run.status === 'running') return 'running';
  const vals = [...run.tests.values()];
  if (vals.some(v => v === 'fail')) return 'fail';
  if (vals.length > 0) return 'pass';
  return '';
}

const runSelectEl = document.getElementById('run-select');
runSelectEl.onchange = () => { if (runSelectEl.value) _viewRun(runSelectEl.value); };

function renderRunList() {
  runSelectEl.innerHTML = '';
  if (_runs.length === 0) {
    runSelectEl.add(new Option('no runs', ''));
    return;
  }
  for (const run of _runs) {
    const ts = new Date(run.startedAt).toLocaleTimeString();
    const pass = [...run.tests.values()].filter(v => v === 'pass').length;
    const fail = [...run.tests.values()].filter(v => v === 'fail').length;
    const total = run.tests.size;
    const counts = total > 0
      ? ` ${pass}/${total}${fail > 0 ? ` · ${fail} fail` : ''}`
      : '';
    const status = _runDotClass(run);
    const prefix = status === 'running' ? '⟳ ' : status === 'fail' ? '✗ ' : status === 'pass' ? '✓ ' : '';
    const opt = new Option(prefix + ts + counts, run.id);
    if (_viewId === run.id) opt.selected = true;
    runSelectEl.add(opt);
  }
}

async function fetchRuns() {
  try {
    const resp = await fetch('/api/runs');
    if (!resp.ok) return;
    const data = await resp.json();
    for (const r of data) {
      if (!_findRun(r.id)) {
        const run = { id: r.id, startedAt: r.startedAt, tests: new Map(Object.entries(r.tests)), logs: [], status: r.status };
        _runs.push(run);
      }
    }
    // Load logs for the most recent run
    if (_runs.length > 0 && _viewId === null) {
      const newest = _runs[0];
      const full = await fetch(`/api/runs/${encodeURIComponent(newest.id)}`).then(r => r.json()).catch(() => null);
      if (full) {
        newest.logs = full.logs.map(raw => ({ test: null, raw }));
        _viewRun(newest.id);
      } else {
        renderRunList();
      }
    } else {
      renderRunList();
    }
  } catch (_) {}
}

// ── State ─────────────────────────────────────────────────────────────────────

const tests = new Map(); // name → status string (view state)
let currentTest = null;  // name of test whose lines are currently accumulating
let activeFilter = null; // null = show all, string = show only that test
const allLogs = [];      // [{test: null|name, raw: string}] (view state)

// ── SSE log stream ─────────────────────────────────────────────────────────────

const logEl = document.getElementById('log');
const sseEl = document.getElementById('sse-indicator');

const ANSI_COLORS = [
  [/\x1b\[0m/g,        '</span>'],
  [/\x1b\[32m/g,       '<span class="log-pass">'],
  [/\x1b\[31m/g,       '<span class="log-fail">'],
  [/\x1b\[36m/g,       '<span class="log-step">'],
  [/\x1b\[33m/g,       '<span class="log-warn">'],
  [/\x1b\[[0-9;]+m/g,  ''],
];

function escapeHtml(s) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function renderAnsi(raw) {
  let s = escapeHtml(raw);
  for (const [re, rep] of ANSI_COLORS) s = s.replace(re, rep);
  return s;
}

function stripAnsi(raw) {
  return raw.replace(/\x1b\[[0-9;]*m/g, '');
}

function renderLogs() {
  logEl.innerHTML = '';
  const lines = activeFilter === null
    ? allLogs
    : allLogs.filter(e => e.test === activeFilter || e.test === null);
  for (const { raw } of lines) {
    const div = document.createElement('div');
    div.innerHTML = renderAnsi(raw);
    logEl.appendChild(div);
  }
  logEl.scrollTop = logEl.scrollHeight;
}

function appendLog(raw) {
  const entry = { test: currentTest, raw };
  allLogs.push(entry);
  // Also store in the live run
  const liveRun = _findRun(_liveId);
  if (liveRun) liveRun.logs.push(entry);

  if (_viewId === _liveId && (activeFilter === null || activeFilter === currentTest)) {
    const div = document.createElement('div');
    div.innerHTML = renderAnsi(raw);
    logEl.appendChild(div);
    logEl.scrollTop = logEl.scrollHeight;
  }
}

function connectSSE() {
  const es = new EventSource('/events');

  es.onopen = () => { sseEl.style.color = '#3fb950'; };

  es.onerror = () => {
    sseEl.style.color = '#f85149';
    setTimeout(connectSSE, 3000);
    es.close();
  };

  es.onmessage = (e) => {
    const evt = JSON.parse(e.data);
    const isViewing = _viewId === (evt.runId ?? _liveId);

    if (evt.type === 'run_start') {
      _liveId = evt.runId;
      _viewId = evt.runId;
      const run = _addRun(evt.runId, evt.startedAt);
      tests.clear();
      allLogs.length = 0;
      currentTest = null;
      activeFilter = null;
      renderTestList();
      renderLogs();
      updateLogHeader();
    } else if (evt.type === 'test_start') {
      currentTest = evt.name;
      const liveRun = _findRun(_liveId);
      if (liveRun) liveRun.tests.set(evt.name, 'running');
      if (isViewing) {
        upsertTest(evt.name, 'running');
        jobStatusEl.textContent = `running: ${evt.name}`;
      }
      appendLog(evt.raw);
      renderRunList();
    } else if (evt.type === 'test_result') {
      const liveRun = _findRun(_liveId);
      if (liveRun) liveRun.tests.set(evt.name, evt.result);
      if (isViewing) upsertTest(evt.name, evt.result);
      appendLog(evt.raw);
      currentTest = null;
      renderRunList();
    } else if (evt.type === 'done') {
      const liveRun = _findRun(_liveId);
      if (liveRun) liveRun.status = 'complete';
      if (isViewing) {
        jobStatusEl.textContent = 'run complete';
        btnRunAll.disabled = false;
      }
      appendLog(evt.raw);
      currentTest = null;
      renderRunList();
    } else {
      appendLog(evt.raw);
    }

    if (isViewing) updateSummary();
  };
}

// ── Test list ─────────────────────────────────────────────────────────────────

const testListEl = document.getElementById('test-list');

function upsertTest(name, status) {
  tests.set(name, status);
  renderTestList();
}

function setFilter(name) {
  activeFilter = (activeFilter === name) ? null : name;
  renderTestList();
  renderLogs();
  updateLogHeader();
}

function renderTestList() {
  testListEl.innerHTML = '';
  for (const [name, status] of tests) {
    const row = document.createElement('div');
    row.className = 'test-item' + (activeFilter === name ? ' selected' : '');

    const dot = document.createElement('div');
    dot.className = `test-dot ${status}`;

    const label = document.createElement('span');
    label.className = 'test-name';
    label.textContent = name;
    label.onclick = () => setFilter(name);
    label.style.cursor = 'pointer';

    const btn = document.createElement('button');
    btn.className = 'test-btn';
    btn.textContent = '▶';
    btn.title = `Run: ${name}`;
    btn.onclick = (ev) => { ev.stopPropagation(); runTest(name); };

    row.appendChild(dot);
    row.appendChild(label);
    row.appendChild(btn);
    testListEl.appendChild(row);
  }
}

// ── Controls ──────────────────────────────────────────────────────────────────

const btnRunAll   = document.getElementById('btn-run-all');
const btnCancel   = document.getElementById('btn-cancel');
const summaryEl   = document.getElementById('summary');
const jobStatusEl = document.getElementById('job-status');
const logHeaderEl = document.getElementById('log-header-filter');
const btnCopy     = document.getElementById('btn-copy-logs');

function updateLogHeader() {
  logHeaderEl.textContent = activeFilter ? `Logs — ${activeFilter}` : 'Logs';
}

function updateSummary() {
  let pass = 0, fail = 0, running = 0;
  for (const s of tests.values()) {
    if (s === 'pass') pass++;
    else if (s === 'fail') fail++;
    else if (s === 'running') running++;
  }
  const total = tests.size;
  let txt = `${pass + fail}/${total}`;
  if (fail > 0) txt += ` <span style="color:#f85149">${fail} fail</span>`;
  if (running > 0) txt += ` <span style="color:#d29922">running</span>`;
  summaryEl.innerHTML = txt;
}

async function runAll() {
  btnRunAll.disabled = true;
  jobStatusEl.textContent = 'starting...';
  const resp = await fetch('/api/run', { method: 'POST' });
  const data = await resp.json();
  if (data.status >= 300) {
    jobStatusEl.textContent = `error ${data.status}`;
    btnRunAll.disabled = false;
  }
}

async function runTest(name) {
  btnRunAll.disabled = true;
  jobStatusEl.textContent = 'starting...';
  const resp = await fetch(`/api/run/${encodeURIComponent(name)}`, { method: 'POST' });
  const data = await resp.json();
  if (data.status >= 300) {
    jobStatusEl.textContent = `error ${data.status}`;
    btnRunAll.disabled = false;
  }
}

async function cancelRun() {
  await fetch('/api/run', { method: 'DELETE' });
  jobStatusEl.textContent = 'cancelled';
  btnRunAll.disabled = false;
}

function copyLogs() {
  const lines = activeFilter === null
    ? allLogs
    : allLogs.filter(e => e.test === activeFilter || e.test === null);
  const text = lines.map(e => stripAnsi(e.raw)).join('\n');

  const confirm = () => {
    btnCopy.textContent = 'Copied!';
    setTimeout(() => { btnCopy.textContent = 'Copy'; }, 1500);
  };
  const fallback = () => {
    const ta = document.createElement('textarea');
    ta.value = text;
    ta.style.cssText = 'position:fixed;left:-9999px;top:0';
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    try {
      if (document.execCommand('copy')) confirm();
    } catch (_) {}
    document.body.removeChild(ta);
  };

  if (navigator.clipboard) {
    navigator.clipboard.writeText(text).then(confirm).catch(fallback);
  } else {
    fallback();
  }
}

async function unlockMini() {
  const btn = document.getElementById('btn-unlock-mini');
  btn.disabled = true;
  btn.textContent = 'Unlocking…';
  try {
    const resp = await fetch('/api/unlock-mini', { method: 'POST' });
    const data = await resp.json();
    btn.textContent = data.ok ? 'Sent!' : `Error: ${data.error}`;
  } catch (e) {
    btn.textContent = 'Failed';
  }
  setTimeout(() => { btn.textContent = 'Unlock'; btn.disabled = false; }, 2000);
}

btnRunAll.onclick = runAll;
btnCancel.onclick = cancelRun;
btnCopy.onclick   = copyLogs;
document.getElementById('btn-unlock-mini').onclick = unlockMini;

// ── Control API polling ────────────────────────────────────────────────────────

const _cachedUserId = {};

async function _getUserId(instance) {
  if (_cachedUserId[instance]) return _cachedUserId[instance];
  const resp = await fetch(`/api/${instance}/auth/state`);
  if (!resp.ok) return null;
  const data = await resp.json();
  if (data.userId) _cachedUserId[instance] = data.userId;
  return data.userId ?? null;
}

async function pollState(instance, prefix) {
  try {
    const resp = await fetch(`/api/${instance}/voice/state`);
    if (!resp.ok) return;
    const data = await resp.json();

    const peers   = Object.keys(data.peerStates || {}).length;
    const sharing = data.isScreenSharing ?? null;

    document.getElementById(`${prefix}-peers`).textContent = peers;

    const sharingEl = document.getElementById(`${prefix}-sharing`);
    sharingEl.textContent = sharing === true ? '✓' : sharing === false ? '✗' : '–';
    sharingEl.className = `state-val ${sharing === true ? 'state-ok' : sharing === false ? 'state-bad' : ''}`;

    let fps = '–';
    if (sharing) {
      const userId = await _getUserId(instance);
      if (userId) {
        const mResp = await fetch(`/api/${instance}/screen-share/metrics/${userId}`);
        if (mResp.ok) {
          const m = await mResp.json();
          fps = typeof m.sentFps === 'number' ? m.sentFps.toFixed(1) : '–';
        }
      }
    }
    document.getElementById(`${prefix}-fps`).textContent = fps;
  } catch (_) {
    // instance not reachable yet
  }
}

function startPolling() {
  setInterval(() => {
    pollState('local', 'local');
    pollState('mini',  'mini');
  }, 2000);
}

// ── Boot ──────────────────────────────────────────────────────────────────────

fetchRuns();
connectSSE();
startPolling();
