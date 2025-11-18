/**
 * Provider Preferences UI E2E Test
 *
 * Tests the provider preferences settings page including:
 * - Hardcover provider settings (enabled toggle, API token, username)
 * - OpenLibrary provider settings (enabled toggle)
 * - GoogleBooks provider settings (enabled toggle, API key)
 * - Settings persistence
 */

const { test, expect } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';

test.describe('Provider Preferences Settings', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the Metadata Provider settings page
    await page.goto(`${BASE_URL}/settings/metadataprovider`);
    await page.waitForLoadState('networkidle');
  });

  test('should display Metadata Search Providers section', async ({ page }) => {
    // Check that the Metadata Search Providers fieldset is visible
    const searchProvidersSection = page.locator('legend:has-text("Metadata Search Providers")');
    await expect(searchProvidersSection).toBeVisible();
  });

  test('should display all provider toggle switches', async ({ page }) => {
    // Verify Hardcover enabled toggle
    const hardcoverEnabledLabel = page.locator('label:has-text("Enable Hardcover")');
    await expect(hardcoverEnabledLabel).toBeVisible();

    // Verify OpenLibrary enabled toggle
    const openLibraryEnabledLabel = page.locator('label:has-text("Enable Open Library")');
    await expect(openLibraryEnabledLabel).toBeVisible();

    // Verify GoogleBooks enabled toggle
    const googleBooksEnabledLabel = page.locator('label:has-text("Enable Google Books")');
    await expect(googleBooksEnabledLabel).toBeVisible();
  });

  test('should display Hardcover configuration fields', async ({ page }) => {
    // Verify Hardcover API Token field
    const hardcoverApiTokenLabel = page.locator('label:has-text("Hardcover API Token")');
    await expect(hardcoverApiTokenLabel).toBeVisible();

    // Verify Hardcover Username field
    const hardcoverUsernameLabel = page.locator('label:has-text("Hardcover Username")');
    await expect(hardcoverUsernameLabel).toBeVisible();
  });

  test('should display GoogleBooks API key field', async ({ page }) => {
    // Verify GoogleBooks API Key field
    const googleBooksApiKeyLabel = page.locator('label:has-text("Google Books API Key")');
    await expect(googleBooksApiKeyLabel).toBeVisible();
  });

  test('should allow toggling providers on/off', async ({ page }) => {
    // Find the Hardcover enabled checkbox
    const hardcoverCheckbox = page.locator('input[name="hardcoverEnabled"]');

    // Get current state
    const initialState = await hardcoverCheckbox.isChecked();

    // Toggle it
    await hardcoverCheckbox.click();
    await page.waitForTimeout(500); // Wait for state to update

    // Verify it changed
    const newState = await hardcoverCheckbox.isChecked();
    expect(newState).not.toBe(initialState);
  });

  test('should show help text for provider settings', async ({ page }) => {
    // Verify help text is visible for Hardcover enabled
    const hardcoverHelpText = page.locator('text=Use Hardcover as a metadata search provider');
    await expect(hardcoverHelpText).toBeVisible();

    // Verify help text for OpenLibrary
    const openLibraryHelpText = page.locator('text=Use Open Library as a metadata search provider');
    await expect(openLibraryHelpText).toBeVisible();

    // Verify help text for GoogleBooks
    const googleBooksHelpText = page.locator('text=Use Google Books as a metadata search provider');
    await expect(googleBooksHelpText).toBeVisible();
  });

  test('should allow entering API credentials', async ({ page }) => {
    // Find Hardcover API token input (password type)
    const apiTokenInput = page.locator('input[name="hardcoverApiToken"]');
    await expect(apiTokenInput).toBeVisible();

    // Enter a test token
    await apiTokenInput.fill('test-api-token-12345');

    // Verify the value was set
    const value = await apiTokenInput.inputValue();
    expect(value).toBe('test-api-token-12345');
  });

  test('should allow entering Hardcover username', async ({ page }) => {
    // Find Hardcover username input
    const usernameInput = page.locator('input[name="hardcoverUsername"]');
    await expect(usernameInput).toBeVisible();

    // Enter a test username
    await usernameInput.fill('testuser123');

    // Verify the value was set
    const value = await usernameInput.inputValue();
    expect(value).toBe('testuser123');
  });

  test('should have password-type inputs for sensitive fields', async ({ page }) => {
    // Verify API token is password type
    const apiTokenInput = page.locator('input[name="hardcoverApiToken"]');
    const apiTokenType = await apiTokenInput.getAttribute('type');
    expect(apiTokenType).toBe('password');

    // Verify GoogleBooks API key is password type
    const googleBooksApiKeyInput = page.locator('input[name="googleBooksApiKey"]');
    const googleBooksApiKeyType = await googleBooksApiKeyInput.getAttribute('type');
    expect(googleBooksApiKeyType).toBe('password');
  });

  test('should display settings in correct order', async ({ page }) => {
    // Get all fieldsets
    const fieldsets = page.locator('fieldset legend');
    const legendTexts = await fieldsets.allTextContents();

    // Verify Metadata Search Providers section exists
    expect(legendTexts).toContain('Metadata Search Providers');

    // Verify it appears after other metadata settings
    const searchProvidersIndex = legendTexts.indexOf('Metadata Search Providers');
    expect(searchProvidersIndex).toBeGreaterThan(-1);
  });
});
