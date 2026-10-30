import { expect, test } from '../support/fixtures';
import { chapterName, loadFixture } from '../support/chapter-workspace';

test('shortcut hints follow remaps and clears while fixed aliases and editor undo keep working', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');

  await page.getByTestId('settings-open').click();
  const settings = page.getByRole('dialog', { name: 'Settings', exact: true });
  await settings.locator('#settings-tab-5').click();
  const previewRow = settings.locator('.shortcut-row').filter({ has: page.locator('#shortcut-preview') });

  await settings.locator('#shortcut-load').press('Control+Alt+l');
  await expect(settings.locator('#shortcut-load')).toHaveValue('Ctrl+Alt+L');
  await settings.locator('#shortcut-save').press('Control+Alt+k');
  await expect(settings.locator('#shortcut-save')).toHaveValue('Ctrl+Alt+K');
  await previewRow.getByRole('button', { name: 'Clear', exact: true }).click();
  await expect(settings.locator('#shortcut-preview')).toHaveValue('');
  await settings.getByTestId('settings-apply').click();

  const loadButton = page.getByRole('button', { name: 'Load', exact: true });
  const saveButton = page.getByRole('button', { name: 'Save', exact: true });
  const previewButton = page.getByRole('button', { name: 'View export text', exact: true });
  await expect(loadButton).toHaveAttribute('title', /Ctrl\+Alt\+L|⌃\+⌥\+L/);
  await expect(loadButton).toHaveAttribute('title', /Meta\+O|⌘\+O/);
  await expect(loadButton).not.toHaveAttribute('title', /Ctrl\+O|⌃\+O/);
  await expect(saveButton).toHaveAttribute('title', /Ctrl\+Alt\+K|⌃\+⌥\+K/);
  await expect(saveButton).toHaveAttribute('title', /Meta\+S|⌘\+S/);
  await expect(saveButton).not.toHaveAttribute('title', /Ctrl\+S|⌃\+S/);
  await expect(previewButton).not.toHaveAttribute('title', /F11/);

  await page.evaluate(() => {
    const state = window as Window & { lastShortcutEvent?: KeyboardEvent };
    document.addEventListener('keydown', event => {
      if ((event.ctrlKey || event.metaKey)
        && !['Control', 'Alt', 'Meta', 'Shift'].includes(event.key)) {
        state.lastShortcutEvent = event;
      }
    }, true);
  });
  await page.locator('#chaptertool-shell').focus();

  const remappedLoad = page.waitForEvent('filechooser');
  await page.keyboard.press('Control+Alt+l');
  await remappedLoad;
  await expect.poll(() => page.evaluate(() =>
    (window as Window & { lastShortcutEvent?: KeyboardEvent }).lastShortcutEvent?.defaultPrevented)).toBe(true);
  await page.locator('#chaptertool-shell').focus();

  const remappedSave = page.waitForEvent('download');
  await page.keyboard.press('Control+Alt+k');
  const remappedDownload = await remappedSave;
  await expect.poll(() => page.evaluate(() =>
    (window as Window & { lastShortcutEvent?: KeyboardEvent }).lastShortcutEvent?.defaultPrevented)).toBe(true);
  await remappedDownload.saveAs(testInfo.outputPath(`remapped-${remappedDownload.suggestedFilename()}`));

  const metaLoad = page.waitForEvent('filechooser');
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Meta+o');
  await metaLoad;
  await expect.poll(() => page.evaluate(() =>
    (window as Window & { lastShortcutEvent?: KeyboardEvent }).lastShortcutEvent?.defaultPrevented)).toBe(true);

  const metaSave = page.waitForEvent('download');
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Meta+s');
  const metaDownload = await metaSave;
  await expect.poll(() => page.evaluate(() =>
    (window as Window & { lastShortcutEvent?: KeyboardEvent }).lastShortcutEvent?.defaultPrevented)).toBe(true);
  await metaDownload.saveAs(testInfo.outputPath(`meta-${metaDownload.suggestedFilename()}`));

  const name = await chapterName(page, 1);
  const nativeModifier = process.platform === 'darwin' ? 'Meta' : 'Control';
  await name.click();
  await name.press(`${nativeModifier}+a`);
  await name.pressSequentially('Keyboard edit');
  await name.press(`${nativeModifier}+z`);
  await expect(name).toHaveValue('Opening');
  await expect.poll(() => page.evaluate(() =>
    (window as Window & { lastShortcutEvent?: KeyboardEvent }).lastShortcutEvent?.defaultPrevented)).toBe(false);
});
