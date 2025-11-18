/**
 * Debug script to see what the homepage looks like
 */

const { test } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';

test('debug homepage', async ({ page }) => {
  console.log('Navigating to', BASE_URL);
  await page.goto(BASE_URL);
  await page.waitForLoadState('networkidle');

  await page.screenshot({ path: '/tmp/homepage.png', fullPage: true });
  console.log('Screenshot saved to /tmp/homepage.png');

  // Get page content
  const html = await page.content();
  console.log('Page title:', await page.title());
  console.log('Page URL:', page.url());

  // Look for any links
  const links = await page.locator('a').all();
  console.log('Found', links.length, 'links');

  for (let i = 0; i < Math.min(10, links.length); i++) {
    const href = await links[i].getAttribute('href');
    const text = await links[i].textContent();
    console.log(`Link ${i}: href="${href}" text="${text}"`);
  }
});
