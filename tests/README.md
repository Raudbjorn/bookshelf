# Bookshelf E2E Tests

This directory contains end-to-end tests for Bookshelf using Playwright.

## Setup

Install Playwright and browsers:

```bash
yarn install
npx playwright install
```

## Running Tests

### Run all E2E tests
```bash
yarn test:e2e
```

### Run tests in UI mode (interactive)
```bash
yarn test:e2e:ui
```

### Run tests in headed mode (see browser)
```bash
yarn test:e2e:headed
```

### Run tests in debug mode
```bash
yarn test:e2e:debug
```

### Run specific test file
```bash
npx playwright test tests/e2e/happy-path-ui.spec.js
```

### Run with custom BASE_URL
```bash
BASE_URL=http://localhost:8787 yarn test:e2e
```

## Test Files

### Production Tests

- **`happy-path-ui.spec.js`** - Full user journey test (search, add, remove)
  - Tests the default search provider
  - Adds an author to library
  - Verifies author appears in library
  - Removes author from library

- **`happy-path-googlebooks.spec.js`** - GoogleBooks provider-specific test
  - Tests GoogleBooks metadata provider specifically
  - Same workflow as happy-path-ui but validates GoogleBooks integration

### Debug/Development Tests

- **`debug-search-response.spec.js`** - Captures search API response structure
  - Useful for debugging search API responses
  - Logs response structure and keys
  - Saves screenshot for visual inspection

- **`debug-author-details.spec.js`** - Captures author details page structure
  - Useful for debugging author detail page
  - Lists all buttons and their attributes
  - Saves screenshot for visual inspection

## Configuration

Test configuration is in `playwright.config.js` at the repository root.

Key settings:
- Base URL: `http://127.0.0.1:8787` (configurable via `BASE_URL` env var)
- Retries: 2 on CI, 0 locally
- Screenshot: On failure
- Video: On failure

## Environment Variables

- `BASE_URL` - Bookshelf instance URL (default: `http://127.0.0.1:8787`)
- `CI` - Set to enable CI mode (more retries, parallel execution)

## Troubleshooting

### Tests fail with "element not found"

The UI may have changed. Check screenshots in `test-results/` directory.

### Connection refused errors

Make sure Bookshelf is running:
```bash
systemctl status bookshelf
```

Or start manually:
```bash
cd /var/lib/bookshelf
./Readarr
```

### Screenshots location

Failed test screenshots are saved to:
- `/tmp/` for debug scripts
- `test-results/` for regular test failures

## CI Integration

To run in CI:

```bash
CI=1 yarn test:e2e
```

This enables:
- Retry on failure (2 attempts)
- Stricter mode (fails on test.only)
- Sequential execution
