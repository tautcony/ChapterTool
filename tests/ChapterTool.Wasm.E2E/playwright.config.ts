import { defineConfig } from '@playwright/test';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const suiteDir = dirname(fileURLToPath(import.meta.url));
const baseURL = 'http://127.0.0.1:5261/ChapterTool/';
const runSuffix = process.env.E2E_RUN_NAME ? `-${process.env.E2E_RUN_NAME}` : '';

export default defineConfig({
  testDir: './specs',
  testIgnore: '**/layout.spec.ts',
  outputDir: `../../artifacts/wasm-e2e/results${runSuffix}`,
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.E2E_NO_RETRY ? 0 : process.env.CI ? 1 : 0,
  failOnFlakyTests: Boolean(process.env.CI),
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [
    ['list'],
    ['html', { outputFolder: `../../artifacts/wasm-e2e/report${runSuffix}`, open: 'never' }],
    ['junit', { outputFile: `../../artifacts/wasm-e2e/junit${runSuffix}.xml` }],
  ],
  use: {
    baseURL,
    locale: 'en-US',
    viewport: { width: 1280, height: 800 },
    acceptDownloads: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  webServer: {
    command: 'npm run serve:published -- --root ../../artifacts/wasm-e2e/site --port 5261',
    cwd: suiteDir,
    url: baseURL,
    reuseExistingServer: false,
    timeout: 30_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
  projects: [
    { name: 'chromium', use: { browserName: 'chromium' } },
    { name: 'firefox', use: { browserName: 'firefox' } },
    { name: 'webkit', use: { browserName: 'webkit' } },
  ],
});
