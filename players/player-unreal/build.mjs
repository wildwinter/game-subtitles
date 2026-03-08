import { readFileSync, cpSync, rmSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const { version } = JSON.parse(readFileSync(resolve(__dirname, '../../package.json'), 'utf8'));

const src  = resolve(__dirname, 'GameSubtitles');
const dest = resolve(__dirname, 'dist/GameSubtitles');

rmSync(dest, { recursive: true, force: true });
mkdirSync(dest, { recursive: true });

// Copy plugin source, excluding generated build artefacts
cpSync(src, dest, {
  recursive: true,
  filter: src => {
    const rel = src.replaceAll('\\', '/');
    return !rel.includes('/Binaries/') && !rel.includes('/Intermediate/');
  },
});

console.log(`Built game-subtitles-player-unreal v${version}`);
