import { readFileSync, cpSync, rmSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const { version } = JSON.parse(readFileSync(resolve(__dirname, '../../package.json'), 'utf8'));

const src  = resolve(__dirname, 'GameSubtitles');
const dest = resolve(__dirname, 'dist/GameSubtitles');

rmSync(dest, { recursive: true, force: true });
mkdirSync(dest, { recursive: true });

// Copy Unity package source (no generated artefacts to exclude for a UPM package)
cpSync(src, dest, { recursive: true });

console.log(`Built game-subtitles-player-unity v${version}`);
