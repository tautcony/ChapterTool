import { createReadStream } from 'node:fs';
import { realpath, stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { extname, isAbsolute, relative, resolve, sep } from 'node:path';

function option(name, fallback) {
  const index = process.argv.indexOf(name);
  return index < 0 ? fallback : process.argv[index + 1];
}

const rootArgument = option('--root', '../../artifacts/wasm-e2e/site');
const root = await realpath(resolve(process.cwd(), rootArgument));
const port = Number(option('--port', '5261'));
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Invalid port');

const mimeTypes = new Map([
  ['.css', 'text/css; charset=utf-8'], ['.html', 'text/html; charset=utf-8'],
  ['.ico', 'image/x-icon'], ['.js', 'text/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'], ['.map', 'application/json; charset=utf-8'],
  ['.png', 'image/png'], ['.svg', 'image/svg+xml'], ['.txt', 'text/plain; charset=utf-8'],
  ['.wasm', 'application/wasm'], ['.webmanifest', 'application/manifest+json'],
  ['.woff', 'font/woff'], ['.woff2', 'font/woff2'], ['.xml', 'application/xml; charset=utf-8'],
]);
const appRoot = resolve(root, 'ChapterTool');
const appStat = await stat(appRoot).catch(() => null);
if (!appStat?.isDirectory()) throw new Error(`Site app directory not found: ${appRoot}`);

function withinRoot(target) {
  const rel = relative(root, target);
  return rel === '' || (!rel.startsWith(`..${sep}`) && rel !== '..' && !isAbsolute(rel));
}

const server = createServer(async (request, response) => {
  try {
    const rawPath = new URL(request.url ?? '/', 'http://localhost').pathname;
    const decoded = decodeURIComponent(rawPath);
    if (decoded.includes('\0') || decoded.split('/').includes('..')) {
      response.writeHead(404).end('Not found');
      return;
    }

    if (decoded === '/' || decoded === '/ChapterTool') {
      response.writeHead(302, { location: '/ChapterTool/' }).end();
      return;
    }
    if (!decoded.startsWith('/ChapterTool/')) {
      response.writeHead(404).end('Not found');
      return;
    }

    const relativePath = decoded.slice('/ChapterTool/'.length);
    const candidate = resolve(appRoot, relativePath || 'index.html');
    const file = await realpath(candidate).catch(() => null);
    if (!file || !withinRoot(file) || !withinRoot(appRoot) || !file.startsWith(`${appRoot}${sep}`)) {
      response.writeHead(404).end('Not found');
      return;
    }
    const info = await stat(file);
    if (!info.isFile()) {
      response.writeHead(404).end('Not found');
      return;
    }
    response.writeHead(200, {
      'content-type': mimeTypes.get(extname(file).toLowerCase()) ?? 'application/octet-stream',
      'content-length': info.size,
      'x-content-type-options': 'nosniff',
    });
    createReadStream(file).pipe(response);
  } catch (error) {
    response.writeHead(error instanceof URIError ? 400 : 500).end('Request failed');
  }
});

server.listen(port, '127.0.0.1', () => console.log(`Serving ${root} at http://127.0.0.1:${port}/ChapterTool/`));
