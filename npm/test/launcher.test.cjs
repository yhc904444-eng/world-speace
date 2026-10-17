'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { parseArgs, dataDirectory, verifyBundle, launchPlan } = require('../bin/world-speace.cjs');

test('explicit data path survives spaces, Chinese and punctuation without a shell', () => {
  const folder = path.join(os.tmpdir(), "测试 词书 & don't-expand");
  const parsed = parseArgs(['--data-dir', folder, '--check']);
  assert.equal(parsed.dataDir, folder);
  assert.equal(parsed.check, true);
  const plan = launchPlan(parsed);
  assert.deepEqual(plan.args, ['--data-dir', path.resolve(folder)]);
  assert.equal(path.basename(plan.executable), 'world speace.exe');
});
test('default records live outside npm and npx package directories', () => {
  const root = path.resolve(os.tmpdir(), 'local-app-data');
  assert.equal(dataDirectory(undefined, { LOCALAPPDATA: root }), path.join(root, 'world-speace', 'UserData'));
});
test('missing LOCALAPPDATA falls back to the current user profile', () => {
  const home = path.resolve(os.tmpdir(), 'test-home');
  assert.equal(dataDirectory(undefined, {}, home), path.join(home, 'AppData', 'Local', 'world-speace', 'UserData'));
});
test('malformed or unknown arguments fail before starting the application', () => {
  assert.throws(() => parseArgs(['--data-dir']), /需要/);
  assert.throws(() => parseArgs(['--data-dir', '--check']), /需要/);
  assert.throws(() => parseArgs(['--unexpected']), /未知参数/);
});
test('the bundled public executable has its pinned release hash', () => {
  assert.ok(fs.statSync(verifyBundle()).size > 4000000);
});
test('missing and corrupt bundles are rejected', t => {
  const fixture = fs.mkdtempSync(path.join(os.tmpdir(), 'world-speace-integrity-'));
  t.after(() => fs.rmSync(fixture, { recursive: true, force: true }));
  assert.throws(() => verifyBundle(fixture), /不完整/);
  fs.writeFileSync(path.join(fixture, 'world speace.exe'), 'corrupt-fixture');
  fs.writeFileSync(path.join(fixture, 'world speace.exe.config'), 'fixture');
  assert.throws(() => verifyBundle(fixture), /SHA256/);
});
test('flags for help and version do not require a GUI launch', () => {
  assert.equal(parseArgs(['--help']).help, true);
  assert.equal(parseArgs(['-v']).version, true);
});
