import { build } from 'esbuild';
import { readFileSync, writeFileSync, copyFileSync, mkdirSync, rmSync } from 'node:fs';

const { version } = JSON.parse(readFileSync('../../package.json', 'utf8'));
const banner = { js: `/* @wildwinter/game-subtitles v${version} | MIT */` };

// Keep the npm package's version, and the lockfile's copy of it, in step with the repository's.
const pkg = JSON.parse(readFileSync('package.json', 'utf8'));
if (pkg.version !== version) {
  const text = readFileSync('package.json', 'utf8');
  writeFileSync('package.json', text.replace(/("version":\s*")[^"]+(")/, `$1${version}$2`));
}
const lock = JSON.parse(readFileSync('package-lock.json', 'utf8'));
if (lock.version !== version || lock.packages[''].version !== version) {
  lock.version = version;
  lock.packages[''].version = version;
  writeFileSync('package-lock.json', JSON.stringify(lock, null, 2) + '\n');
}

// dist/ is what the npm package ships. The release zips and the demo take the same two
// bundles from build/player-js/.
rmSync('dist', { recursive: true, force: true });

await build({
  entryPoints: ['src/index.js'],
  bundle: true,
  format: 'esm',
  outfile: 'dist/game-subtitles-player.esm.js',
  banner,
});

await build({
  entryPoints: ['src/index.js'],
  bundle: true,
  format: 'iife',
  globalName: 'GameSubtitles',
  outfile: 'dist/game-subtitles-player.js',
  banner,
});

copyFileSync('src/index.d.ts', 'dist/index.d.ts');
// npm adds a LICENSE file in the package folder to the package; this one is the repository's.
copyFileSync('../../LICENSE', 'LICENSE');

mkdirSync('../../build/player-js', { recursive: true });
for (const file of ['game-subtitles-player.esm.js', 'game-subtitles-player.js']) {
  copyFileSync(`dist/${file}`, `../../build/player-js/${file}`);
}

console.log(`Built @wildwinter/game-subtitles v${version}`);
