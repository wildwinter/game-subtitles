/**
 * Minimal static file server for the player demo.
 * Serves the player-js/ directory so that:
 *   GET /           → demo/index.html
 *   GET /demo/...   → demo/...
 *   GET /dist/...   → dist/...   (IIFE build loaded by the page)
 */
import { createServer } from 'http';
import { readFileSync, existsSync } from 'fs';
import { resolve, extname } from 'path';
import { fileURLToPath } from 'url';
import { exec } from 'child_process';

const __dirname = fileURLToPath(new URL('.', import.meta.url));
const root      = resolve(__dirname, '..');   // render-helper-js/
const port      = 3000;

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js':   'application/javascript; charset=utf-8',
  '.css':  'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.ico':  'image/x-icon',
};

const server = createServer((req, res) => {
  // Strip query string and prevent path traversal via ".."
  const rawPath  = req.url.split('?')[0];
  const safePath = rawPath.replace(/\.\./g, '');
  const urlPath  = safePath === '/' ? '/demo/index.html' : safePath;

  const filePath = resolve(root, '.' + urlPath);

  // Extra guard: resolved path must stay inside root
  if (!filePath.startsWith(root)) {
    res.writeHead(403, { 'Content-Type': 'text/plain' });
    res.end('Forbidden');
    return;
  }

  if (existsSync(filePath)) {
    const mime = MIME[extname(filePath).toLowerCase()] ?? 'application/octet-stream';
    res.writeHead(200, { 'Content-Type': mime });
    res.end(readFileSync(filePath));
  } else {
    res.writeHead(404, { 'Content-Type': 'text/plain' });
    res.end(`Not found: ${urlPath}`);
  }
});

const url = `http://localhost:${port}/`;

server.listen(port, '127.0.0.1', () => {
  console.log(`\n  Game Subtitles — Player Demo`);
  console.log(`  ────────────────────────────────`);
  console.log(`  ${url}\n`);
  console.log('  Press Ctrl+C to stop.\n');

  // Auto-open in default browser
  const open =
    process.platform === 'darwin'  ? `open "${url}"` :
    process.platform === 'win32'   ? `start "" "${url}"` :
                                     `xdg-open "${url}"`;
  exec(open, err => { if (err) console.error('  (could not auto-open browser)'); });
});
