import { mkdir } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test } from '../support/fixtures';
import { chapters, loadBytes } from '../support/chapter-workspace';

test('Web frame cells render exact repetends while retaining numeric editors', async ({ readyPage: page }) => {
  await loadBytes(page, 'repeating-frames.txt', Buffer.from(
    'CHAPTER01=00:00:00.000\nCHAPTER01NAME=Opening\nCHAPTER02=00:00:52.094\nCHAPTER02NAME=Later\n',
  ));

  await page.getByTestId('settings-open').click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await settings.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  await settings.locator('#settings-frame-display').selectOption('2');
  await expect(settings.getByTestId('settings-repeating-frame-decimals')).toBeChecked();
  await settings.getByTestId('settings-apply').click();

  const rate = page.locator('select.fps-box');
  const roundFrames = page.getByLabel('Round frames', { exact: true });
  await expect(roundFrames).not.toBeChecked();
  const frameCell = chapters(page).locator('tbody tr').nth(1).locator('.col-frames');
  await expect(frameCell.locator('.frame-repetend')).toHaveText('006993');
  await expect(frameCell.locator('.frame-repetend')).toHaveCSS('text-decoration-line', 'overline');
  await expect(frameCell.locator('input')).toHaveValue('1249.006993006993006993006993');

  await page.getByTestId('settings-open').click();
  const roundingSettings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await roundingSettings.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  await roundingSettings.locator('#settings-frame-display').selectOption('0');
  await roundingSettings.getByTestId('settings-apply').click();
  await expect(roundFrames).toBeChecked();
  await expect(frameCell.locator('.frame-repetend')).toHaveCount(0);
  await expect(frameCell.locator('.frame-display')).toHaveText('1249');

  await page.getByTestId('settings-open').click();
  const decimalSettings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await decimalSettings.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  await decimalSettings.locator('#settings-frame-display').selectOption('1');
  await decimalSettings.getByTestId('settings-apply').click();
  await expect(roundFrames).not.toBeChecked();
  await expect(frameCell.locator('.frame-repetend')).toHaveCount(0);

  await page.getByTestId('settings-open').click();
  const fullPrecisionSettings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await fullPrecisionSettings.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  await fullPrecisionSettings.locator('#settings-frame-display').selectOption('2');
  await fullPrecisionSettings.getByTestId('settings-apply').click();
  await expect(roundFrames).not.toBeChecked();
  await expect(frameCell.locator('.frame-repetend')).toHaveText('006993');

  await rate.selectOption({ label: '24000 / 1001' });

  await page.locator('#main-expression-editor').fill('t + 0.0002');
  const expressionRow = chapters(page).locator('tbody tr').nth(1);
  await expect(expressionRow.locator('.frame-expression-preview .frame-repetend').nth(0)).toHaveText('006993');
  await expect(expressionRow.locator('.frame-expression-preview .frame-repetend').nth(1)).toHaveText('117882');
  const timeCell = expressionRow.locator('.col-time');
  await expect(timeCell).toHaveCSS('white-space', 'nowrap');
  await expect(timeCell.locator('.time-expression-preview')).toHaveCSS('white-space', 'nowrap');
  await expect.poll(async () => Number.parseFloat(await timeCell.evaluate(element => getComputedStyle(element).width))).toBeGreaterThanOrEqual(288);

  const screenshotDirectory = resolve(process.cwd(), '../../artifacts/compact-repeating-frame-decimals');
  await mkdir(screenshotDirectory, { recursive: true });
  for (const [name, viewport] of [
    ['web-default.png', { width: 1280, height: 800 }],
    ['web-wide.png', { width: 1920, height: 1080 }],
    ['web-narrow.png', { width: 390, height: 844 }],
  ] as const) {
    await page.setViewportSize(viewport);
    await frameCell.scrollIntoViewIfNeeded();
    await expect(expressionRow.locator('.frame-expression-preview .frame-repetend').nth(1)).toBeVisible();
    await page.screenshot({ path: resolve(screenshotDirectory, name), fullPage: false });
  }

  await page.getByTestId('settings-open').click();
  const savedSettings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await savedSettings.getByRole('button', { name: 'Editing preferences', exact: true }).click();
  await savedSettings.getByTestId('settings-repeating-frame-decimals').uncheck();
  await savedSettings.getByTestId('settings-apply').click();
  await expect(frameCell.locator('.frame-repetend')).toHaveCount(0);
});
