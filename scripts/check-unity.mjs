#!/usr/bin/env node
// Runs the Unity package's EditMode tests in a real Unity Editor, in batch mode.
//
//   npm run check:unity
//   UNITY_EDITOR=/path/to/Unity npm run check:unity
//
// Skips (exit 0) when no Unity Editor is installed, so it can run anywhere; the release
// script runs it. Unity must be installed and licensed, and needs Unity 6 or later.
//
// The tests run in a copy of the demo project under build/check-unity/, which loads the
// package straight from players/player-unity/GameSubtitles. That keeps the run clear of
// the demo you may have open in the Editor, and keeps Unity's project upgrades and
// Library out of the working tree. The copy's Library is kept between runs, so only the
// first run imports everything.
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, writeFileSync, rmSync, cpSync, mkdirSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir    = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const packageDir = resolve(rootDir, 'players/player-unity/GameSubtitles');
const demoDir    = resolve(rootDir, 'players/player-unity/GameSubtitlesDemo');
const workDir    = resolve(rootDir, 'build/check-unity/GameSubtitlesDemo');

// Unity's own folders, kept in the copy between runs and never copied from the source.
const UNITY_STATE = ['Library', 'Logs', 'Temp', 'UserSettings', 'obj'];

const fail = msg => {
  console.error(`check-unity: FAIL ${msg}`);
  process.exit(1);
};

function editorBinary(editorDir) {
  if (process.platform === 'darwin') return join(editorDir, 'Unity.app/Contents/MacOS/Unity');
  if (process.platform === 'win32')  return join(editorDir, 'Editor/Unity.exe');
  return join(editorDir, 'Editor/Unity');
}

// The editor matching the demo's ProjectVersion.txt when it is installed, otherwise the
// newest Unity 6 the Hub has installed.
function findUnity() {
  if (process.env.UNITY_EDITOR) return process.env.UNITY_EDITOR;
  const hubDirs = {
    darwin: ['/Applications/Unity/Hub/Editor'],
    win32:  ['C:/Program Files/Unity/Hub/Editor'],
    linux:  [join(process.env.HOME ?? '', 'Unity/Hub/Editor')],
  }[process.platform] ?? [];
  const wanted = readFileSync(join(demoDir, 'ProjectSettings/ProjectVersion.txt'), 'utf8')
    .match(/m_EditorVersion:\s*(\S+)/)?.[1];
  const installed = hubDirs.filter(existsSync)
    .flatMap(dir => readdirSync(dir).map(version => ({ version, binary: editorBinary(join(dir, version)) })))
    .filter(e => /^\d{4}\./.test(e.version) && Number(e.version.split('.')[0]) >= 6000 && existsSync(e.binary))
    .sort((a, b) => a.version.localeCompare(b.version, undefined, { numeric: true }));
  return (installed.find(e => e.version === wanted) ?? installed.at(-1))?.binary;
}

// Makes dest a copy of src, leaving the named entries in dest alone and not copying them.
function syncDir(src, dest, keep) {
  mkdirSync(dest, { recursive: true });
  for (const entry of readdirSync(dest)) {
    if (!keep.includes(entry)) rmSync(join(dest, entry), { recursive: true, force: true });
  }
  for (const entry of readdirSync(src)) {
    if (!keep.includes(entry)) cpSync(join(src, entry), join(dest, entry), { recursive: true });
  }
}

const unity = findUnity();
if (!unity) {
  console.log('check-unity: SKIP no Unity 6 Editor found (set UNITY_EDITOR to the Unity binary)');
  process.exit(0);
}

syncDir(demoDir, workDir, UNITY_STATE);

// Point the copy's manifest at the package in the repo.
const manifestPath = join(workDir, 'Packages/manifest.json');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
manifest.dependencies['net.wildwinter.game-subtitles'] = `file:${packageDir.replaceAll('\\', '/')}`;
writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');

const resultsPath = join(workDir, 'test-results.xml');
const logPath     = join(workDir, 'unity.log');
rmSync(resultsPath, { force: true });

console.log(`check-unity: running EditMode tests with ${unity}`);
console.log('check-unity: the first run imports the project, which takes a few minutes');
const run = spawnSync(unity, [
  '-batchmode', '-nographics',
  '-projectPath', workDir,
  '-runTests', '-testPlatform', 'EditMode',
  '-testResults', resultsPath,
  '-logFile', logPath,
], { stdio: 'inherit' });

if (!existsSync(resultsPath)) {
  const log = existsSync(logPath) ? readFileSync(logPath, 'utf8') : '';
  console.error(log.split('\n').slice(-40).join('\n'));
  fail(`Unity exited with ${run.status ?? run.signal} and wrote no test results; see ${logPath}`);
}

// NUnit 3 results: the <test-run> element carries the totals, and each failed
// <test-case> its name and message.
const xml = readFileSync(resultsPath, 'utf8');
const attr = name => xml.match(new RegExp(`<test-run[^>]*\\s${name}="([^"]*)"`))?.[1];
const total = Number(attr('total') ?? 0);
const passed = Number(attr('passed') ?? 0);
const failed = Number(attr('failed') ?? 0);

for (const [, name, body] of xml.matchAll(/<test-case[^>]*fullname="([^"]*)"[^>]*result="Failed"[^>]*>([\s\S]*?)<\/test-case>/g)) {
  const message = body.match(/<message><!\[CDATA\[([\s\S]*?)\]\]><\/message>/)?.[1].trim() ?? '';
  console.error(`  FAIL ${name}\n    ${message.split('\n').join('\n    ')}`);
}

if (total === 0) fail('no tests ran');
if (failed > 0 || passed !== total || run.status !== 0) fail(`${passed} of ${total} passed, ${failed} failed`);
console.log(`check-unity: PASS ${passed} of ${total} tests`);
