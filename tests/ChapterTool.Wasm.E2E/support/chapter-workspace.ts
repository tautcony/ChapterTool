import { expect, type Locator, type Page, type TestInfo } from '@playwright/test';
import { fileURLToPath } from 'node:url';

export const chapters = (page: Page): Locator => page.getByRole('table', { includeHidden: true, name: 'Chapters', exact: true });

export async function loadFixture(page: Page, fixture: string): Promise<void> {
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  const chooser = await chooserPromise;
  await chooser.setFiles(fileURLToPath(new URL(`../fixtures/${fixture}`, import.meta.url)));
}

export async function loadBytes(page: Page, name: string, buffer: Buffer): Promise<void> {
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  const chooser = await chooserPromise;
  await chooser.setFiles({ name, mimeType: 'text/plain', buffer });
}

export async function chapterName(page: Page, row: number): Promise<Locator> {
  return chapters(page).getByRole('textbox', { includeHidden: true, name: `Name ${row}`, exact: true });
}

export async function commit(locator: Locator, value: string): Promise<void> {
  await locator.fill(value);
  await locator.press('Tab');
  await expect(locator).toHaveValue(value);
}

export async function downloadText(page: Page, testInfo: TestInfo): Promise<{ filename: string; content: Buffer }> {
  const downloadPromise = page.waitForEvent('download', { timeout: 10_000 });
  await page.getByRole('button', { name: 'Save', exact: true }).click();
  const download = await downloadPromise;
  expect(await download.failure()).toBeNull();
  const { readFile } = await import('node:fs/promises');
  const path = testInfo.outputPath(download.suggestedFilename());
  await download.saveAs(path);
  return { filename: download.suggestedFilename(), content: await readFile(path) };
}
