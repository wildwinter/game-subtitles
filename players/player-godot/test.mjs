#!/usr/bin/env node
// Runs the addon's headless tests in Godot. Set GODOT to the Godot binary if it is not on
// the PATH or in /Applications.
import { execFileSync, spawnSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import { dirname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectDir = dirname(fileURLToPath(import.meta.url));

function findGodot() {
  if (process.env.GODOT) return process.env.GODOT;
  for (const name of ['godot', 'godot4']) {
    try {
      execFileSync(name, ['--version'], { stdio: 'ignore' });
      return name;
    } catch { /* not on the PATH */ }
  }
  const mac = '/Applications/Godot.app/Contents/MacOS/Godot';
  if (existsSync(mac)) return mac;
  console.error('player-godot: Godot not found; set GODOT to the path of the Godot 4 binary');
  process.exit(2);
}

const godot = findGodot();

// Import first so the addon's class_name scripts are registered.
const imported = spawnSync(godot, ['--headless', '--path', projectDir, '--import'], { encoding: 'utf8' });
if (imported.status !== 0) {
  process.stdout.write(imported.stdout);
  process.stderr.write(imported.stderr);
  console.error('player-godot: the Godot import failed');
  process.exit(1);
}

// Every script must parse. Godot parses them all when a project opens, so one bad file in the
// addon stops a user's project loading. The test run below only parses what it reaches, and
// --import reports nothing about broken scripts, so check each file on its own. Godot still
// exits 0 on a parse error, so the output is the verdict.
const scripts = readdirSync(projectDir, { recursive: true })
  .filter(f => f.endsWith('.gd') && !f.startsWith('.godot'))
  .map(f => f.split(sep).join('/'))
  .sort();
let broken = 0;
for (const script of scripts) {
  const check = spawnSync(godot, ['--headless', '--path', projectDir, '--check-only', '--script', `res://${script}`],
    { encoding: 'utf8' });
  const errors = (check.stdout + check.stderr).split('\n')
    .filter(line => /Parse Error|SCRIPT ERROR|Failed to load script/.test(line));
  if (errors.length > 0) {
    broken++;
    console.error(`PARSE CHECK: FAIL ${script}\n${errors.slice(0, 5).map(l => `  ${l}`).join('\n')}`);
  }
}
if (broken > 0) {
  console.error(`player-godot: ${broken} of ${scripts.length} scripts do not parse`);
  process.exit(1);
}
console.log(`Parse check: all ${scripts.length} scripts parse`);

const run = spawnSync(godot, ['--headless', '--path', projectDir, '-s', 'res://tests/run_tests.gd'],
  { encoding: 'utf8' });
process.stdout.write(run.stdout);
process.stderr.write(run.stderr);

// A GDScript error does not always set the exit code, so also require the success line.
const output = run.stdout + run.stderr;
if (run.status !== 0 || !run.stdout.includes('ALL TESTS PASSED') || /SCRIPT ERROR|Parse Error/.test(output)) {
  console.error('player-godot: tests failed');
  process.exit(1);
}
