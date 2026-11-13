import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import path from 'node:path';
import vm from 'node:vm';
import { fileURLToPath } from 'node:url';

// Usage: node test_storage_synchronization.mjs --runtime <built dotnet.native.*.js>
// Optional --installer tests a separate source checkout; --report preserves evidence.
const { values: args } = parseArgs({ options: {
  runtime: { type: 'string' }, installer: { type: 'string' }, report: { type: 'string' },
}, strict: true, allowPositionals: false });
assert(args.runtime, 'Provide the actual built native runtime with --runtime');
const root = path.dirname(fileURLToPath(import.meta.url));
const runtimePath = path.resolve(args.runtime);
const installerPath = args.installer ? path.resolve(args.installer)
  : path.resolve(root, '../../samples/UnoDraw/Platforms/WebAssembly/BrowserStorageSynchronization.js');
const runtime = readFileSync(runtimePath, 'utf8');
const installer = readFileSync(installerPath, 'utf8');
const sha = value => createHash('sha256').update(value).digest('hex');
const startMarker = 'var IDBFS=', endMarker = ';var FS=';
const start = runtime.indexOf(startMarker);
assert(start >= 0 && runtime.indexOf(startMarker, start + startMarker.length) === -1,
  'The pinned runtime requires exactly one original IDBFS object');
const end = runtime.indexOf(endMarker, start);
assert(end > start, 'The pinned IDBFS/FS extraction boundary changed');
const source = runtime.slice(start, end + 1);
assert(source.startsWith('var IDBFS={') && source.endsWith('};'), 'The IDBFS object format changed');
// Review the helper semantics explicitly when upgrading the pinned runtime.
assert.equal(sha(source), '2956e143931ec282d48bbc897f1ac3599430275f19fda70df33c9fc427a11bb3',
  'The real pinned IDBFS helpers changed; do not silently qualify a new ordering contract');
const FILE = 0o100644, DIR = 0o40755, MOUNT = '/data';
const bytes = [0, 1, 17, 127, 128, 254, 255];
const file = (contents = bytes, time = 20) => ({ mode: FILE, timestamp: new Date(time), contents: Uint8Array.from(contents) });
const directory = (time = 10) => ({ mode: DIR, timestamp: new Date(time) });
const copy = value => structuredClone(value);
const error = (message, errno) => Object.assign(new Error(message), errno === undefined ? {} : { errno });
const serial = entries => [...entries].sort(([a], [b]) => a.localeCompare(b)).map(([name, entry]) => ({
  path: name, mode: entry.mode, timestamp: entry.timestamp.getTime(),
  ...(entry.contents ? { contents: [...entry.contents], contents_sha256: sha(entry.contents) } : {}),
}));

// These doubles implement only the APIs used by the original IDBFS object.
// IndexedDB requests remain queued events; put() structured-clones bytes now,
// while write transactions publish their staged state only on completion.
function fixture({ local = { '/data/old.svg': file() }, remote = {}, duringEnumeration,
                   openFailure, enumerationFailure, localStatFailure, writeFailure,
                   hydrationFailure, afterPut } = {}) {
  const trace = [], queue = [], localNodes = new Map([[MOUNT, directory()], ...Object.entries(copy(local))]);
  let remoteNodes = new Map(Object.entries(copy(remote)));
  let turn = 0, firstEnumeration = true, firstPut = true;
  const record = (event, name) => trace.push({ turn, event, ...(name ? { path: name } : {}) });
  const enqueue = (event, work) => queue.push({ event, work });
  const missing = name => error('No such file or directory: ' + name, 44);
  const node = name => { const value = localNodes.get(name); if (!value) throw missing(name); return value; };
  const parent = name => path.posix.dirname(name);
  const localSnapshot = () => new Map([...localNodes].filter(([name]) => name !== MOUNT));
  const FS = {
    readdir(name) {
      record('local-readdir', name);
      assert.equal(node(name).mode & 0o170000, 0o40000);
      return ['.', '..', ...[...localNodes.keys()].filter(key => key !== name && parent(key) === name)
        .map(key => path.posix.basename(key))];
    },
    stat(name) {
      record('local-stat', name);
      if (localStatFailure && name === localStatFailure.path) throw localStatFailure.error;
      const value = node(name); return { mode: value.mode, mtime: new Date(value.timestamp) };
    },
    lookupPath(name) { record('local-lookup', name); return { node: node(name) }; },
    isDir(mode) { return (mode & 0o170000) === 0o40000; },
    isFile(mode) { return (mode & 0o170000) === 0o100000; },
    mkdirTree(name, mode) {
      record('local-mkdir', name);
      const pieces = name.split('/').filter(Boolean); let cursor = '';
      for (const piece of pieces) { cursor += '/' + piece; if (!localNodes.has(cursor)) localNodes.set(cursor, directory()); }
      localNodes.get(name).mode = mode;
    },
    writeFile(name, contents) {
      record('local-write', name);
      if (hydrationFailure) throw hydrationFailure;
      assert.equal(node(parent(name)).mode & 0o170000, 0o40000);
      localNodes.set(name, file(contents, 0));
    },
    chmod(name, mode) { node(name).mode = mode; },
    utime(name, atime, mtime) { node(name).timestamp = new Date(mtime); },
    rmdir(name) {
      record('local-rmdir', name);
      assert(![...localNodes.keys()].some(key => key !== name && key.startsWith(name + '/')));
      if (!localNodes.delete(name)) throw missing(name);
    },
    unlink(name) { record('local-unlink', name); if (!localNodes.delete(name)) throw missing(name); },
    rename(from, to) {
      record('local-rename', from); node(from);
      const changes = [...localNodes].filter(([name]) => name === from || name.startsWith(from + '/'));
      for (const [name] of changes) localNodes.delete(name);
      for (const [name, value] of changes) localNodes.set(to + name.slice(from.length), value);
    },
  };
  const event = failure => ({ target: { error: failure }, preventDefault() { record('prevent-default'); } });
  const db = {
    close() {},
    transaction(stores, mode) {
      assert.deepEqual(Array.from(stores), ['FILE_DATA']); record('transaction-' + mode);
      const staged = new Map([...remoteNodes].map(([key, entry]) => [key, copy(entry)]));
      let pending = 0, aborted = false, completed = false, completionQueued = false;
      const transaction = {};
      const queueCompletion = () => {
        if (mode !== 'readwrite' || completionQueued || completed) return;
        completionQueued = true;
        enqueue('transaction-completion', () => {
          completionQueued = false;
          if (pending || completed) return;
          completed = true;
          if (aborted) return;
          remoteNodes = staged;
          record('transaction-committed');
          transaction.oncomplete?.({ target: transaction });
        });
      };
      const request = (kind, name, value) => {
        const req = {}; pending++; record('remote-' + kind, name);
        // IDBObjectStore.put() serializes immediately, before the async request.
        const snapshot = value === undefined ? undefined : copy(value);
        enqueue('request-' + kind, () => {
          if (writeFailure && kind !== 'get') {
            aborted = true;
            req.onerror?.(event(writeFailure));
            transaction.onerror?.(event(writeFailure));
            transaction.onabort?.(event(writeFailure));
          } else {
            if (kind === 'put') staged.set(name, snapshot);
            if (kind === 'delete') staged.delete(name);
            req.onsuccess?.({ target: { result: kind === 'get' ? copy(staged.get(name)) : name } });
          }
          pending--; queueCompletion();
        });
        if (kind === 'put' && firstPut && afterPut) {
          firstPut = false; enqueue('after-put-mutation', () => afterPut(FS, localNodes));
        }
        return req;
      };
      const store = {
        index(name) {
          assert.equal(name, 'timestamp');
          return { openKeyCursor() {
            const req = {}, entries = [...staged].sort(([a], [b]) => a.localeCompare(b));
            let next = 0;
            const advance = () => enqueue('cursor-event', () => {
              if (firstEnumeration) {
                firstEnumeration = false;
                if (duringEnumeration) { record('enumeration-mutation'); duringEnumeration(FS, localNodes); }
              }
              if (enumerationFailure) { transaction.onerror?.(event(enumerationFailure)); return; }
              record('remote-cursor');
              const entry = entries[next++];
              const cursor = entry ? { primaryKey: entry[0], key: new Date(entry[1].timestamp), continue: advance } : null;
              req.onsuccess?.({ target: { result: cursor } });
            });
            advance(); return req;
          } };
        },
        get(name) { return request('get', name); },
        put(value, name) { return request('put', name, value); },
        delete(name) { return request('delete', name); },
      };
      transaction.objectStore = name => { assert.equal(name, 'FILE_DATA'); return store; };
      queueCompletion(); return transaction;
    },
  };
  const indexedDB = { open(name, version) {
    record('open-database', name); assert.equal(name, MOUNT); assert.equal(version, 21);
    const req = {};
    enqueue('open-event', () => {
      if (openFailure) { req.onerror?.(event(openFailure)); return; }
      req.result = db; req.onsuccess?.({ target: req });
    });
    return req;
  } };
  const context = vm.createContext({ FS, indexedDB, PATH: { join2: path.posix.join },
    MEMFS: { mount() { throw new Error('Mount is outside this test'); }, getFileDataAsTypedArray: entry => entry.contents } });
  vm.runInContext(source, context, { filename: runtimePath + '#IDBFS' });
  const IDBFS = context.IDBFS;
  const helperHashes = () => Object.fromEntries(Object.entries(IDBFS)
    .filter(([name, value]) => name !== 'syncfs' && typeof value === 'function')
    .map(([name, value]) => [name, sha(value.toString())]));
  const originalHelpers = helperHashes();
  function useRemoteFirstFlush() {
    assert.equal(vm.runInContext(installer, context, { filename: installerPath }), 'installed');
    const installed = IDBFS.syncfs;
    assert.equal(vm.runInContext(installer, context, { filename: installerPath }), 'already-installed');
    assert.equal(IDBFS.syncfs, installed, 'Repeated installation must preserve the existing wrapper');
    assert.deepEqual(helperHashes(), originalHelpers);
  }
  function run({ corrected = false, populate = false } = {}) {
    if (corrected) useRemoteFirstFlush();
    const callbacks = [];
    IDBFS.syncfs({ mountpoint: MOUNT }, populate, failure => {
      record('callback'); callbacks.push(failure ?? null);
    });
    let events = 0;
    while (queue.length) {
      const task = queue.shift(); turn++; record('event-' + task.event); task.work();
      assert(++events <= 1000, 'The controlled scheduler did not drain');
    }
    assert.equal(callbacks.length, 1, 'The completion callback must fire exactly once after all events drain');
    return { callbacks, trace, local: localSnapshot(), remote: remoteNodes, originalHelpers };
  }
  return { run };
}

const cases = [];
function record(name, mode, result) {
  cases.push({ name, mode, callback_count: result.callbacks.length,
    error: result.callbacks[0] ? { message: result.callbacks[0].message, errno: result.callbacks[0].errno ?? null } : null,
    local: serial(result.local), persisted: serial(result.remote), trace: result.trace,
    unchanged_helper_sha256: result.originalHelpers });
}
function success(result) { assert.equal(result.callbacks[0], null); }
function persistedEqualsLocal(result) { assert.deepEqual(serial(result.remote), serial(result.local)); }
function runPair(name, options, check) {
  const old = fixture(options).run();
  const corrected = fixture(options).run({ corrected: true });
  check(old, corrected);
  record(name, 'original', old); record(name, 'remote-first-flush', corrected);
}

runPair('unchanged-flush-byte-preservation', { remote: { '/data/stale.svg': file([3], 1) } }, (old, corrected) => {
  success(old); success(corrected); persistedEqualsLocal(old); persistedEqualsLocal(corrected);
  assert.deepEqual(serial(old.remote), serial(corrected.remote));
});
runPair('rename-during-remote-enumeration', {
  remote: { '/data/old.svg': file([9], 1) }, duringEnumeration: FS => FS.rename('/data/old.svg', '/data/new.svg'),
}, (old, corrected) => {
  assert.equal(old.callbacks[0]?.errno, 44); success(corrected); persistedEqualsLocal(corrected);
  assert(!corrected.remote.has('/data/old.svg')); assert.deepEqual([...corrected.remote.get('/data/new.svg').contents], bytes);
  const snapshot = result => result.trace.find(row => row.event === 'local-readdir' && row.path === MOUNT).turn;
  const mutation = result => result.trace.find(row => row.event === 'enumeration-mutation').turn;
  assert(snapshot(old) < mutation(old)); assert(snapshot(corrected) >= mutation(corrected));
  const lookupTurns = corrected.trace.filter(row => row.event === 'local-lookup').map(row => row.turn);
  assert(lookupTurns.length && lookupTurns.every(turn => turn === snapshot(corrected)));
});
runPair('delete-during-remote-enumeration', {
  remote: { '/data/old.svg': file([9], 1) }, duringEnumeration: FS => FS.unlink('/data/old.svg'),
}, (old, corrected) => {
  assert.equal(old.callbacks[0]?.errno, 44); success(corrected); persistedEqualsLocal(corrected); assert.equal(corrected.remote.size, 0);
});
runPair('rewrite-during-remote-enumeration', {
  remote: { '/data/old.svg': file() },
  duringEnumeration: (FS, nodes) => nodes.set('/data/old.svg', file([255, 0, 87, 4], 40)),
}, (old, corrected) => {
  success(old); success(corrected); assert.notDeepEqual(serial(old.remote), serial(old.local)); persistedEqualsLocal(corrected);
});
runPair('create-during-remote-enumeration', {
  local: {}, duringEnumeration: (FS, nodes) => nodes.set('/data/new.svg', file([2, 4, 6, 8], 40)),
}, (old, corrected) => {
  success(old); success(corrected); assert.equal(old.remote.size, 0); persistedEqualsLocal(corrected);
});
runPair('nested-directory-rename', {
  local: { '/data/old': directory(), '/data/old/art.svg': file() },
  remote: { '/data/old': directory(1), '/data/old/art.svg': file([4], 1) },
  duringEnumeration: FS => FS.rename('/data/old', '/data/new'),
}, (old, corrected) => {
  assert.equal(old.callbacks[0]?.errno, 44); success(corrected); persistedEqualsLocal(corrected);
  assert.deepEqual([...corrected.remote.get('/data/new/art.svg').contents], bytes);
});

for (const kind of ['openFailure', 'enumerationFailure', 'writeFailure']) {
  const failure = error('Controlled ' + kind, 28);
  runPair('propagates-' + kind, { [kind]: failure }, (old, corrected) => {
    assert.equal(old.callbacks[0], failure); assert.equal(corrected.callbacks[0], failure);
  });
}
{
  const failure = error('Controlled local stat error', 13);
  runPair('propagates-local-stat-failure', { localStatFailure: { path: '/data/old.svg', error: failure } }, (old, corrected) => {
    assert.equal(old.callbacks[0], failure); assert.equal(corrected.callbacks[0], failure);
  });
}
runPair('put-clones-bytes-before-later-local-mutation', {
  afterPut: (FS, nodes) => nodes.get('/data/old.svg').contents.fill(99),
}, (old, corrected) => {
  success(old); success(corrected);
  assert.deepEqual([...old.remote.get('/data/old.svg').contents], bytes);
  assert.deepEqual([...corrected.remote.get('/data/old.svg').contents], bytes);
  assert([...corrected.local.get('/data/old.svg').contents].every(value => value === 99));
});

for (const failing of [false, true]) {
  const failure = error('Controlled hydration write failure', 28);
  const options = { local: { '/data/stale.svg': file([3], 1) },
    remote: { '/data/docs': directory(30), '/data/docs/document.svg': file(bytes, 50) },
    ...(failing ? { hydrationFailure: failure } : {}) };
  const old = fixture(options).run({ populate: true });
  const corrected = fixture(options).run({ corrected: true, populate: true });
  assert.deepEqual(old.trace, corrected.trace, 'populate=true must preserve the original event order');
  assert.deepEqual(serial(old.local), serial(corrected.local));
  assert.deepEqual(serial(old.remote), serial(corrected.remote));
  if (failing) { assert.equal(old.callbacks[0], failure); assert.equal(corrected.callbacks[0], failure); }
  else { success(old); success(corrected); persistedEqualsLocal(corrected); }
  record(failing ? 'hydration-error-preserved' : 'initial-hydration-preserved', 'original', old);
  record(failing ? 'hydration-error-preserved' : 'initial-hydration-preserved', 'remote-first-flush', corrected);
}

const guardResults = [];
const host = () => ({ syncfs() {}, getRemoteSet() {}, getLocalSet() {}, reconcile() {} });
const readonly = host(); Object.defineProperty(readonly, 'syncfs', { writable: false });
const accessor = host(), getterValue = accessor.syncfs;
Object.defineProperty(accessor, 'syncfs', { get() { return getterValue; } });
const missing = host(); delete missing.reconcile;
for (const [name, target] of [
  ['absent-host', undefined], ['null-host', null], ['missing-helper', missing],
  ['frozen-host', Object.freeze(host())], ['nonextensible-host', Object.preventExtensions(host())],
  ['readonly-syncfs', readonly], ['accessor-syncfs', accessor], ['inherited-syncfs', Object.create(host())],
]) {
  const context = vm.createContext(target === undefined ? {} : { IDBFS: target });
  const before = target ? Object.getOwnPropertyDescriptors(target) : null;
  assert.equal(vm.runInContext(installer, context, { filename: installerPath }), 'unavailable', name);
  if (target) assert.deepEqual(Object.getOwnPropertyDescriptors(target), before, name + ' must stay unchanged');
  guardResults.push({ name, passed: true });
}
{
  const target = host(), context = vm.createContext({ IDBFS: target });
  assert.equal(vm.runInContext(installer, context, { filename: installerPath }), 'installed');
  const replacement = () => {};
  target.syncfs = replacement;
  assert.equal(vm.runInContext(installer, context, { filename: installerPath }), 'unavailable');
  assert.equal(target.syncfs, replacement, 'An intervening installer must not be overwritten');
  guardResults.push({ name: 'foreign-replacement-preserved', passed: true });
}

const output = {
  source: {
    runtime_path: runtimePath, runtime_sha256: sha(runtime),
    extracted_idbfs_start: start, extracted_idbfs_end: end + 1,
    extracted_idbfs_sha256: sha(source), extracted_idbfs_bytes: Buffer.byteLength(source),
    actual_installer_path: installerPath, actual_installer_sha256: sha(installer),
    test_sha256: sha(readFileSync(fileURLToPath(import.meta.url))),
  },
  passed: true, scenario_count: 13, executions: cases.length, installation_guard_count: guardResults.length, cases, installation_guards: guardResults,
  scope: [
    'The unchanged IDBFS object is extracted from the explicitly supplied built native runtime and bound to the pinned helper hash. Only syncfs is wrapped for the proposed flush experiment; all original helper functions are retained.',
    'The controlled filesystem and asynchronous IndexedDB doubles demonstrate the local-snapshot-to-read race. This is not a browser run and does not identify the cause of the observed anonymous errno 44 in CI.',
    'The remote-first flush takes its local snapshot and invokes reconcile in the same JavaScript turn. Every loadLocalEntry and put argument capture for that flush is synchronous in that turn.',
    'No retry, scheduling delay, fallback or error exemption is added. Errors retain object identity. After all queued events drain, each tested invocation has exactly one callback; error callbacks may precede transaction completion.',
    'Request-error, transaction-error and abort notifications in the write-failure scenario are controlled fault injection. They test the native once-only error guard, not real IndexedDB request-error cancellation or abort behavior.',
    'The deliberate enumeration-order change can alter which error is reported first if remote and local enumeration independently fail; this test preserves each individual operation error without suppressing it.',
    'populate=true delegates to the original syncfs and preserves initial hydration data, timestamps, modes, error behavior and event order.',
    'Mutations after put argument capture are outside that flush snapshot and still require a later synchronization. The test double verifies structured-clone byte isolation but does not qualify a production IndexedDB implementation.',
  ],
};
assert.equal(cases.length, 26);
if (args.report) {
  const reportPath = path.resolve(args.report);
  assert(![runtimePath, installerPath, fileURLToPath(import.meta.url)].includes(reportPath),
    'The report must not overwrite runtime, installer or test code');
  mkdirSync(path.dirname(reportPath), { recursive: true });
  writeFileSync(reportPath, JSON.stringify(output, null, 2) + '\n');
}
console.log(JSON.stringify({ passed: output.passed, scenarios: output.scenario_count, executions: output.executions,
  installation_guards: output.installation_guard_count, source: output.source,
  original_race_failures: cases.filter(row => row.mode === 'original' && row.error?.errno === 44).map(row => row.name),
  corrected_race_results: cases.filter(row => row.mode !== 'original' && /rename|delete/.test(row.name))
    .map(row => ({ name: row.name, callback_count: row.callback_count, error: row.error })),
}, null, 2));
