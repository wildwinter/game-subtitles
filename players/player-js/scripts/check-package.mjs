#!/usr/bin/env node
// Checks the npm package as a user would get it: packs it, checks the file list, installs the
// tarball in a scratch project, imports it in Node, and type-checks a consumer against it.
//
//   npm run check:package          (from players/player-js)
//
// Needs no network: the package has no dependencies, and TypeScript comes from this folder.
import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const pkgDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const { name, version } = JSON.parse(readFileSync(join(pkgDir, 'package.json'), 'utf8'));
const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const run = (cmd, args, cwd) => execFileSync(cmd, args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit'] });
const fail = msg => {
  console.error(`check-package: FAIL ${msg}`);
  process.exit(1);
};

// 1. Exactly these files ship. (npm adds package.json, README, and LICENSE itself.)
const EXPECTED = [
  'LICENSE',
  'README.md',
  'dist/game-subtitles-player.esm.js',
  'dist/game-subtitles-player.js',
  'dist/index.d.ts',
  'package.json',
];
run('node', ['build.mjs'], pkgDir);
const [packed] = JSON.parse(run(npm, ['pack', '--dry-run', '--json', '--ignore-scripts'], pkgDir));
const files = packed.files.map(f => f.path).sort();
if (JSON.stringify(files) !== JSON.stringify(EXPECTED)) {
  fail(`the package would ship:\n  ${files.join('\n  ')}\nbut should ship:\n  ${EXPECTED.join('\n  ')}`);
}
console.log(`check-package: ${name}@${version} ships ${files.length} files (${(packed.size / 1024).toFixed(1)} kB packed)`);

// 2. Install the real tarball in a scratch project.
const scratch = mkdtempSync(join(tmpdir(), 'game-subtitles-package-'));
try {
  const tarball = run(npm, ['pack', '--ignore-scripts', '--pack-destination', scratch], pkgDir).trim().split('\n').pop();
  writeFileSync(join(scratch, 'package.json'), JSON.stringify({ name: 'scratch', private: true, type: 'module' }));
  run(npm, ['install', '--no-audit', '--no-fund', join(scratch, tarball)], scratch);

  // 3. It imports and runs in Node.
  writeFileSync(join(scratch, 'smoke.mjs'), `
    import { SubtitlePlayer, DomRenderer, CanvasRenderer, estimateDuration, wrapAndPaginate, allocateTimings, CHARACTER_NAME_CLASS } from '${name}';
    const renderer = {
      measureLineWidth: t => t.length * 10, getContainerWidth: () => 100,
      render: lines => rendered.push(lines), clear: () => {},
    };
    const rendered = [];
    let done = false;
    const player = new SubtitlePlayer({ renderer, maxLines: 2 });
    player.start({ text: 'Get down, now! They have seen us.', characterName: 'Tam', onComplete: () => { done = true; } });
    player.tick(60);
    const checks = {
      estimate: Math.abs(estimateDuration('Get down, now! They have seen us.') - 33 / 14) < 1e-9,
      rendered: rendered.length > 0,
      completed: done,
      layout: wrapAndPaginate('aaaaa bbbbb', t => t.length * 10, 100, 1).length === 2,
      timings: allocateTimings([['abc'], ['def']], 4)[0] === 2,
      classes: typeof DomRenderer === 'function' && typeof CanvasRenderer === 'function',
      className: CHARACTER_NAME_CLASS === 'gs-character-name',
      iife: import.meta.resolve('${name}/iife').endsWith('/dist/game-subtitles-player.js'),
    };
    const failed = Object.entries(checks).filter(([, ok]) => !ok).map(([k]) => k);
    if (failed.length) { console.error('failed: ' + failed.join(', ')); process.exit(1); }
  `);
  try {
    run('node', ['smoke.mjs'], scratch);
  } catch {
    fail('the installed package did not import and run in Node');
  }
  console.log('check-package: the installed package imports and runs in Node');

  // 4. Its types work for a consumer, under both NodeNext and bundler module resolution.
  writeFileSync(join(scratch, 'consumer.ts'), `
    import { SubtitlePlayer, DomRenderer, estimateDuration, type SubtitleRenderer } from '${name}';
    const renderer: SubtitleRenderer = new DomRenderer(document.createElement('div'));
    const player = new SubtitlePlayer({ renderer });
    player.start({ text: 'Hello', duration: estimateDuration('Hello', { charsPerSecond: 12 }), onComplete: () => player.stop() });
    // @ts-expect-error the text is required
    player.start({ duration: 1 });
  `);
  const tsc = join(pkgDir, 'node_modules/typescript/bin/tsc');
  for (const resolution of [['NodeNext', 'NodeNext'], ['ESNext', 'Bundler']]) {
    try {
      run('node', [tsc, '--noEmit', '--strict', '--target', 'ES2022', '--lib', 'ES2022,DOM',
        '--module', resolution[0], '--moduleResolution', resolution[1], 'consumer.ts'], scratch);
    } catch (e) {
      fail(`a consumer does not type-check with ${resolution[1]} resolution:\n${e.stdout ?? ''}`);
    }
  }
  console.log('check-package: a consumer type-checks with NodeNext and Bundler resolution');

  // 5. Every code example in the README is valid against the installed package. TypeScript
  // examples are checked strictly; JavaScript ones as a JS user's editor would, without
  // strict null checks.
  const readme = readFileSync(join(pkgDir, 'README.md'), 'utf8');
  const blocks = [...readme.matchAll(/```(javascript|typescript)\n([\s\S]*?)```/g)];
  if (blocks.length === 0) fail('found no code examples in README.md');
  const examples = { javascript: [], typescript: [] };
  blocks.forEach(([, lang, code], i) => {
    const file = `readme-example-${i + 1}.${lang === 'typescript' ? 'ts' : 'js'}`;
    writeFileSync(join(scratch, file), code);
    examples[lang].push(file);
  });
  const common = ['--noEmit', '--target', 'ES2022', '--lib', 'ES2022,DOM', '--module', 'NodeNext', '--moduleResolution', 'NodeNext'];
  const checks = [
    ['TypeScript', examples.typescript, ['--strict']],
    ['JavaScript', examples.javascript, ['--allowJs', '--checkJs']],
  ];
  for (const [label, files, flags] of checks) {
    if (files.length === 0) continue;
    try {
      run('node', [tsc, ...common, ...flags, ...files], scratch);
    } catch (e) {
      fail(`a ${label} example in README.md is not valid:\n${e.stdout ?? ''}`);
    }
  }
  console.log(`check-package: all ${blocks.length} README code examples type-check against the package`);
} finally {
  rmSync(scratch, { recursive: true, force: true });
}

console.log(`check-package: PASS ${name}@${version}`);
