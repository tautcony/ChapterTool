import { expect, test as base } from '@playwright/test';
import type { Page, TestInfo } from '@playwright/test';

type BrowserDiagnostics = {
  errors: string[];
};

type Fixtures = {
  diagnostics: BrowserDiagnostics;
  readyPage: Page;
};

async function attachDiagnostics(page: Page, testInfo: TestInfo, diagnostics: BrowserDiagnostics) {
  if (diagnostics.errors.length > 0) {
    await testInfo.attach('browser-diagnostics.txt', {
      body: Buffer.from(diagnostics.errors.join('\n')),
      contentType: 'text/plain',
    });
  }
}

export const test = base.extend<Fixtures>({
  diagnostics: [async ({ page, context }, use, testInfo) => {
    const diagnostics: BrowserDiagnostics = { errors: [] };
    const watched = new WeakSet<Page>();
    const watch = (observedPage: Page) => {
      if (watched.has(observedPage)) return;
      watched.add(observedPage);
      observedPage.on('pageerror', error => diagnostics.errors.push(`pageerror: ${error.message}`));
      observedPage.on('console', message => {
        if (message.type() === 'error') diagnostics.errors.push(`console.error: ${message.text()}`);
      });
      observedPage.on('requestfailed', request => {
        diagnostics.errors.push(`requestfailed: ${request.method()} ${request.url()} ${request.failure()?.errorText ?? ''}`);
      });
      observedPage.on('response', response => {
        const request = response.request();
        if (new URL(response.url()).origin === 'http://127.0.0.1:5261' && response.status() >= 400) {
          diagnostics.errors.push(`resource ${response.status()}: ${request.method()} ${response.url()}`);
        }
      });
    };
    watch(page);
    context.on('page', watch);
    await use(diagnostics);
    await attachDiagnostics(page, testInfo, diagnostics);
    expect(diagnostics.errors, diagnostics.errors.join('\n')).toEqual([]);
  }, { auto: true }],
  readyPage: async ({ page }, use) => {
    await page.goto('./');
    const shell = page.locator('#chaptertool-shell');
    await expect(shell).toHaveAttribute('data-app-ready', 'true');
    await expect(shell).toHaveAttribute('aria-busy', 'false');
    await use(page);
  },
});

export { expect };
