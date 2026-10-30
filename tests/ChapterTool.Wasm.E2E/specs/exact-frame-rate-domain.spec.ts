import { mkdir, readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { expect, test } from '../support/fixtures';
import { chapters, downloadText, loadBytes } from '../support/chapter-workspace';

const evidence = resolve(process.cwd(), '../../artifacts/exact-frame-rate-domain');

test('exact MPLS frame edits, display-only refresh, preview, keyboard history and export', async ({ readyPage: page }, testInfo) => {
  await mkdir(evidence, { recursive: true });
  await page.screenshot({ path: resolve(evidence, 'web-empty.png') });
  const bytes = await readFile(resolve(process.cwd(), '../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_fch.mpls'));
  await loadBytes(page, '00001_fch.mpls', bytes);
  await expect(chapters(page).locator('tbody tr')).toHaveCount(9);
  await expect(page.getByRole('button', { name: 'View export text', exact: true })).toBeVisible();
  const frame = chapters(page).getByRole('textbox', { name: 'Frames 2', exact: true, includeHidden: true });
  const time = chapters(page).getByRole('textbox', { name: 'Time 2', exact: true, includeHidden: true });
  const originalTime = await time.inputValue();
  const originalExport = await downloadText(page, testInfo);
  const round = page.getByLabel('Show integer frames', { exact: true });
  await round.check();
  await frame.focus();
  await frame.press('Tab');
  await expect(time).toHaveValue(originalTime);
  expect((await downloadText(page, testInfo)).content.equals(originalExport.content)).toBe(true);
  await frame.fill('1.5');
  await frame.press('Tab');
  await expect(time).not.toHaveValue(originalTime);
  const editedTime = await time.inputValue();
  await page.evaluate(() => document.addEventListener('keydown', event => {
    if (event.ctrlKey && ['z', 'y'].includes(event.key.toLowerCase())) {
      document.documentElement.dataset.historyShortcutPrevented = String(event.defaultPrevented);
    }
  }, true));
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Control+z');
  await expect(page.locator('html')).toHaveAttribute('data-history-shortcut-prevented', 'true');
  await expect(time).toHaveValue(originalTime);
  await page.keyboard.press('Control+y');
  await expect(time).toHaveValue(editedTime);
  await frame.focus();
  await frame.fill('123');
  await frame.press('Control+z');
  await expect(page.locator('html')).toHaveAttribute('data-history-shortcut-prevented', 'false');
  await frame.press('Escape');
  await expect(time).toHaveValue(editedTime);
  const editor = page.locator('#main-expression-editor');
  await editor.fill('t +');
  await expect(editor).toHaveAttribute('aria-invalid', 'true');
  await expect(page.locator('.inline-expression-actions button').first()).toBeDisabled();
  await page.screenshot({ path: resolve(evidence, 'web-invalid.png') });
  await editor.fill('t + 0.0002');
  const apply = page.getByRole('button', { name: 'Apply changes', exact: true });
  await expect(apply).toBeEnabled();
  await round.uncheck();
  await expect(apply).toBeEnabled();
  for (const [name, viewport] of [
    ['default', { width: 1280, height: 800 }],
    ['medium', { width: 900, height: 600 }],
    ['wide', { width: 1920, height: 1080 }],
    ['narrow', { width: 390, height: 844 }],
  ] as const) {
    await page.setViewportSize(viewport);
    const previewRow = chapters(page).locator('tbody tr').nth(1);
    await previewRow.locator('.col-time').scrollIntoViewIfNeeded();
    const timePreview = previewRow.locator('.col-time .time-expression-preview');
    await expect(timePreview).toBeVisible();
    const timeLayout = await timePreview.evaluate(preview => {
      const cell = preview.closest('td')!;
      const cellRect = cell.getBoundingClientRect();
      const children = Array.from(preview.children) as HTMLElement[];
      const before = children[0]!;
      const candidate = children[2]!;
      return {
        childrenInsideCell: children.every(child => {
          const rect = child.getBoundingClientRect();
          return rect.left >= cellRect.left && rect.right <= cellRect.right;
        }),
        noHorizontalOverflow: preview.scrollWidth <= preview.clientWidth,
        timeValuesStayOnOneLine: [before, candidate].every(value =>
          getComputedStyle(value).whiteSpace === 'nowrap'),
      };
    });
    expect(timeLayout.childrenInsideCell).toBe(true);
    expect(timeLayout.noHorizontalOverflow).toBe(true);
    expect(timeLayout.timeValuesStayOnOneLine).toBe(true);
    await page.screenshot({ path: resolve(evidence, `web-${name}-time.png`) });
    await previewRow.locator('.col-frames').scrollIntoViewIfNeeded();
    await expect(previewRow.locator('.frame-expression-preview')).toBeVisible();
    for (const display of await previewRow.locator('.frame-expression-preview .frame-display').all()) {
      expect(await display.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
      await expect(display).toHaveCSS('white-space', 'nowrap');
    }
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBe(true);
    await page.screenshot({ path: resolve(evidence, `web-${name}.png`) });
  }
  await apply.click();
  const committed = await downloadText(page, testInfo);
  expect((await downloadText(page, testInfo)).content.equals(committed.content)).toBe(true);
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Control+z');
  await expect(time).toHaveValue(editedTime);
  await page.keyboard.press('Control+y');
  expect((await downloadText(page, testInfo)).content.equals(committed.content)).toBe(true);
});

test('Auto suggestions do not authorize Unknown frame edits', async ({ readyPage: page }) => {
  await loadBytes(page, 'unknown.txt', Buffer.from('CHAPTER01=00:00:00.000\nCHAPTER01NAME=A\nCHAPTER02=00:00:12.500\nCHAPTER02NAME=B\n'));
  const frame = chapters(page).getByRole('textbox', { name: 'Frames 2', exact: true, includeHidden: true });
  const time = chapters(page).getByRole('textbox', { name: 'Time 2', exact: true, includeHidden: true });
  await frame.fill('1.5');
  await frame.press('Tab');
  await expect(time).toHaveValue('00:00:12.500');
  await page.locator('select.fps-box').selectOption({ label: '24000 / 1001' });
  await frame.fill('1.5');
  await frame.press('Tab');
  await expect(time).toHaveValue('00:00:00.063');
});

test('frame guidance uses control hover and conversion shows exact source, target and time effect before apply', async ({ readyPage: page }) => {
  const bytes = await readFile(resolve(process.cwd(), '../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_fch.mpls'));
  await loadBytes(page, '00001_fch.mpls', bytes);
  const roundFrames = page.locator('input[type="checkbox"][title="Shows whether times align with the selected frame rate."]');
  await expect(roundFrames).toBeVisible();
  await expect(page.locator('.frame-accuracy-info')).toHaveCount(0);
  const rate = page.locator('select.fps-box');
  const targetValue = await rate.locator('option').evaluateAll(options =>
    options.find(option => /(?:^|\s)25(?:\.0+)?(?:\s|$)|25000\s*\/\s*1000/.test(option.textContent ?? ''))?.getAttribute('value'));
  expect(targetValue).toBeTruthy();
  await rate.selectOption(targetValue!);
  const convertButton = page.getByRole('button', { name: 'Convert chapter FPS', exact: true });
  await expect(convertButton).toHaveAttribute('title', 'Convert chapter FPS');
  await expect(convertButton).toHaveText('');
  await convertButton.click();
  const review = page.locator('.fps-review');
  await expect(review).toBeVisible();
  await expect(page.getByTestId('fps-source')).toContainText('24000/1001');
  await expect(page.getByTestId('fps-target')).toContainText('25/1');
  await expect(page.getByTestId('fps-time-effect')).toContainText('Chapter times will change');
  await expect(page.getByRole('button', { name: 'Apply', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(page.locator('.fps-review')).toHaveCount(0);
});

test('frame-rate conversion keeps unknown source visible and cannot apply', async ({ readyPage: page }) => {
  await loadBytes(page, 'unknown.txt', Buffer.from('CHAPTER01=00:00:00.000\nCHAPTER01NAME=A\nCHAPTER02=00:00:12.500\nCHAPTER02NAME=B\n'));
  const rate = page.locator('select.fps-box');
  const targetValue = await rate.locator('option').evaluateAll(options =>
    options.find(option => /(?:^|\s)25(?:\.0+)?(?:\s|$)|25000\s*\/\s*1000/.test(option.textContent ?? ''))?.getAttribute('value'));
  expect(targetValue).toBeTruthy();
  await rate.selectOption(targetValue!);
  await page.getByRole('button', { name: /Convert chapter FPS/ }).click();
  await expect(page.getByTestId('fps-time-effect')).toContainText('source rate is unknown');
  await expect(page.getByRole('button', { name: 'Apply', exact: true })).toBeDisabled();
  await expect(page.getByTestId('fps-source')).toContainText('Not set');
  await expect(page.getByTestId('fps-target')).toContainText('25/1');
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
});

test('exact candidate pending, stale and localized states retain content and selector', async ({ readyPage: page }) => {
  const bytes = await readFile(resolve(process.cwd(), '../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_fch.mpls'));
  await loadBytes(page, '00001_fch.mpls', bytes);
  const editor = page.locator('#main-expression-editor');
  const original = await chapters(page).getByRole('textbox', { name: 'Time 2', exact: true }).inputValue();
  const pending = page.evaluate(() => new Promise<boolean>(resolvePending => {
    const editor = document.querySelector('#main-expression-editor')!;
    const observer = new MutationObserver(() => {
      if (editor.getAttribute('aria-busy') === 'true') {
        observer.disconnect();
        resolvePending(document.querySelector('.options-zone')!.getBoundingClientRect().height > 0);
      }
    });
    observer.observe(editor, { attributes: true, attributeFilter: ['aria-busy'] });
  }));
  await editor.fill('t / 2');
  expect(await pending).toBe(true);
  await expect(page.getByRole('button', { name: 'Apply changes', exact: true })).toBeEnabled();
  await page.locator('.inline-expression > button.secondary').click();
  const expression = page.getByRole('dialog');
  await expression.locator('#expression-draft').fill('t * 2');
  await expression.locator('.expression-footer button.primary').click();
  await expect(expression).toBeHidden();
  await expect(page.locator('.inline-expression-actions button').first()).toHaveText('Update change preview');
  await expect(page.getByRole('button', { name: 'Apply changes', exact: true })).toHaveCount(0);
  await page.screenshot({ path: resolve(evidence, 'web-stale.png') });
  await page.locator('.inline-expression-actions button').nth(1).click();
  const rate = page.locator('select.fps-box');
  for (const [language, autoLabel] of [['zh-CN', '自动'], ['ja-JP', '自動'], ['en-US', 'Auto']] as const) {
    await page.getByTestId('settings-open').click();
    const settings = page.getByRole('dialog');
    await settings.locator('#settings-ui-language').selectOption(language);
    await settings.getByTestId('settings-apply').click();
    await expect(page.locator('html')).toHaveAttribute('lang', language);
    await rate.selectOption('0');
    await expect(rate.locator('option:checked')).toHaveText(autoLabel);
    await rate.selectOption({ label: '24000 / 1001' });
    await expect(rate.locator('option:checked')).toHaveText('24000 / 1001');
    await rate.selectOption({ label: '25000 / 1000' });
    await rate.selectOption({ label: '24000 / 1001' });
    await expect(rate.locator('option:checked')).toHaveText('24000 / 1001');
    await expect(page.locator('.chapter-grid tbody tr').nth(1).locator('.col-time input')).not.toHaveValue(original);
    await page.screenshot({ path: resolve(evidence, `web-${language}.png`) });
  }
});
