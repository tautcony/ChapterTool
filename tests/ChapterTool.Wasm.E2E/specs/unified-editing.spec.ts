import { expect, test } from '../support/fixtures';
import { chapterName, chapters, commit, downloadText, loadFixture } from '../support/chapter-workspace';

test('B19 history dialog navigates between retained sibling edits and redoes the selected branch', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const name = await chapterName(page, 1);
  await commit(name, 'First branch');
  await page.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(name).toHaveValue('Opening');
  await commit(name, 'Second branch');

  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  const history = page.getByRole('dialog', { name: 'Edit history', exact: true });
  const entries = history.locator('.history-entry');
  await expect(entries).toHaveCount(3);
  await expect(name).toHaveValue('Second branch');

  await entries.nth(1).click();
  await expect(name).toHaveValue('First branch');
  await entries.nth(2).click();
  await expect(name).toHaveValue('Second branch');
  await entries.nth(0).click();
  await expect(name).toHaveValue('Opening');
  await history.getByRole('button', { name: 'Redo', exact: true }).click();
  await expect(name).toHaveValue('Second branch');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(2);
});

test('B20 expression preview is read-only until applied, then export and undo use committed content', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const secondTime = chapters(page).getByRole('textbox', { includeHidden: true, name: 'Time 2', exact: true });
  await expect(secondTime).toHaveValue('00:00:12.500');

  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  const expression = page.getByLabel('Custom expression', { exact: true });
  await expression.fill('t / 2');
  const livePreview = page.getByTestId('expression-preview');
  await expect(livePreview).toContainText('00:00:12.500');
  await expect(livePreview).toContainText('00:00:06.250');
  await expect(secondTime).toHaveValue('00:00:12.500');

  await page.getByRole('dialog', { name: 'Expression', exact: true }).getByRole('button', { name: 'Cancel', exact: true }).click();
  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  const previewDialog = page.getByRole('dialog', { name: 'Preview', exact: true });
  const uncommittedPreview = await previewDialog.locator('[data-testid="preview-content"]').textContent();
  expect(uncommittedPreview).toContain('00:00:12.500');
  expect(uncommittedPreview).not.toContain('00:00:06.250');
  await previewDialog.getByRole('button', { name: 'Close', exact: true }).first().click();
  const uncommittedDownload = await downloadText(page, testInfo);
  expect(uncommittedDownload.content.toString('utf8')).toBe(uncommittedPreview);

  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  await expression.fill('t / 2');
  await expect(livePreview).toContainText('00:00:06.250');
  await page.getByRole('dialog', { name: 'Expression', exact: true }).getByRole('button', { name: 'Apply changes', exact: true }).click();
  await expect(secondTime).toHaveValue('00:00:06.250');
  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  const committedPreview = await page.getByRole('dialog', { name: 'Preview', exact: true })
    .locator('[data-testid="preview-content"]').textContent();
  expect(committedPreview).toContain('00:00:06.250');
  await page.getByRole('dialog', { name: 'Preview', exact: true }).getByRole('button', { name: 'Close', exact: true }).first().click();
  const committedDownload = await downloadText(page, testInfo);
  expect(committedDownload.content.toString('utf8')).toBe(committedPreview);

  await page.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(secondTime).toHaveValue('00:00:12.500');
  await page.getByRole('button', { name: 'Preview', exact: true }).click();
  const undonePreview = await page.getByRole('dialog', { name: 'Preview', exact: true })
    .locator('[data-testid="preview-content"]').textContent();
  expect(undonePreview).toContain('00:00:12.500');
  await page.getByRole('dialog', { name: 'Preview', exact: true }).getByRole('button', { name: 'Close', exact: true }).first().click();
  const undoneDownload = await downloadText(page, testInfo);
  expect(undoneDownload.content.toString('utf8')).toBe(undonePreview);
});

test('B21 expression diagnostics cannot be applied and cancel leaves committed rows unchanged', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const secondTime = chapters(page).getByRole('textbox', { includeHidden: true, name: 'Time 2', exact: true });
  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  const expression = page.getByLabel('Custom expression', { exact: true });
  await expression.fill('return bad()');

  const livePreview = page.getByTestId('expression-preview');
  await expect(livePreview).toContainText('Lua');
  await expect(page.getByRole('dialog', { name: 'Expression', exact: true }).getByRole('button', { name: 'Apply changes', exact: true })).toBeDisabled();
  await expect(secondTime).toHaveValue('00:00:12.500');
  await page.getByRole('dialog', { name: 'Expression', exact: true }).getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(page.getByTestId('expression-preview')).toHaveCount(0);
  await expect(secondTime).toHaveValue('00:00:12.500');
});

test('B22 editing an applied chapter does not rerun its previous expression during export', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const secondTime = chapters(page).getByRole('textbox', { includeHidden: true, name: 'Time 2', exact: true });
  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  await page.getByLabel('Custom expression', { exact: true }).fill('t / 2');
  const livePreview = page.getByTestId('expression-preview');
  await expect(livePreview).toBeVisible();
  await page.getByRole('dialog', { name: 'Expression', exact: true }).getByRole('button', { name: 'Apply changes', exact: true }).click();
  await expect(secondTime).toHaveValue('00:00:06.250');

  await commit(secondTime, '00:00:08.000');
  await expect(chapters(page).getByLabel('Frames 2', { exact: true })).toHaveValue('192');
  await expect(secondTime).toHaveValue('00:00:08.000');
  const downloaded = await downloadText(page, testInfo);
  expect(downloaded.content.toString('utf8')).toContain('00:00:08.000');
  expect(downloaded.content.toString('utf8')).not.toContain('00:00:04.000');
});

test('B23 rapid expression edits display diagnostics only for the latest draft', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  const expression = page.getByLabel('Custom expression', { exact: true });
  await expression.fill('t / 2');
  await expression.fill('return bad()');

  const livePreview = page.getByTestId('expression-preview');
  await expect(livePreview).toContainText('Lua');
  await expect(page.getByRole('dialog', { name: 'Expression', exact: true }).getByRole('button', { name: 'Apply changes', exact: true })).toBeDisabled();
  await expect(chapters(page).getByRole('textbox', { includeHidden: true, name: 'Time 2', exact: true })).toHaveValue('00:00:12.500');
});

test('B24 expression review shows before and after values and keeps multiline preset edits', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  const editor = dialog.getByLabel('Custom expression', { exact: true });
  const preset = dialog.getByLabel('Preset', { exact: true });
  await preset.selectOption('offset-seconds');
  const presetText = await editor.inputValue();
  expect(presetText).toContain('\n');
  await editor.fill('local factor = 0.5\nreturn t * factor');
  await dialog.getByRole('checkbox', { name: 'All chapters', exact: true }).check();
  await expect(dialog.locator('.expression-chapter')).toHaveCount(2);
  await expect(dialog.locator('.expression-chapter').nth(1)).toContainText('00:00:12.500');
  await expect(dialog.locator('.expression-chapter').nth(1)).toContainText('00:00:06.250');
  await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeEnabled();
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
});

test('B25 input composition delays calculation until composition ends', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  const editor = dialog.getByLabel('Custom expression', { exact: true });
  await expect(editor).toHaveAttribute('data-composition-ready', 'true');
  await editor.evaluate(element => element.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true })));
  await expect(dialog.getByText(/Finish text composition/)).toBeVisible();
  await editor.evaluate(element => {
    (element as HTMLTextAreaElement).value = 't / 2';
    element.dispatchEvent(new InputEvent('input', {
      bubbles: true, data: 't / 2', inputType: 'insertCompositionText', isComposing: true,
    }));
  });
  await expect(dialog.getByText(/Finish text composition/)).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeDisabled();
  await editor.evaluate(element => element.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true })));
  await expect(dialog.locator('.expression-chapter').nth(1)).toContainText('00:00:06.250');
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
});
