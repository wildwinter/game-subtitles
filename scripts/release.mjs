#!/usr/bin/env node
// Cut a release: check, test (including the Unity and Unreal tests, in each engine that is
// installed), set the version, build, sign and notarise the macOS preprocessor,
// commit, tag, push to main, and publish the GitHub Release with the five zips.
//
//   npm run release -- 0.2.0
//   npm run release -- 0.2.0 --dry-run    checks, tests, and a signed build of the current
//                                         version; skips notarising and changes nothing
//
// Write the release notes under "## [Unreleased]" in CHANGELOG.md and commit them first.
// Run it on a Mac with the signing setup described in scripts/macos.mjs. It works from main
// or from any branch that contains origin/main; HEAD is pushed to main.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { tmpdir } from 'node:os';
import { fileURLToPath } from 'node:url';
import { resolveSignIdentity, notaryCredentials, notarizeBinary } from './macos.mjs';

const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');

const git = (...args) => execFileSync('git', args, { cwd: rootDir, encoding: 'utf8' }).trim();
const succeeds = (cmd, args) => {
  try { execFileSync(cmd, args, { cwd: rootDir, stdio: 'ignore' }); return true; } catch { return false; }
};
const run = (cmd, args, env = process.env) => {
  console.log(`\n> ${cmd} ${args.join(' ')}`);
  execFileSync(cmd, args, { cwd: rootDir, stdio: 'inherit', env });
};
const fail = msg => {
  console.error(`\nrelease: ${msg}`);
  process.exit(1);
};

// ── Arguments ─────────────────────────────────────────────────────────────────

const args    = process.argv.slice(2);
const dry     = args.includes('--dry-run');
const version = args.find(a => !a.startsWith('--'));
if (!version || !/^\d+\.\d+\.\d+$/.test(version)) fail('usage: npm run release -- <major.minor.patch> [--dry-run]');
const tag = `v${version}`;

const pkgPath = resolve(rootDir, 'package.json');
const current = JSON.parse(readFileSync(pkgPath, 'utf8')).version;
const newer = (a, b) => {
  const [x, y] = [a, b].map(v => v.split('.').map(Number));
  for (let i = 0; i < 3; i++) if (x[i] !== y[i]) return x[i] > y[i];
  return false;
};
if (!newer(version, current)) fail(`${version} is not newer than the current version ${current}`);

// ── Checks ────────────────────────────────────────────────────────────────────

if (process.platform !== 'darwin') fail('releases are built on macOS so the preprocessor binary can be signed');
if (git('status', '--porcelain')) fail('the working tree is not clean; commit first');

git('fetch', '--quiet', '--tags', 'origin');
if (!succeeds('git', ['merge-base', '--is-ancestor', 'origin/main', 'HEAD'])) {
  fail('HEAD does not contain origin/main; pull or rebase first');
}
const ahead = Number(git('rev-list', '--count', 'origin/main..HEAD'));
if (git('tag', '--list', tag) || git('ls-remote', '--tags', 'origin', `refs/tags/${tag}`)) fail(`tag ${tag} already exists`);
if (!succeeds('gh', ['auth', 'status'])) fail('the GitHub CLI is not signed in; run gh auth login');

const changelogPath = resolve(rootDir, 'CHANGELOG.md');
const changelog = readFileSync(changelogPath, 'utf8');
const unreleased = changelog.match(/^## \[Unreleased\]\s*\n([\s\S]*?)(?=^## \[|(?![\s\S]))/m);
const notes = unreleased?.[1].trim();
if (!notes) fail('CHANGELOG.md has nothing under "## [Unreleased]"; write the release notes there first');

const identity = resolveSignIdentity();
if (!identity) fail('no Developer ID Application identity; set APPLE_CODESIGN_ID or add one to the keychain');
const notary = notaryCredentials();
if (!notary) {
  const msg = 'APPLE_ID, APPLE_APP_SPECIFIC_PASSWORD, and APPLE_TEAM_ID must all be set to notarise';
  if (dry) console.warn(`\nrelease: WARNING: ${msg}`);
  else fail(msg);
}

console.log(`
release: ${dry ? 'DRY RUN for' : 'releasing'} ${tag} (currently ${current})
  signing as   ${identity}
  notarising   ${notary ? `as ${notary.appleId}` : 'NO'}${dry ? ' (skipped in a dry run)' : ''}
  pushing      ${ahead} commit(s) already ahead of origin/main, plus the release commit
  notes
${notes.replace(/^/gm, '    ')}`);

// ── Test and build ────────────────────────────────────────────────────────────

run('npm', ['test']);
// The Godot addon's tests need Godot, which CI installs separately, so they are not in npm test.
run('npm', ['test', '--prefix', 'players/player-godot']);
// The Unity and Unreal tests need the engines. Each check skips, and says so, when its engine
// is not installed.
run('npm', ['run', 'check:unity']);
run('npm', ['run', 'check:unreal']);

const buildVersion = dry ? current : version;
if (!dry) {
  // Edit the version in place so the rest of package.json keeps its hand alignment.
  const pkgText = readFileSync(pkgPath, 'utf8');
  writeFileSync(pkgPath, pkgText.replace(/("version":\s*")[^"]+(")/, `$1${version}$2`));

  const date = new Date().toISOString().slice(0, 10);
  writeFileSync(changelogPath, changelog.replace(/^## \[Unreleased\]\s*\n/m, `## [Unreleased]\n\n## [${version}] - ${date}\n\n`));
}

// The build also copies the version into the Unity package.json, the Unreal .uplugin, and the
// Godot plugin.cfg.
run('npm', ['run', 'dist'], { ...process.env, APPLE_CODESIGN_ID: identity });

const binary = resolve(rootDir, 'build/preprocessor/osx-arm64/game-subtitles-preprocess');
const reported = execFileSync(binary, ['--version'], { encoding: 'utf8' }).trim();
console.log(`\nrelease: signed preprocessor launches and reports ${reported}`);

const zips = ['js', 'unreal', 'unity', 'godot', 'lib'].map(kind => resolve(rootDir, 'dist', `game-subtitles-${kind}-v${buildVersion}.zip`));
for (const zip of zips) if (!existsSync(zip)) fail(`missing ${zip}`);

if (dry) {
  const dirty = git('status', '--porcelain');
  if (dirty) console.warn(`\nrelease: WARNING: the build changed tracked files:\n${dirty}`);
  console.log(`\nrelease: dry run passed. A real run would notarise, set ${version}, commit, tag ${tag}, push to main, and publish:`);
  for (const zip of zips) console.log(`  ${zip.replace(`v${buildVersion}`, tag)}`);
  process.exit(0);
}

console.log('\nrelease: notarising the preprocessor (this can take a few minutes)');
const submission = notarizeBinary(binary, notary);
console.log(`release: notarised (submission ${submission})`);

// ── Commit, tag, push, publish ────────────────────────────────────────────────

run('git', ['add', 'package.json', 'CHANGELOG.md',
  'players/player-unity/GameSubtitles/package.json',
  'players/player-unreal/GameSubtitles/GameSubtitles.uplugin',
  'players/player-godot/addons/game_subtitles/plugin.cfg']);
run('git', ['commit', '-m', `Release ${version}`]);
run('git', ['tag', '-a', tag, '-m', `Game Subtitles ${version}`]);
run('git', ['push', 'origin', 'HEAD:main']);
run('git', ['push', 'origin', tag]);

const notesDir = mkdtempSync(join(tmpdir(), 'game-subtitles-release-'));
const notesFile = join(notesDir, 'notes.md');
writeFileSync(notesFile, `${notes}\n`);
try {
  run('gh', ['release', 'create', tag, ...zips, '--verify-tag', '--title', tag, '--notes-file', notesFile]);
} finally {
  rmSync(notesDir, { recursive: true, force: true });
}

console.log(`\nrelease: published ${tag}: https://github.com/wildwinter/game-subtitles/releases/tag/${tag}`);
