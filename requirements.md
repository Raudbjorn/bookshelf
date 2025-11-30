# Anna's Archive Provider - Requirements Specification

**Project:** Bookshelf (Readarr fork)
**Feature:** Anna's Archive metadata provider integration
**Version:** 1.0
**Date:** 2025-11-27
**Status:** In Development 🚧

---

## 1. Executive Summary

Add Anna's Archive (https://annas-archive.org) as a metadata provider to enable searching and metadata retrieval from the world's largest open library (165M+ files aggregated from 11+ sources including Libgen, Z-Library, Sci-Hub, Internet Archive, DuXiu, ISBNdb, OpenLibrary, and more).

---

## 2. Background & Context

### 2.1 Problem Statement

Bookshelf currently lacks access to:
- Comprehensive aggregated metadata from multiple shadow libraries
- 165M+ book records across all major sources
- Unified, de-duplicated metadata from 11+ providers
- IPFS-based decentralized download information

### 2.2 Solution Overview

Implement Anna's Archive provider using their official JSON API to:
- Retrieve metadata by MD5 hash
- Aggregate metadata from multiple sources (priority: ISBNdb → Libgen → Z-Library)
- Provide IPFS gateway information for decentralized downloads
- Enable future search functionality via light HTML parsing

### 2.3 Research Findings

**Key Discovery**: Anna's Archive DOES have official data access methods:
- **Direct JSON API**: `https://annas-archive.org/db/aarecord_elasticsearch/md5:{hash}.json.html`
- **Bulk Torrents**: 500GB ElasticSearch + MariaDB dumps available
- **Database Scripts**: Official generation scripts available

This corrects the initial assumption that AA only supports web scraping.

---

## 3. Functional Requirements

### 3.1 Core Functionality (Phase 1 - MVP)

#### FR-1: Metadata Retrieval by MD5
**Priority**: MUST HAVE
**User Story**: As a Bookshelf user, I want to retrieve book metadata using MD5 hash so I can get comprehensive information from all available sources.

**Acceptance Criteria**:
- [ ] System accepts 32-character MD5 hash as input
- [ ] System fetches JSON data from AA API endpoint
- [ ] System deserializes JSON to DTO classes
- [ ] System maps AA metadata to Bookshelf Book/Edition models
- [ ] System returns Book object with aggregated metadata
- [ ] System caches results for 24 hours

**Technical Details**:
- Endpoint: `GET https://annas-archive.org/db/aarecord_elasticsearch/md5:{hash}.json.html`
- Response: JSON with aggregated metadata from multiple sources
- Caching: 24-hour TTL via `ICachedHttpResponseService`

#### FR-2: Metadata Source Aggregation
**Priority**: MUST HAVE
**User Story**: As a user, I want the best available metadata from all sources so I get the most accurate and complete information.

**Acceptance Criteria**:
- [ ] System prioritizes ISBNdb for structured metadata
- [ ] System falls back to Libgen.rs for book metadata
- [ ] System falls back to Z-Library for file metadata
- [ ] System uses file_unified_data as final fallback
- [ ] System combines authors from all available sources
- [ ] System validates and normalizes ISBN-13 numbers

**Metadata Priority Order**:
1. **Title**: ISBNdb → Libgen → Z-Library → file_unified_data
2. **Authors**: ISBNdb (structured) → Libgen → Z-Library → file_unified_data
3. **Publisher**: ISBNdb → Libgen → Z-Library → file_unified_data
4. **ISBN**: ISBNdb → Libgen → Z-Library (with checksum validation)
5. **Description**: ISBNdb → OpenLibrary → Libgen → Z-Library
6. **Year**: ISBNdb → Libgen → Z-Library → file_unified_data

#### FR-3: Foreign ID Format
**Priority**: MUST HAVE
**User Story**: As the system, I need unique identifiers for AA books to prevent conflicts with other providers.

**Acceptance Criteria**:
- [ ] Book foreign ID format: `aa:{md5hash}` (e.g., `aa:8336332bf5877e3adbfb60ac70720cd5`)
- [ ] Edition foreign ID format: `aa:{md5hash}` (same as book)
- [ ] Author foreign ID format: `aa-author:{slug}` (e.g., `aa-author:michele-boldrin`)
- [ ] System can extract MD5 from foreign ID

#### FR-4: IPFS Information Extraction
**Priority**: SHOULD HAVE
**User Story**: As a user, I want IPFS gateway links so I can download books via decentralized networks.

**Acceptance Criteria**:
- [ ] System extracts IPFS CID from `ipfs_infos` array
- [ ] System adds IPFS gateway link to Edition.Links
- [ ] System uses first available IPFS info if multiple exist
- [ ] Format: `https://ipfs.io/ipfs/{cid}`

#### FR-5: Error Handling
**Priority**: MUST HAVE
**User Story**: As a developer, I want clear error messages so I can debug integration issues.

**Acceptance Criteria**:
- [ ] System throws `BookNotFoundException` for 404 responses
- [ ] System throws `AnnasArchiveException` for API errors
- [ ] System logs all errors with context (MD5, error message)
- [ ] System returns empty list for search (not errors) when search not implemented
- [ ] System validates MD5 format before making API calls

### 3.2 Search Functionality (Phase 2 - Future)

#### FR-6: Search by Title/Author
**Priority**: COULD HAVE (Phase 2)
**User Story**: As a user, I want to search by title and author so I can find books without knowing the MD5 hash.

**Acceptance Criteria** (Deferred to Phase 2):
- [ ] System accepts title and optional author parameters
- [ ] System performs HTML scraping of AA search page
- [ ] System extracts MD5 hashes from search results
- [ ] System fetches full metadata for each MD5 using FR-1
- [ ] System returns list of Book objects
- [ ] System caches search results for 2 hours

#### FR-7: Search by ISBN
**Priority**: COULD HAVE (Phase 2)
**User Story**: As a user, I want to search by ISBN so I can find specific editions.

**Acceptance Criteria** (Deferred to Phase 2):
- [ ] System accepts ISBN-10 or ISBN-13
- [ ] System normalizes ISBN (removes hyphens)
- [ ] System performs search using normalized ISBN
- [ ] System returns matching books

#### FR-8: Search by ASIN
**Priority**: WON'T HAVE
**Rationale**: Anna's Archive doesn't track Amazon ASINs

---

## 4. Non-Functional Requirements

### 4.1 Performance

#### NFR-1: Response Time
**Requirement**: Metadata retrieval should complete within 5 seconds under normal conditions.

**Acceptance Criteria**:
- [ ] API calls complete within 3 seconds (95th percentile)
- [ ] Cached responses return within 100ms
- [ ] Timeout set to 30 seconds for slow connections

#### NFR-2: Caching Strategy
**Requirement**: Minimize API calls to AA servers while keeping metadata reasonably fresh.

**Acceptance Criteria**:
- [ ] Metadata cache TTL: 24 hours
- [ ] Search cache TTL: 2 hours (Phase 2)
- [ ] Cache uses HTTP response caching via `ICachedHttpResponseService`
- [ ] Cache key format: URL with MD5 hash

### 4.2 Reliability

#### NFR-3: Error Recovery
**Requirement**: System should gracefully handle API failures.

**Acceptance Criteria**:
- [ ] HTTP 404 → throw `BookNotFoundException`
- [ ] HTTP 429 (rate limit) → throw `AnnasArchiveException` with clear message
- [ ] HTTP 500+ → throw `AnnasArchiveException` with status code
- [ ] Network timeout → throw `AnnasArchiveException` with timeout message
- [ ] Invalid JSON → throw `AnnasArchiveException` with deserialization error

#### NFR-4: Data Validation
**Requirement**: Validate all external data before using it.

**Acceptance Criteria**:
- [ ] MD5 hash format validated (32 hex characters)
- [ ] ISBN-13 checksum validated before use
- [ ] Null checks on all optional fields
- [ ] Empty string checks on required fields
- [ ] Year range validation (1-9999)

### 4.3 Maintainability

#### NFR-5: Code Organization
**Requirement**: Code should follow Bookshelf/Readarr patterns and conventions.

**Acceptance Criteria**:
- [ ] Follows existing provider pattern (see InternetArchiveProxy)
- [ ] Uses `HttpRequestBuilder` for HTTP calls
- [ ] Uses `ICachedHttpResponseService` for caching
- [ ] DTOs in separate `Resources/` subdirectory
- [ ] Custom exceptions in provider namespace
- [ ] Proper NLog logging throughout

#### NFR-6: Testing
**Requirement**: Code should have comprehensive test coverage.

**Acceptance Criteria**:
- [ ] Unit tests for all public methods
- [ ] Unit tests for metadata aggregation logic
- [ ] Unit tests for ISBN validation
- [ ] Unit tests for error handling
- [ ] Integration test with real AA API call
- [ ] Mock tests for all DTO deserialization

### 4.4 Legal & Ethical

#### NFR-7: Metadata Usage
**Requirement**: Use AA only for metadata retrieval (not file downloads).

**Acceptance Criteria**:
- [ ] No download functionality implemented
- [ ] Only metadata URLs stored (IPFS links for reference only)
- [ ] User-Agent identifies as "Bookshelf/1.0"
- [ ] Respects any robots.txt directives (if present)

#### NFR-8: Attribution
**Requirement**: Properly attribute data sources.

**Acceptance Criteria**:
- [ ] Edition.Links includes "Anna's Archive" link
- [ ] Edition.Links includes IPFS link (if available)
- [ ] Comments document data sources in code

---

## 5. Technical Constraints

### 5.1 Platform Requirements

- **Framework**: .NET 6.0
- **Language**: C# 10
- **HTTP Client**: Use existing `IHttpClient` and `ICachedHttpResponseService`
- **JSON**: System.Text.Json (already in use)
- **Logging**: NLog (already in use)

### 5.2 Integration Requirements

- **Interfaces**: Implement `ISearchForNewBook` and `IProvideBookInfo`
- **Dependency Injection**: Use DryIoc (auto-scanning)
- **Database**: No direct database access required (uses existing models)

### 5.3 API Constraints

- **No Authentication**: AA JSON API doesn't require auth
- **No Rate Limits**: AA doesn't enforce rate limits (but be respectful)
- **No Search API**: Search requires HTML parsing (Phase 2)
- **HTTPS Only**: Use HTTPS for all API calls

---

## 6. Data Models

### 6.1 Input Models (DTOs)

**AARecord** - Main aggregated record from Anna's Archive
```csharp
public class AARecord
{
    string Id;                              // "md5:hash"
    AAFileUnifiedData FileUnifiedData;      // Aggregated metadata
    List<AAIPFSInfo> IpfsInfos;            // IPFS download info
    AALibgenBook LibgenNonFiction;          // Libgen.rs non-fiction
    AALibgenBook LibgenFiction;             // Libgen.rs fiction
    AAZLibBook ZLibrary;                    // Z-Library data
    AAIsbndb IsbnDb;                        // ISBNdb metadata
    AAOpenLibrary OpenLibrary;              // OpenLibrary metadata
    AAInternetArchive InternetArchive;      // IA metadata
}
```

**AAFileUnifiedData** - Most reliable aggregated metadata
```csharp
public class AAFileUnifiedData
{
    string Title;
    string Author;
    string Publisher;
    string Year;
    string Language;
    long? Filesize;
    string Extension;
    string Md5;
    string Description;
}
```

### 6.2 Output Models (Bookshelf)

**Book** - Main book entity
```csharp
public class Book
{
    string ForeignBookId;      // "aa:{md5}"
    string Title;
    string TitleSlug;          // MD5 hash
    string CleanTitle;
    DateTime? ReleaseDate;
    List<Links> Links;
    List<string> Genres;
    Ratings Ratings;
    List<Edition> Editions;
    bool AnyEditionOk;
}
```

**Edition** - Book edition/format
```csharp
public class Edition
{
    string ForeignEditionId;   // "aa:{md5}"
    string TitleSlug;          // MD5 hash
    string Title;
    string Publisher;
    DateTime? ReleaseDate;
    string Overview;
    string Language;
    string Isbn13;
    string Format;             // Extension (PDF, EPUB, etc.)
    int PageCount;
    bool Monitored;
    List<Links> Links;
    List<MediaCover> Images;
}
```

**AuthorMetadata** - Author information
```csharp
public class AuthorMetadata
{
    string ForeignAuthorId;    // "aa-author:{slug}"
    string Name;
}
```

---

## 7. Success Criteria

### 7.1 Phase 1 (MVP) Success Criteria

✅ **Must Achieve** (all required):
1. Build succeeds with 0 errors, 0 warnings
2. `GetBookInfo(foreignBookId)` works with valid AA foreign ID
3. `GetBookByMd5(md5)` fetches real metadata from AA API
4. Metadata correctly maps to Book/Edition/AuthorMetadata models
5. ISBN-13 validation works correctly
6. IPFS links extracted and added to Edition.Links
7. Errors handled gracefully with appropriate exceptions
8. All unit tests pass

### 7.2 Phase 2 (Search) Success Criteria

✅ **Could Achieve** (optional for future):
1. `SearchForNewBook(title, author)` returns results
2. HTML parsing extracts MD5 hashes from search page
3. Search caching works (2-hour TTL)
4. `SearchByIsbn(isbn)` returns results

---

## 8. Out of Scope

The following are explicitly OUT OF SCOPE for this implementation:

❌ **Not Included**:
1. File downloading functionality
2. Local metadata mirror support (Tier 3)
3. Bulk torrent integration
4. ElasticSearch/MariaDB local database
5. Direct integration with Z-Library API (use AA aggregation instead)
6. User authentication/accounts
7. Download quota tracking
8. Rate limiting (AA doesn't enforce limits)
9. Goodreads ID mapping (AA doesn't have this)

---

## 9. Dependencies

### 9.1 External Dependencies

- **Anna's Archive JSON API**: `https://annas-archive.org/db/aarecord_elasticsearch/md5:{hash}.json.html`
- **IPFS Gateway**: `https://ipfs.io/ipfs/{cid}` (for links only)

### 9.2 Internal Dependencies

- `IHttpClient` - HTTP request execution
- `ICachedHttpResponseService` - Response caching
- `ISearchForNewBook` - Search interface
- `IProvideBookInfo` - Metadata retrieval interface
- Book/Edition/AuthorMetadata models
- NLog Logger

### 9.3 Development Dependencies

- .NET 6.0 SDK
- NUnit (for tests)
- Moq (for mocking)

---

## 10. Risks & Mitigations

### Risk 1: API Changes
**Likelihood**: Medium
**Impact**: High
**Mitigation**:
- Monitor AA for API changes
- Use official dataset documentation as reference
- Add integration tests to detect breaking changes

### Risk 2: JSON Structure Changes
**Likelihood**: Low
**Impact**: Medium
**Mitigation**:
- Use nullable types for all optional fields
- Graceful degradation if fields missing
- Comprehensive null checking

### Risk 3: AA Service Downtime
**Likelihood**: Low
**Impact**: Medium
**Mitigation**:
- Use as optional provider (not primary)
- Cache aggressively (24-hour TTL)
- Graceful error handling

### Risk 4: Legal Concerns
**Likelihood**: Low
**Impact**: High
**Mitigation**:
- Use only for metadata (not downloads)
- Make provider opt-in
- Add legal disclaimers
- Document fair use justification

---

## 11. Acceptance Testing

### Test Case 1: Valid MD5 Retrieval
```
Given: Valid MD5 hash "8336332bf5877e3adbfb60ac70720cd5"
When: Call GetBookByMd5(md5)
Then:
  - Returns Book object
  - Book.Title = "Against intellectual monopoly"
  - Book.ForeignBookId = "aa:8336332bf5877e3adbfb60ac70720cd5"
  - Edition.Isbn13 is valid ISBN-13
  - Edition.Links contains AA link
```

### Test Case 2: Invalid MD5
```
Given: Invalid MD5 hash "invalid"
When: Call GetBookByMd5("invalid")
Then: Throws AnnasArchiveException with "Invalid MD5 hash" message
```

### Test Case 3: Not Found
```
Given: Valid but non-existent MD5 "00000000000000000000000000000000"
When: Call GetBookByMd5(md5)
Then: Throws BookNotFoundException
```

### Test Case 4: Metadata Aggregation
```
Given: Book with metadata in ISBNdb, Libgen, and Z-Library
When: Call GetBookByMd5(md5)
Then:
  - Title comes from ISBNdb (highest priority)
  - Publisher comes from ISBNdb
  - Authors include all from ISBNdb (structured list)
  - ISBN-13 validated with checksum
```

---

## 12. Timeline

**Phase 1 (MVP)**:
- Requirements & Design: 2 hours ✅
- Fix compilation errors: 3-4 hours ⏳
- Testing & refinement: 2-3 hours
- Documentation: 1 hour
- **Total**: ~10 hours

**Phase 2 (Search)**:
- HTML parsing research: 2 hours
- Search implementation: 6-8 hours
- Testing: 2-3 hours
- **Total**: ~12 hours (future work)

---

## 13. Approval

**Requirements Approved By**: [Pending]
**Date**: [Pending]

---

**Document Version**: 1.0
**Last Updated**: 2025-11-27
**Author**: Claude Code (AI Assistant)
