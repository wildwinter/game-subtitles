#!/usr/bin/env node
// Reads version from master package.json and runs dotnet publish for all three targets.
import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const rootPkg = JSON.parse(readFileSync(resolve(__dirname, '../../package.json'), 'utf8'));
const version = rootPkg.version;
const preprocessorDir = resolve(__dirname, '..');

console.log(`Building preprocessor v${version}...`);

function run(cmd) {
  console.log(`\n> ${cmd}`);
  execSync(cmd, { cwd: preprocessorDir, stdio: 'inherit' });
}

// macOS Apple Silicon
run(
  `dotnet publish PreprocessorTool -c Release -r osx-arm64` +
  ` --self-contained true -p:PublishSingleFile=true` +
  ` -p:Version=${version} -p:AssemblyVersion=${version}.0` +
  ` -o dist/osx-arm64`
);

// Apple codesign — force-replace dotnet's ad-hoc signature with Developer ID
const codesignId = process.env.APPLE_CODESIGN_ID;
if (!codesignId) {
  console.warn('\nWARN: APPLE_CODESIGN_ID is not set — skipping macOS code signing.');
} else {
  const binary = 'dist/osx-arm64/game-subtitles-preprocess';
  const entitlements = 'entitlements.plist';
  run(
    `codesign --force --sign ${JSON.stringify(codesignId)}` +
    ` --entitlements ${entitlements}` +
    ` --options runtime --timestamp` +
    ` ${binary}`
  );
  run(`codesign --verify --strict ${binary}`);
}

// Windows x64
run(
  `dotnet publish PreprocessorTool -c Release -r win-x64` +
  ` --self-contained true -p:PublishSingleFile=true` +
  ` -p:Version=${version} -p:AssemblyVersion=${version}.0` +
  ` -o dist/win-x64`
);

// Library DLL (framework-dependent, agnostic)
run(
  `dotnet build PreprocessorLib -c Release` +
  ` -p:Version=${version} -p:AssemblyVersion=${version}.0` +
  ` -o dist/lib`
);

console.log(`\nPreprocessor dist complete.`);
