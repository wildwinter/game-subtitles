#!/usr/bin/env node
// Builds the Unreal plugin and runs its automation tests, once for each Unreal Engine 5
// installed (the plugin supports more than one, so each is worth checking).
//
//   npm run check:unreal
//   UE_ROOT=/path/to/UE_5.8 npm run check:unreal     only this engine
//
// Skips (exit 0) when no engine is found, so it can run anywhere; the release script runs
// it. Engines are found from the Epic Games Launcher's list of installs.
//
// Each engine builds a copy of the demo project and plugin under build/check-unreal/,
// laid out as in players/player-unreal so the demo finds the plugin the same way. That
// keeps engine binaries out of the working tree and clear of an editor you may have open.
// The copies' Binaries and Intermediate are kept between runs, so rebuilds are quick.
import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, rmSync, cpSync, mkdirSync } from 'node:fs';
import { resolve, dirname, join, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir   = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const pluginDir = resolve(rootDir, 'players/player-unreal/GameSubtitles');
const demoDir   = resolve(rootDir, 'players/player-unreal/GameSubtitlesDemo');

// Engine output, kept in the copies between runs and never copied from the source.
const ENGINE_STATE = ['Binaries', 'Intermediate', 'Saved', 'DerivedDataCache', 'Plugins'];

const platform = {
  darwin: { name: 'Mac',   build: 'Engine/Build/BatchFiles/Mac/Build.sh',   editor: 'Engine/Binaries/Mac/UnrealEditor.app/Contents/MacOS/UnrealEditor' },
  win32:  { name: 'Win64', build: 'Engine/Build/BatchFiles/Build.bat',      editor: 'Engine/Binaries/Win64/UnrealEditor-Cmd.exe' },
  linux:  { name: 'Linux', build: 'Engine/Build/BatchFiles/Linux/Build.sh', editor: 'Engine/Binaries/Linux/UnrealEditor' },
}[process.platform];

function findEngines() {
  if (process.env.UE_ROOT) return [process.env.UE_ROOT];
  const launcherList = {
    darwin: join(process.env.HOME ?? '', 'Library/Application Support/Epic/UnrealEngineLauncher/LauncherInstalled.dat'),
    win32:  'C:/ProgramData/Epic/UnrealEngineLauncher/LauncherInstalled.dat',
  }[process.platform];
  if (!launcherList || !existsSync(launcherList)) return [];
  const installs = JSON.parse(readFileSync(launcherList, 'utf8')).InstallationList ?? [];
  return [...new Set(installs.filter(i => /^UE_5\.\d+$/.test(i.AppName)).map(i => i.InstallLocation))]
    .filter(root => existsSync(join(root, platform.editor)))
    .sort((a, b) => basename(a).localeCompare(basename(b), undefined, { numeric: true }));
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

// Builds and tests with one engine; returns an error message, or null when it passes.
function check(engineRoot) {
  const label   = basename(engineRoot);
  const workDir = resolve(rootDir, 'build/check-unreal', label);
  const project = join(workDir, 'GameSubtitlesDemo/GameSubtitlesDemo.uproject');
  syncDir(pluginDir, join(workDir, 'GameSubtitles'), ENGINE_STATE);
  syncDir(demoDir, join(workDir, 'GameSubtitlesDemo'), ENGINE_STATE);

  console.log(`\ncheck-unreal: ${label}: building`);
  const build = spawnSync(join(engineRoot, platform.build),
    ['GameSubtitlesDemoEditor', platform.name, 'Development', `-Project=${project}`, '-WaitMutex'],
    { stdio: 'inherit', shell: process.platform === 'win32' });
  if (build.status !== 0) return `${label}: the build failed`;

  console.log(`check-unreal: ${label}: running the GameSubtitles automation tests`);
  const reportDir = join(workDir, 'TestReport');
  rmSync(reportDir, { recursive: true, force: true });
  const run = spawnSync(join(engineRoot, platform.editor), [
    project,
    '-ExecCmds=Automation RunTests GameSubtitles',
    '-TestExit=Automation Test Queue Empty',
    `-ReportExportPath=${reportDir}`,
    '-unattended', '-nopause', '-nullrhi', '-nosplash', '-nosound', '-NoP4', '-stdout',
  ], { encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });

  const reportPath = join(reportDir, 'index.json');
  if (!existsSync(reportPath)) {
    console.error((run.stdout ?? '').split('\n').slice(-40).join('\n'));
    return `${label}: the editor exited with ${run.status ?? run.signal} and wrote no test report`;
  }
  // The report is UTF-8 with a byte order mark.
  const report = JSON.parse(readFileSync(reportPath, 'utf8').replace(/^\uFEFF/, ''));
  const tests = report.tests ?? [];
  for (const test of tests.filter(t => t.state !== 'Success')) {
    console.error(`  ${test.state.toUpperCase()} ${test.fullTestPath}`);
    for (const entry of test.entries ?? []) {
      if (entry.event?.type === 'Error') console.error(`    ${entry.event.message}`);
    }
  }
  const passed = tests.filter(t => t.state === 'Success').length;
  if (tests.length === 0) return `${label}: no tests ran`;
  if (passed !== tests.length) return `${label}: ${passed} of ${tests.length} tests passed`;
  console.log(`check-unreal: ${label}: PASS ${passed} of ${tests.length} tests`);
  return null;
}

const engines = platform ? findEngines() : [];
if (engines.length === 0) {
  console.log('check-unreal: SKIP no Unreal Engine 5 found (set UE_ROOT to the engine folder)');
  process.exit(0);
}

const failures = engines.map(check).filter(Boolean);
if (failures.length > 0) {
  for (const f of failures) console.error(`check-unreal: FAIL ${f}`);
  process.exit(1);
}
console.log(`\ncheck-unreal: PASS with ${engines.map(e => basename(e)).join(', ')}`);
