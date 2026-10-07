import { expect, test } from '../support/fixtures';
import { chapters, commit, downloadText, loadBytes, loadFixture } from '../support/chapter-workspace';
import { readFile } from 'node:fs/promises';

declare global {
  interface Window {
    chapterToolWasm: {
      setLocalStorage: (key: string, value: string) => void;
      copyText: (text: string) => void;
      downloadText: (name: string, content: string, encoding: string, bom: boolean) => void;
      applyAppearance: (theme: string, language: string, font: string, monospace: string) => void;
    };
  }
}

test('P01 naming review commits once and settings cannot replay it', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const original = await chapters(page).getByLabel('Name 1', { exact: true }).inputValue();
  await page.getByRole('button', { name: 'Naming and numbering', exact: true }).click();
  const naming = page.getByRole('dialog', { name: 'Naming and numbering', exact: true });
  await naming.getByLabel('Chapter name', { exact: true }).selectOption('1');
  await naming.locator('#content-number').fill('2');
  await naming.getByRole('button', { name: 'Preview', exact: true }).click();
  await expect(naming.getByTestId('content-review')).toContainText('Chapter 01');
  await expect(chapters(page).getByLabel('Name 1', { exact: true })).toHaveValue(original);
  await naming.getByRole('button', { name: 'Apply', exact: true }).click();
  await expect(naming).toHaveCount(0);
  await commit(chapters(page).getByLabel('Name 1', { exact: true }), 'Manual 名称');
  const before = await downloadText(page, testInfo);
  await page.getByTestId('settings-open').click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await settings.getByRole('button', { name: 'Output preferences', exact: true }).click();
  await expect(settings.locator('#settings-xml-language')).toContainText('ja — Japanese');
  await expect(settings.locator('#settings-xml-language')).toContainText('zh — Chinese');
  await settings.locator('#settings-save-format').selectOption('1');
  await settings.getByRole('button', { name: 'Appearance', exact: true }).click();
  await settings.locator('#settings-theme').selectOption('ayu-dark');
  await settings.getByTestId('settings-apply').click();
  await expect(chapters(page).getByLabel('Name 1', { exact: true })).toHaveValue('Manual 名称');
  const after = await downloadText(page, testInfo);
  expect(after.content.equals(before.content)).toBe(true);
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  await expect(page.getByRole('dialog').locator('.history-entry')).toHaveCount(3);
});

for (const fixture of ['00001_fch.mpls', '00001_Hidan_no_Aria_AA.mpls']) {
test(`P10 FPS review is read-only and applies one exact conversion with Undo (${fixture})`, async ({ readyPage: page }) => {
  const bytes = await readFile(new URL(`../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/${fixture}`, import.meta.url));
  await loadBytes(page, fixture, bytes);
  const time = chapters(page).getByLabel('Time 2', { exact: true });
  const before = await time.inputValue();
  await page.locator('select.fps-box').selectOption('3');
  const open = async () => {
    await page.locator('select.fps-box').dispatchEvent('contextmenu', { button: 2 });
    await page.getByRole('button', { name: 'Change FPS', exact: true }).click();
  };
  await open();
  const dialog = page.getByRole('dialog', { name: 'Change FPS', exact: true });
  await expect(dialog).toContainText('Source FPS');
  await expect(dialog).toContainText('Target FPS');
  const sourceRate = (await dialog.getByTestId('fps-source').innerText()).match(/^(\d+)\/(\d+) fps$/);
  expect(sourceRate).not.toBeNull();
  expect(Number(sourceRate![1]) / Number(sourceRate![2])).toBeCloseTo(24000 / 1001, 6);
  await expect(dialog.getByTestId('fps-target')).toContainText('25/1');
  await expect(time).toHaveValue(before);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(time).toHaveValue(before);
  await open();
  await dialog.getByRole('button', { name: 'Apply', exact: true }).click();
  await expect(time).not.toHaveValue(before);
  const after = await time.inputValue();
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Meta+z');
  await expect(time).toHaveValue(before);
  await page.keyboard.press('Control+Shift+z');
  await expect(time).toHaveValue(after);
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  await expect(page.getByRole('dialog').locator('.history-entry')).toHaveCount(2);
});
}

test('P11 settings validate the whole draft and rollback runtime failure', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const prior = await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--window-bg').trim().toLowerCase());
  await page.getByTestId('settings-open').click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await settings.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  await settings.locator('#settings-frame-display').selectOption('1');
  for (const value of ['0', '1.5', '7', '']) {
    await settings.locator('#settings-frame-decimals').fill(value);
    await expect(settings.getByTestId('settings-apply')).toBeDisabled();
  }
  await settings.locator('#settings-frame-decimals').fill('4');
  await settings.getByRole('button', { name: 'Appearance', exact: true }).click();
  await settings.locator('#settings-theme').selectOption('ayu-dark');
  await page.evaluate(() => {
    const apply = window.chapterToolWasm.applyAppearance;
    let fail = true;
    window.chapterToolWasm.applyAppearance = (...args) => {
      if (fail) { fail = false; throw new Error('Runtime unavailable'); }
      apply(...args);
    };
  });
  await settings.getByTestId('settings-apply').click();
  await expect(settings.getByRole('alert')).toBeVisible();
  await expect(settings.locator('#settings-theme')).toHaveValue('ayu-dark');
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--window-bg').trim().toLowerCase())).toBe(prior);
  await settings.getByTestId('settings-cancel').click();
  await page.getByTestId('settings-open').click();
  await settings.getByRole('button', { name: 'Appearance', exact: true }).click();
  await expect(settings.locator('#settings-theme')).toHaveValue('avalonia-default');
});

test('P12 row command prerequisites and clip shortcuts preserve bounds', async ({ readyPage: page }) => {
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Insert');
  await page.keyboard.press('Delete');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(0);
  const bytes = await readFile(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_Hidan_no_Aria_AA.mpls', import.meta.url));
  await loadBytes(page, '00001_Hidan_no_Aria_AA.mpls', bytes);
  const clip = page.locator('select.clip-box');
  const first = await clip.inputValue();
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('PageUp');
  await expect(clip).toHaveValue(first);
  await page.keyboard.press('PageDown');
  await expect(clip).not.toHaveValue(first);
  const last = await clip.inputValue();
  await page.keyboard.press('PageDown');
  await expect(clip).toHaveValue(last);
  await chapters(page).locator('tbody tr').first().locator('td').first().click();
  const count = await chapters(page).locator('tbody tr').count();
  await page.keyboard.press('Delete');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(count - 1);
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Meta+z');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(count);
});

test('P02 cells commit once, reject invalid input, cancel, and retain text undo', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.locator('.fps-box').selectOption('1');
  const frames = chapters(page).getByLabel('Frames 2', { exact: true });
  const original = await frames.inputValue();
  await frames.fill('240');
  await frames.press('Escape');
  await expect(frames).toHaveValue(original);
  await frames.fill('240');
  await frames.press('Enter');
  await frames.press('Tab');
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  await expect(page.getByRole('dialog').locator('.history-entry')).toHaveCount(2);
  await page.keyboard.press('Escape');
  const time = chapters(page).getByLabel('Time 2', { exact: true });
  const committed = await time.inputValue();
  await time.fill('invalid');
  await time.press('Enter');
  await expect(time).toHaveAttribute('aria-invalid', 'true');
  await time.press('Escape');
  await expect(time).toHaveValue(committed);
  await time.fill('draft text');
  await time.press('Control+z');
  await time.press('Escape');
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Control+z');
  await expect(frames).toHaveValue(original);
});

test('P03 shortcuts save, conflict, fixed row commands, and settings discard', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByTestId('settings-open').click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await settings.getByRole('button', { name: 'Shortcuts', exact: true }).click();
  await settings.locator('#shortcut-save').press('Control+o');
  await expect(settings.getByTestId('settings-apply')).toBeDisabled();
  await settings.locator('#shortcut-save').press('Control+Shift+s');
  await settings.getByTestId('settings-apply').click();
  await page.locator('#chaptertool-shell').focus();
  const downloadPromise = page.waitForEvent('download');
  await page.keyboard.press('Control+Shift+s');
  const download = await downloadPromise;
  await download.saveAs(testInfo.outputPath('custom-shortcut.txt'));
  await chapters(page).locator('tbody tr').first().locator('td').first().click();
  await page.keyboard.press('Insert');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(3);
  await page.getByTestId('settings-open').click();
  await settings.locator('#settings-ui-language').selectOption('ja-JP');
  await settings.locator('.modal-close').click();
  await expect(settings.getByRole('alertdialog')).toBeVisible();
  await settings.getByRole('button', { name: 'Keep editing', exact: true }).click();
  await expect(settings.locator('#settings-ui-language')).toHaveValue('ja-JP');
  await settings.locator('.modal-close').click();
  await settings.getByRole('button', { name: 'Discard', exact: true }).click();
  await page.getByTestId('settings-open').click();
  await expect(settings.locator('#settings-ui-language')).toHaveValue('en-US');
  await settings.getByRole('button', { name: 'Shortcuts', exact: true }).click();
  await expect(settings.locator('#shortcut-save')).toHaveValue('Ctrl+Shift+S');
});

test('P04 Lua files, highlighting, completion, diagnostics, and cancellation', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  await dialog.locator('#expression-script').setInputFiles({ name: 'scale.lua', mimeType: 'text/plain', buffer: Buffer.from('local factor = 2\nreturn t / factor') });
  const editor = dialog.getByLabel('Custom expression', { exact: true });
  await expect(editor).toHaveValue('local factor = 2\nreturn t / factor');
  await expect(dialog.locator('.lua-token-keyword').first()).toBeVisible();
  await editor.fill('math.fl');
  await editor.press('Control+Space');
  await expect(dialog.getByRole('listbox')).toBeVisible();
  await editor.press('Escape');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('listbox')).toHaveCount(0);
  await editor.press('Control+Space');
  await editor.press('Enter');
  await expect(editor).toHaveValue('math.floor()');
  await editor.fill('return missing_function()');
  await expect(dialog.locator('.lua-diagnostics')).toContainText('Position');
  await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeDisabled();
  await editor.press('Escape');
  await page.getByRole('button', { name: 'Expression', exact: true }).click();
  await expect(editor).toHaveValue('t');
  await expect(dialog).not.toContainText('scale.lua');
});

test('P05 filtered log inspection and export preserve membership', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Log', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Activity log', exact: true });
  await expect(dialog.locator('.log-inspector')).toHaveCount(0);
  await dialog.locator('.log-entry').first().click();
  await expect(dialog.locator('.log-inspector')).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Details', exact: true }).first().click();
  await expect(dialog.locator('.log-inspector')).toBeVisible();
  await dialog.getByRole('button', { name: 'Back to logs', exact: true }).click();
  await dialog.getByLabel('Search logs', { exact: true }).fill('Loaded');
  const count = await dialog.locator('.log-entry').count();
  expect(count).toBeGreaterThan(0);
  await dialog.getByText('Export logs', { exact: true }).click();
  const downloading = page.waitForEvent('download');
  await dialog.getByRole('button', { name: 'JSON', exact: true }).click();
  const download = await downloading;
  const path = testInfo.outputPath('logs.json');
  await download.saveAs(path);
  const { readFile } = await import('node:fs/promises');
  expect(JSON.parse(await readFile(path, 'utf8'))).toHaveLength(count);
  await expect(dialog.locator('.log-entry')).toHaveCount(count);
});

test('P06 all rows and changed tools remain reachable at supported sizes', async ({ readyPage: page }, testInfo) => {
  test.setTimeout(180_000);
  const content = Array.from({ length: 1000 }, (_, i) => `CHAPTER${String(i + 1).padStart(2, '0')}=00:${String(Math.floor(i / 60)).padStart(2, '0')}:${String(i % 60).padStart(2, '0')}.000\nCHAPTER${String(i + 1).padStart(2, '0')}NAME=Chapter ${i + 1}`).join('\n');
  await loadBytes(page, 'large.txt', Buffer.from(content));
  const last = chapters(page).getByLabel('Name 1000', { exact: true });
  await last.scrollIntoViewIfNeeded();
  await commit(last, 'Last edited');
  expect((await downloadText(page, testInfo)).content.toString('utf8')).toContain('Last edited');
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  await expect(page.getByRole('dialog').locator('.history-entry')).toHaveCount(2);
  await page.keyboard.press('Escape');
  await page.reload();
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await loadFixture(page, 'minimal-ogm.txt');
  const sizes = [{ width: 1280, height: 800 }, { width: 1920, height: 1080 }, { width: 390, height: 844 }, { width: 390, height: 640 }, { width: 844, height: 390 }];
  for (const size of sizes) {
    await page.setViewportSize(size);
    for (const title of ['Naming and numbering', 'Expression', 'Settings', 'Log']) {
      const opener = title === 'Settings' ? page.getByTestId('settings-open') : page.getByRole('button', { name: title, exact: true });
      await opener.click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toBeVisible();
      const box = await dialog.boundingBox();
      expect(box!.x).toBeGreaterThanOrEqual(0);
      expect(box!.x + box!.width).toBeLessThanOrEqual(size.width + 1);
      expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
      await page.screenshot({ path: testInfo.outputPath(`${title}-${size.width}-${size.height}.png`) });
      await dialog.locator('.modal-close').click();
      await expect(opener).toBeFocused();
    }
  }
});

test('P07 storage failure preserves settings draft and active appearance', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const prior = await page.evaluate(() => document.documentElement.style.getPropertyValue('--window-bg'));
  await page.getByTestId('settings-open').click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await settings.getByRole('button', { name: 'Appearance', exact: true }).click();
  await settings.locator('#settings-theme').selectOption('ayu-dark');
  await page.evaluate(() => { window.chapterToolWasm.setLocalStorage = () => { throw new DOMException('Unavailable', 'QuotaExceededError'); }; });
  await settings.getByTestId('settings-apply').click();
  await expect(settings.getByRole('alert')).toBeVisible();
  await expect(settings.locator('#settings-theme')).toHaveValue('ayu-dark');
  expect(await page.evaluate(() => document.documentElement.style.getPropertyValue('--window-bg'))).toBe(prior);
  await settings.getByTestId('settings-cancel').click();
  await page.getByTestId('settings-open').click();
  await settings.getByRole('button', { name: 'Appearance', exact: true }).click();
  await expect(settings.locator('#settings-theme')).toHaveValue('avalonia-default');
  await settings.getByRole('button', { name: 'Restore defaults', exact: true }).click();
  await settings.getByTestId('settings-cancel').click();
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  await expect(page.getByRole('dialog').locator('.history-entry')).toHaveCount(1);
});

test('P08 numbering rejects invalid drafts and templates remain uncommitted until Apply', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const original = await chapters(page).getByLabel('Name 1', { exact: true }).inputValue();
  await page.getByRole('button', { name: 'Naming and numbering', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Naming and numbering', exact: true });
  for (const value of ['-1', '1.5', '1001']) {
    await dialog.locator('#content-number').fill(value);
    await dialog.getByRole('button', { name: 'Preview', exact: true }).click();
    await expect(dialog.getByRole('alert')).toBeVisible();
    await expect(dialog.getByRole('button', { name: 'Apply', exact: true })).toBeDisabled();
  }
  await dialog.locator('#content-number').fill('0');
  await dialog.locator('#content-template').setInputFiles({ name: 'names.txt', mimeType: 'text/plain', buffer: Buffer.from('章节一\n第二章') });
  await dialog.getByRole('button', { name: 'Preview', exact: true }).click();
  await expect(dialog.getByTestId('content-review')).toContainText('章节一');
  await expect(chapters(page).getByLabel('Name 1', { exact: true })).toHaveValue(original);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(chapters(page).getByLabel('Name 1', { exact: true })).toHaveValue(original);
});

test('P09 log copy and export errors recover without stale inspection', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.setViewportSize({ width: 390, height: 640 });
  await page.getByRole('button', { name: 'Log', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Activity log', exact: true });
  await dialog.getByRole('button', { name: 'Details', exact: true }).first().click();
  await expect(dialog.locator('#log-back')).toBeFocused();
  await page.evaluate(() => { window.chapterToolWasm.copyText = () => { throw new Error('Denied'); }; window.chapterToolWasm.downloadText = () => { throw new Error('Denied'); }; });
  await dialog.getByRole('button', { name: 'Copy', exact: true }).click();
  await expect(dialog.getByRole('status').filter({ hasText: 'failed' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Back to logs', exact: true }).click();
  await expect(dialog.locator('.log-entry').first()).toBeFocused();
  await dialog.getByText('Export logs', { exact: true }).click();
  await dialog.getByRole('button', { name: 'CSV', exact: true }).click();
  await expect(dialog.getByRole('status').filter({ hasText: 'failed' })).toBeVisible();
  await dialog.getByRole('button', { name: 'Clear', exact: true }).click();
  await expect(dialog.locator('.log-entry')).toHaveCount(0);
  await expect(dialog.locator('.log-inspector')).toHaveCount(0);
});
