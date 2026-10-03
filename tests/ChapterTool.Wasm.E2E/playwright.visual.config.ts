import { defineConfig } from '@playwright/test';
import baseConfig from './playwright.config.js';

export default defineConfig({
  ...baseConfig,
  testIgnore: [],
  testMatch: '**/layout.spec.ts',
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
  use: {
    ...baseConfig.use,
    locale: 'en-US',
    viewport: { width: 1280, height: 800 },
    colorScheme: 'light',
  },
});
