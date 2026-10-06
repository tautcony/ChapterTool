import { defineConfig } from '@playwright/test';
import baseConfig from './playwright.visual.config.js';

export default defineConfig({
  ...baseConfig,
  snapshotPathTemplate: '../../artifacts/wasm-e2e/review-snapshots/{arg}-{projectName}-{platform}{ext}',
  outputDir: '../../artifacts/wasm-e2e/results-review',
  reporter: [
    ['list'],
    ['html', { outputFolder: '../../artifacts/wasm-e2e/report-review', open: 'never' }],
    ['junit', { outputFile: '../../artifacts/wasm-e2e/junit-review.xml' }],
  ],
});
