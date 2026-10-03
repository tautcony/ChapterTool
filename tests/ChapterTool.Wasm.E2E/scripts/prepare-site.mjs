import { cp, mkdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../../');
const allowedOutputRoot = resolve(repoRoot, 'artifacts/wasm-e2e');

function option(name, fallback) {
  const index = process.argv.indexOf(name);
  return index < 0 ? fallback : process.argv[index + 1];
}

const publishDir = resolve(repoRoot, option('--publish', 'artifacts/wasm-e2e/publish/wwwroot'));
const siteRoot = resolve(repoRoot, option('--site', 'artifacts/wasm-e2e/site'));
const siteApp = resolve(siteRoot, 'ChapterTool');
const baseHref = '/ChapterTool/';

function isWithin(parent, target) {
  const rel = relative(parent, target);
  return rel === '' || (!rel.startsWith(`..${sep}`) && rel !== '..' && !isAbsolute(rel));
}

if (!isWithin(allowedOutputRoot, siteRoot) || siteRoot === allowedOutputRoot) {
  throw new Error(`Refusing to clear site directory outside artifacts/wasm-e2e: ${siteRoot}`);
}

const publishInfo = await stat(publishDir).catch(() => null);
if (!publishInfo?.isDirectory()) {
  throw new Error(`Publish wwwroot directory does not exist: ${publishDir}`);
}
for (const required of ['index.html', '_framework', 'js/download.js', 'css/app.css']) {
  const entry = await stat(resolve(publishDir, required)).catch(() => null);
  if (!entry) throw new Error(`Missing required publish asset: ${required}`);
}

await rm(siteRoot, { recursive: true, force: true });
await mkdir(siteApp, { recursive: true });
await cp(publishDir, siteApp, { recursive: true });

for (const entryName of ['index.html', '404.html']) {
  const path = resolve(siteApp, entryName);
  const source = entryName === '404.html'
    ? await readFile(resolve(siteApp, 'index.html'), 'utf8')
    : await readFile(path, 'utf8');
  const updated = source.replace(/(<base\s+href=["'])[^"']*(["']\s*\/?\s*>)/i, `$1${baseHref}$2`);
  if (updated === source && !source.includes(`href="${baseHref}"`)) {
    throw new Error(`Could not set the base href in ${entryName}`);
  }
  await writeFile(path, updated, 'utf8');
}

await writeFile(resolve(siteApp, '.nojekyll'), '', 'utf8');
const metadata = [
  `repository=${process.env.GITHUB_REPOSITORY ?? 'local'}`,
  `sha=${process.env.GITHUB_SHA ?? 'local'}`,
  `ref=${process.env.GITHUB_REF ?? 'local'}`,
  `base_href=${baseHref}`,
  `built_at=${new Date().toISOString()}`,
  '',
].join('\n');
await writeFile(resolve(siteApp, 'deploy-info.txt'), metadata, 'utf8');

console.log(`Prepared Pages-shaped site at ${siteRoot}`);
