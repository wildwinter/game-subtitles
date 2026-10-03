#!/usr/bin/env node
// Reads version from master package.json and runs dotnet publish for all three targets.
import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { resolveSignIdentity, signBinary } from '../../scripts/macos.mjs';

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
  ` -o ../build/preprocessor/osx-arm64`
);

// Apple codesign: force-replace dotnet's ad-hoc signature with Developer ID
const codesignId = resolveSignIdentity();
if (!codesignId) {
  console.warn('\nWARN: no Developer ID Application identity (set APPLE_CODESIGN_ID or add one to the keychain); skipping macOS code signing.');
} else {
  console.log(`\nSigning macOS binary with "${codesignId}"`);
  signBinary(
    resolve(preprocessorDir, '../build/preprocessor/osx-arm64/game-subtitles-preprocess'),
    codesignId,
    resolve(preprocessorDir, 'entitlements.plist'),
  );
}

// Windows x64
run(
  `dotnet publish PreprocessorTool -c Release -r win-x64` +
  ` --self-contained true -p:PublishSingleFile=true` +
  ` -p:Version=${version} -p:AssemblyVersion=${version}.0` +
  ` -o ../build/preprocessor/win-x64`
);

// Library DLL (framework-dependent, agnostic)
run(
  `dotnet build PreprocessorLib -c Release` +
  ` -p:Version=${version} -p:AssemblyVersion=${version}.0` +
  ` -o ../build/preprocessor/lib`
);

console.log(`\nPreprocessor dist complete.`);
