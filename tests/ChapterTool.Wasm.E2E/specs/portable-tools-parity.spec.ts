import { expect, test } from '../support/fixtures';
import { loadBytes, loadFixture } from '../support/chapter-workspace';

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

test('portable export options validate drafts, cancel cleanly, and retain a failed apply', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const format = page.locator('#saveFormat');
  const xmlOption = format.locator('option').filter({ hasText: /^XML$/ }).first();
  await format.selectOption(await xmlOption.getAttribute('value') ?? '');

  const open = async () => {
    await page.getByRole('button', { name: 'Advanced export options', exact: true }).click();
    return page.getByRole('dialog', { name: 'Advanced export options', exact: true });
  };
  const dialog = await open();
  const language = dialog.locator('#xmlLang');
  const encoding = dialog.locator('#export-encoding');
  const bom = dialog.locator('#export-bom');
  const original = { language: await language.inputValue(), encoding: await encoding.inputValue(), bom: await bom.isChecked() };
  const alternateLanguage = await language.locator('option').nth(1).getAttribute('value');
  await language.selectOption(alternateLanguage ?? '');
  await encoding.selectOption('1');
  await bom.setChecked(!original.bom);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();

  const reopened = await open();
  await expect(reopened.locator('#xmlLang')).toHaveValue(original.language);
  await expect(reopened.locator('#export-encoding')).toHaveValue(original.encoding);
  await expect(reopened.locator('#export-bom')).toHaveJSProperty('checked', original.bom);

  await reopened.locator('#xmlLang').selectOption(alternateLanguage ?? '');
  await reopened.locator('#export-encoding').selectOption('1');
  await reopened.locator('#export-bom').setChecked(!original.bom);
  await page.evaluate(() => {
    Storage.prototype.setItem = () => { throw new DOMException('Storage unavailable', 'QuotaExceededError'); };
  });
  await reopened.getByRole('button', { name: 'Apply', exact: true }).click();
  await expect(reopened.getByRole('alert')).toBeVisible();
  await expect(reopened.locator('#xmlLang')).toHaveValue(alternateLanguage ?? '');
  await expect(reopened.locator('#export-encoding')).toHaveValue('1');
  await expect(reopened.locator('#export-bom')).toHaveJSProperty('checked', !original.bom);

  await reopened.getByRole('button', { name: 'Cancel', exact: true }).click();
  const afterFailure = await open();
  await expect(afterFailure.locator('#xmlLang')).toHaveValue(original.language);
  await expect(afterFailure.locator('#export-encoding')).toHaveValue(original.encoding);
  await expect(afterFailure.locator('#export-bom')).toHaveJSProperty('checked', original.bom);
});

test('content operation review shows typed candidate values and cancel preserves content', async ({ readyPage: page }) => {
  const { readFile } = await import('node:fs/promises');
  const bytes = await readFile(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_Hidan_no_Aria_AA.mpls', import.meta.url));
  await loadBytes(page, '00001_Hidan_no_Aria_AA.mpls', bytes);
  await expect(page.locator('.chapter-grid tbody tr')).toHaveCount(6);
  const time = page.getByLabel('Time 2', { exact: true });
  const originalTime = await time.inputValue();
  await page.locator('.fps-box').selectOption('3');

  const open = async () => {
    await page.locator('.fps-box').dispatchEvent('contextmenu', { button: 2 });
    await page.locator('.context-menu').getByRole('button', { name: 'Change FPS', exact: true }).click();
    return page.getByRole('dialog', { name: 'Change FPS', exact: true });
  };
  const dialog = await open();
  const source = (await dialog.getByTestId('fps-source').innerText()).match(/^(\d+)\/(\d+) fps$/);
  const target = (await dialog.getByTestId('fps-target').innerText()).match(/^(\d+)\/(\d+) fps$/);
  expect(source).not.toBeNull();
  expect(target).not.toBeNull();
  expect(Number(source![1]) / Number(source![2])).toBeCloseTo(24000 / 1001, 4);
  expect(Number(target![1]) / Number(target![2])).toBe(25);
  await expect(dialog.locator('.content-review-row').first().getByTestId('review-before-time')).not.toBeEmpty();
  await expect(dialog.locator('.content-review-row').first().getByTestId('review-candidate-time')).not.toBeEmpty();
  await expect(dialog.locator('.content-review-row').first().getByTestId('review-before-frames')).not.toBeEmpty();
  await expect(dialog.locator('.content-review-row').first().getByTestId('review-candidate-frames')).not.toBeEmpty();
  await expect(time).toHaveValue(originalTime);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(time).toHaveValue(originalTime);

  const applyDialog = await open();
  await applyDialog.getByRole('button', { name: 'Apply', exact: true }).click();
  await expect(time).not.toHaveValue(originalTime);
  const changedTime = await time.inputValue();
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Control+z');
  await expect(time).toHaveValue(originalTime);
  await page.keyboard.press('Control+Shift+z');
  await expect(time).toHaveValue(changedTime);
});

test('portable log inspector keeps the selected browser record through filtering', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Log', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Activity log', exact: true });
  const first = dialog.locator('.log-entry').first();
  const select = first.locator('.log-select');
  await select.click();
  await expect(select).toHaveAttribute('aria-pressed', 'true');
  await first.getByRole('button', { name: 'Details', exact: true }).click();
  const inspector = dialog.locator('.log-inspector');
  await expect(inspector).toBeVisible();
  const message = await inspector.locator('p').nth(1).innerText();

  await dialog.getByLabel('Search logs', { exact: true }).fill('no-browser-log-matches-this');
  await expect(dialog.locator('.log-entry')).toHaveCount(0);
  await expect(inspector).toContainText(message);
  await dialog.getByRole('button', { name: 'Reset filters', exact: true }).click();
  await expect(dialog.locator('.log-entry')).not.toHaveCount(0);
  await dialog.getByRole('button', { name: 'Back to logs', exact: true }).click();

  const search = dialog.getByLabel('Search logs', { exact: true });
  await search.fill((await dialog.locator('.log-entry').first().locator('time').innerText()).slice(0, 5));
  const visibleCount = await dialog.locator('.log-entry').count();
  const downloaded = page.waitForEvent('download');
  await dialog.getByText('Export logs', { exact: true }).click();
  await dialog.getByRole('button', { name: 'JSON', exact: true }).click();
  const download = await downloaded;
  const path = testInfo.outputPath('filtered-browser-logs.json');
  await download.saveAs(path);
  const records = JSON.parse(await (await import('node:fs/promises')).readFile(path, 'utf8')) as unknown[];
  expect(records).toHaveLength(visibleCount);
  await expect(dialog.locator('.log-entry')).toHaveCount(visibleCount);
});
