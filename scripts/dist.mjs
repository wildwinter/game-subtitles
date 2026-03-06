#!/usr/bin/env node
// Master dist script: builds all components and assembles three distribution zips.
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

// 3. Assemble zips
console.log('\n=== Assembling distribution zips ===');

// Dynamic import of archiver (CommonJS module)
const archiver = require('archiver');
const { createWriteStream } = await import('node:fs');

const jsFile = resolve(rootDir, 'players/player-js/dist/game-subtitles-player.js');
const readme = resolve(rootDir, 'README.md');

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

await makeZip(`game-subtitles-win-v${version}.zip`, archive => {
  archive.file(resolve(rootDir, 'preprocessor/dist/win-x64/game-subtitles-preprocess.exe'),
    { name: 'game-subtitles-preprocess.exe' });
  archive.file(jsFile,  { name: 'game-subtitles-player.js' });
  archive.file(readme,  { name: 'README.md' });
});

await makeZip(`game-subtitles-osx-v${version}.zip`, archive => {
  archive.file(resolve(rootDir, 'preprocessor/dist/osx-arm64/game-subtitles-preprocess'),
    { name: 'game-subtitles-preprocess' });
  archive.file(jsFile,  { name: 'game-subtitles-player.js' });
  archive.file(readme,  { name: 'README.md' });
});

await makeZip(`game-subtitles-lib-v${version}.zip`, archive => {
  archive.file(resolve(rootDir, 'preprocessor/dist/lib/PreprocessorLib.dll'),
    { name: 'PreprocessorLib.dll' });
  archive.file(jsFile,  { name: 'game-subtitles-player.js' });
  archive.file(readme,  { name: 'README.md' });
});

console.log('\nDist complete.');
