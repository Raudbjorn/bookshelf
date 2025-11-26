# Metadata Sources Evaluation for Bookshelf

**Date:** 2025-11-15
**Branch:** `feature/anna-archive-integration`
**Purpose:** Evaluate Anna's Archive and related book metadata sources for integration into the Bookshelf project

---

## Executive Summary

After comprehensive research into Anna's Archive and the various book metadata sources it aggregates, **I recommend a phased approach prioritizing sources with official APIs** before implementing web scraping solutions.

### Key Findings:
1. ✅ **OpenLibrary** - Already integrated, has official API
2. ✅ **Internet Archive** - Has official API, recommended for integration
3. ⚠️ **Anna's Archive** - No official API, requires web scraping or special access key
4. ⚠️ **LibGen (all variants)** - No official API, requires web scraping
5. ⚠️ **Z-Library** - No official API, requires web scraping
6. ❌ **Sci-Hub** - Focused on academic papers, not books; legal concerns

---

## Detailed Source Analysis

### 1. Anna's Archive

**URL:** https://annas-archive.org/
**Type:** Meta-search engine aggregating multiple sources
**Size:** 165,495,548 files across all sources

#### API Status: ❌ No Official Public API

**Evidence:**
- GitLab issue #54 discusses adding a public API (similar to OpenLibrary's `.json` endpoints), but **not yet implemented**
- Current implementations use **web scraping** only:
  - `annas-py` (Python) - scrapes HTML with BeautifulSoup
  - `annas-mcp` (Go) - requires donated "API key" (special access, not public)
  - Dart package `annas_archive_api` - scrapes HTML
  - SearXNG engine integration - scrapes search pages

**Search URL Pattern:**
```
https://annas-archive.org/search?q=<query>&index=<index>&content=<content>&ext=<extension>&sort=<sort>
```

**Parameters:**
- `q` - Search query
- `index` - `books` (default), `journals`, `digital_lending`, `meta`
- `content` - Filter by type: `book_fiction`, `book_nonfiction`, `book_unknown`, `book_comic`, `magazine`, `standards_document`
- `ext` - File extension: `pdf`, `epub`, `mobi`, `azw3`, etc.
- `sort` - `newest`, `oldest`, `largest`, `smallest`, or empty for relevance

**Authentication:**
- Public search: No authentication required for web scraping
- Special access: "Donated API key" system exists but requires donation to Anna's Archive

**Data Sources Aggregated:**
Anna's Archive is a **metasearch** that aggregates these sources:

| Source | Files | Size | API Available? |
|--------|-------|------|----------------|
| Libgen.li (lgli) | 21.9M | 336.4 TB | ❌ No |
| Z-Library (zlib) | 22.4M | 154.5 TB | ❌ No |
| Sci-Hub (scihub) | 95.5M | 99.1 TB | ❌ No (papers, not books) |
| Libgen.rs (lgrs) | 7.6M | 87.5 TB | ❌ No |
| Internet Archive (ia) | 12.3M | 393.9 TB | ✅ **YES** |
| DuXiu (duxiu) | 3.9M | 206.4 TB | ❌ Unknown |
| Uploads to AA (upload) | 10.7M | 168.4 TB | N/A |
| MagzDB (magzdb) | 649K | 17.1 TB | ❌ Unknown |
| Nexus/STC (nexusstc) | 4.8M | 76.0 TB | ❌ Unknown |
| HathiTrust (hathi) | 19.0M | Partial | ⚠️ Limited API |

#### Pros:
✅ Comprehensive aggregation of multiple sources
✅ 165M+ files across all categories
✅ Clean, fast web interface
✅ Active development and community
✅ Includes metadata from multiple providers

#### Cons:
❌ No official public API
❌ Web scraping is fragile (breaks when HTML changes)
❌ Rate limiting concerns
❌ Legal gray area for some source data
❌ Requires C# HTML parsing library (no native support in .NET for this use case)

#### Integration Complexity: **HIGH**

**Required Work:**
1. Implement HTML parser in C# (using AngleSharp or similar)
2. Reverse-engineer current search page structure
3. Handle pagination, filtering, and result parsing
4. Implement robust error handling and retry logic
5. Add rate limiting and caching
6. Monitor for HTML structure changes
7. Handle CAPTCHAs and anti-bot measures

**Estimated Effort:** 40-60 hours

---

### 2. LibGen (Library Genesis)

**Variants:**
- **Libgen.rs** - http://libgen.rs/ (Non-Fiction & Fiction)
- **Libgen.li** - http://libgen.li/ (Fiction + other collections)
- **Libgen.st** - Mirror site

**Size:** ~21.9M files (lgli), ~7.6M files (lgrs)

#### API Status: ❌ No Official API

**Evidence:**
- Multiple third-party scraping libraries exist:
  - `libgen-api` (Python) - 5+ forks with different implementations
  - `libgen-api-enhanced` (Python)
  - `libgen-api-modern` (Python)
  - `libgen-rs` (Rust)
  - `libgen` (NPM/JavaScript)
- All libraries use **web scraping** with BeautifulSoup or similar

**Search URL Pattern:**
```
http://libgen.rs/search.php?req=<query>&lg_topic=libgen&open=0&view=simple&res=25&phrase=1&column=def
http://libgen.li/index.php?req=<query>&columns[]=title&columns[]=author&objects[]=f&objects[]=e&objects[]=s&objects[]=a&objects[]=p&objects[]=w&topics[]=l&topics[]=f
```

**Metadata Retrieval:**
```
http://libgen.rs/book/index.php?md5=<MD5_HASH>
http://libgen.li/edition.php?id=<ID>
```

#### Pros:
✅ Large collection of books (21.9M+ files)
✅ Good metadata quality
✅ Multiple mirrors for reliability
✅ Established community tools

#### Cons:
❌ No official API
❌ HTML structure varies between mirrors
❌ Rate limiting can be aggressive
❌ Legal concerns with scraped content
❌ Requires maintaining scraper for each mirror

#### Integration Complexity: **HIGH**

**Estimated Effort:** 35-50 hours per mirror variant

---

### 3. Z-Library

**URL:** Multiple domains (z-lib.se, zlibrary-global.se, etc.)
**Size:** 22.4M+ files

#### API Status: ❌ No Official API

**Evidence:**
- Unofficial Python libraries:
  - `zlibrary` (async, by sertraline)
  - `zlibrary-sync` (sync, by Advik-B)
- Both use web scraping
- Requires account login (cookie-based authentication)
- Frequently changes domains due to takedown attempts

**Search Requires Authentication:**
- Cookie: `remix-sid` (session ID)
- Account registration required

**Search URL Pattern:**
```
https://<current-domain>/s/<query>?yearFrom=&yearTo=&extensions[]=epub&extensions[]=pdf&order=bestmatch
```

#### Pros:
✅ Large, well-organized collection
✅ Good search functionality
✅ Multiple format support
✅ User-friendly metadata

#### Cons:
❌ No official API
❌ Requires authentication (accounts)
❌ Frequently changes domains (legal issues)
❌ Cookie management complexity
❌ Risk of account bans for automated access
❌ Ethical/legal concerns

#### Integration Complexity: **VERY HIGH**

**Estimated Effort:** 50-70 hours + ongoing maintenance

---

### 4. Internet Archive (IA) ✅ RECOMMENDED

**URL:** https://archive.org/
**Size:** 12.3M book files + millions more items

#### API Status: ✅ **OFFICIAL PUBLIC API**

**Official Documentation:** https://archive.org/services/docs/api/

**Key APIs:**
1. **Metadata API** - https://archive.org/metadata/{identifier}
2. **Search API** - https://archive.org/advancedsearch.php
3. **Books API** - Integrated with OpenLibrary

**Search API Example:**
```
GET https://archive.org/advancedsearch.php?q=title:(lord+of+the+rings)&fl[]=identifier&fl[]=title&fl[]=creator&fl[]=year&output=json
```

**Parameters:**
- `q` - Lucene query syntax (supports field searches: `title:`, `creator:`, `subject:`, `isbn:`)
- `fl[]` - Fields to return
- `output` - `json`, `xml`, `csv`
- `rows` - Number of results (default 50, max 10000)
- `page` - Page number
- `sort[]` - Sort field and order

**Metadata API Example:**
```
GET https://archive.org/metadata/lordoftheringsj00tolk
```

Returns complete JSON metadata including:
- Title, creator, date, publisher
- File list with formats, sizes, MD5 hashes
- Subject tags, descriptions
- Download URLs
- Cover images

**Rate Limiting:**
- Generally permissive (500+ requests/second observed)
- No API key required for read operations
- Caching encouraged

**Client Libraries:**
- **Python:** `internetarchive` (official)
- **R:** `internetarchive`
- **JavaScript:** Various community libraries
- **C#:** Can use standard HTTP client (no official library needed)

#### Pros:
✅ **Official, documented API**
✅ **No authentication required** for search/metadata
✅ **High rate limits** (500+ req/sec)
✅ **Stable, reliable** (non-profit organization)
✅ **Rich metadata** (MARC records, etc.)
✅ **Legal and ethical** (public domain & licensed content)
✅ **12.3M+ books** (Controlled Digital Lending)
✅ **JSON/XML output** (easy to parse)
✅ **Well-maintained** infrastructure

#### Cons:
⚠️ Controlled Digital Lending (some books require borrowing)
⚠️ Not all books available for download
⚠️ Metadata quality varies by source

#### Integration Complexity: **LOW**

**Required Work:**
1. Implement HTTP requests to Search & Metadata APIs
2. Parse JSON responses (native .NET support)
3. Map IA fields to Bookshelf domain models
4. Implement basic caching
5. Handle pagination

**Estimated Effort:** 15-25 hours

**Integration Priority:** ⭐⭐⭐⭐⭐ **HIGHEST**

---

### 5. OpenLibrary ✅ ALREADY INTEGRATED

**URL:** https://openlibrary.org/
**Status:** ✅ **Already used as fallback in BookInfoProxy.cs**

#### API Status: ✅ **OFFICIAL PUBLIC API**

**Official Documentation:** https://openlibrary.org/developers/api

**Key APIs:**
1. **Search API** - https://openlibrary.org/search.json
2. **Books API** - https://openlibrary.org/api/books
3. **Works API** - https://openlibrary.org/works/{id}.json
4. **Authors API** - https://openlibrary.org/authors/{id}.json
5. **ISBN API** - https://openlibrary.org/isbn/{isbn}.json

**Current Usage in Bookshelf:**
- File: `/src/NzbDrone.Core/MetadataSource/BookInfo/BookInfoProxy.cs`
- Used as fallback when Hardcover API fails
- Searches by title, author, ISBN, ASIN

**Search API Example:**
```
GET https://openlibrary.org/search.json?q=lord+of+the+rings&author=tolkien
```

**ISBN Lookup:**
```
GET https://openlibrary.org/isbn/9780547928227.json
```

**Work Details:**
```
GET https://openlibrary.org/works/OL27448W.json
```

#### Pros:
✅ **Already integrated**
✅ **Official API with extensive documentation**
✅ **Free, no rate limits** (reasonable use)
✅ **Rich metadata** (descriptions, covers, ratings)
✅ **ISBN/ASIN lookup**
✅ **Work/Edition separation** (matches bookshelf model)
✅ **Active development** (Internet Archive project)
✅ **Legal and ethical**

#### Cons:
⚠️ Metadata quality varies (crowdsourced)
⚠️ Some records incomplete

#### Integration Status: **COMPLETE** ✅

---

### 6. Sci-Hub

**URL:** Multiple domains (sci-hub.se, sci-hub.st, etc.)
**Size:** 95.5M files

#### API Status: ❌ Not applicable (academic papers, not books)

**Recommendation:** ❌ **DO NOT INTEGRATE**

**Reasons:**
1. Primarily academic journal articles, not books
2. Frozen since 2021 (no new content)
3. Significant legal issues (ongoing lawsuits)
4. No API available
5. Out of scope for a book library application
6. Ethical concerns with large-scale automated access

---

## Comparison Matrix

| Source | Official API | Auth Required | Rate Limits | Books Count | Legal Status | Complexity | Priority |
|--------|--------------|---------------|-------------|-------------|--------------|------------|----------|
| **Internet Archive** | ✅ Yes | ❌ No | High (500+/s) | 12.3M | ✅ Legal | Low | ⭐⭐⭐⭐⭐ |
| **OpenLibrary** | ✅ Yes | ❌ No | Reasonable | 30M+ | ✅ Legal | Low (Done) | ✅ Integrated |
| **Anna's Archive** | ❌ No | ❌ No | Unknown | 165M+ | ⚠️ Gray | High | ⭐⭐ |
| **LibGen.rs** | ❌ No | ❌ No | Medium | 7.6M | ⚠️ Gray | High | ⭐⭐ |
| **LibGen.li** | ❌ No | ❌ No | Medium | 21.9M | ⚠️ Gray | High | ⭐⭐ |
| **Z-Library** | ❌ No | ✅ Yes | Low | 22.4M | ⚠️ Gray | Very High | ⭐ |
| **Sci-Hub** | ❌ No | ❌ No | Unknown | 95.5M | ❌ Illegal | N/A | ❌ No |

---

## Recommendations

### Phase 1: Immediate (Low-Hanging Fruit) ✅

**1. Enhance OpenLibrary Integration (Already Exists)**
- Status: ✅ Already integrated in `BookInfoProxy.cs`
- Effort: 5-10 hours
- Actions:
  - Review current implementation
  - Add better error handling
  - Improve field mapping
  - Add more search methods (ISBN, LCCN, OCLC)

**2. Add Internet Archive as Primary Provider** ⭐⭐⭐⭐⭐
- Status: Not yet integrated
- Effort: 15-25 hours
- Benefits:
  - Official API with excellent documentation
  - 12.3M+ books
  - No authentication required
  - High rate limits
  - Legal and ethical
  - Stable, reliable service
- Implementation:
  1. Create `InternetArchiveProxy.cs` provider
  2. Implement `ISearchForNewBook` interface
  3. Implement `IProvideBookInfo` interface
  4. Use Search API for queries
  5. Use Metadata API for full book details
  6. Map IA metadata to `Book` and `Edition` models
  7. Add to provider fallback chain

### Phase 2: Optional Enhancements (Medium Effort)

**3. Implement Anna's Archive Web Scraping** (Optional)
- Status: Not implemented
- Effort: 40-60 hours
- Risk: High (fragile, legal concerns)
- Benefits:
  - Access to 165M+ aggregated files
  - Single search across multiple sources
- Considerations:
  - Requires HTML parsing library (AngleSharp)
  - Fragile (breaks when website changes)
  - Ethical/legal gray area
  - Rate limiting concerns
  - Ongoing maintenance burden
- Recommendation: **Delay until Phase 3** or **use API if/when released**

**4. Implement LibGen Web Scraping** (Not Recommended)
- Status: Not implemented
- Effort: 35-50 hours per variant
- Risk: High
- Recommendation: **Only if business need justifies legal/ethical risks**

### Phase 3: Future Considerations

**5. Anna's Archive Official API** (When Available)
- Monitor GitLab issue #54: https://annas-software.org/AnnaArchivist/annas-archive/-/issues/54
- If official API is released, re-evaluate for integration
- Would dramatically reduce implementation complexity

**6. Additional Metadata Sources**
- **Google Books API** - Has official API, good metadata, limited download access
- **Goodreads API** - Currently used; being deprecated; consider alternatives
- **ISBNdb API** - Commercial service with excellent metadata ($10-50/month)
- **WorldCat Search API** - OCLC service, excellent library metadata (requires API key)

---

## Implementation Plan

### Recommended Approach: Start with Internet Archive

```csharp
// 1. Create provider interface implementation
public class InternetArchiveProxy : ISearchForNewBook, IProvideBookInfo
{
    private readonly IHttpClient _httpClient;
    private readonly ICachedHttpResponseService _cachedHttpClient;
    private readonly Logger _logger;

    // Constructor with DI
    public InternetArchiveProxy(
        IHttpClient httpClient,
        ICachedHttpResponseService cachedHttpClient,
        Logger logger)
    {
        _httpClient = httpClient;
        _cachedHttpClient = cachedHttpClient;
        _logger = logger;
    }

    // 2. Implement search
    public List<Book> SearchForNewBook(string title, string author, bool getAllEditions)
    {
        var query = BuildLuceneQuery(title, author);
        var url = $"https://archive.org/advancedsearch.php?q={query}&fl[]=identifier&fl[]=title&fl[]=creator&fl[]=date&fl[]=subject&output=json&rows=25";

        var response = _cachedHttpClient.Get<IASearchResponse>(
            new HttpRequestBuilder(url).Build(),
            useCache: true,
            lifetime: TimeSpan.FromDays(7)
        );

        return response.Resource.Response.Docs
            .Select(MapToBook)
            .ToList();
    }

    // 3. Implement metadata retrieval
    public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
    {
        // foreignBookId is IA identifier
        var url = $"https://archive.org/metadata/{foreignBookId}";

        var response = _httpClient.Get<IAMetadataResponse>(
            new HttpRequestBuilder(url).Build()
        );

        var book = MapMetadataToBook(response.Resource);
        var authors = ExtractAuthors(response.Resource);

        return Tuple.Create(authors[0].ForeignAuthorId, book, authors);
    }

    // Helper methods
    private string BuildLuceneQuery(string title, string author)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(title))
            parts.Add($"title:({EscapeLucene(title)})");

        if (!string.IsNullOrEmpty(author))
            parts.Add($"creator:({EscapeLucene(author)})");

        parts.Add("mediatype:texts");
        parts.Add("collection:printdisabled OR collection:inlibrary OR collection:books");

        return string.Join(" AND ", parts);
    }

    private Book MapToBook(IASearchDoc doc)
    {
        return new Book
        {
            ForeignBookId = $"ia:{doc.Identifier}",
            Title = doc.Title,
            ReleaseDate = ParseDate(doc.Date),
            // ... map other fields
        };
    }
}
```

### File Structure
```
/src/NzbDrone.Core/MetadataSource/InternetArchive/
├── InternetArchiveProxy.cs          // Main provider implementation
├── InternetArchiveException.cs      // Custom exception class
└── Resources/
    ├── IASearchResponse.cs          // Search API response DTO
    ├── IAMetadataResponse.cs        // Metadata API response DTO
    └── IASearchDoc.cs               // Search result item DTO
```

### Testing Strategy
1. Unit tests for DTO mapping
2. Integration tests with live API calls
3. Rate limiting tests
4. Error handling tests (404, 500, timeout)
5. Pagination tests

---

## Legal & Ethical Considerations

### ✅ Recommended (Legal & Ethical)
- **Internet Archive** - Non-profit, controlled digital lending, public domain content
- **OpenLibrary** - Part of Internet Archive, open data initiative
- **Google Books API** - Official API with licensing agreements

### ⚠️ Gray Area (Use with Caution)
- **Anna's Archive** - Aggregates copyrighted content; no official API; web scraping concerns
- **LibGen** - Hosts copyrighted books without authorization; legal issues in multiple jurisdictions
- **Z-Library** - Ongoing legal battles; domains frequently seized

### ❌ Not Recommended
- **Sci-Hub** - Active lawsuits; primarily papers not books; frozen since 2021
- **Unauthorized book repositories** - Legal liability, ethical concerns

### Best Practices
1. **Prefer official APIs** over web scraping
2. **Respect rate limits** and implement caching
3. **Only download metadata**, not full books (unless legally authorized)
4. **Clearly document data sources** in UI and documentation
5. **Provide attribution** to metadata providers
6. **Monitor for API changes** and legal developments
7. **Implement kill switches** for providers with legal issues

---

## Technical Considerations for C# Implementation

### HTTP Client Requirements
✅ Already available in bookshelf:
- `IHttpClient` - Standard HTTP operations
- `ICachedHttpResponseService` - HTTP with caching
- `HttpRequestBuilder` - Fluent request builder

### JSON Parsing
✅ Already available:
- `System.Text.Json` (primary)
- `Newtonsoft.Json` (fallback)

### HTML Parsing (if web scraping needed)
❌ Not currently in project, would need to add:
- **AngleSharp** (recommended) - Modern, fast, well-maintained
- **HtmlAgilityPack** (alternative) - Older but stable

### Rate Limiting
✅ Existing infrastructure:
- Caching via `ICachedHttpResponseService`
- Retry logic in `BookInfoProxy.cs`
- Thread.Sleep for rate limit delays

---

## Cost-Benefit Analysis

### Internet Archive Integration
**Effort:** 15-25 hours
**Benefits:**
- 12.3M+ books
- Official API (no legal risk)
- No authentication required
- High reliability
- Rich metadata

**ROI:** ⭐⭐⭐⭐⭐ Excellent

### Anna's Archive Web Scraping
**Effort:** 40-60 hours initial + 10-20 hours/year maintenance
**Benefits:**
- 165M+ files
- Aggregates multiple sources
- Comprehensive coverage

**Risks:**
- Legal gray area
- Fragile (breaks on HTML changes)
- Rate limiting unknown
- Ethical concerns

**ROI:** ⭐⭐ Poor (high effort, high risk, uncertain legality)

### LibGen Web Scraping
**Effort:** 35-50 hours per variant
**Benefits:**
- 21.9M files (lgli)
- Direct access to source

**Risks:**
- No official API
- Legal concerns
- Rate limiting
- Multiple variants to maintain

**ROI:** ⭐⭐ Poor

---

## Conclusion

**Primary Recommendation:**
Focus on integrating **Internet Archive** as the next metadata provider. It offers:
1. ✅ Official, documented API
2. ✅ Large collection (12.3M+ books)
3. ✅ No legal/ethical concerns
4. ✅ Low implementation complexity (15-25 hours)
5. ✅ High reliability and rate limits

**Secondary Recommendation:**
Enhance existing **OpenLibrary** integration (already in the codebase).

**Anna's Archive Recommendation:**
⏸️ **Postpone** until official API is released. Monitor GitLab issue #54 for updates. Web scraping approach has:
- ❌ High implementation complexity
- ❌ High maintenance burden
- ❌ Legal gray area
- ❌ Fragile (breaks on HTML changes)

If business need absolutely requires Anna's Archive access, consider:
1. Donating to obtain official API key access
2. Implementing web scraping with clear legal review
3. Using it only for metadata (not file downloads)
4. Implementing robust error handling and monitoring

---

## Next Steps

1. ✅ Review and approve this evaluation
2. ✅ Create implementation plan for Internet Archive integration
3. ⏭️ Implement InternetArchiveProxy (15-25 hours)
4. ⏭️ Add unit and integration tests (5-10 hours)
5. ⏭️ Update documentation and UI attribution (2-5 hours)
6. ⏭️ Monitor for Anna's Archive official API announcement

**Total Estimated Effort for Phase 1:** 25-40 hours

---

## References

- Internet Archive API Docs: https://archive.org/services/docs/api/
- OpenLibrary API Docs: https://openlibrary.org/developers/api
- Anna's Archive Public API Issue: https://annas-software.org/AnnaArchivist/annas-archive/-/issues/54
- Anna's Archive Datasets: https://annas-archive.org/datasets
- Bookshelf Requirements: `/bookshelf/requirements.md`
- Bookshelf Phase 2 Progress: `/bookshelf/PHASE2_PROGRESS.md`
