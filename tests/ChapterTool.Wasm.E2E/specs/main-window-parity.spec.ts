import { fileURLToPath } from 'node:url';
import { expect, test } from '../support/fixtures';
import { chapters, downloadText, loadBytes, loadFixture } from '../support/chapter-workspace';

test('changing naming after applying the main expression does not run the expression again', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.locator('#main-expression-editor').fill('t / 2');
  const secondRow = chapters(page).locator('tbody tr').nth(1);
  await expect(secondRow.locator('.col-time')).toContainText('00:00:06.250');
  await page.getByRole('button', { name: 'Apply changes', exact: true }).click();
  await expect(secondRow.getByRole('textbox', { name: 'Time 2', exact: true })).toHaveValue('00:00:06.250');
  await page.locator('#nameMode').selectOption('1');
  await expect(secondRow.locator('.col-name')).toContainText('Chapter 02');
  await expect(secondRow.getByRole('textbox', { name: 'Time 2', exact: true })).toHaveValue('00:00:06.250');
  await page.getByRole('button', { name: 'Apply changes', exact: true }).click();
  await expect(secondRow.getByRole('textbox', { name: 'Time 2', exact: true })).toHaveValue('00:00:06.250');
  expect((await downloadText(page, testInfo)).content.toString('utf8')).toContain('00:00:06.250');
});

test('main candidate actions remain reachable at supported sizes and capture review images', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.locator('#main-order-shift').fill('1');
  await page.locator('#main-expression-editor').fill('t / 2');
  await expect(chapters(page).locator('tbody tr').nth(1).locator('.col-time')).toContainText('00:00:06.250');
  for (const viewport of [
    { name: 'default', width: 1280, height: 800 },
    { name: 'wide', width: 1920, height: 1080 },
    { name: 'narrow', width: 390, height: 844 },
    { name: 'short', width: 390, height: 640 },
    { name: 'landscape', width: 844, height: 390 },
  ]) {
    await page.setViewportSize(viewport);
    const apply = page.getByRole('button', { name: 'Apply changes', exact: true });
    await apply.scrollIntoViewIfNeeded();
    await expect(apply).toBeInViewport();
    await expect(apply).toBeEnabled();
    const loadButton = page.getByRole('button', { name: 'Load', exact: true });
    expect(await loadButton.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    if (viewport.name === 'default') {
      const actionButtons = page.locator('.inline-expression-actions button');
      await expect(actionButtons).toHaveCount(2);
      const actionTops = await actionButtons.evaluateAll(elements => elements.map(element => element.getBoundingClientRect().top));
      expect(Math.abs(actionTops[0] - actionTops[1])).toBeLessThan(1);
    }
    if (viewport.name === 'landscape') {
      for (const id of ['saveFormat', 'main-xml-language']) {
        const control = page.locator(`#${id}`);
        const label = page.locator(`label[for="${id}"]`);
        const metrics = await control.evaluate(element => {
          const bounds = element.getBoundingClientRect();
          return { width: bounds.width, left: bounds.left };
        });
        const labelRight = await label.evaluate(element => element.getBoundingClientRect().right);
        expect(metrics.width).toBeGreaterThanOrEqual(100);
        expect(labelRight).toBeLessThanOrEqual(metrics.left);
      }
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
    await page.screenshot({ path: testInfo.outputPath(`main-${viewport.name}.png`), fullPage: true });
  }
});

test('main load menu exposes reload and append without a context click', async ({ readyPage: page }) => {
  const first = fileURLToPath(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_Hidan_no_Aria_AA.mpls', import.meta.url));
  const second = fileURLToPath(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00020_Terminator2.mpls', import.meta.url));
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  await (await chooserPromise).setFiles(first);

  const grid = chapters(page);
  await expect(grid.locator('tbody tr').first()).toBeVisible();
  const initialCount = await grid.locator('tbody tr').count();
  const menuToggle = page.getByTestId('load-menu-toggle');
  await menuToggle.click();
  const menu = page.locator('#load-menu');
  await expect(menu).toBeVisible();
  await expect(menu.getByRole('menuitem', { name: 'Reload', exact: true })).toBeEnabled();
  await expect(menu.getByRole('menuitem', { name: /^Append MPLS/ })).toBeEnabled();

  await menu.getByRole('menuitem', { name: 'Reload', exact: true }).click();
  await expect(grid.locator('tbody tr')).toHaveCount(initialCount);
  await menuToggle.click();
  const appendChooser = page.waitForEvent('filechooser');
  await menu.getByRole('menuitem', { name: /^Append MPLS/ }).click();
  await (await appendChooser).setFiles(second);
  await expect.poll(() => grid.locator('tbody tr').count(), { timeout: 30_000 }).toBeGreaterThan(initialCount);
});

test('combined main options stay a draft until one apply and preserve grid state', async ({ readyPage: page }, testInfo) => {
  const chaptersText = Array.from({ length: 36 }, (_, index) => {
    const number = String(index + 1).padStart(2, '0');
    const totalSeconds = index * 5;
    const minutes = String(Math.floor(totalSeconds / 60)).padStart(2, '0');
    const seconds = String(totalSeconds % 60).padStart(2, '0');
    return `CHAPTER${number}=${`00:${minutes}:${seconds}.000`}\nCHAPTER${number}NAME=Original ${number}`;
  }).join('\n');
  await loadBytes(page, 'main-options.txt', Buffer.from(chaptersText));
  const grid = chapters(page);
  await expect(grid.locator('tbody tr')).toHaveCount(36);

  const templateChooser = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Template file', exact: true }).click();
  await (await templateChooser).setFiles({
    name: 'chapter-names.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from(Array.from({ length: 36 }, (_, index) => `Candidate ${index + 1}`).join('\n')),
  });
  await page.locator('#main-order-shift').fill('2');
  await page.locator('#main-expression-editor').fill('t / 2');

  const secondRow = grid.locator('tbody tr').nth(1);
  await expect(secondRow.locator('.col-num')).toContainText('2');
  await expect(secondRow.locator('.col-num')).toContainText('4');
  await expect(secondRow.locator('.col-name')).toContainText('Candidate 2');
  await expect(secondRow.locator('.col-time')).toContainText('00:00:02.500');
  await expect(page.getByRole('button', { name: 'Apply changes', exact: true })).toBeVisible();

  await secondRow.click();
  const lastRow = grid.locator('tbody tr').last();
  const selectionModifier = process.platform === 'darwin' ? 'Meta' : 'Control';
  await lastRow.click({ modifiers: [selectionModifier] });
  await expect(grid.locator('tbody tr.selected')).toHaveCount(2);
  const scrollContainer = page.locator('.grid-wrap');
  await scrollContainer.evaluate(element => { element.scrollTop = element.scrollHeight; });
  const scrollBeforeApply = await scrollContainer.evaluate(element => element.scrollTop);

  const uncommitted = await downloadText(page, testInfo);
  const uncommittedText = uncommitted.content.toString('utf8');
  expect(uncommittedText).toContain('CHAPTER02NAME=Original 02');
  expect(uncommittedText).not.toContain('Candidate 2');

  await page.getByRole('button', { name: 'Apply changes', exact: true }).click();
  await expect(secondRow.getByRole('textbox', { name: 'Name 2', exact: true })).toHaveValue('Candidate 2');
  await expect(secondRow.locator('.col-num')).toHaveText('4');
  await expect(scrollContainer).toHaveJSProperty('scrollTop', scrollBeforeApply);
  await expect(secondRow).toHaveClass(/selected/);
  await expect(lastRow).toHaveClass(/selected/);

  await page.locator('#chaptertool-shell').focus();
  const undoModifier = process.platform === 'darwin' ? 'Meta' : 'Control';
  await page.keyboard.press(`${undoModifier}+z`);
  await expect(secondRow.getByRole('textbox', { name: 'Name 2', exact: true })).toHaveValue('Original 02');
  await expect(secondRow.locator('.col-num')).toHaveText('2');
  await expect(secondRow.getByRole('textbox', { name: 'Time 2', exact: true })).toHaveValue('00:00:05.000');
});

test('1000-row combined preview shows and applies the final candidate', async ({ readyPage: page }) => {
  test.setTimeout(90_000);
  const rowCount = 1000;
  const chaptersText = Array.from({ length: rowCount }, (_, index) => {
    const number = String(index + 1).padStart(2, '0');
    const totalSeconds = index * 5;
    const hours = String(Math.floor(totalSeconds / 3600)).padStart(2, '0');
    const minutes = String(Math.floor(totalSeconds % 3600 / 60)).padStart(2, '0');
    const seconds = String(totalSeconds % 60).padStart(2, '0');
    return `CHAPTER${number}=${hours}:${minutes}:${seconds}.000\nCHAPTER${number}NAME=Original ${number}`;
  }).join('\n');
  await loadBytes(page, 'large-main-options.txt', Buffer.from(chaptersText));
  const grid = chapters(page);
  await expect(grid.locator('tbody tr')).toHaveCount(rowCount, { timeout: 30_000 });

  const templateChooser = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Template file', exact: true }).click();
  await (await templateChooser).setFiles({
    name: 'large-chapter-names.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from(Array.from({ length: rowCount }, (_, index) => `Candidate ${index + 1}`).join('\n')),
  });
  await page.locator('#main-order-shift').fill('1');
  await page.locator('#main-expression-editor').fill('t / 2');

  const lastRow = grid.locator('tbody tr').last();
  await expect(lastRow.locator('.col-num')).toContainText('1001', { timeout: 30_000 });
  await expect(lastRow.locator('.col-name')).toContainText('Candidate 1000');
  await expect(lastRow.locator('.col-time')).toContainText('00:41:37.500');
  await page.getByRole('button', { name: 'Apply changes', exact: true }).click();
  await expect(lastRow.locator('.col-num')).toHaveText('1001');
  await expect(lastRow.getByRole('textbox', { name: 'Name 1000', exact: true })).toHaveValue('Candidate 1000');
  await expect(lastRow.getByRole('textbox', { name: 'Time 1000', exact: true })).toHaveValue('00:41:37.500');
});

test('expression dialog cancellation leaves the main candidate and committed content intact', async ({ readyPage: page }, testInfo) => {
  await loadFixture(page, 'minimal-ogm.txt');
  await page.locator('#main-order-shift').fill('1');
  await page.locator('#main-expression-editor').fill('t / 2');
  const secondRow = chapters(page).locator('tbody tr').nth(1);
  await expect(secondRow.locator('.col-time')).toContainText('00:00:06.250');
  const candidateCell = await secondRow.locator('.col-time').textContent();
  const candidateGeometry = await secondRow.locator('.col-time').evaluate(element => {
    const { x, y, width, height } = element.getBoundingClientRect();
    return [x, y, width, height];
  });

  await page.getByRole('button', { name: 'Expression editor', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Expression', exact: true });
  await expect(dialog).toBeVisible();
  await expect.poll(() => secondRow.locator('.col-time').textContent()).toBe(candidateCell);
  expect(await secondRow.locator('.col-time').evaluate(element => {
    const { x, y, width, height } = element.getBoundingClientRect();
    return [x, y, width, height];
  })).toEqual(candidateGeometry);
  await dialog.locator('#expression-draft').fill('t * 2');
  await expect(dialog.locator('#expression-draft')).toHaveValue('t * 2');
  await expect.poll(() => secondRow.locator('.col-time').textContent()).toBe(candidateCell);
  expect(await secondRow.locator('.col-time').evaluate(element => {
    const { x, y, width, height } = element.getBoundingClientRect();
    return [x, y, width, height];
  })).toEqual(candidateGeometry);
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();

  await expect.poll(() => secondRow.locator('.col-time').textContent()).toBe(candidateCell);
  expect(await secondRow.locator('.col-time').evaluate(element => {
    const { x, y, width, height } = element.getBoundingClientRect();
    return [x, y, width, height];
  })).toEqual(candidateGeometry);
  await expect(page.getByRole('button', { name: 'Apply changes', exact: true })).toBeVisible();
  const saved = await downloadText(page, testInfo);
  expect(saved.content.toString('utf8')).toContain('00:00:12.500');
  expect(saved.content.toString('utf8')).not.toContain('00:00:06.250');
});

test('settings drafts apply live, cancel rolls them back, and saved defaults keep session outputs', async ({ readyPage: page }) => {
  const storageKey = 'chaptertool.wasm.settings';
  const storageBefore = await page.evaluate(key => localStorage.getItem(key), storageKey);
  await loadFixture(page, 'minimal-ogm.txt');
  const roundFrames = page.getByRole('checkbox', { name: 'Round frames', exact: true });
  await roundFrames.setChecked(false);
  await expect(roundFrames).not.toBeChecked();
  await page.getByTestId('settings-open').click();
  let dialog = page.getByRole('dialog');
  await dialog.locator('#settings-ui-language').selectOption('zh-CN');
  await expect(page.locator('html')).toHaveAttribute('lang', 'zh-CN');
  await dialog.locator('#settings-tab-4').click();
  await dialog.locator('#settings-theme').selectOption('solarized-dark');
  await expect.poll(() => page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--window-bg').trim())).toBe('#002B36');
  expect(await page.evaluate(key => localStorage.getItem(key), storageKey)).toBe(storageBefore);
  await dialog.getByTestId('settings-cancel').click();
  await expect(page.locator('html')).toHaveAttribute('lang', 'en-US');
  await expect.poll(() => page.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue('--window-bg').trim())).toBe('#F0F0F0');
  await expect(roundFrames).not.toBeChecked();
  expect(await page.evaluate(key => localStorage.getItem(key), storageKey)).toBe(storageBefore);

  const sessionFormat = page.locator('#saveFormat');
  await sessionFormat.selectOption({ label: 'XML' });
  const sessionXmlLanguage = page.locator('#main-xml-language');
  await expect(sessionXmlLanguage).toBeEnabled();
  await sessionXmlLanguage.selectOption('jpn');
  const formatBeforeSettings = await sessionFormat.inputValue();
  const xmlBeforeSettings = await sessionXmlLanguage.inputValue();

  await page.getByTestId('settings-open').click();
  dialog = page.getByRole('dialog', { name: 'Settings', exact: true });
  await dialog.getByRole('button', { name: 'Output preferences', exact: true }).click();
  const defaultFormat = dialog.locator('#settings-save-format');
  const differentFormat = await defaultFormat.locator('option').evaluateAll(options => {
    const current = (document.querySelector('#settings-save-format') as HTMLSelectElement).value;
    return options.map(option => (option as HTMLOptionElement).value).find(value => value !== current) ?? current;
  });
  await defaultFormat.selectOption(differentFormat);
  const defaultXml = dialog.locator('#settings-xml-language');
  const differentXml = await defaultXml.locator('option').evaluateAll(options => {
    const current = (document.querySelector('#settings-xml-language') as HTMLSelectElement).value;
    return options.map(option => (option as HTMLOptionElement).value).find(value => value !== current) ?? current;
  });
  await defaultXml.selectOption(differentXml);
  await expect(sessionFormat).toHaveValue(formatBeforeSettings);
  await expect(sessionXmlLanguage).toHaveValue(xmlBeforeSettings);
  await dialog.getByTestId('settings-apply').click();
  const savedSettings = await page.evaluate(key => JSON.parse(localStorage.getItem(key) ?? '{}'), storageKey);
  expect(savedSettings.application.defaultXmlLanguage).toBe(differentXml);
  expect(savedSettings.application.defaultSaveFormat).not.toBeUndefined();
  await expect(sessionFormat).toHaveValue(formatBeforeSettings);
  await expect(sessionXmlLanguage).toHaveValue(xmlBeforeSettings);
});
