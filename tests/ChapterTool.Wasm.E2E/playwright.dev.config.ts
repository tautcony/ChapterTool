import { defineConfig } from '@playwright/test';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const suiteDir = dirname(fileURLToPath(import.meta.url));
const baseURL = 'http://127.0.0.1:5261/';

export default defineConfig({
  testDir: './specs',
  outputDir: '../../artifacts/wasm-e2e/results-dev',
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  reporter: [['list'], ['html', { outputFolder: '../../artifacts/wasm-e2e/report-dev', open: 'never' }]],
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
    command: 'dotnet run --project ../../src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --no-launch-profile --no-restore --urls http://127.0.0.1:5261',
    cwd: suiteDir,
    url: baseURL,
    reuseExistingServer: false,
    timeout: 60_000,
    stdout: 'pipe',
    stderr: 'pipe',
    env: { ASPNETCORE_ENVIRONMENT: 'Development' },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
