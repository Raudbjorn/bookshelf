# Anna's Archive Provider - Design Document

**Project:** Bookshelf (Readarr fork)
**Feature:** Anna's Archive metadata provider integration
**Version:** 1.0
**Date:** 2025-11-27
**Status:** In Development 🚧

---

## 1. Architecture Overview

### 1.1 System Context

```
┌─────────────────────────────────────────────────────────────┐
│                      Bookshelf System                        │
│  ┌──────────────────────────────────────────────────────┐  │
│  │          Metadata Provider Layer                      │  │
│  │  ┌────────────┐  ┌──────────────┐  ┌──────────────┐ │  │
│  │  │ Goodreads  │  │    Internet  │  │    Anna's    │ │  │
│  │  │  Provider  │  │    Archive   │  │   Archive    │ │  │
│  │  └────────────┘  └──────────────┘  └──────────────┘ │  │
│  └──────────────────────────────────────────────────────┘  │
│                          ↓                                   │
│  ┌──────────────────────────────────────────────────────┐  │
│  │            Book/Edition/Author Models                 │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                          ↓
         ┌────────────────────────────────────┐
         │   Anna's Archive JSON API          │
         │   https://annas-archive.org/       │
         │   /db/aarecord_elasticsearch/      │
         │   md5:{hash}.json.html             │
         └────────────────────────────────────┘
```

### 1.2 Component Diagram

```
┌──────────────────────────────────────────────────────────────┐
│                AnnasArchiveProxy                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  ISearchForNewBook                                      │ │
│  │  - SearchForNewBook(title, author) [Phase 2]           │ │
│  │  - SearchByIsbn(isbn) [Phase 2]                        │ │
│  │  - SearchByAsin(asin) [Not Supported]                  │ │
│  │  - SearchByGoodreadsBookId(id) [Not Supported]         │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  IProvideBookInfo                                       │ │
│  │  - GetBookInfo(foreignBookId) → Tuple<...>            │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Core Methods                                           │ │
│  │  - GetBookByMd5(md5) → Book                           │ │
│  │  - MapRecordToBook(AARecord) → Book                   │ │
│  │  - CreateEdition(AARecord) → Edition                  │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Metadata Aggregation                                   │ │
│  │  - GetBestTitle(AARecord) → string                     │ │
│  │  - GetBestPublisher(AARecord) → string                 │ │
│  │  - GetBestDescription(AARecord) → string               │ │
│  │  - GetAuthorNames(AARecord) → List<string>             │ │
│  │  - ExtractBestIsbn13(AARecord) → string                │ │
│  │  - GetBestPageCount(AARecord) → int?                   │ │
│  └────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Helper Methods                                         │ │
│  │  - SplitAuthors(authorString) → List<string>           │ │
│  │  - ParseReleaseDate(AARecord) → DateTime?              │ │
│  │  - IsValidIsbn13(isbn) → bool                          │ │
│  │  - CleanIsbn(isbn) → string                            │ │
│  │  - ExtractMd5FromForeignId(id) → string                │ │
│  │  - CreateSlug(name) → string                           │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                              │
│  Dependencies:                                               │
│  - IHttpClient (HTTP requests)                              │
│  - ICachedHttpResponseService (caching)                     │
│  - Logger (NLog)                                            │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│                      DTOs (Resources/)                        │
│  ┌────────────────┐  ┌───────────────────┐                  │
│  │    AARecord    │  │ AAFileUnifiedData │                  │
│  └────────────────┘  └───────────────────┘                  │
│  ┌────────────────┐  ┌───────────────────┐                  │
│  │  AAIPFSInfo    │  │   AALibgenBook    │                  │
│  └────────────────┘  └───────────────────┘                  │
│  ┌────────────────┐  ┌───────────────────┐                  │
│  │   AAZLibBook   │  │     AAIsbndb      │                  │
│  └────────────────┘  └───────────────────┘                  │
│  ┌────────────────┐  ┌───────────────────┐                  │
│  │ AAOpenLibrary  │  │ AAInternetArchive │                  │
│  └────────────────┘  └───────────────────┘                  │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│                     Exceptions                                │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         AnnasArchiveException                         │   │
│  │  - Generic errors, API failures, validation errors   │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Data Flow

### 2.1 Metadata Retrieval Flow

```
User Request
    ↓
[1] AnnasArchiveProxy.GetBookInfo(foreignBookId)
    │
    ├─→ Extract MD5 from "aa:8336332bf5877e3adbfb60ac70720cd5"
    │
    ↓
[2] GetBookByMd5("8336332bf5877e3adbfb60ac70720cd5")
    │
    ├─→ Validate MD5 format (32 hex chars)
    │
    ├─→ Build URL: https://annas-archive.org/db/aarecord_elasticsearch/
    │                md5:8336332bf5877e3adbfb60ac70720cd5.json.html
    │
    ├─→ Check Cache (24-hour TTL)
    │   ├─→ HIT: Return cached response
    │   └─→ MISS: Proceed to API call
    │
    ↓
[3] HTTP GET Request (via ICachedHttpResponseService)
    │
    ├─→ User-Agent: "Bookshelf/1.0"
    ├─→ Timeout: 30 seconds
    │
    ↓
[4] Anna's Archive API Response
    │
    ├─→ 200 OK: JSON with metadata
    ├─→ 404 Not Found: Throw BookNotFoundException
    ├─→ 429 Rate Limit: Throw AnnasArchiveException
    ├─→ 500+ Server Error: Throw AnnasArchiveException
    ├─→ Timeout: Throw AnnasArchiveException
    │
    ↓
[5] JSON Deserialization (System.Text.Json)
    │
    ├─→ Deserialize to AARecord
    ├─→ Validate: FileUnifiedData not null
    ├─→ Validate: MD5 present
    │
    ↓
[6] Metadata Aggregation
    │
    ├─→ GetBestTitle():
    │   Priority: ISBNdb → Libgen → Z-Library → file_unified_data
    │
    ├─→ GetBestPublisher():
    │   Priority: ISBNdb → Libgen → Z-Library → file_unified_data
    │
    ├─→ GetBestDescription():
    │   Priority: ISBNdb → OpenLibrary → Libgen → Z-Library
    │
    ├─→ GetAuthorNames():
    │   Priority: ISBNdb (structured) → Libgen → Z-Library → file_unified_data
    │   └─→ SplitAuthors() if string format (";", ",", " and ", " & ")
    │
    ├─→ ExtractBestIsbn13():
    │   Priority: ISBNdb → Libgen → Z-Library
    │   └─→ IsValidIsbn13() with checksum validation
    │
    ├─→ GetBestPageCount():
    │   Priority: ISBNdb → Z-Library → Libgen
    │
    ├─→ ParseReleaseDate():
    │   Try: DateTime.Parse(yearString)
    │   Fallback: Extract 4-digit year
    │
    ↓
[7] Model Mapping
    │
    ├─→ Create Book:
    │   ├─ ForeignBookId = "aa:{md5}"
    │   ├─ Title = best title
    │   ├─ TitleSlug = md5
    │   ├─ CleanTitle = Parser.CleanAuthorName(title)
    │   ├─ ReleaseDate = parsed date
    │   ├─ Genres = [] (empty)
    │   ├─ Ratings = {Votes: 0, Value: 0}
    │   └─ AnyEditionOk = true
    │
    ├─→ Create Edition:
    │   ├─ ForeignEditionId = "aa:{md5}"
    │   ├─ TitleSlug = md5
    │   ├─ Title = best title
    │   ├─ Publisher = best publisher
    │   ├─ ReleaseDate = parsed date
    │   ├─ Isbn13 = validated ISBN-13
    │   ├─ Overview = best description
    │   ├─ Language = file_unified_data.language
    │   ├─ Format = file_unified_data.extension (uppercase)
    │   ├─ PageCount = best page count
    │   ├─ Monitored = true
    │   ├─ Links = [AA link, IPFS link]
    │   └─ Images = [] (no covers currently)
    │
    ├─→ Create AuthorMetadata[]:
    │   For each author name:
    │   ├─ ForeignAuthorId = "aa-author:{slug}"
    │   │   └─→ CreateSlug(name): lowercase, remove punctuation, hyphens
    │   └─ Name = author name
    │
    ↓
[8] Return Tuple<string, Book, List<AuthorMetadata>>
    │
    ├─→ Item1: First author's ForeignAuthorId (or "unknown")
    ├─→ Item2: Book object with single Edition
    └─→ Item3: List of AuthorMetadata objects
```

### 2.2 Error Handling Flow

```
Error Occurs
    ↓
Check Error Type
    ├─→ HTTP 404
    │   └─→ Throw BookNotFoundException("Book with MD5 {md5} not found")
    │
    ├─→ HTTP 429 (Rate Limit)
    │   └─→ Throw AnnasArchiveException("Rate limited by Anna's Archive")
    │
    ├─→ HTTP 500+
    │   └─→ Throw AnnasArchiveException("API error: {statusCode}")
    │
    ├─→ Network Timeout
    │   └─→ Throw AnnasArchiveException("Request timed out")
    │
    ├─→ JSON Deserialization Error
    │   └─→ Throw AnnasArchiveException("Failed to deserialize response")
    │
    ├─→ Invalid MD5 Format
    │   └─→ Throw AnnasArchiveException("Invalid MD5 hash")
    │
    └─→ Other Exceptions
        └─→ Log error, throw AnnasArchiveException with inner exception
```

---

## 3. Metadata Aggregation Strategy

### 3.1 Priority Matrix

| Field | 1st Priority | 2nd Priority | 3rd Priority | 4th Priority | Fallback |
|-------|--------------|--------------|--------------|--------------|----------|
| **Title** | ISBNdb | Libgen.rs | Z-Library | OpenLibrary | file_unified_data |
| **Authors** | ISBNdb (list) | Libgen.rs | Z-Library | OpenLibrary | file_unified_data |
| **Publisher** | ISBNdb | Libgen.rs | Z-Library | - | file_unified_data |
| **Year** | ISBNdb | Libgen.rs | Z-Library | OpenLibrary | file_unified_data |
| **ISBN** | ISBNdb | Libgen.rs | Z-Library | OpenLibrary | - |
| **Description** | ISBNdb.synopsis | OpenLibrary | Libgen.descr | Z-Library | file_unified_data |
| **Pages** | ISBNdb | Z-Library | Libgen.rs | - | - |
| **Language** | file_unified_data | Libgen.rs | Z-Library | ISBNdb | - |
| **Format** | file_unified_data.extension | - | - | - | - |
| **Filesize** | file_unified_data | Z-Library | - | - | - |

### 3.2 Aggregation Logic

**Title Aggregation**:
```csharp
string GetBestTitle(AARecord record)
{
    return record.IsbnDb?.Title
        ?? record.LibgenNonFiction?.Title
        ?? record.LibgenFiction?.Title
        ?? record.ZLibrary?.Title
        ?? record.OpenLibrary?.Title
        ?? record.FileUnifiedData.Title;
}
```

**Author Aggregation**:
```csharp
List<string> GetAuthorNames(AARecord record)
{
    // Prefer ISBNdb (already a list)
    if (record.IsbnDb?.Authors != null && record.IsbnDb.Authors.Any())
        return record.IsbnDb.Authors;

    // Otherwise split string format
    if (!string.IsNullOrEmpty(record.LibgenNonFiction?.Author))
        return SplitAuthors(record.LibgenNonFiction.Author);

    if (!string.IsNullOrEmpty(record.LibgenFiction?.Author))
        return SplitAuthors(record.LibgenFiction.Author);

    if (!string.IsNullOrEmpty(record.ZLibrary?.Author))
        return SplitAuthors(record.ZLibrary.Author);

    if (!string.IsNullOrEmpty(record.FileUnifiedData.Author))
        return SplitAuthors(record.FileUnifiedData.Author);

    return new List<string>();
}

List<string> SplitAuthors(string authorString)
{
    // Try common separators in order
    var separators = new[] { ";", ",", " and ", " & " };

    foreach (var sep in separators)
    {
        if (authorString.Contains(sep))
        {
            return authorString.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .ToList();
        }
    }

    return new List<string> { authorString.Trim() };
}
```

**ISBN Validation**:
```csharp
bool IsValidIsbn13(string isbn)
{
    if (string.IsNullOrEmpty(isbn) || isbn.Length != 13)
        return false;

    if (!isbn.All(char.IsDigit))
        return false;

    // Calculate checksum
    var sum = 0;
    for (var i = 0; i < 12; i++)
    {
        var digit = isbn[i] - '0';
        sum += (i % 2 == 0) ? digit : digit * 3;
    }

    var checkDigit = isbn[12] - '0';
    var calculatedCheck = (10 - (sum % 10)) % 10;

    return checkDigit == calculatedCheck;
}
```

---

## 4. Class Design

### 4.1 AnnasArchiveProxy Class

```csharp
public class AnnasArchiveProxy : ISearchForNewBook, IProvideBookInfo
{
    // Constants
    private const string JsonApiUrl = "https://annas-archive.org/db/aarecord_elasticsearch/md5:{0}.json.html";
    private const int MaxSearchResults = 25;
    private const string UserAgent = "Bookshelf/1.0";

    // Dependencies (injected)
    private readonly IHttpClient _httpClient;
    private readonly ICachedHttpResponseService _cachedHttpClient;
    private readonly Logger _logger;

    // Constructor
    public AnnasArchiveProxy(
        IHttpClient httpClient,
        ICachedHttpResponseService cachedHttpClient,
        Logger logger);

    // IProvideBookInfo Implementation
    public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId);

    // ISearchForNewBook Implementation
    public List<Book> SearchForNewBook(string title, string author, bool getAllEditions);
    public List<Book> SearchByIsbn(string isbn);
    public List<Book> SearchByAsin(string asin);
    public List<Book> SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions);

    // Core Methods
    private Book GetBookByMd5(string md5);
    private Book MapRecordToBook(AARecord record);
    private Edition CreateEdition(AARecord record);

    // Metadata Aggregation
    private string GetBestTitle(AARecord record);
    private string GetBestPublisher(AARecord record);
    private string GetBestDescription(AARecord record);
    private List<string> GetAuthorNames(AARecord record);
    private List<AuthorMetadata> GetAuthorMetadata(AARecord record);
    private string ExtractBestIsbn13(AARecord record);
    private int GetBestPageCount(AARecord record);
    private List<Links> GetLinks(AARecord record);

    // Helper Methods
    private List<string> SplitAuthors(string authorString);
    private DateTime? ParseReleaseDate(AARecord record);
    private bool IsValidIsbn13(string isbn);
    private string CleanIsbn(string isbn);
    private List<string> ExtractIsbn13sFromString(string isbnString);
    private string ExtractMd5FromForeignId(string foreignId);
    private string CreateSlug(string name);
    private string ExtractStringFromJsonElement(object value);
}
```

### 4.2 DTO Class Hierarchy

```
AARecord (root)
├── AAFileUnifiedData (most reliable, aggregated)
├── List<AAIPFSInfo> (IPFS download info)
├── AALibgenBook (Libgen.rs non-fiction)
├── AALibgenBook (Libgen.rs fiction)
├── AAZLibBook (Z-Library)
├── AAIsbndb (ISBNdb - highest quality structured metadata)
├── AAOpenLibrary (OpenLibrary)
└── AAInternetArchive (IA data within AA)
```

---

## 5. Caching Strategy

### 5.1 Cache Configuration

```csharp
// Metadata Cache (long TTL - data rarely changes)
var metadataCache = TimeSpan.FromHours(24);

// Search Cache (shorter TTL - Phase 2)
var searchCache = TimeSpan.FromHours(2);

// Cache Key Format
var cacheKey = $"https://annas-archive.org/db/aarecord_elasticsearch/md5:{md5}.json.html";
```

### 5.2 Cache Flow

```
Request → Check Cache → HIT: Return immediately
                      ↓
                     MISS: API Call → Store in Cache → Return
```

### 5.3 Cache Invalidation

- **Manual**: None required (metadata rarely changes)
- **Automatic**: TTL expiration
- **Clear**: User can clear all provider caches via Bookshelf UI

---

## 6. Error Handling Design

### 6.1 Exception Hierarchy

```
Exception (base)
    └─→ AnnasArchiveException (custom)
            └─→ Used for all AA-specific errors

BookNotFoundException (existing)
    └─→ Used for HTTP 404 (book not found)
```

### 6.2 Exception Messages

| Error Type | Exception | Message Format |
|------------|-----------|----------------|
| Invalid MD5 | AnnasArchiveException | "Invalid MD5 hash. Must be 32 characters" |
| Invalid Foreign ID | AnnasArchiveException | "Invalid foreign book ID format. Expected: aa:md5hash" |
| HTTP 404 | BookNotFoundException | "Book with MD5 {md5} not found in Anna's Archive" |
| HTTP 429 | AnnasArchiveException | "Rate limited by Anna's Archive" |
| HTTP 500+ | AnnasArchiveException | "API error: HTTP {statusCode}" |
| Timeout | AnnasArchiveException | "Request timed out after 30 seconds" |
| JSON Error | AnnasArchiveException | "Failed to deserialize Anna's Archive response" |
| Missing Data | AnnasArchiveException | "Invalid record: missing file_unified_data" |

### 6.3 Logging Strategy

**Log Levels**:
- **Debug**: All requests, cache hits/misses, MD5 validation
- **Info**: Successful metadata retrieval
- **Warn**: Missing optional fields, fallback to lower-priority sources
- **Error**: All exceptions with full context

**Log Examples**:
```csharp
_logger.Debug("Fetching metadata for MD5: {0}", md5);
_logger.Debug("Cache hit for MD5: {0}", md5);
_logger.Info("Successfully retrieved metadata for: {0}", title);
_logger.Warn("No ISBN found in ISBNdb, trying Libgen");
_logger.Error(ex, "Error fetching metadata for MD5: {0}", md5);
```

---

## 7. Performance Considerations

### 7.1 Optimizations

1. **Caching**:
   - 24-hour TTL for metadata (reduces API calls by ~95%)
   - Cache key includes MD5 for granularity

2. **Lazy Evaluation**:
   - Only fetch metadata when explicitly requested
   - Don't populate search results until needed (Phase 2)

3. **Efficient Aggregation**:
   - Use null coalescing (`??`) for priority chains
   - Stop at first non-null value

4. **String Operations**:
   - Use `StringBuilder` for slug creation
   - Minimize string allocations

### 7.2 Resource Usage

| Resource | Estimated Usage |
|----------|-----------------|
| Memory | ~10 KB per AARecord object |
| CPU | Minimal (JSON deserialization only) |
| Network | 1-5 KB per API call |
| Cache Storage | ~10 KB × cache size |

### 7.3 Scalability

- **Concurrent Requests**: No locking required (HTTP client handles this)
- **Rate Limiting**: None enforced by AA (but use caching to be respectful)
- **Bulk Operations**: Not applicable (single book per request)

---

## 8. Security Considerations

### 8.1 Input Validation

| Input | Validation |
|-------|------------|
| MD5 hash | Length = 32, all hex chars (0-9, a-f) |
| Foreign ID | Format: `aa:{32-char-md5}` |
| ISBN | Digits only, length 13, checksum valid |
| Author names | Trim whitespace, check for empty |
| Year | Integer, range 1-9999 |

### 8.2 Output Sanitization

- **HTML**: No HTML content expected (JSON API)
- **SQL Injection**: N/A (no direct database queries)
- **XSS**: N/A (server-side only)

### 8.3 HTTPS Enforcement

```csharp
const string JsonApiUrl = "https://annas-archive.org/...";  // Always HTTPS
```

### 8.4 Secrets Management

- **No Secrets Required**: AA API doesn't require authentication
- **User-Agent**: Public identifier, not sensitive

---

## 9. Testing Strategy

### 9.1 Unit Tests

**Test Coverage**:
- All public methods
- All metadata aggregation methods
- All validation methods
- All error scenarios

**Mock Strategy**:
```csharp
// Mock ICachedHttpResponseService
var mockCache = new Mock<ICachedHttpResponseService>();
mockCache.Setup(c => c.Get(It.IsAny<HttpRequest>(), true, It.IsAny<TimeSpan>()))
    .Returns(new HttpResponse
    {
        StatusCode = HttpStatusCode.OK,
        Content = sampleJson
    });
```

### 9.2 Integration Tests

**Test Cases**:
1. **Happy Path**: Fetch real book from AA API
2. **Not Found**: 404 for non-existent MD5
3. **Network Error**: Timeout/connection failure
4. **Invalid JSON**: Malformed response

**Sample MD5s for Testing**:
- Valid: `8336332bf5877e3adbfb60ac70720cd5` (Against intellectual monopoly)
- Invalid: `00000000000000000000000000000000` (non-existent)

### 9.3 Test Data

**Sample AARecord JSON** (minimal):
```json
{
  "id": "md5:8336332bf5877e3adbfb60ac70720cd5",
  "file_unified_data": {
    "title": "Against intellectual monopoly",
    "author": "Michele Boldrin; David K. Levine",
    "md5": "8336332bf5877e3adbfb60ac70720cd5",
    "extension": "pdf"
  }
}
```

---

## 10. Deployment Considerations

### 10.1 Configuration

**No Configuration Required**:
- API URL is hardcoded constant
- No authentication needed
- Caching TTLs are constants

**Future Configuration** (Phase 2+):
```json
{
  "AnnasArchive": {
    "Enabled": true,
    "MetadataCacheTtl": 86400,
    "SearchCacheTtl": 7200,
    "MaxSearchResults": 25
  }
}
```

### 10.2 Monitoring

**Metrics to Track**:
- API call count (should be low due to caching)
- Cache hit rate (should be >90%)
- Error rate (should be <1%)
- Average response time

**Logging**:
- All errors logged with context
- Debug logs for cache hits/misses
- Info logs for successful retrievals

---

## 11. Future Enhancements (Phase 2+)

### 11.1 Search Implementation

**Approach**: Light HTML parsing
```
Search Request
    ↓
Scrape Search Page
    ↓
Extract MD5 Hashes
    ↓
Fetch Metadata for Each MD5 (using existing GetBookByMd5)
    ↓
Return List<Book>
```

**Library Options**:
- AngleSharp (recommended - HTML5 compliant)
- HtmlAgilityPack (alternative - widely used)

### 11.2 Local Mirror Support (Phase 3)

**Tier 3 Implementation**:
- Download metadata torrents
- Load into local ElasticSearch
- Hybrid mode: prefer local, fallback to API

**Not Planned for Initial Release**

---

## 12. Open Questions & Decisions

### Q1: Should we implement search in Phase 1?
**Decision**: No - defer to Phase 2
**Rationale**: Requires HTML parsing library, adds complexity, API-only is sufficient for MVP

### Q2: Should we cache IPFS gateway lists?
**Decision**: Yes - included in 24-hour metadata cache
**Rationale**: Gateway lists rarely change, saves API calls

### Q3: Should we validate all ISBN-13s or just use first match?
**Decision**: Validate with checksum
**Rationale**: Data quality is important, prevents invalid ISBNs

### Q4: Should we aggregate ratings from multiple sources?
**Decision**: No - return zeros
**Rationale**: AA doesn't provide aggregated ratings, inconsistent across sources

---

## 13. Design Decisions Log

| Date | Decision | Rationale |
|------|----------|-----------|
| 2025-11-27 | Use AA JSON API (not web scraping) | Official API is stable, maintainable |
| 2025-11-27 | Prioritize ISBNdb for metadata | Highest quality structured data |
| 2025-11-27 | 24-hour metadata cache TTL | Good balance of freshness vs. API load |
| 2025-11-27 | Defer search to Phase 2 | Simplifies MVP, reduces dependencies |
| 2025-11-27 | Use ISBN-13 checksum validation | Ensures data quality |
| 2025-11-27 | Return empty list for unsupported search methods | Graceful degradation, no errors |
| 2025-11-27 | Use MD5 as TitleSlug | Unique, immutable, no conflicts |

---

**Document Version**: 1.0
**Last Updated**: 2025-11-27
**Author**: Claude Code (AI Assistant)
