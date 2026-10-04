import { readFileSync, writeFileSync, cpSync, rmSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const { version } = JSON.parse(readFileSync(resolve(__dirname, '../../package.json'), 'utf8'));

const src  = resolve(__dirname, 'addons/game_subtitles');
const dest = resolve(__dirname, '../../build/player-godot/addons/game_subtitles');

// Patch the version in plugin.cfg to match the root package.json version
const cfgPath = resolve(src, 'plugin.cfg');
const cfg = readFileSync(cfgPath, 'utf8');
const patched = cfg.replace(/^version=".*"$/m, `version="${version}"`);
if (patched !== cfg) writeFileSync(cfgPath, patched, 'utf8');

// Copy the addon only; the demo and tests in this folder are not shipped
rmSync(resolve(dest, '../..'), { recursive: true, force: true });
mkdirSync(dest, { recursive: true });
cpSync(src, dest, { recursive: true });

console.log(`Built game-subtitles-player-godot v${version}`);
