import { cp, mkdir, readFile, rm, stat, writeFile } from 'node:fs/promises';
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../../');
const allowedOutputRoot = resolve(repoRoot, 'artifacts/wasm-e2e');

function option(name, fallback) {
  const index = process.argv.indexOf(name);
  return index < 0 ? fallback : process.argv[index + 1];
}

const publishPath = process.env.CHAPTERTOOL_E2E_PUBLISH
  ?? option('--publish', 'artifacts/wasm-e2e/publish/wwwroot');
const publishDir = resolve(repoRoot, publishPath);
const apiDocsPath = option('--api-docs', 'artifacts/core-api-docs/site');
const apiDocsDir = resolve(repoRoot, apiDocsPath);
const siteRoot = resolve(repoRoot, option('--site', 'artifacts/wasm-e2e/site'));
const siteApp = resolve(siteRoot, 'ChapterTool');
const siteDocs = resolve(siteApp, 'docs');
const baseHref = '/ChapterTool/';

function isWithin(parent, target) {
  const rel = relative(parent, target);
  return rel === '' || (!rel.startsWith(`..${sep}`) && rel !== '..' && !isAbsolute(rel));
}

function updateBaseHref(source, entryName) {
  const tagStart = source.search(/<base\b/i);
  const tagEnd = tagStart < 0 ? -1 : source.indexOf('>', tagStart);
  if (tagStart < 0 || tagEnd < 0) {
    throw new Error(`Could not find a base tag in ${entryName}`);
  }

  const tag = source.slice(tagStart, tagEnd + 1);
  const hrefAttribute = /\bhref\s*=\s*(["'])/i.exec(tag);
  if (!hrefAttribute) {
    throw new Error(`Could not find a quoted base href in ${entryName}`);
  }

  const valueStart = hrefAttribute.index + hrefAttribute[0].length;
  const valueEnd = tag.indexOf(hrefAttribute[1], valueStart);
  if (valueEnd < 0) {
    throw new Error(`Could not find the end of the base href in ${entryName}`);
  }

  const updatedTag = `${tag.slice(0, valueStart)}${baseHref}${tag.slice(valueEnd)}`;
  return `${source.slice(0, tagStart)}${updatedTag}${source.slice(tagEnd + 1)}`;
}

if (!isWithin(allowedOutputRoot, siteRoot) || siteRoot === allowedOutputRoot) {
  throw new Error(`Refusing to clear site directory outside artifacts/wasm-e2e: ${siteRoot}`);
}

const publishInfo = await stat(publishDir).catch(() => null);
if (!publishInfo?.isDirectory()) {
  throw new Error(`Publish wwwroot directory does not exist: ${publishDir}`);
}
const apiDocsInfo = await stat(apiDocsDir).catch(() => null);
if (!apiDocsInfo?.isDirectory() || !(await stat(resolve(apiDocsDir, 'index.html')).catch(() => null))) {
  throw new Error(`Generated Core API docs site does not exist: ${apiDocsDir}`);
}
for (const required of ['index.html', '_framework', 'js/download.js', 'css/app.css']) {
  const entry = await stat(resolve(publishDir, required)).catch(() => null);
  if (!entry) throw new Error(`Missing required publish asset: ${required}`);
}

await rm(siteRoot, { recursive: true, force: true });
await mkdir(siteApp, { recursive: true });
await cp(publishDir, siteApp, { recursive: true });
await cp(apiDocsDir, siteDocs, { recursive: true });

for (const entryName of ['index.html', '404.html']) {
  const path = resolve(siteApp, entryName);
  const source = entryName === '404.html'
    ? await readFile(resolve(siteApp, 'index.html'), 'utf8')
    : await readFile(path, 'utf8');
  const updated = updateBaseHref(source, entryName);
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
  'api_docs_path=docs/',
  `built_at=${new Date().toISOString()}`,
  '',
].join('\n');
await writeFile(resolve(siteApp, 'deploy-info.txt'), metadata, 'utf8');

console.log(`Prepared Pages-shaped site at ${siteRoot}`);
