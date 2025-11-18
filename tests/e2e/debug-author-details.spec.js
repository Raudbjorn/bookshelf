/**
 * Debug script to see author details page structure
 */

const { test } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';
const TEST_AUTHOR = 'Isaac Asimov';

test('capture author details page', async ({ page }) => {
  console.log('Navigating to library');
  await page.goto(BASE_URL);
  await page.waitForLoadState('networkidle');

  // Search for author
  console.log('Searching for author');
  const searchInput = page.locator('input[type="text"]').first();
  await searchInput.fill(TEST_AUTHOR);
  await page.waitForTimeout(1000);

  // Click on author
  console.log('Clicking on author');
  const authorLink = page.locator(`text=${TEST_AUTHOR}`).first();
  await authorLink.click();
  await page.waitForLoadState('networkidle');

  // Take screenshot
  await page.screenshot({ path: '/tmp/author-details.png', fullPage: true });
  console.log('Screenshot saved to /tmp/author-details.png');

  // Get page HTML to see button structure
  const html = await page.content();
  console.log('Page URL:', page.url());

  // Look for any buttons on the page
  const allButtons = await page.locator('button').all();
  console.log(`Found ${allButtons.length} buttons`);

  for (let i = 0; i < Math.min(allButtons.length, 20); i++) {
    const text = await allButtons[i].textContent().catch(() => '');
    const ariaLabel = await allButtons[i].getAttribute('aria-label').catch(() => '');
    const title = await allButtons[i].getAttribute('title').catch(() => '');
    console.log(`Button ${i}: text="${text}", aria-label="${ariaLabel}", title="${title}"`);
  }
});
