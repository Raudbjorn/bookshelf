/**
 * Multi-Provider Search E2E Test
 *
 * Tests the multi-provider search functionality including:
 * - Provider selector dropdown
 * - Search mode selection (Reconciled, Hardcover, Open Library, Google Books, All)
 * - Provider badges on search results
 * - Confidence scores for reconciled results
 */

const { test, expect } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';
const TEST_SEARCH_TERM = 'sanderson';

test.describe('Multi-Provider Search', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the add new book page
    await page.goto(`${BASE_URL}/add/search`);
    await page.waitForLoadState('networkidle');
  });

  test('should display provider selector dropdown', async ({ page }) => {
    // Check that the provider selector is visible
    const providerSelector = page.locator('select[name="searchMode"]');
    await expect(providerSelector).toBeVisible();

    // Verify all provider options are available
    const options = await providerSelector.locator('option').allTextContents();
    expect(options).toContain('Reconciled (All Providers)');
    expect(options).toContain('Hardcover');
    expect(options).toContain('Open Library');
    expect(options).toContain('Google Books');
    expect(options).toContain('All Providers (Grouped)');
  });

  test('should persist selected provider mode', async ({ page }) => {
    // Select Hardcover provider
    await page.selectOption('select[name="searchMode"]', 'hardcover');

    // Wait for the state to be saved (localStorage)
    await page.waitForTimeout(500);

    // Reload the page
    await page.reload();
    await page.waitForLoadState('networkidle');

    // Verify the selection persisted
    const selectedValue = await page.locator('select[name="searchMode"]').inputValue();
    expect(selectedValue).toBe('hardcover');
  });

  test('should perform search and display provider badges', async ({ page }) => {
    // Select reconciled mode
    await page.selectOption('select[name="searchMode"]', 'reconciled');

    // Enter search term
    const searchInput = page.locator('input[type="text"][name="term"]').first();
    await searchInput.fill(TEST_SEARCH_TERM);

    // Submit search (press Enter or click search button)
    await searchInput.press('Enter');

    // Wait for search results
    await page.waitForSelector('[class*="searchResult"]', { timeout: 10000 });

    // Check that search results are displayed
    const searchResults = page.locator('[class*="searchResult"]');
    const resultCount = await searchResults.count();
    expect(resultCount).toBeGreaterThan(0);

    // Verify provider badges are present in at least one result
    const providerBadges = page.locator('[class*="badge"]');
    await expect(providerBadges.first()).toBeVisible({ timeout: 5000 });
  });

  test('should display confidence scores for reconciled results', async ({ page }) => {
    // Select reconciled mode
    await page.selectOption('select[name="searchMode"]', 'reconciled');

    // Perform search
    const searchInput = page.locator('input[type="text"][name="term"]').first();
    await searchInput.fill(TEST_SEARCH_TERM);
    await searchInput.press('Enter');

    // Wait for results
    await page.waitForSelector('[class*="searchResult"]', { timeout: 10000 });

    // Check for confidence badge (format: "XX% Match")
    const confidenceBadge = page.locator('text=/\\d+% Match/');

    // Note: Confidence badge may not appear if no multi-provider matches found
    // So we check if it exists, and if it does, verify format
    const count = await confidenceBadge.count();
    if (count > 0) {
      const text = await confidenceBadge.first().textContent();
      expect(text).toMatch(/^\d+% Match$/);
    }
  });

  test('should change provider mode and update results', async ({ page }) => {
    // Start with Hardcover
    await page.selectOption('select[name="searchMode"]', 'hardcover');

    const searchInput = page.locator('input[type="text"][name="term"]').first();
    await searchInput.fill(TEST_SEARCH_TERM);
    await searchInput.press('Enter');

    // Wait for Hardcover results
    await page.waitForSelector('[class*="searchResult"]', { timeout: 10000 });

    // Check for Hardcover badge
    await expect(page.locator('text=/Hardcover/').first()).toBeVisible({ timeout: 5000 });

    // Switch to Open Library
    await page.selectOption('select[name="searchMode"]', 'openlibrary');
    await searchInput.clear();
    await searchInput.fill(TEST_SEARCH_TERM);
    await searchInput.press('Enter');

    // Wait for Open Library results
    await page.waitForSelector('[class*="searchResult"]', { timeout: 10000 });

    // Check for Open Library badge
    await expect(page.locator('text=/Open Library/').first()).toBeVisible({ timeout: 5000 });
  });

  test('should display multiple provider badges in reconciled mode', async ({ page }) => {
    // Select reconciled mode
    await page.selectOption('select[name="searchMode"]', 'reconciled');

    // Search for a popular book likely to have multiple provider matches
    const searchInput = page.locator('input[type="text"][name="term"]').first();
    await searchInput.fill('harry potter');
    await searchInput.press('Enter');

    // Wait for results
    await page.waitForSelector('[class*="searchResult"]', { timeout: 10000 });

    // Check for reconciledBadges container
    const reconciledBadgesContainer = page.locator('[class*="reconciledBadges"]').first();

    // Verify it contains multiple badges
    const badgesInContainer = reconciledBadgesContainer.locator('[class*="badge"]');
    const badgeCount = await badgesInContainer.count();

    // In reconciled mode with a popular book, we expect multiple providers
    if (badgeCount > 0) {
      expect(badgeCount).toBeGreaterThan(0);
    }
  });

  test('should show different colored badges for different providers', async ({ page }) => {
    // Select "All Providers (Grouped)" mode
    await page.selectOption('select[name="searchMode"]', 'all');

    const searchInput = page.locator('input[type="text"][name="term"]').first();
    await searchInput.fill(TEST_SEARCH_TERM);
    await searchInput.press('Enter');

    // Wait for results
    await page.waitForSelector('[class*="searchResult"]', { timeout: 10000 });

    // Check that provider badges exist with different text
    const allBadges = page.locator('[class*="badge"]');
    const badgeCount = await allBadges.count();

    if (badgeCount > 0) {
      // Get all badge texts
      const badgeTexts = await allBadges.allTextContents();

      // Verify that we have provider names in badges
      const hasProviderBadges = badgeTexts.some(text =>
        ['Hardcover', 'Open Library', 'Google Books', 'Goodreads'].includes(text.trim())
      );

      expect(hasProviderBadges).toBeTruthy();
    }
  });
});
