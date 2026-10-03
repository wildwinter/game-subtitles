// macOS code signing and notarisation for the preprocessor binary.
//
// Signing uses APPLE_CODESIGN_ID if set, otherwise the first "Developer ID Application"
// identity in the keychain. Notarising needs APPLE_ID, APPLE_APP_SPECIFIC_PASSWORD, and
// APPLE_TEAM_ID. A bare executable can't have its ticket stapled, so Gatekeeper fetches it
// online the first time the binary runs.
import { execFileSync } from 'node:child_process';
import { rmSync } from 'node:fs';

export function resolveSignIdentity() {
  if (process.env.APPLE_CODESIGN_ID) return process.env.APPLE_CODESIGN_ID;
  if (process.platform !== 'darwin') return null;
  try {
    const out = execFileSync('security', ['find-identity', '-v', '-p', 'codesigning'], { encoding: 'utf8' });
    return out.match(/"(Developer ID Application: [^"]+)"/)?.[1] ?? null;
  } catch {
    return null;
  }
}

export function notaryCredentials() {
  const { APPLE_ID, APPLE_APP_SPECIFIC_PASSWORD, APPLE_TEAM_ID } = process.env;
  if (!APPLE_ID || !APPLE_APP_SPECIFIC_PASSWORD || !APPLE_TEAM_ID) return null;
  return { appleId: APPLE_ID, password: APPLE_APP_SPECIFIC_PASSWORD, teamId: APPLE_TEAM_ID };
}

// Replace dotnet's ad-hoc signature with a Developer ID one, with the hardened runtime.
export function signBinary(binaryPath, identity, entitlementsPath) {
  execFileSync('codesign', [
    '--force', '--sign', identity,
    '--entitlements', entitlementsPath,
    '--options', 'runtime', '--timestamp',
    binaryPath,
  ], { stdio: 'inherit' });
  execFileSync('codesign', ['--verify', '--strict', '--verbose=2', binaryPath], { stdio: 'inherit' });
}

// Submit a signed binary to Apple and wait. Throws unless Apple accepts it.
export function notarizeBinary(binaryPath, creds) {
  const zipPath = `${binaryPath}.notarize.zip`;
  execFileSync('ditto', ['-c', '-k', '--keepParent', binaryPath, zipPath], { stdio: 'inherit' });
  try {
    const out = execFileSync('xcrun', [
      'notarytool', 'submit', zipPath,
      '--apple-id', creds.appleId, '--password', creds.password, '--team-id', creds.teamId,
      '--wait', '--output-format', 'json',
    ], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
    const result = JSON.parse(out);
    if (result.status !== 'Accepted') {
      throw new Error(`notarisation was ${result.status} (submission ${result.id}); see: xcrun notarytool log ${result.id}`);
    }
    return result.id;
  } finally {
    rmSync(zipPath, { force: true });
  }
}
