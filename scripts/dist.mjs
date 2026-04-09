#!/usr/bin/env node
// Master dist script: builds all components and assembles distribution zips.
import { execSync } from 'node:child_process';
import { createReadStream, mkdirSync, readFileSync } from 'node:fs';
import { resolve, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const __dirname = dirname(fileURLToPath(import.meta.url));
const rootDir = resolve(__dirname, '..');
const require = createRequire(import.meta.url);

const rootPkg = JSON.parse(readFileSync(resolve(rootDir, 'package.json'), 'utf8'));
const version = rootPkg.version;
const distDir = resolve(rootDir, 'dist');

mkdirSync(distDir, { recursive: true });

function run(cmd, cwd = rootDir) {
  console.log(`\n> ${cmd}`);
  execSync(cmd, { cwd, stdio: 'inherit' });
}

// 1. Build preprocessor (win, osx, lib)
console.log('\n=== Building preprocessor ===');
run('npm run dist --prefix preprocessor');

// 2. Build JS player
console.log('\n=== Building player-js ===');
run('npm run build --prefix players/player-js');

// 3. Build Unreal plugin
console.log('\n=== Building player-unreal ===');
run('npm run build --prefix players/player-unreal');

// 4. Build Unity package
console.log('\n=== Building player-unity ===');
run('npm run build --prefix players/player-unity');

// 5. Assemble zips
console.log('\n=== Assembling distribution zips ===');

// Dynamic import of archiver (CommonJS module)
const archiver = require('archiver');
const { createWriteStream } = await import('node:fs');

const jsFile        = resolve(rootDir, 'players/player-js/dist/game-subtitles-player.js');
const unrealPlugin  = resolve(rootDir, 'players/player-unreal/dist/GameSubtitles');
const unityPackage  = resolve(rootDir, 'players/player-unity/dist/GameSubtitles');
const readme        = resolve(rootDir, 'README.md');

async function makeZip(zipName, addFn) {
  const output = resolve(distDir, zipName);
  const stream = createWriteStream(output);
  const archive = archiver('zip', { zlib: { level: 9 } });

  await new Promise((resolve, reject) => {
    stream.on('close', resolve);
    archive.on('error', reject);
    archive.pipe(stream);
    addFn(archive);
    archive.finalize();
  });

  console.log(`  Created: ${zipName} (${archive.pointer()} bytes)`);
}

await makeZip(`game-subtitles-js-v${version}.zip`, archive => {
  archive.file(jsFile,            { name: 'game-subtitles-player.js' });
  archive.file(resolve(rootDir, 'preprocessor/dist/win-x64/game-subtitles-preprocess.exe'),
    { name: 'game-subtitles-preprocess.exe' });
  archive.file(resolve(rootDir, 'preprocessor/dist/osx-arm64/game-subtitles-preprocess'),
    { name: 'game-subtitles-preprocess' });
  archive.file(readme,            { name: 'README.md' });
});

await makeZip(`game-subtitles-lib-v${version}.zip`, archive => {
  archive.file(resolve(rootDir, 'preprocessor/dist/lib/PreprocessorLib.dll'),
    { name: 'PreprocessorLib.dll' });
  archive.file(jsFile,            { name: 'player-js/game-subtitles-player.js' });
  archive.directory(unrealPlugin, 'player-unreal/GameSubtitles');
  archive.directory(unityPackage, 'player-unity/GameSubtitles');
  archive.file(readme,            { name: 'README.md' });
});

await makeZip(`game-subtitles-unreal-v${version}.zip`, archive => {
  archive.directory(unrealPlugin, 'GameSubtitles');
  archive.file(resolve(rootDir, 'preprocessor/dist/win-x64/game-subtitles-preprocess.exe'),
    { name: 'GameSubtitles/ThirdParty/game-subtitles-preprocess.exe' });
  archive.file(resolve(rootDir, 'preprocessor/dist/osx-arm64/game-subtitles-preprocess'),
    { name: 'GameSubtitles/ThirdParty/game-subtitles-preprocess' });
  archive.file(readme,            { name: 'README.md' });
});

await makeZip(`game-subtitles-unity-v${version}.zip`, archive => {
  archive.directory(unityPackage, 'GameSubtitles');
  archive.file(resolve(rootDir, 'preprocessor/dist/win-x64/game-subtitles-preprocess.exe'),
    { name: 'game-subtitles-preprocess.exe' });
  archive.file(resolve(rootDir, 'preprocessor/dist/osx-arm64/game-subtitles-preprocess'),
    { name: 'game-subtitles-preprocess' });
  archive.file(readme,            { name: 'README.md' });
});

console.log('\nDist complete.');
