import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test } from '../support/fixtures';
import { loadFixture } from '../support/chapter-workspace';

test('captures expression review at default, wide, and narrow viewports', async ({ readyPage: page }, testInfo) => {
  test.skip(testInfo.project.name !== 'chromium', 'Capture one browser set for visual review.');
  const directory = resolve(process.cwd(), '../../artifacts/expression-preview');
  if (process.env.E2E_CAPTURE_REVIEW) await mkdir(directory, { recursive: true });
  await loadFixture(page, 'minimal-ogm.txt');
  await page.getByRole('button', { name: 'Expression editor', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  await dialog.getByLabel('Custom expression', { exact: true }).fill('t / 2');
  await expect(dialog.getByTestId('expression-preview')).toContainText('00:00:06.250');

  for (const [name, viewport] of [
    ['web-default.png', { width: 1280, height: 800 }],
    ['web-wide.png', { width: 1920, height: 1080 }],
    ['web-narrow.png', { width: 390, height: 844 }],
  ] as const) {
    await page.setViewportSize(viewport);
    const value = dialog.locator('.expression-value').filter({ hasText: '00:00:06.250' });
    await expect(value).toBeInViewport();
    const valueBox = await value.boundingBox();
    const footerBox = await dialog.locator('.expression-footer').boundingBox();
    expect(valueBox).not.toBeNull();
    expect(footerBox).not.toBeNull();
    expect(valueBox!.y + valueBox!.height).toBeLessThanOrEqual(footerBox!.y);
    if (process.env.E2E_CAPTURE_REVIEW) await page.screenshot({ path: resolve(directory, name), fullPage: false });
  }

  await dialog.getByText('Other changes', { exact: true }).click();
  await expect(dialog.locator('.expression-properties')).toContainText('Frame rate');
  await expect(dialog.locator('.expression-properties')).toContainText('24/1 fps');

  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();

  await page.getByTestId('settings-open').click();
  const settings = page.getByRole('dialog');
  await settings.locator('#settings-ui-language').selectOption('zh-CN');
  await settings.getByTestId('settings-apply').click();
  await page.getByRole('button', { name: '表达式', exact: true }).click();
  const chineseDialog = page.getByRole('dialog', { name: '表达式', exact: true });
  await chineseDialog.getByRole('textbox', { name: '表达式', exact: true }).fill('t / 2');
  await expect(chineseDialog.getByTestId('expression-preview-summary')).toHaveText('时间修改 1/2 个章节');
  for (const [name, viewport] of [
    ['web-default-zh.png', { width: 1280, height: 800 }],
    ['web-wide-zh.png', { width: 1920, height: 1080 }],
    ['web-narrow-zh.png', { width: 390, height: 844 }],
  ] as const) {
    await page.setViewportSize(viewport);
    const value = chineseDialog.locator('.expression-value').filter({ hasText: '00:00:06.250' });
    await expect(value).toBeInViewport();
    const valueBox = await value.boundingBox();
    const footerBox = await chineseDialog.locator('.expression-footer').boundingBox();
    expect(valueBox!.y + valueBox!.height).toBeLessThanOrEqual(footerBox!.y);
    if (process.env.E2E_CAPTURE_REVIEW) await page.screenshot({ path: resolve(directory, name), fullPage: false });
  }
});
