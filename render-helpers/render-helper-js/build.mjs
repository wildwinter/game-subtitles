import { build } from 'esbuild';
import { readFileSync } from 'node:fs';

const { version } = JSON.parse(readFileSync('../../package.json', 'utf8'));
const banner = { js: `/* game-subtitles-render v${version} | MIT */` };

await build({
  entryPoints: ['src/index.js'],
  bundle: true,
  format: 'esm',
  outfile: 'dist/game-subtitles-render.esm.js',
  banner,
});

await build({
  entryPoints: ['src/index.js'],
  bundle: true,
  format: 'iife',
  globalName: 'GameSubtitles',
  outfile: 'dist/game-subtitles-render.js',
  banner,
});

console.log(`Built game-subtitles-render v${version}`);
