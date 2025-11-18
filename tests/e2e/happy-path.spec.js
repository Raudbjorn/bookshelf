/**
 * Happy Path E2E Test
 *
 * Tests the complete user journey:
 * 1. Search for a book
 * 2. Add the author/book to library
 * 3. Verify it's in the library
 * 4. Remove it from library
 * 5. Verify it's gone
 */

const { test, expect } = require('@playwright/test');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:8787';
const API_KEY = process.env.API_KEY || '85890af3b929408e88f0e1b6e38e2b43';

test.describe('Happy Path: Search, Add, Remove', () => {
  const TEST_AUTHOR = 'Isaac Asimov';
  let addedAuthorId = null;
  let addedBookId = null;

  test('complete user journey', async ({ request }) => {
    // Step 1: Search for a book
    console.log('Step 1: Searching for books by', TEST_AUTHOR);
    const searchResponse = await request.get(
      `${BASE_URL}/api/v1/search/provider/googlebooks?term=${encodeURIComponent(TEST_AUTHOR)}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );

    expect(searchResponse.ok()).toBeTruthy();
    const searchResults = await searchResponse.json();
    expect(searchResults.length).toBeGreaterThan(0);

    const firstResult = searchResults[0];
    console.log('Found book:', firstResult.book.title);
    console.log('Author:', firstResult.book.author.authorName);

    // Step 2: Get author lookup data
    console.log('Step 2: Looking up author metadata');
    const authorName = firstResult.book.author.authorName;
    const lookupResponse = await request.get(
      `${BASE_URL}/api/v1/search?term=${encodeURIComponent(authorName)}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );

    expect(lookupResponse.ok()).toBeTruthy();
    const authorResults = await lookupResponse.json();
    expect(authorResults.length).toBeGreaterThan(0);

    const authorData = authorResults[0];
    console.log('Found author:', authorData.authorName);
    console.log('Books:', authorData.books?.length || 0);

    // Step 3: Add author to library
    console.log('Step 3: Adding author to library');

    // Prepare author data for adding
    const addAuthorPayload = {
      ...authorData,
      monitored: true,
      qualityProfileId: 1,
      metadataProfileId: 1,
      rootFolderPath: '/mnt/mrgr/media/books/ebooks',
      addOptions: {
        monitor: 'all',
        searchForMissingBooks: false
      }
    };

    const addResponse = await request.post(
      `${BASE_URL}/api/v1/author`,
      {
        headers: {
          'X-Api-Key': API_KEY,
          'Content-Type': 'application/json'
        },
        data: addAuthorPayload
      }
    );

    if (!addResponse.ok()) {
      const errorText = await addResponse.text();
      console.log('Add author failed:', errorText);
    }

    expect(addResponse.ok()).toBeTruthy();
    const addedAuthor = await addResponse.json();
    addedAuthorId = addedAuthor.id;

    console.log('Added author with ID:', addedAuthorId);

    // Step 4: Verify author is in library
    console.log('Step 4: Verifying author is in library');

    const getAuthorResponse = await request.get(
      `${BASE_URL}/api/v1/author/${addedAuthorId}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );

    expect(getAuthorResponse.ok()).toBeTruthy();
    const retrievedAuthor = await getAuthorResponse.json();
    expect(retrievedAuthor.id).toBe(addedAuthorId);
    expect(retrievedAuthor.authorName).toContain(TEST_AUTHOR);

    console.log('✓ Author verified in library');

    // Step 5: Get books for this author
    console.log('Step 5: Getting books for author');

    const getBooksResponse = await request.get(
      `${BASE_URL}/api/v1/book?authorId=${addedAuthorId}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );

    expect(getBooksResponse.ok()).toBeTruthy();
    const authorBooks = await getBooksResponse.json();

    if (Array.isArray(authorBooks) && authorBooks.length > 0) {
      addedBookId = authorBooks[0].id;
      console.log('Found', authorBooks.length, 'books');
      console.log('First book:', authorBooks[0].title);
    } else if (authorBooks.records && authorBooks.records.length > 0) {
      addedBookId = authorBooks.records[0].id;
      console.log('Found', authorBooks.records.length, 'books');
      console.log('First book:', authorBooks.records[0].title);
    }

    // Step 6: Remove author from library
    console.log('Step 6: Removing author from library');

    const deleteResponse = await request.delete(
      `${BASE_URL}/api/v1/author/${addedAuthorId}?deleteFiles=true`,
      { headers: { 'X-Api-Key': API_KEY } }
    );

    expect(deleteResponse.ok()).toBeTruthy();
    console.log('✓ Author deleted');

    // Step 7: Verify author is gone
    console.log('Step 7: Verifying author is removed');

    const verifyDeleteResponse = await request.get(
      `${BASE_URL}/api/v1/author/${addedAuthorId}`,
      { headers: { 'X-Api-Key': API_KEY } }
    );

    // Should return 404 or error
    expect(verifyDeleteResponse.ok()).toBeFalsy();
    console.log('✓ Author confirmed removed');

    console.log('');
    console.log('=== HAPPY PATH TEST COMPLETE ===');
    console.log('✓ Searched for books');
    console.log('✓ Added author to library');
    console.log('✓ Verified author in library');
    console.log('✓ Removed author from library');
    console.log('✓ Verified author removed');
  });
});
