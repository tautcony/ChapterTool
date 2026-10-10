import { fileURLToPath } from 'node:url';
import { expect, test } from '../support/fixtures';
import { chapterName, chapters, downloadText, loadFixture } from '../support/chapter-workspace';

async function applyLanguage(page: import('@playwright/test').Page, language: string) {
  await page.getByTestId('settings-open').click();
  const dialog = page.getByRole('dialog');
  await dialog.locator('#settings-ui-language').selectOption(language);
  await dialog.getByTestId('settings-apply').click();
}

test('B08 settings save, persist across refresh, and discard canceled drafts', async ({ readyPage: page }) => {
  await page.getByTestId('settings-open').click();
  const dialog = page.getByRole('dialog', { name: 'Settings', exact: true });
  await dialog.locator('#settings-ui-language').selectOption('zh-CN');
  await dialog.getByTestId('settings-apply').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'zh-CN');
  await expect(page.getByRole('button', { name: '加载', exact: true })).toBeVisible();
  await page.reload();
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await expect(page.locator('html')).toHaveAttribute('lang', 'zh-CN');

  await page.getByTestId('settings-open').click();
  const localizedDialog = page.getByRole('dialog', { name: '设置', exact: true });
  await localizedDialog.locator('#settings-ui-language').selectOption('en-US');
  await localizedDialog.getByTestId('settings-cancel').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'zh-CN');
});

test('repeating frame preference stays in the settings draft until a successful save', async ({ readyPage: page }) => {
  await expect(page.getByTestId('settings-repeating-frame-decimals')).toHaveCount(0);
  await page.getByTestId('settings-open').click();
  let dialog = page.getByRole('dialog', { name: 'Settings', exact: true });
  await dialog.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  let repeating = dialog.getByTestId('settings-repeating-frame-decimals');
  await expect(repeating).toBeChecked();
  await expect(repeating).toBeDisabled();
  await dialog.locator('#settings-frame-display').selectOption('1');
  await expect(repeating).toBeDisabled();
  await dialog.locator('#settings-frame-display').selectOption('2');
  await expect(repeating).toBeEnabled();
  await repeating.uncheck();
  await dialog.getByTestId('settings-cancel').click();

  await page.getByTestId('settings-open').click();
  dialog = page.getByRole('dialog', { name: 'Settings', exact: true });
  await dialog.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  repeating = dialog.getByTestId('settings-repeating-frame-decimals');
  await expect(repeating).toBeChecked();
  await dialog.locator('#settings-frame-display').selectOption('2');
  await repeating.uncheck();
  await dialog.getByTestId('settings-apply').click();
  await expect(page.getByTestId('settings-repeating-frame-decimals')).toHaveCount(0);
  expect(await page.evaluate(() => JSON.parse(localStorage.getItem('chaptertool.wasm.settings') ?? '{}').application.showRepeatingFrameDecimals)).toBe(false);

  await page.reload();
  await expect(page.locator('#chaptertool-shell')).toHaveAttribute('data-app-ready', 'true');
  await page.getByTestId('settings-open').click();
  dialog = page.getByRole('dialog', { name: 'Settings', exact: true });
  await dialog.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  await dialog.locator('#settings-frame-display').selectOption('2');
  await expect(dialog.getByTestId('settings-repeating-frame-decimals')).not.toBeChecked();
  await dialog.getByTestId('settings-cancel').click();
  await expect(page.getByTestId('settings-repeating-frame-decimals')).toHaveCount(0);
});

test('B09 switches English, Chinese, and Japanese in the app and preserves Unicode chapter text', async ({ readyPage: page }) => {
  await loadFixture(page, 'unicode-ogm.txt');
  await expect(chapters(page).locator('tbody tr')).toHaveCount(2);
  await expect(await chapterName(page, 1)).toHaveValue('開幕');

  for (const [language, loadLabel, gridLabel] of [
    ['en-US', 'Load', 'Chapters'],
    ['zh-CN', '加载', '章节'],
    ['ja-JP', '読み込み', 'チャプター'],
  ]) {
    await applyLanguage(page, language);
    await expect(page.locator('html')).toHaveAttribute('lang', language);
    await expect(page.getByRole('button', { name: loadLabel, exact: true })).toBeVisible();
    await expect(page.getByRole('table', { name: gridLabel, exact: true })).toBeVisible();
  }
});

test('B05 exports representative formats through the visible controls', async ({ readyPage: page }, testInfo) => {
  const mpls = fileURLToPath(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_TwoChapter.mpls', import.meta.url));
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  await (await chooserPromise).setFiles(mpls);
  const format = page.getByLabel('Format');
  for (const [label, extension, marker] of [
    ['TXT', '.txt', 'CHAPTER01NAME=Chapter 01'],
    ['XML', '.xml', '<Chapters>'],
    ['QPFile', '.qpf', ' I'],
    ['TimeCodes', '.TimeCodes.txt', '00:00:00.000'],
  ]) {
    await test.step(`export ${label}`, async () => {
      await format.selectOption({ label });
      if (label === 'QPFile') {
        const fps = page.locator('select.fps-box');
        await fps.selectOption('2');
        await expect(fps).toHaveValue('2');
        await fps.dispatchEvent('contextmenu', { button: 2 });
        await page.locator('.context-menu').getByRole('button', { name: 'Change FPS', exact: true }).click();
        await page.getByRole('dialog', { name: 'Change FPS', exact: true }).getByRole('button', { name: 'Apply', exact: true }).click();
      }
      const output = await downloadText(page, testInfo);
      expect(output.filename.toLowerCase()).toContain(extension.toLowerCase());
      expect(output.content.toString('utf8')).toContain(marker);
    });
  }
});
