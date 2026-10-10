import { test, expect } from '../support/fixtures';
import { chapters, commit, downloadText, loadBytes, loadFixture } from '../support/chapter-workspace';

const viewports = [
  { width: 1280, height: 800 }, { width: 1920, height: 1080 },
  { width: 390, height: 844 }, { width: 390, height: 640 }, { width: 844, height: 390 },
  ...[320, 519, 520, 521, 759, 760, 761].map(width => ({ width, height: 640 })),
];

async function inViewport(locator: import('@playwright/test').Locator, width: number, height: number) {
  const box = await locator.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.x).toBeGreaterThanOrEqual(0);
  expect(box!.y).toBeGreaterThanOrEqual(0);
  expect(box!.x + box!.width).toBeLessThanOrEqual(width + 1);
  expect(box!.y + box!.height).toBeLessThanOrEqual(height + 1);
}

test('M01 @smoke history is an independent native dialog on a short screen', async ({ readyPage: page }) => {
  await page.setViewportSize({ width: 390, height: 640 });
  await loadFixture(page, 'minimal-ogm.txt');
  await expect(page.getByRole('textbox', { includeHidden: true, name: 'Name 1', exact: true })).toBeVisible();
  const grid = page.locator('.grid-wrap');
  const before = await grid.boundingBox();
  const opener = page.getByRole('button', { name: 'Edit history', exact: true });
  await opener.click();
  const dialog = page.getByRole('dialog', { name: 'Edit history', exact: true });
  await expect(dialog).toBeVisible({ timeout: 4000 });
  await expect(dialog).toHaveJSProperty('open', true);
  expect(await grid.boundingBox()).toEqual(before);
  const close = dialog.getByRole('button', { name: 'Close', exact: true }).first();
  const box = await close.boundingBox();
  expect(box!.y).toBeGreaterThanOrEqual(0);
  expect(box!.y + box!.height).toBeLessThanOrEqual(640);
  await close.click();
  await expect(dialog).toHaveCount(0);
  await expect(opener).toBeFocused();
});

test('M02 dialogs preserve main geometry and reachable actions across viewport boundaries', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await expect(page.getByRole('button', { name: 'Expression editor', exact: true })).toBeVisible({ timeout: 4000 });
  for (const size of viewports) {
    await page.setViewportSize(size);
    for (const title of ['Edit history', 'Expression', 'Advanced export options']) {
      const opener = page.getByRole('button', { name: title === 'Expression' ? 'Expression editor' : title, exact: true });
      await opener.scrollIntoViewIfNeeded();
      const before = await page.locator('.grid-wrap').boundingBox();
      await opener.click();
      const dialog = page.getByRole('dialog', { name: title, exact: true });
      await expect(dialog).toBeVisible();
      await expect(page.getByRole('dialog')).toHaveCount(1);
      expect(await page.locator('.grid-wrap').boundingBox()).toEqual(before);
      await inViewport(dialog, size.width, size.height);
      if (title === 'Expression') {
        await dialog.getByLabel('Custom expression', { exact: true }).fill('return missing_function()');
        await expect(dialog.getByTestId('expression-preview')).toContainText('Lua');
        await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeDisabled();
        const input = await dialog.getByLabel('Custom expression', { exact: true }).boundingBox();
        expect(input!.width).toBeGreaterThan(150);
      }
      const footerActions = dialog.locator('.modal-footer button');
      for (const action of await footerActions.all()) {
        await inViewport(action, size.width, size.height);
        // Firefox can round a 44 CSS pixel target just below 44 in its DOMRect.
        expect((await action.boundingBox())!.height + 0.001).toBeGreaterThanOrEqual(44);
      }
      await dialog.getByRole('button', { name: 'Close', exact: true }).first().click();
      await expect(dialog).toHaveCount(0);
      await expect(opener).toBeFocused();
      expect(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth)).toBe(false);
    }
    const bounds = await page.locator('.grid-zone').boundingBox();
    const options = await page.locator('.options-zone').boundingBox();
    expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(options!.y);
    expect(bounds!.height).toBeGreaterThanOrEqual(112);
  }
});

test('M07 history inspection keeps the document unchanged until explicit restoration', async ({ readyPage: page }) => {
  await page.setViewportSize({ width: 390, height: 640 });
  await loadFixture(page, 'minimal-ogm.txt');
  const name = chapters(page).getByRole('textbox', { includeHidden: true, name: 'Name 1', exact: true });
  for (let index = 1; index <= 24; index++) await commit(name, `Edit ${index}`);
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  const history = page.getByRole('dialog', { name: 'Edit history', exact: true });
  const list = history.locator('.history-list');
  await list.evaluate(el => { el.scrollTop = el.scrollHeight; });
  await expect(history.locator('.history-entry[aria-current="step"]')).toBeInViewport();
  const entries = history.locator('.history-entry');
  const count = await entries.count();
  await entries.nth(count - 2).click();
  await expect(name).toHaveValue('Edit 24');
  await history.getByRole('button', { name: 'Restore to this node', exact: true }).click();
  await expect(name).toHaveValue('Edit 23');
  await expect(history.locator('.history-entry[aria-current="step"]')).toBeInViewport();
  await history.locator('.modal-close').click();
  await page.getByRole('button', { name: 'Redo', exact: true }).click();
  await expect(name).toHaveValue('Edit 24');
});

test('M08 cancellation restores an applied preset and no-change Apply stays disabled', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const opener = page.getByRole('button', { name: 'Expression editor', exact: true });
  await opener.click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  const preset = dialog.getByLabel('Preset', { exact: true });
  await preset.selectOption('offset-seconds');
  const appliedExpression = await dialog.getByLabel('Custom expression', { exact: true }).inputValue();
  await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeEnabled();
  await dialog.getByRole('button', { name: 'Apply changes', exact: true }).click();
  await expect(dialog).toHaveCount(0);
  await opener.click();
  await expect(preset).toHaveValue('offset-seconds');
  await preset.selectOption('identity');
  await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeDisabled();
  await dialog.getByLabel('Custom expression', { exact: true }).fill('t / 2');
  await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeEnabled();
  await page.keyboard.press('Escape');
  await opener.click();
  await expect(preset).toHaveValue('offset-seconds');
  await expect(dialog.getByLabel('Custom expression', { exact: true })).toHaveValue(appliedExpression);
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  await expect(page.getByRole('dialog').locator('.history-entry')).toHaveCount(2);
});

test('M03 modal focus, Escape, rapid cancellation and background isolation preserve the document', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const name = chapters(page).getByRole('textbox', { includeHidden: true, name: 'Name 1', exact: true });
  await commit(name, 'Retained edit');
  const baseline = await downloadText(page, testInfo);
  const opener = page.getByRole('button', { name: 'Expression editor', exact: true });
  await opener.click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  const input = dialog.getByLabel('Custom expression', { exact: true });
  await expect(input).toHaveValue('t');
  await input.fill('t / 2');
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(opener).toBeFocused();
  await opener.click();
  await expect(input).toHaveValue('t');
  await expect(dialog.getByTestId('expression-preview')).toBeVisible();
  await expect(dialog.getByTestId('expression-preview')).not.toContainText('00:00:06.250');
  await input.fill('return bad()');
  await expect(dialog.getByTestId('expression-preview')).toContainText('Lua');
  // Native focus stays inside the dialog during forward and reverse navigation.
  for (const key of ['Tab', 'Shift+Tab', ...Array<string>(12).fill('Tab')]) {
    await page.keyboard.press(key);
    expect(await dialog.evaluate(el => el.contains(document.activeElement))).toBe(true);
  }
  await dialog.locator('.modal-close').focus();
  await page.keyboard.press('Control+z');
  await page.locator('#chaptertool-shell').dispatchEvent('keydown', { key: 'z', ctrlKey: true });
  const transfer = await page.evaluateHandle(() => {
    const data = new DataTransfer();
    data.items.add(new File(['CHAPTER01=00:00:00.000\nCHAPTER01NAME=Unwanted\n'], 'drop.txt'));
    return data;
  });
  await page.locator('#chaptertool-shell').dispatchEvent('drop', { dataTransfer: transfer });
  await transfer.dispose();
  await expect(name).toHaveValue('Retained edit');
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  const cancelled = await downloadText(page, testInfo);
  expect(cancelled.content.equals(baseline.content)).toBe(true);
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  await expect(page.getByRole('dialog').locator('.history-entry')).toHaveCount(2);
  await page.keyboard.press('Escape');
});

test('M04 output preferences stage bytes and templates use separate content review', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const baseline = await downloadText(page, testInfo);
  const storage = await page.evaluate(() => localStorage.getItem('chaptertool.wasm.settings'));
  const opener = page.getByRole('button', { name: 'Advanced export options', exact: true });
  await opener.click();
  const dialog = page.getByRole('dialog', { name: 'Advanced export options', exact: true });
  await dialog.locator('#export-encoding').selectOption('1');
  await dialog.locator('#export-bom').check();
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(await page.evaluate(() => localStorage.getItem('chaptertool.wasm.settings'))).toBe(storage);
  expect((await downloadText(page, testInfo)).content.equals(baseline.content)).toBe(true);
  await opener.click();
  await expect(dialog.locator('#export-encoding')).toHaveValue('0');
  await dialog.locator('#export-encoding').selectOption('1');
  await dialog.locator('#export-bom').check();
  await dialog.getByRole('button', { name: 'Apply', exact: true }).click();
  await expect(dialog).toHaveCount(0);
  const name = chapters(page).getByRole('textbox', { name: 'Name 1', exact: true });
  const original = await name.inputValue();
  await page.getByRole('button', { name: 'Naming and numbering', exact: true }).click();
  const naming = page.getByRole('dialog', { name: 'Naming and numbering', exact: true });
  await naming.locator('#content-template').setInputFiles({ name: 'names.txt', mimeType: 'text/plain', buffer: Buffer.from('Alpha\nBeta') });
  await naming.getByRole('button', { name: 'Preview', exact: true }).click();
  await expect(naming.getByTestId('content-review')).toContainText('Alpha');
  await expect(name).toHaveValue(original);
  await naming.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(name).toHaveValue(original);
  await page.getByRole('button', { name: 'Naming and numbering', exact: true }).click();
  await naming.locator('#content-template').setInputFiles({ name: 'names.txt', mimeType: 'text/plain', buffer: Buffer.from('Alpha\nBeta') });
  await naming.getByRole('button', { name: 'Preview', exact: true }).click();
  await naming.getByRole('button', { name: 'Apply', exact: true }).click();
  await expect(chapters(page).getByRole('textbox', { name: 'Name 1', exact: true })).toHaveValue('Alpha');
  const applied = await downloadText(page, testInfo);
  expect([...applied.content.subarray(0, 2)]).toEqual([0xff, 0xfe]);
  expect(applied.content.toString('utf16le')).toContain('Alpha');
  await page.reload();
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await opener.click();
  await expect(dialog.locator('#export-encoding')).toHaveValue('1');
  await expect(dialog.locator('#export-bom')).toBeChecked();
});

test('M06 existing tools share Escape, focus restoration and draft dismissal behavior', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  for (const [openerName, title] of [['Settings', 'Settings'], ['Preview', 'Preview'], ['Log', 'Activity log']] as const) {
    const opener = page.getByRole('button', { name: openerName, exact: true });
    await opener.click();
    const dialog = page.getByRole('dialog', { name: title, exact: true });
    await expect(dialog).toHaveJSProperty('open', true);
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(opener).toBeFocused();
  }
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await settings.getByRole('button', { name: 'Output preferences', exact: true }).click();
  await settings.locator('#settings-encoding').selectOption('1');
  await page.mouse.click(2, 2);
  await expect(settings).toBeVisible();
  await settings.locator('#settings-encoding').focus();
  await page.keyboard.press('Escape');
  await expect(settings.getByRole('alertdialog')).toBeVisible();
  await settings.getByRole('button', { name: 'Discard', exact: true }).click();
  await page.getByRole('button', { name: 'Advanced export options', exact: true }).click();
  await expect(page.getByRole('dialog').locator('#export-encoding')).toHaveValue('0');
  await page.keyboard.press('Escape');

  await page.getByRole('button', { name: 'Expression editor', exact: true }).click();
  const expression = page.getByRole('dialog', { name: 'Expression', exact: true });
  const preset = expression.getByLabel('Preset', { exact: true });
  const presetId = await preset.locator('option').nth(1).getAttribute('value');
  await preset.selectOption(presetId!);
  await expect(preset).toHaveValue(presetId!);
  await expression.getByLabel('Custom expression', { exact: true }).fill('t / 2');
  await page.mouse.click(2, 2);
  await expect(expression).toBeVisible();
  await expression.getByLabel('Custom expression', { exact: true }).focus();
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Expression editor', exact: true }).click();
  await expect(preset).toHaveValue('');
  await expect(expression.getByLabel('Custom expression', { exact: true })).toHaveValue('t');
  await page.keyboard.press('Escape');

  const time = chapters(page).getByRole('textbox', { includeHidden: true, name: 'Time 2', exact: true });
  await chapters(page).locator('tbody tr').nth(1).locator('td').first().click({ button: 'right' });
  await page.getByRole('button', { name: /^Forward translation/ }).click();
  const forward = page.getByRole('dialog', { name: 'Forward translation', exact: true });
  await forward.getByLabel('Frames to shift', { exact: true }).fill('20');
  await page.keyboard.press('Escape');
  await expect(forward).toHaveCount(0);
  await expect(time).toHaveValue('00:00:12.500');
});

test('M05 long expression results scroll while footer actions remain reachable', async ({ readyPage: page }) => {
  await page.setViewportSize({ width: 844, height: 390 });
  const text = Array.from({ length: 80 }, (_, i) => `CHAPTER${String(i + 1).padStart(2, '0')}=00:00:${String(i).padStart(2, '0')}.000\nCHAPTER${String(i + 1).padStart(2, '0')}NAME=Chapter ${i + 1}`).join('\n');
  // Use valid minute/second fields for all rows.
  await loadBytes(page, 'many.txt', Buffer.from(text.replace(/00:00:(\d+)\.000/g, (_, seconds: string) => `00:${String(Math.floor(Number(seconds) / 60)).padStart(2, '0')}:${String(Number(seconds) % 60).padStart(2, '0')}.000`)));
  await page.getByRole('button', { name: 'Expression editor', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  await dialog.getByLabel('Custom expression', { exact: true }).fill('t / 2');
  await expect(dialog.getByTestId('expression-preview')).toContainText('00:00:39.500');
  expect(await dialog.locator('.expression-chapter').count()).toBeGreaterThanOrEqual(79);
  const body = dialog.locator('.modal-body');
  expect(await body.evaluate(el => el.scrollHeight > el.clientHeight)).toBe(true);
  await body.evaluate(el => { el.scrollTop = el.scrollHeight; });
  await inViewport(dialog.getByRole('button', { name: 'Apply changes', exact: true }), 844, 390);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(chapters(page).locator('tbody tr')).toHaveCount(80);
});
