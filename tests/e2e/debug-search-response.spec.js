/**
 * Debug script to capture what the frontend receives from search API
 */

const { test } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';

test('debug search API response', async ({ page }) => {
  console.log('Navigating to Add Search page');
  await page.goto(`${BASE_URL}/add/search`);
  await page.waitForLoadState('networkidle');

  // Listen for API responses
  page.on('response', async response => {
    if (response.url().includes('/api/v1/search')) {
      console.log('\n=== API Call ===');
      console.log('URL:', response.url());
      console.log('Status:', response.status());

      try {
        const data = await response.json();
        console.log('Response structure:', Object.keys(data));

        if (data.books) {
          console.log('Books count:', data.books.length);
          if (data.books.length > 0) {
            console.log('First book keys:', Object.keys(data.books[0]));
          }
        }

        if (data.authors) {
          console.log('Authors count:', data.authors.length);
          if (data.authors.length > 0) {
            console.log('First author keys:', Object.keys(data.authors[0]));
          }
        }

        // Check if it's an array (old provider format)
        if (Array.isArray(data)) {
          console.log('Response is an array with', data.length, 'items');
          if (data.length > 0) {
            console.log('First item keys:', Object.keys(data[0]));
          }
        }
      } catch (e) {
        console.log('Failed to parse response:', e.message);
      }
    }
  });

  // Perform search
  console.log('Searching for Isaac Asimov');
  const searchInput = page.locator('input[type="text"]').first();
  await searchInput.fill('Isaac Asimov');
  await searchInput.press('Enter');

  // Wait for API call
  await page.waitForTimeout(3000);

  // Take screenshot
  await page.screenshot({ path: '/tmp/debug-search.png', fullPage: true });
  console.log('Screenshot saved to /tmp/debug-search.png');
});
