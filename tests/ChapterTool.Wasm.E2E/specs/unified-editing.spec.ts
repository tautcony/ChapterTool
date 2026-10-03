import { expect, test } from '../support/fixtures';
import { chapterName, chapters, commit, downloadText, loadFixture } from '../support/chapter-workspace';

test('B19 history panel navigates between retained sibling edits and redoes the selected branch', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const name = await chapterName(page, 1);
  await commit(name, 'First branch');
  await page.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(name).toHaveValue('Opening');
  await commit(name, 'Second branch');

  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  const history = page.getByRole('region', { name: 'Edit history', exact: true });
  const entries = history.locator('.history-entry');
  await expect(entries).toHaveCount(3);
  await expect(name).toHaveValue('Second branch');

  await entries.nth(1).click();
  await expect(name).toHaveValue('First branch');
  await entries.nth(2).click();
  await expect(name).toHaveValue('Second branch');
  await entries.nth(0).click();
  await expect(name).toHaveValue('Opening');
  await page.getByRole('button', { name: 'Redo', exact: true }).click();
  await expect(name).toHaveValue('Second branch');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(2);
});

test('B20 applied expression is committed content shared by preview, download, and undo', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const secondTime = chapters(page).getByRole('textbox', { name: 'Time 2', exact: true });
  await expect(secondTime).toHaveValue('00:00:12.500');

  await page.getByLabel('Use', { exact: true }).check();
  const expression = page.getByLabel('Custom expression', { exact: true });
  await expression.fill('t / 2');
  await expression.press('Tab');
  await expect(secondTime).toHaveValue('00:00:06.250');

  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  const previewDialog = page.getByRole('dialog', { name: 'Preview', exact: true });
  const preview = await previewDialog.locator('[data-testid="preview-content"]').textContent();
  expect(preview).toContain('00:00:06.250');
  await previewDialog.getByRole('button', { name: 'Close', exact: true }).click();
  const downloaded = await downloadText(page, testInfo);
  expect(downloaded.content.toString('utf8')).toBe(preview);

  await page.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(secondTime).toHaveValue('00:00:12.500');
  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  const undonePreview = await page.getByRole('dialog', { name: 'Preview', exact: true })
    .locator('[data-testid="preview-content"]').textContent();
  expect(undonePreview).toContain('00:00:12.500');
  await page.getByRole('dialog', { name: 'Preview', exact: true }).getByRole('button', { name: 'Close', exact: true }).click();
  const undoneDownload = await downloadText(page, testInfo);
  expect(undoneDownload.content.toString('utf8')).toBe(undonePreview);
});
