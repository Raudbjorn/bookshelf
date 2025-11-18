/**
 * Provider Caching E2E Test
 *
 * Tests the provider caching functionality including:
 * - Repeated searches return consistent results
 * - Cache improves response times
 * - Different search terms don't return cached results incorrectly
 */

const { test, expect } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';
const API_KEY = process.env.API_KEY || '85890af3b929408e88f0e1b6e38e2b43';

test.describe('Provider Caching', () => {
  test('GoogleBooks repeated searches return consistent results', async ({ request }) => {
    const searchTerm = 'sanderson';
    const endpoint = `${BASE_URL}/api/v1/search/provider/googlebooks?term=${searchTerm}`;

    // First search (cache miss)
    const firstResponse = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(firstResponse.ok()).toBeTruthy();
    const firstResults = await firstResponse.json();
    expect(firstResults.length).toBeGreaterThan(0);

    // Second search (cache hit)
    const secondResponse = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(secondResponse.ok()).toBeTruthy();
    const secondResults = await secondResponse.json();

    // Results should be consistent
    expect(secondResults.length).toBe(firstResults.length);
    expect(secondResults[0].foreignId).toBe(firstResults[0].foreignId);
    expect(secondResults[0].title).toBe(firstResults[0].title);
  });

  test('Open Library repeated searches return consistent results', async ({ request }) => {
    const searchTerm = 'tolkien';
    const endpoint = `${BASE_URL}/api/v1/search/provider/openlibrary?term=${searchTerm}`;

    // First search (cache miss)
    const firstResponse = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(firstResponse.ok()).toBeTruthy();
    const firstResults = await firstResponse.json();
    expect(firstResults.length).toBeGreaterThan(0);

    // Second search (cache hit)
    const secondResponse = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(secondResponse.ok()).toBeTruthy();
    const secondResults = await secondResponse.json();

    // Results should be consistent
    expect(secondResults.length).toBe(firstResults.length);
    expect(secondResults[0].foreignId).toBe(firstResults[0].foreignId);
  });

  test('Different search terms return different results (no cross-contamination)', async ({ request }) => {
    const searchTerm1 = 'sanderson';
    const searchTerm2 = 'tolkien';

    // Search for sanderson
    const response1 = await request.get(
      `${BASE_URL}/api/v1/search/provider/googlebooks?term=${searchTerm1}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );
    expect(response1.ok()).toBeTruthy();
    const results1 = await response1.json();

    // Search for tolkien
    const response2 = await request.get(
      `${BASE_URL}/api/v1/search/provider/googlebooks?term=${searchTerm2}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );
    expect(response2.ok()).toBeTruthy();
    const results2 = await response2.json();

    // Results should be different
    expect(results1[0].foreignId).not.toBe(results2[0].foreignId);
    expect(results1[0].title.toLowerCase()).toContain('sanderson');
    expect(results2[0].title.toLowerCase()).toContain('tolkien');
  });

  test('Cache improves response time for GoogleBooks', async ({ request }) => {
    const searchTerm = 'rowling';
    const endpoint = `${BASE_URL}/api/v1/search/provider/googlebooks?term=${searchTerm}`;

    // Measure first request time (cache miss)
    const start1 = Date.now();
    const firstResponse = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });
    const duration1 = Date.now() - start1;

    expect(firstResponse.ok()).toBeTruthy();
    const firstResults = await firstResponse.json();
    expect(firstResults.length).toBeGreaterThan(0);

    // Measure second request time (cache hit)
    const start2 = Date.now();
    const secondResponse = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });
    const duration2 = Date.now() - start2;

    expect(secondResponse.ok()).toBeTruthy();
    const secondResults = await secondResponse.json();

    // Results should be identical
    expect(JSON.stringify(secondResults)).toBe(JSON.stringify(firstResults));

    // Second request should be faster or similar (cached)
    // Note: We don't assert it's always faster because network/system variance
    // but we log it for observation
    console.log(`First request: ${duration1}ms, Second request (cached): ${duration2}ms`);
    expect(duration2).toBeLessThanOrEqual(duration1 + 100); // Allow 100ms variance
  });

  test('Cache respects different providers independently', async ({ request }) => {
    const searchTerm = 'asimov';

    // Search GoogleBooks
    const googleResponse = await request.get(
      `${BASE_URL}/api/v1/search/provider/googlebooks?term=${searchTerm}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );
    expect(googleResponse.ok()).toBeTruthy();
    const googleResults = await googleResponse.json();

    // Search OpenLibrary
    const openLibraryResponse = await request.get(
      `${BASE_URL}/api/v1/search/provider/openlibrary?term=${searchTerm}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );
    expect(openLibraryResponse.ok()).toBeTruthy();
    const openLibraryResults = await openLibraryResponse.json();

    // Results should exist from both providers
    expect(googleResults.length).toBeGreaterThan(0);
    expect(openLibraryResults.length).toBeGreaterThan(0);

    // Foreign IDs should have different prefixes
    expect(googleResults[0].foreignId).toContain('googlebooks:');
    expect(openLibraryResults[0].foreignId).toContain('openlibrary:');
  });
});
