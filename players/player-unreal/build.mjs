import { readFileSync, writeFileSync, cpSync, rmSync, mkdirSync } from 'node:fs';
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

// Patch version fields in the .uplugin to match the root package.json version
const [major, minor, patch] = version.split('.').map(Number);
const versionInt = major * 10000 + minor * 100 + patch;

function patchUplugin(upluginPath) {
  const uplugin = JSON.parse(readFileSync(upluginPath, 'utf8'));
  uplugin.Version = versionInt;
  uplugin.VersionName = version;
  writeFileSync(upluginPath, JSON.stringify(uplugin, null, 4) + '\n', 'utf8');
}

patchUplugin(resolve(src,  'GameSubtitles.uplugin'));
patchUplugin(resolve(dest, 'GameSubtitles.uplugin'));

console.log(`Built game-subtitles-player-unreal v${version}`);
