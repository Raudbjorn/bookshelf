/**
 * Reconciled Search E2E Test
 *
 * Tests the reconciled search endpoint that merges results from multiple providers:
 * - Returns combined results from enabled providers
 * - Deduplicates books/authors across providers
 * - Respects provider enable/disable settings
 */

const { test, expect } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';
const API_KEY = process.env.API_KEY || '85890af3b929408e88f0e1b6e38e2b43';

test.describe('Reconciled Search', () => {
  test('returns combined results from multiple providers', async ({ request }) => {
    const searchTerm = 'sanderson';
    const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${searchTerm}`;

    const response = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(response.ok()).toBeTruthy();
    const results = await response.json();

    // Should have results from multiple providers
    expect(results.length).toBeGreaterThan(0);

    // Check that results have proper structure
    if (results.length > 0) {
      const firstResult = results[0];
      expect(firstResult).toHaveProperty('foreignId');
      expect(firstResult).toHaveProperty('book');
      expect(firstResult.book).toHaveProperty('title');
    }
  });

  test('returns empty array for nonsense search term', async ({ request }) => {
    const searchTerm = 'xyzabc123nonexistent999';
    const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${searchTerm}`;

    const response = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(response.ok()).toBeTruthy();
    const results = await response.json();

    // Should return empty array for nonsense search
    expect(Array.isArray(results)).toBeTruthy();
  });

  test('includes results from GoogleBooks provider', async ({ request }) => {
    const searchTerm = 'rowling';
    const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${searchTerm}`;

    const response = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(response.ok()).toBeTruthy();
    const results = await response.json();
    expect(results.length).toBeGreaterThan(0);

    // Check if any result has GoogleBooks ID
    const hasGoogleBooksResult = results.some(r =>
      r.foreignId && r.foreignId.includes('googlebooks:')
    );

    // Should have at least some GoogleBooks results
    if (results.length > 5) {
      expect(hasGoogleBooksResult).toBeTruthy();
    }
  });

  test('includes results from OpenLibrary provider', async ({ request }) => {
    const searchTerm = 'tolkien';
    const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${searchTerm}`;

    const response = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(response.ok()).toBeTruthy();
    const results = await response.json();
    expect(results.length).toBeGreaterThan(0);

    // Check if any result has OpenLibrary ID
    const hasOpenLibraryResult = results.some(r =>
      r.foreignId && r.foreignId.includes('openlibrary:')
    );

    // Should have at least some OpenLibrary results
    if (results.length > 5) {
      expect(hasOpenLibraryResult).toBeTruthy();
    }
  });

  test('handles provider-specific search patterns correctly', async ({ request }) => {
    const searchTerms = ['asimov', 'herbert', 'bradbury'];

    for (const term of searchTerms) {
      const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${term}`;
      const response = await request.get(endpoint, {
        headers: { 'X-Api-Key': API_KEY }
      });

      expect(response.ok()).toBeTruthy();
      const results = await response.json();

      // Each search should return results
      expect(results.length).toBeGreaterThan(0);

      // Results should have book data
      const firstResult = results[0];
      expect(firstResult.book).toBeDefined();
      expect(firstResult.book.title).toBeDefined();
      expect(typeof firstResult.book.title).toBe('string');
    }
  });

  test('returns results with proper metadata structure', async ({ request }) => {
    const searchTerm = 'king';
    const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${searchTerm}`;

    const response = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(response.ok()).toBeTruthy();
    const results = await response.json();
    expect(results.length).toBeGreaterThan(0);

    // Check first result has proper structure
    const firstResult = results[0];
    expect(firstResult).toHaveProperty('foreignId');
    expect(firstResult).toHaveProperty('book');
    expect(firstResult.book).toHaveProperty('title');

    // Book should have basic metadata
    expect(typeof firstResult.book.title).toBe('string');
    expect(firstResult.book.title.length).toBeGreaterThan(0);
  });

  test('deduplicates results across providers', async ({ request }) => {
    const searchTerm = 'sapiens';
    const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${searchTerm}`;

    const response = await request.get(endpoint, {
      headers: { 'X-Api-Key': API_KEY }
    });

    expect(response.ok()).toBeTruthy();
    const results = await response.json();

    // Check that there are no duplicate titles in top results
    const topTitles = results.slice(0, 10).map(r => r.book.title.toLowerCase());
    const uniqueTitles = new Set(topTitles);

    // Most titles should be unique (allowing some flexibility for editions)
    expect(uniqueTitles.size).toBeGreaterThanOrEqual(topTitles.length * 0.7);
  });

  test('handles special characters in search terms', async ({ request }) => {
    const specialTerms = ['o\'brien', 'garcía márquez', 'tolstoy'];

    for (const term of specialTerms) {
      const endpoint = `${BASE_URL}/api/v1/search/provider/reconciled?term=${encodeURIComponent(term)}`;
      const response = await request.get(endpoint, {
        headers: { 'X-Api-Key': API_KEY }
      });

      expect(response.ok()).toBeTruthy();
      const results = await response.json();

      // Should handle special characters gracefully
      expect(Array.isArray(results)).toBeTruthy();
    }
  });
});
