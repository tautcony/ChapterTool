import { expect, test } from '../support/fixtures';
import { chapterName, chapters, commit, loadFixture } from '../support/chapter-workspace';

test('invalid main expression diagnostics do not resize workflow zones', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const editor = page.locator('#main-expression-editor');
  const grid = page.locator('.grid-wrap');
  const options = page.locator('.options-zone');
  const secondTime = chapters(page).getByRole('textbox', { name: 'Time 2', exact: true });
  for (const viewport of [
    { name: 'default', width: 1280, height: 800 },
    { name: 'wide', width: 1920, height: 1080 },
    { name: 'narrow', width: 390, height: 844 },
  ]) {
    await page.setViewportSize(viewport);
    await editor.fill('');
    await expect(editor).toHaveAttribute('aria-busy', 'false');
    await expect(page.locator('.inline-expression [role="alert"]')).toBeHidden();
    const beforeGrid = await grid.boundingBox();
    const beforeOptions = await options.boundingBox();
    await editor.fill('t +');
    await expect(page.locator('.inline-expression [role="alert"]')).toBeVisible();
    await expect(page.locator('.inline-expression [role="alert"]')).toContainText('unexpected');
    await expect(editor).toHaveAttribute('aria-invalid', 'true');
    await expect(editor).toHaveAttribute('aria-describedby', 'main-expression-diagnostics');
    await expect(page.locator('.inline-expression-actions button').first()).toBeDisabled();
    await expect(secondTime).toHaveValue('00:00:12.500');
    await page.screenshot({ path: testInfo.outputPath(`diagnostic-${viewport.name}.png`), fullPage: true });
    const afterGrid = await grid.boundingBox();
    const afterOptions = await options.boundingBox();
    expect.soft(afterGrid?.height, `${viewport.name}: grid height`).toBeCloseTo(beforeGrid!.height, 0);
    expect.soft(afterOptions?.height, `${viewport.name}: options height`).toBeCloseTo(beforeOptions!.height, 0);
    expect.soft(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
    await page.mouse.move(0, 0);
    await editor.press('Tab');
    await expect(page.locator('.inline-expression [role="alert"]')).toBeHidden();
    await editor.focus();
    await expect(page.locator('.inline-expression [role="alert"]')).toBeVisible();
    await editor.fill('t / 2');
    await expect(chapters(page).locator('tbody tr').nth(1).locator('.col-time')).toContainText('00:00:06.250');
    expect.soft((await options.boundingBox())?.height, `${viewport.name}: valid options height`).toBeCloseTo(beforeOptions!.height, 0);
    expect.soft((await grid.boundingBox())?.height, `${viewport.name}: valid grid height`).toBeCloseTo(beforeGrid!.height, 0);
    await page.locator('.inline-expression-actions button').nth(1).click();
    await expect(secondTime).toHaveValue('00:00:12.500');
    expect.soft((await options.boundingBox())?.height, `${viewport.name}: discard options height`).toBeCloseTo(beforeOptions!.height, 0);
  }
});

test('undo and redo belong to edit history and keep their workflow', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await expect(page.locator('.top-bar').getByRole('button', { name: 'Undo', exact: true })).toHaveCount(0);
  await expect(page.locator('.top-bar').getByRole('button', { name: 'Redo', exact: true })).toHaveCount(0);
  await page.locator('#main-expression-editor').fill('t / 2');
  await page.getByRole('button', { name: 'Apply changes', exact: true }).click();
  const time = chapters(page).getByRole('textbox', { name: 'Time 2', exact: true });
  await expect(time).toHaveValue('00:00:06.250');
  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  const history = page.getByRole('dialog', { name: 'Edit history', exact: true });
  await history.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(time).toHaveValue('00:00:12.500');
  await history.getByRole('button', { name: 'Redo', exact: true }).click();
  await expect(time).toHaveValue('00:00:06.250');
});

test('pending and stale main previews retain the workflow geometry', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const options = page.locator('.options-zone');
  const editor = page.locator('#main-expression-editor');
  const before = await options.boundingBox();
  const pendingGeometry = page.evaluate(() => new Promise<number>(resolve => {
    const editor = document.querySelector('#main-expression-editor')!;
    const observer = new MutationObserver(() => {
      if (editor.getAttribute('aria-busy') === 'true') {
        observer.disconnect();
        resolve(document.querySelector('.options-zone')!.getBoundingClientRect().height);
      }
    });
    observer.observe(editor, { attributes: true, attributeFilter: ['aria-busy'] });
  }));
  await editor.fill('t / 2');
  expect(await pendingGeometry).toBeCloseTo(before!.height, 0);
  const secondTime = chapters(page).locator('tbody tr').nth(1).locator('.col-time');
  await expect(secondTime).toContainText('00:00:06.250');
  await page.locator('.inline-expression > button.secondary').click();
  const dialog = page.getByRole('dialog');
  await dialog.locator('#expression-draft').fill('t * 2');
  await dialog.locator('.expression-footer button.primary').click();
  await expect(dialog).toBeHidden();
  const refresh = page.locator('.inline-expression-actions button').first();
  await expect(refresh).toHaveText('Update preview');
  expect((await options.boundingBox())?.height).toBeCloseTo(before!.height, 0);
  await expect(secondTime.getByRole('textbox')).toHaveValue('00:00:25.000');
  await refresh.click();
  await expect(page.locator('.inline-expression-actions button').first()).toBeDisabled();
  expect((await options.boundingBox())?.height).toBeCloseTo(before!.height, 0);
  await page.locator('.inline-expression-actions button').nth(1).click();
  await expect(secondTime.getByRole('textbox')).toHaveValue('00:00:25.000');
});

test('diagnostics keep layout and keyboard access after Chinese and Japanese language switches', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.setViewportSize({ width: 390, height: 844 });
  const editor = page.locator('#main-expression-editor');
  const options = page.locator('.options-zone');
  for (const language of ['zh-CN', 'ja-JP', 'en-US']) {
    await page.getByTestId('settings-open').click();
    const settings = page.getByRole('dialog');
    await settings.locator('#settings-ui-language').selectOption(language);
    await settings.getByTestId('settings-apply').click();
    await expect(page.locator('html')).toHaveAttribute('lang', language);
    await editor.fill('');
    await expect(editor).toHaveAttribute('aria-busy', 'false');
    const before = await options.boundingBox();
    await editor.fill('t +');
    await expect(page.locator('#main-expression-diagnostics')).toBeVisible();
    await expect(editor).toHaveAttribute('aria-describedby', 'main-expression-diagnostics');
    expect((await options.boundingBox())?.height).toBeCloseTo(before!.height, 0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
  }
});

test('document undo and redo prevent native text undo after a cell commit', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const name = await chapterName(page, 1);
  await commit(name, 'Changed opening');
  await page.evaluate(() => {
    document.addEventListener('keydown', event => {
      if (event.ctrlKey && ['z', 'y'].includes(event.key.toLowerCase())) {
        document.documentElement.dataset.historyShortcutPrevented = String(event.defaultPrevented);
      }
    }, true);
  });
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Control+z');
  await expect(page.locator('html')).toHaveAttribute('data-history-shortcut-prevented', 'true');
  await expect(name).toHaveValue('Opening');
  await page.keyboard.press('Control+y');
  await expect(page.locator('html')).toHaveAttribute('data-history-shortcut-prevented', 'true');
  await expect(name).toHaveValue('Changed opening');
  await name.focus();
  await name.fill('Uncommitted draft');
  await name.press('Control+z');
  await expect(page.locator('html')).toHaveAttribute('data-history-shortcut-prevented', 'false');
  await name.press('Escape');
  await expect(name).toHaveValue('Changed opening');
});
