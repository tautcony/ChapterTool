import { fileURLToPath } from 'node:url';
import { expect, test } from '../support/fixtures';
import { chapterName, chapters, commit, downloadText, loadFixture } from '../support/chapter-workspace';

test('B06 canceling replacement keeps the open document; confirming replaces it', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const grid = chapters(page);
  await commit(await chapterName(page, 1), 'Kept edit');
  const dialogPromise = page.waitForEvent('dialog');
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  const chooser = await chooserPromise;
  await chooser.setFiles(fileURLToPath(new URL('../fixtures/unicode-ogm.txt', import.meta.url)));
  const confirmation = await dialogPromise;
  expect(confirmation.type()).toBe('confirm');
  await confirmation.dismiss();
  const retainedName = await chapterName(page, 1);
  await expect(retainedName).toHaveValue('Kept edit');
  await expect(grid.locator('tbody tr')).toHaveCount(2);
  await page.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(retainedName).toHaveValue('Opening');
  await page.getByRole('button', { name: 'Redo', exact: true }).click();
  await expect(retainedName).toHaveValue('Kept edit');

  page.once('dialog', dialog => dialog.accept());
  const secondChooser = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  await (await secondChooser).setFiles(fileURLToPath(new URL('../fixtures/unicode-ogm.txt', import.meta.url)));
  await expect(await chapterName(page, 1)).toHaveValue('開幕');
  await expect(page.getByRole('button', { name: 'Undo', exact: true })).toBeDisabled();
});

test('B07 malformed XML reports an error and retains the current document', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const currentName = await chapterName(page, 1);
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  const chooser = await chooserPromise;
  await chooser.setFiles(fileURLToPath(new URL('../fixtures/invalid.xml', import.meta.url)));
  await expect(page.locator('.status-text')).toContainText(/fail|invalid|error/i);
  await expect(currentName).toHaveValue('Opening');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(2);
});

test('B04 preview content and Save download contain the same chapter data', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Preview', exact: true });
  const preview = await dialog.locator('[data-testid="preview-content"]').textContent();
  await dialog.getByRole('button', { name: 'Close', exact: true }).click();
  const downloaded = await downloadText(page, testInfo);
  expect(downloaded.content.toString('utf8')).toBe(preview);
});
