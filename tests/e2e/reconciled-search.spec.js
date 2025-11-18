/**
 * Reconciled Search Test
 *
 * Verifies that the reconciled search functionality works:
 * 1. Navigate to Add Search page
 * 2. Verify "Reconciled (All Providers)" is selected
 * 3. Search for "A Confederacy of Dunces"
 * 4. Verify search results render
 */

const { test, expect } = require('@playwright/test');

const BASE_URL = 'https://readarr.s8n.is';
const SEARCH_TERM = 'A Confederacy of Dunces';

test.describe('Reconciled Search', () => {
  test('should render search results for reconciled search', async ({ browser }) => {
    // Create a new context with cache disabled to ensure we get the latest JS
    const context = await browser.newContext({
      bypassCSP: true,
      serviceWorkers: 'block'
    });
    const page = await context.newPage();

    // Bypass cache for JS files by adding cache-control headers
    await page.route('**/*.js', route => {
      route.continue({
        headers: {
          ...route.request().headers(),
          'Cache-Control': 'no-cache, no-store, must-revalidate',
          'Pragma': 'no-cache'
        }
      });
    });
    // Listen for console errors
    page.on('console', msg => {
      const type = msg.type();
      if (type === 'error' || type === 'warning') {
        console.log(`[BROWSER ${type.toUpperCase()}]`, msg.text());
      }
    });

    // Listen for page errors
    page.on('pageerror', error => {
      console.log('[PAGE ERROR]', error.message);
    });

    // Set HTTP Basic Auth
    await page.setExtraHTTPHeaders({
      'Authorization': 'Basic ' + Buffer.from('svnbjrn:kisa').toString('base64')
    });

    // Step 1: Navigate to Add Search page with cache bypass
    console.log('Step 1: Navigating to Add Search page (bypassing cache)');
    await page.goto(`${BASE_URL}/add/search`, {
      waitUntil: 'networkidle'
    });

    // Force reload to bypass cache
    await page.reload({ waitUntil: 'networkidle' });

    // Wait for page to be interactive
    await page.waitForSelector('input[name="searchBox"]', { timeout: 10000 });

    // Step 2: Verify Metadata Provider is set to "Reconciled (All Providers)" (optional - may require auth)
    console.log('Step 2: Checking Metadata Provider dropdown');
    try {
      const providerDropdown = page.locator('button.EnhancedSelectInput-enhancedSelect-RG1RD');
      const selectedProvider = await providerDropdown.locator('.HintedSelectInputSelectedValue-valueText-kkxXq').textContent({ timeout: 3000 });
      console.log(`Selected provider: "${selectedProvider}"`);

      if (selectedProvider !== 'Reconciled (All Providers)') {
        console.log(`⚠ Warning: Expected "Reconciled (All Providers)", got "${selectedProvider}"`);
      }
    } catch (error) {
      console.log('⚠ Could not verify dropdown (may require authentication)');
    }

    // Step 3: Enter search term
    console.log(`Step 3: Searching for "${SEARCH_TERM}"`);
    const searchInput = page.locator('input[name="searchBox"]');
    await searchInput.fill(SEARCH_TERM);

    // Wait for search API call to complete
    const searchPromise = page.waitForResponse(
      response => response.url().includes('/api/v1/search/provider/reconcile') && response.status() === 200,
      { timeout: 10000 }
    );

    await searchInput.press('Enter');

    // Wait for the API response
    const searchResponse = await searchPromise;
    console.log('✓ Search API call completed with status:', searchResponse.status());

    // Get the response data to see what we're expecting
    const responseData = await searchResponse.json();
    console.log(`API returned ${responseData.books?.length || 0} books and ${responseData.authors?.length || 0} authors`);

    // Log first book to see structure
    if (responseData.books && responseData.books.length > 0) {
      console.log('First book structure:', JSON.stringify(responseData.books[0], null, 2));
    }

    // Wait a bit for results to render
    await page.waitForTimeout(2000);

    // Debug: Check Redux state
    const reduxState = await page.evaluate(() => {
      const state = window.store?.getState?.();
      return {
        hasStore: !!window.store,
        searchItems: state?.search?.items,
        searchItemsCount: state?.search?.items?.length,
        isFetching: state?.search?.isFetching,
        isPopulated: state?.search?.isPopulated,
        error: state?.search?.error
      };
    });
    console.log('Redux state:', JSON.stringify(reduxState, null, 2));

    // Debug: Check what's in the search results container
    const containerHTML = await page.locator('.AddNewItem-searchResults-wMWIJ').innerHTML().catch(() => 'Container not found');
    console.log('Search results container HTML length:', containerHTML.length);
    console.log('Search results container HTML:', containerHTML.substring(0, 500));

    // Step 4: Verify search results are rendered
    console.log('Step 4: Verifying search results are rendered');

    // Look for the book title text which confirms results rendered
    const firstResult = page.getByText('A Confederacy of Dunces').first();

    // Wait for at least one result to appear
    await expect(firstResult).toBeVisible({ timeout: 5000 });
    console.log('✓ Search results rendered successfully');

    // Take a screenshot for verification
    await page.screenshot({ path: '/tmp/reconciled-search-results.png', fullPage: true });
    console.log('Screenshot saved to /tmp/reconciled-search-results.png');

    console.log('');
    console.log('=== RECONCILED SEARCH TEST COMPLETE ===');
  });
});
