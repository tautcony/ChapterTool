import { expect, test } from '../support/fixtures';
import { chapters, downloadText, loadFixture } from '../support/chapter-workspace';

const viewports = [
  { name: 'default', width: 1280, height: 800 },
  { name: 'wide', width: 1920, height: 1080 },
  { name: 'narrow', width: 390, height: 844 },
];

test('B18 layout supports the core workflow at default, wide, and narrow sizes', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  for (const viewport of viewports) {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    const grid = chapters(page);
    await expect(grid).toBeVisible();
    await expect(page.getByLabel('Format')).toBeVisible();
    await expect(page.getByRole('status')).toBeVisible();
    await page.getByTestId('settings-open').click();
    const settings = page.getByRole('dialog', { name: 'Settings' });
    await expect(settings).toBeVisible();
    const settingsBounds = await settings.boundingBox();
    const viewportSize = page.viewportSize();
    expect(settingsBounds).not.toBeNull();
    expect(settingsBounds!.x).toBeGreaterThanOrEqual(0);
    expect(settingsBounds!.y).toBeGreaterThanOrEqual(0);
    expect(settingsBounds!.x + settingsBounds!.width).toBeLessThanOrEqual(viewportSize!.width);
    expect(settingsBounds!.y + settingsBounds!.height).toBeLessThanOrEqual(viewportSize!.height);
    await expect(settings.getByTestId('settings-cancel')).toBeInViewport();
    await settings.getByTestId('settings-cancel').click();
    await page.getByRole('button', { name: 'Preview', exact: true }).click();
    const preview = page.getByRole('dialog', { name: 'Preview' });
    await expect(preview).toBeVisible();
    const previewBounds = await preview.boundingBox();
    expect(previewBounds).not.toBeNull();
    expect(previewBounds!.x + previewBounds!.width).toBeLessThanOrEqual(viewportSize!.width);
    expect(previewBounds!.y + previewBounds!.height).toBeLessThanOrEqual(viewportSize!.height);
    await preview.getByRole('button', { name: 'Close', exact: true }).click();
    const downloaded = await downloadText(page, testInfo);
    expect(downloaded.filename).toMatch(/\.txt$/i);

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    expect(overflow, `Unexpected outer horizontal overflow at ${viewport.name}`).toBe(false);
    await page.evaluate(() => document.fonts.ready);
    await expect(page).toHaveScreenshot(`wasm-${viewport.name}.png`, { fullPage: true, animations: 'disabled' });
  }
});
