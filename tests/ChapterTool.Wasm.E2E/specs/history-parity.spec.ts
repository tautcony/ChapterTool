import { expect, test } from '../support/fixtures';
import { chapterName, commit, loadFixture } from '../support/chapter-workspace';

test('history inspection shows localized root and field details without moving the cursor, then redoes the chosen branch', async ({ readyPage: page }) => {
  await loadFixture(page, 'minimal-ogm.txt');
  const name = await chapterName(page, 1);
  await commit(name, 'First branch');
  await page.locator('#chaptertool-shell').focus();
  await page.keyboard.press('Control+z');
  await expect(name).toHaveValue('Opening');
  await commit(name, 'Second branch');

  await page.getByRole('button', { name: 'Edit history', exact: true }).click();
  const history = page.getByRole('dialog', { name: 'Edit history', exact: true });
  const defaultBounds = await history.evaluate(element => {
    const rect = element.getBoundingClientRect();
    return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom, width: window.innerWidth, height: window.innerHeight };
  });
  expect(defaultBounds.left).toBeGreaterThanOrEqual(0);
  expect(defaultBounds.right).toBeLessThanOrEqual(defaultBounds.width);
  expect(defaultBounds.top).toBeGreaterThanOrEqual(0);
  expect(defaultBounds.bottom).toBeLessThanOrEqual(defaultBounds.height);
  const entries = history.locator('.history-entry');
  await expect(entries).toHaveCount(3);
  await expect(entries.filter({ hasText: 'Edit chapter' }).last()).toHaveAttribute('aria-current', 'step');

  await entries.nth(0).click();
  await expect(history.locator('.history-root-rows')).toContainText('Opening');
  await expect(name).toHaveValue('Second branch');
  await expect(entries.filter({ hasText: 'Edit chapter' }).last()).toHaveAttribute('aria-current', 'step');

  await entries.nth(2).click();
  await expect(history.getByRole('button', { name: 'Restore to this node', exact: true })).toBeInViewport();
  await expect(history.locator('.history-change-table')).toContainText('Opening');
  await expect(history.locator('.history-change-table')).toContainText('Second branch');
  await history.locator('.history-filter select').selectOption('Modified');
  await expect(history.locator('.history-change-row')).toHaveCount(2);
  await expect(name).toHaveValue('Second branch');

  await history.getByRole('button', { name: 'Undo', exact: true }).click();
  await expect(name).toHaveValue('Opening');
  await expect(entries.filter({ hasText: 'Edit chapter' })).toHaveCount(2);
  const redoChoices = history.getByLabel('Redo branch choices', { exact: true });
  await expect(redoChoices.locator('option')).toHaveCount(2);
  await redoChoices.selectOption({ index: 0 });
  await expect(name).toHaveValue('Opening');
  await expect(history.locator('.history-entry[aria-current="step"]')).toContainText('Initial document');
  await history.getByRole('button', { name: 'Redo', exact: true }).click();
  await expect(name).toHaveValue('First branch');
  await expect(history.locator('.history-entry[aria-current="step"]')).toContainText('Edit chapter');
});

test('narrow history details retain reachable restore and modal keyboard focus', async ({ readyPage: page }) => {
  await page.setViewportSize({ width: 390, height: 640 });
  await loadFixture(page, 'minimal-ogm.txt');
  const name = await chapterName(page, 1);
  await commit(name, 'Narrow history edit');
  const opener = page.getByRole('button', { name: 'Edit history', exact: true });
  await opener.click();

  const history = page.getByRole('dialog', { name: 'Edit history', exact: true });
  const narrowBounds = await history.evaluate(element => {
    const rect = element.getBoundingClientRect();
    return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom, width: window.innerWidth, height: window.innerHeight };
  });
  expect(narrowBounds.left).toBeGreaterThanOrEqual(0);
  expect(narrowBounds.right).toBeLessThanOrEqual(narrowBounds.width);
  expect(narrowBounds.top).toBeGreaterThanOrEqual(0);
  expect(narrowBounds.bottom).toBeLessThanOrEqual(narrowBounds.height);
  const entries = history.locator('.history-entry');
  const historyTab = history.getByRole('tab', { name: 'History', exact: true });
  const detailsTab = history.getByRole('tab', { name: 'Details', exact: true });
  await expect(detailsTab).toBeVisible();
  await historyTab.focus();
  await page.keyboard.press('ArrowRight');
  await expect(detailsTab).toHaveAttribute('aria-selected', 'true');
  await expect(detailsTab).toBeFocused();
  await page.keyboard.press('ArrowLeft');
  await expect(historyTab).toHaveAttribute('aria-selected', 'true');
  await expect(historyTab).toBeFocused();
  await entries.nth(0).click();
  await expect(history.locator('#history-details')).toBeVisible();
  const restore = history.getByRole('button', { name: 'Restore to this node', exact: true });
  await expect(restore).toBeEnabled();
  await expect(restore).toBeInViewport();
  await restore.click();
  await expect(name).toHaveValue('Opening');
  await page.keyboard.press('Escape');
  await expect(history).toHaveCount(0);
  await expect(opener).toBeFocused();
});
