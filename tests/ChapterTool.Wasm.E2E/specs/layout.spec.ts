import { expect, test } from '../support/fixtures';
import { chapters, downloadText, loadBytes, loadFixture } from '../support/chapter-workspace';

const viewports = [
  { name: 'default', width: 1280, height: 800 },
  { name: 'wide', width: 1920, height: 1080 },
  { name: 'narrow', width: 390, height: 844 },
  { name: 'narrow-short', width: 390, height: 640 },
  { name: 'landscape', width: 844, height: 390 },
];

test('B18 idle and dialog visual states at desktop and mobile sizes', async ({ readyPage: page }, testInfo) => {
  test.setTimeout(180_000);
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
    await preview.getByRole('button', { name: 'Close', exact: true }).first().click();
    const downloaded = await downloadText(page, testInfo);
    expect(downloaded.filename).toMatch(/\.txt$/i);

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    expect(overflow, `Unexpected outer horizontal overflow at ${viewport.name}`).toBe(false);
    await page.evaluate(() => document.fonts.ready);
    await expect(page).toHaveScreenshot(`wasm-${viewport.name}.png`, { fullPage: true, animations: 'disabled' });
    for (const [title, state] of [['Edit history', 'history'], ['Expression', 'expression'], ['Advanced export options', 'export-options'], ['Settings', 'settings']] as const) {
      await page.getByRole('button', { name: title, exact: true }).click();
      const dialog = page.getByRole('dialog', { name: title, exact: true });
      await expect(dialog).toBeVisible();
      if (title === 'Expression') {
        await dialog.getByLabel('Custom expression', { exact: true }).fill('t / 2');
        await expect(dialog.getByTestId('expression-preview')).toContainText('00:00:06.250');
      }
      await expect(page).toHaveScreenshot(`wasm-${viewport.name}-${state}.png`, { animations: 'disabled' });
      if (title === 'Expression') {
        await dialog.getByLabel('Custom expression', { exact: true }).fill('return missing_function()');
        await expect(dialog.getByRole('alert')).toBeVisible();
        await expect(dialog.getByTestId('expression-preview')).toContainText('Lua');
        await expect(dialog.getByRole('button', { name: 'Apply changes', exact: true })).toBeDisabled();
        await expect(page).toHaveScreenshot(`wasm-${viewport.name}-expression-error.png`, { animations: 'disabled' });
      }
      await dialog.getByRole('button', { name: 'Close', exact: true }).first().click();
      await expect(dialog).toHaveCount(0);
    }
  }
});

test('long expression differences keep the footer visible', async ({ readyPage: page }) => {
  const text = Array.from({ length: 80 }, (_, i) => {
    const number = String(i + 1).padStart(2, '0');
    const time = `00:${String(Math.floor(i / 60)).padStart(2, '0')}:${String(i % 60).padStart(2, '0')}.000`;
    return `CHAPTER${number}=${time}\nCHAPTER${number}NAME=Chapter ${i + 1}`;
  }).join('\n');
  await loadBytes(page, 'many.txt', Buffer.from(text));
  for (const viewport of viewports.filter(size => ['narrow-short', 'landscape'].includes(size.name))) {
    await page.setViewportSize(viewport);
    await page.getByRole('button', { name: 'Expression', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
    await dialog.getByLabel('Custom expression', { exact: true }).fill('t / 2');
    await expect(dialog.getByTestId('expression-preview')).toContainText('00:00:39.500');
    await dialog.locator('.modal-body').evaluate(el => { el.scrollTop = el.scrollHeight; });
    await expect(dialog.getByRole('button', { name: 'Cancel', exact: true })).toBeInViewport();
    await expect(page).toHaveScreenshot(`wasm-${viewport.name}-expression-long.png`, { animations: 'disabled' });
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  }
});
