import { fileURLToPath } from 'node:url';
import { expect, test } from '../support/fixtures';
import { chapters, loadFixture } from '../support/chapter-workspace';

test('B16 imports an MPLS playlist and appends a second playlist from the Load menu', async ({ readyPage: page }) => {
  const first = fileURLToPath(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00001_Hidan_no_Aria_AA.mpls', import.meta.url));
  const second = fileURLToPath(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00020_Terminator2.mpls', import.meta.url));
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  await (await chooserPromise).setFiles(first);
  const grid = chapters(page);
  await expect(grid.locator('tbody tr').first()).toBeVisible();
  const initialCount = await grid.locator('tbody tr').count();

  await page.getByRole('button', { name: 'Load', exact: true }).dispatchEvent('contextmenu', { button: 2 });
  const appendChooser = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: /Append MPLS/ }).click();
  await (await appendChooser).setFiles(second);
  await expect.poll(() => grid.locator('tbody tr').count(), { timeout: 30_000 }).toBeGreaterThan(initialCount);
});

test('B16 imports representative XPL input through the browser file picker', async ({ readyPage: page }) => {
  const xpl = fileURLToPath(new URL('../../ChapterTool.Core.Tests/Fixtures/Importing/Disc/Xpl/VPLST000.XPL', import.meta.url));
  const chooserPromise = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Load', exact: true }).click();
  await (await chooserPromise).setFiles(xpl);
  await expect(chapters(page).locator('tbody tr')).not.toHaveCount(0);
});
