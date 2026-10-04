import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { expect, test } from '../support/fixtures';
import { chapterName, chapters, commit, loadFixture } from '../support/chapter-workspace';

test('B10 multi-selects rows, deletes only the selection, and restores it with Undo', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const rows = chapters(page).locator('tbody tr');
  await rows.nth(0).locator('td').first().click();
  await rows.nth(1).locator('td').first().click({ modifiers: ['Shift'] });
  await expect(chapters(page).locator('tbody tr.selected')).toHaveCount(2);
  await rows.nth(1).locator('td').first().dispatchEvent('contextmenu', { button: 2 });
  await expect(chapters(page).locator('tbody tr.selected')).toHaveCount(2);
  await page.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(rows).toHaveCount(0);
  await page.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(rows).toHaveCount(2);
});

test('B11 Ctrl+S downloads through the app and Ctrl+Z inside a field stays an edit operation', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const name = await chapterName(page, 1);
  await commit(name, 'Keyboard edit');
  await name.fill('Transient edit');
  await page.keyboard.press('Control+z');
  await name.press('Tab');
  await expect(name).toHaveValue('Keyboard edit');
  const downloadPromise = page.waitForEvent('download');
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Control+s');
  const download = await downloadPromise;
  expect(await download.failure()).toBeNull();
  await download.saveAs(testInfo.outputPath(download.suggestedFilename()));
});

test('B12 fixed frame rate and an expression update chapter projection', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.locator('select.fps-box').selectOption('2');
  await expect(chapters(page).locator('tbody tr').nth(1).locator('.frame-text')).toHaveText('300');
  const expression = page.getByLabel('Custom expression', { exact: true });
  await expression.fill('t / 2');
  const livePreview = page.getByTestId('expression-preview');
  await expect(livePreview).toContainText('00:00:06.250');
  const secondTime = chapters(page).getByRole('textbox', { name: 'Time 2', exact: true });
  await expect(secondTime).toHaveValue('00:00:12.500');
  await livePreview.getByRole('button', { name: 'Apply', exact: true }).click();
  await expect(secondTime).toHaveValue('00:00:06.250');
  const committed = await secondTime.inputValue();
  await expression.fill('t + (');
  await expect(page.getByTestId('expression-preview')).toContainText('unexpected symbol');
  await expect(page.getByTestId('expression-preview').getByRole('button', { name: 'Apply', exact: true })).toBeDisabled();
  await expect(secondTime).toHaveValue(committed);
});

test('B13 DOM file drop imports a file and oversized file input is rejected', async ({ readyPage: page }) => {
  await page.evaluateHandle(() => new DataTransfer()).then(async transfer => {
    await page.locator('#chaptertool-shell').dispatchEvent('drop', { dataTransfer: transfer });
    await transfer.dispose();
  });
  await expect(page.locator('.status-text')).toContainText(/empty/i);

  await page.evaluateHandle(() => {
    const transfer = new DataTransfer();
    transfer.items.add(new File([
      'CHAPTER01=00:00:00.000\nCHAPTER01NAME=Drop import\n',
    ], 'dropped.txt', { type: 'text/plain' }));
    return transfer;
  }).then(async transfer => {
    await page.locator('#chaptertool-shell').dispatchEvent('drop', { dataTransfer: transfer });
    await transfer.dispose();
  });
  await expect(chapters(page).locator('tbody tr')).toHaveCount(1);
  await expect(await chapterName(page, 1)).toHaveValue('Drop import');

  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  const chooser = await chooserPromise;
  const { mkdtemp, open, rm } = await import('node:fs/promises');
  const { tmpdir } = await import('node:os');
  const { join } = await import('node:path');
  const directory = await mkdtemp(join(tmpdir(), 'chaptertool-oversized-'));
  const largeFile = join(directory, 'oversized.txt');
  const handle = await open(largeFile, 'w');
  await handle.truncate(64 * 1024 * 1024 + 1);
  await handle.close();
  await chooser.setFiles(largeFile);
  await expect(page.locator('.status-text')).toContainText(/64 MB|too large/i);
  await expect(chapters(page).locator('tbody tr')).toHaveCount(1);
  await rm(directory, { recursive: true, force: true });

  const templateChooser = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: /Browse template/ }).click();
  await (await templateChooser).setFiles({ name: 'oversized-template.txt', mimeType: 'text/plain', buffer: Buffer.alloc(2 * 1024 * 1024 + 1) });
  await expect(page.locator('.status-text')).toContainText(/template|large|size/i);
});

test('B14 separate tabs keep chapter sessions independent and refresh clears only the current session', async ({ page, context, diagnostics }) => {
  void diagnostics;
  await page.goto('./');
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await loadFixture(page, 'minimal-ogm.txt');
  const second = await context.newPage();
  await second.goto('./');
  await expect(second.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  const secondChooser = second.waitForEvent('filechooser');
  await second.getByRole('button', { name: 'Load', exact: true }).click();
  await (await secondChooser).setFiles(fileURLToPath(new URL('../fixtures/unicode-ogm.txt', import.meta.url)));
  await expect(await chapterName(second, 1)).toHaveValue('開幕');
  await expect(await chapterName(page, 1)).toHaveValue('Opening');
  await page.reload();
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await expect(page.getByRole('button', { name: 'Save', exact: true })).toBeDisabled();
  await expect(await chapterName(second, 1)).toHaveValue('開幕');
});

test('B15 malformed settings are discarded', async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem('chaptertool.wasm.settings', '{broken'));
  await page.goto('./');
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await loadFixture(page, 'minimal-ogm.txt');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(2);
});

test('B15 storage denial does not block file imports', async ({ page }) => {
  await page.addInitScript(() => {
    const original = Storage.prototype.getItem;
    Storage.prototype.getItem = function (key: string) {
      if (key === 'chaptertool.wasm.settings') throw new DOMException('blocked', 'SecurityError');
      return original.call(this, key);
    };
  });
  await page.goto('./');
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await loadFixture(page, 'minimal-ogm.txt');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(2);
});

test('B17 UTF-8, UTF-16LE, and UTF-16BE downloads preserve BOM and Unicode bytes', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'unicode-ogm.txt');
  for (const [index, prefix] of [[0, [0xef, 0xbb, 0xbf]], [1, [0xff, 0xfe]], [2, [0xfe, 0xff]]] as const) {
    await page.getByTestId('settings-open').click();
    const dialog = page.getByRole('dialog', { name: 'Settings' });
    await dialog.getByRole('button', { name: 'Output preferences', exact: true }).click();
    const encoding = dialog.locator('#settings-encoding');
    await encoding.selectOption(String(index));
    const bom = dialog.getByTestId('settings-emit-bom');
    if (!(await bom.isChecked())) await bom.check();
    await dialog.getByTestId('settings-apply').click();
    const downloadPromise = page.waitForEvent('download', { timeout: 10_000 });
    await page.getByRole('button', { name: 'Save', exact: true }).click();
    const download = await downloadPromise;
    const path = testInfo.outputPath(`${index}-${download.suggestedFilename()}`);
    await download.saveAs(path);
    const bytes = await readFile(path);
    expect([...bytes.subarray(0, prefix.length)]).toEqual(prefix);
    expect(bytes.length).toBeGreaterThan(prefix.length);
    if (index === 0) expect(bytes.toString('utf8')).toContain('開幕');
    if (index === 1) expect(new TextDecoder('utf-16le').decode(bytes.subarray(2))).toContain('開幕');
    if (index === 2) expect(new TextDecoder('utf-16be').decode(bytes.subarray(2))).toContain('開幕');
  }
});
