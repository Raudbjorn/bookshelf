/**
 * Debug script to capture console errors
 */

const { test } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';

test('capture console errors during search', async ({ page }) => {
  // Listen for console messages
  page.on('console', msg => {
    const type = msg.type();
    if (type === 'error' || type === 'warning') {
      console.log(`[${type.toUpperCase()}]`, msg.text());
    }
  });

  // Listen for page errors
  page.on('pageerror', error => {
    console.log('[PAGE ERROR]', error.message);
    console.log('[STACK]', error.stack);
  });

  console.log('Navigating to Add Search page');
  await page.goto(`${BASE_URL}/add/search`, { waitUntil: 'networkidle' });

  // Perform search
  console.log('Searching for Isaac Asimov');
  const searchInput = page.locator('input[type="text"]').first();
  await searchInput.fill('Isaac Asimov');
  await searchInput.press('Enter');

  // Wait for API call
  await page.waitForTimeout(3000);

  console.log('Test complete');
});
