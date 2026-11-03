#!/usr/bin/env node
// Checks that every player says the root package.json's version: each manifest, and each version
// constant game code reads at runtime. The builds write them all, so a difference means one was
// edited by hand or a build was skipped. A library that ships Game Subtitles reads the constant to
// tell which copy it is running with, so it must never say another version.
//
// Runs first in `npm test`, and in CI before the build, so a drifted file that has been committed
// fails there too.

import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { VERSION_CONSTANTS, readVersionConstant, rootDir } from './lib/version-constants.mjs';

const read = rel => readFileSync(resolve(rootDir, rel), 'utf8');
const json = rel => JSON.parse(read(rel));

const version = json('package.json').version;
const [major, minor, patch] = version.split('.').map(Number);

const uplugin = json('players/player-unreal/GameSubtitles/GameSubtitles.uplugin');
const lock = json('players/player-js/package-lock.json');
const found = {
  'players/player-js/package.json': json('players/player-js/package.json').version,
  'players/player-js/package-lock.json': lock.version,
  'players/player-js/package-lock.json (root package)': lock.packages[''].version,
  'players/player-unity/GameSubtitles/package.json': json('players/player-unity/GameSubtitles/package.json').version,
  'players/player-unreal/GameSubtitles/GameSubtitles.uplugin (VersionName)': uplugin.VersionName,
  'players/player-godot/addons/game_subtitles/plugin.cfg': read('players/player-godot/addons/game_subtitles/plugin.cfg').match(/^version="([^"]*)"$/m)?.[1],
};
for (const constant of Object.values(VERSION_CONSTANTS)) {
  found[`${constant.rel} (constant)`] = readVersionConstant(read(constant.rel), constant);
}

const wrong = Object.entries(found).filter(([, v]) => v !== version);
// The .uplugin's integer Version is the same version as one number, which Unreal compares.
const versionInt = major * 10000 + minor * 100 + patch;
if (uplugin.Version !== versionInt) {
  wrong.push(['players/player-unreal/GameSubtitles/GameSubtitles.uplugin (Version)', `${uplugin.Version}, not ${versionInt}`]);
}

if (wrong.length) {
  console.error(`check-versions: these do not say ${version}, the version in package.json:\n`);
  for (const [file, v] of wrong) console.error(`  ${v ?? '(not found)'}\t${file}`);
  console.error('\nRun `npm run build` to write the version into each of them.');
  process.exit(1);
}
console.log(`check-versions: every player manifest and version constant says ${version}`);
