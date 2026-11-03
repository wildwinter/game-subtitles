// Where each player's source says its own version: the constant game code reads at runtime, so a
// library that ships its own copy of Game Subtitles (Patterstage, for one) can tell when a game is
// running a different copy. Nothing in the manifests is readable from a shipped build, so the
// version lives in code too.
//
// Shared by the player builds, which write the root package.json's version into each one beside
// the manifests, and the lockstep check, which fails when one differs from them. Paths are from
// the repository root, and each pattern's one capture group is the version.

import { readFileSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

export const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

export const VERSION_CONSTANTS = {
  js:     { rel: 'players/player-js/src/version.js', pattern: /^export const GAME_SUBTITLES_VERSION = '([^']*)';$/m },
  unity:  { rel: 'players/player-unity/GameSubtitles/Runtime/GameSubtitlesVersion.cs', pattern: /^\s*public const string Current = "([^"]*)";$/m },
  unreal: { rel: 'players/player-unreal/GameSubtitles/Source/GameSubtitles/Public/GameSubtitlesVersion.h', pattern: /^inline constexpr const char\* GameSubtitlesVersion = "([^"]*)";$/m },
  godot:  { rel: 'players/player-godot/addons/game_subtitles/game_subtitles_version.gd', pattern: /^const CURRENT := "([^"]*)"$/m },
};

/** The version a constant's source says, or undefined when the pattern isn't found. */
export function readVersionConstant(source, { pattern }) {
  return pattern.exec(source)?.[1];
}

/** The source with the constant set to `version`. Throws when the pattern isn't found. */
export function writeVersionConstant(source, { rel, pattern }, version) {
  const m = pattern.exec(source);
  if (!m) throw new Error(`${rel}: no Game Subtitles version constant found`);
  // Each constant's line holds one quoted string, the version.
  const line = m[0].replace(/(["'])[^"']*\1/, `$1${version}$1`);
  return source.slice(0, m.index) + line + source.slice(m.index + m[0].length);
}

/** Sets one player's constant in its source file to `version`, writing only when it changes. */
export function setVersionConstant(player, version) {
  const constant = VERSION_CONSTANTS[player];
  const path = resolve(rootDir, constant.rel);
  const source = readFileSync(path, 'utf8');
  const updated = writeVersionConstant(source, constant, version);
  if (updated !== source) writeFileSync(path, updated, 'utf8');
}
