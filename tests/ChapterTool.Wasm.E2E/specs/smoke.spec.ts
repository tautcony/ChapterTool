import { readFile } from 'node:fs/promises';
import { expect, test } from '../support/fixtures';
import { chapterName, chapters, commit, downloadText, loadFixture } from '../support/chapter-workspace';

test('B01 @smoke cold starts at the Pages subpath and loads the sample', async ({ readyPage: page }) => {
  await expect(page.getByRole('button', { name: 'Save', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Load OGM sample', exact: true }).click();
  await expect(chapters(page).locator('tbody tr')).toHaveCount(3);
  await expect(page.getByRole('button', { name: 'Save', exact: true })).toBeEnabled();
});

test('B02–B04 @smoke imports, edits, undoes, redoes, previews, and downloads a fixed result', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const grid = chapters(page);
  await expect(grid.locator('tbody tr')).toHaveCount(2);
  await expect(await chapterName(page, 1)).toHaveValue('Opening');
  await expect(await chapterName(page, 2)).toHaveValue('第二章');

  const name = await chapterName(page, 1);
  await commit(name, 'Revised opening');
  const undo = page.getByRole('button', { name: 'Undo', exact: true });
  const redo = page.getByRole('button', { name: 'Redo', exact: true });
  await expect(undo).toBeEnabled();
  await undo.click();
  await expect(name).toHaveValue('Opening');
  await redo.click();
  await expect(name).toHaveValue('Revised opening');

  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Preview', exact: true });
  await expect(dialog.getByText('Revised opening')).toBeVisible();
  await dialog.getByRole('button', { name: 'Close', exact: true }).first().click();

  const downloaded = await downloadText(page, testInfo);
  expect(downloaded.filename).toMatch(/\.txt$/i);
  const expected = await readFile(new URL('../fixtures/expected/edited-ogm.txt', import.meta.url));
  expect(downloaded.content.toString('utf8').replace(/\r\n/g, '\n')).toBe(expected.toString('utf8').replace(/\r\n/g, '\n'));
});
