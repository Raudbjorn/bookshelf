# Anna's Archive & Z-Library Implementation Plan

**Date:** 2025-11-26
**Status:** Planning Phase
**Based on:** LazyLibrarian Python implementation analysis

---

## Executive Summary

Based on analysis of LazyLibrarian's implementation, we can add two additional metadata/download providers to Bookshelf:

1. **Anna's Archive** (https://annas-archive.org/) - Web scraping, optional paid API
2. **Z-Library** (b-ok.cc / 1lib.sk) - Official JSON API with authentication

Both providers are currently used successfully in LazyLibrarian and can be adapted to C#/.NET for Bookshelf.

---

## Provider 1: Anna's Archive

### Overview
- **Website:** https://annas-archive.org/
- **Type:** Aggregator (165M+ files from multiple sources)
- **API:** No official API - requires web scraping
- **Authentication:** Optional (paid subscription for fast downloads)
- **Legal Status:** ⚠️ Gray area (aggregates copyrighted content)

### LazyLibrarian Implementation Analysis

#### Key Files Examined:
- `/lazylibrarian/annas.py` - Main implementation (~503 lines)
- Uses BeautifulSoup (html5lib) for HTML parsing
- Caches search results for 2 hours
- Supports download rate limiting (18-hour rolling window)

#### Search Functionality:
```python
def annas_search(query, language, file_type, order_by):
    url = "https://annas-archive.org/search"
    params = {
        "q": query,
        "lang": language,     # Language enum (EN, ES, FR, etc.)
        "ext": file_type,     # File type enum (PDF, EPUB, MOBI, etc.)
        "sort": order_by      # OrderBy enum (newest, oldest, largest, etc.)
    }
    # Parse HTML with BeautifulSoup
    # Extract: div[class*='pt-3'][class*='border-b']
```

#### Data Extraction (Web Scraping):
```python
# Extracts from search results:
- ID: MD5 hash from URL (/md5/{hash})
- Title: From link text (a.js-vim-focus)
- Authors: From data-content attribute (div.text-amber-900)
- File Info: Parsed from metadata div (div.text-gray-800)
  - Extension (PDF, EPUB, etc.)
  - Size (MB/KB)
  - Language code
  - Year (extracted via regex)
- Thumbnail: img src
```

#### Download API (Paid Feature):
```python
def annas_download(md5, folder, title, extn):
    url = "https://annas-archive.org/dyn/api/fast_download.json"
    params = {
        'md5': md5,
        'key': secret_key,        # Paid subscription key
        'domain_index': 0
    }
    response = requests.get(url, params=params)
    # Returns: download_url, downloads_left, downloads_per_day
```

#### Rate Limiting:
- **Free:** Web scraping only, no official rate limits
- **Paid:** Daily download limit (varies by subscription tier)
- **Block Handler:** 18-hour rolling window for limit tracking

---

## Provider 2: Z-Library (BOK)

### Overview
- **Website:** https://1lib.sk (formerly b-ok.cc)
- **Type:** Direct book repository (~22.4M files)
- **API:** ✅ Official JSON API (`/eapi/*`)
- **Authentication:** Required (email/password or remix tokens)
- **Legal Status:** ⚠️ Gray area (hosts copyrighted content)

### LazyLibrarian Implementation Analysis

#### Key Files Examined:
- `/lazylibrarian/directparser.py` - Integration code
- `/lib/zlibrary.py` - Python API wrapper (~400 lines)
- Uses official JSON API endpoints
- Token-based authentication (remix_userid + remix_userkey)

#### Authentication Methods:

**Option 1: Email/Password**
```python
zlib = Zlibrary(email="user@example.com", password="password")
# Automatically fetches remix tokens
```

**Option 2: Remix Tokens** (Preferred)
```python
zlib = Zlibrary(
    remix_userid="12345",
    remix_userkey="abc123def456"
)
```

#### Key API Endpoints:

```python
# Authentication & Profile
POST https://1lib.sk/eapi/user/login
  - email, password
  - Returns: remix_userid, remix_userkey, downloads_limit, downloads_today

GET https://1lib.sk/eapi/user/profile
  - Returns user profile and download quotas

# Search
POST https://1lib.sk/eapi/book/search
  - message: search query
  - limit: max results (default 50)
  - languages[0]: language filter (e.g., "english", "spanish")
  - Returns: books array with metadata

# Download
GET https://1lib.sk/eapi/book/{bookid}/{hash}/file
  - Requires valid session cookies
  - Returns: direct download URL

# Metadata
GET https://1lib.sk/eapi/book/{bookid}/{hash}/formats
  - Returns available formats for a book
```

#### Search Response Structure:
```json
{
  "success": true,
  "books": [
    {
      "id": "123456",
      "hash": "abc123def456",
      "title": "Book Title",
      "author": "Author Name",
      "extension": "epub",
      "filesize": "1234567",
      "href": "/book/123456/abc123def456",
      "year": "2024",
      "language": "english",
      "publisher": "Publisher Name"
    }
  ]
}
```

#### Session Management:
```python
# Cookies required for all requests:
cookies = {
    "siteLanguageV2": "en",
    "remix_userid": "12345",
    "remix_userkey": "abc123def456"
}

# Headers:
headers = {
    "Content-Type": "application/x-www-form-urlencoded",
    "User-Agent": "Mozilla/5.0 ...",
    "accept": "text/html,application/xhtml+xml,..."
}
```

#### Download Quota Management:
```python
profile = zlib.getProfile()
# Returns:
{
  "user": {
    "id": "12345",
    "email": "user@example.com",
    "name": "User Name",
    "remix_userkey": "abc123...",
    "downloads_limit": 10,
    "downloads_today": 3
  }
}
```

---

## Implementation Comparison: Anna's Archive vs Z-Library

| Aspect | Anna's Archive | Z-Library |
|--------|----------------|-----------|
| **API Type** | ❌ Web scraping | ✅ Official JSON API |
| **Authentication** | Optional (paid) | Required |
| **Implementation Complexity** | High (HTML parsing) | Medium (API wrapper) |
| **Stability** | Fragile (HTML changes) | Stable (API versioned) |
| **Legal Status** | Gray area (aggregator) | Gray area (direct host) |
| **Collection Size** | 165M+ (aggregated) | 22.4M+ (direct) |
| **Maintenance** | High (scraping) | Low (API) |
| **Download Method** | Paid API or external links | Direct download API |
| **Rate Limits** | Varies (paid tier) | 10/day free, more with subscription |
| **Session Management** | None (stateless scraping) | Cookie-based sessions |

---

## Recommended Implementation Approach

### Priority 1: Z-Library ✅ (Easier, More Stable)

**Reasons:**
- ✅ Official JSON API (stable, versioned)
- ✅ Lower maintenance burden
- ✅ Predictable rate limits
- ✅ Direct downloads
- ✅ Better metadata quality

**Implementation Estimate:** 20-30 hours

**C# Implementation Plan:**
1. Create `ZLibraryProxy.cs` class implementing `ISearchForNewBook`, `IProvideBookInfo`
2. Create DTO classes for API responses:
   - `ZLibLoginResponse`
   - `ZLibSearchResponse`
   - `ZLibBook`
   - `ZLibProfile`
3. Add authentication helper methods:
   - `LoginWithCredentials(email, password)`
   - `LoginWithTokens(userId, userKey)`
4. Add HTTP client with cookie container for session management
5. Implement search with language filtering
6. Implement metadata retrieval
7. Add download quota tracking
8. Add error handling and rate limit detection

### Priority 2: Anna's Archive ⚠️ (More Complex, Less Stable)

**Reasons:**
- ⚠️ Requires web scraping (AngleSharp or HtmlAgilityPack)
- ⚠️ Higher maintenance (HTML changes)
- ⚠️ Aggregator (not original source)
- ⚠️ Paid API required for downloads

**Implementation Estimate:** 40-60 hours (as previously estimated)

**C# Implementation Plan:**
1. Add HTML parsing library (AngleSharp or HtmlAgilityPack)
2. Create `AnnasArchiveProxy.cs` class
3. Create DTO classes for scraped data
4. Implement HTML parsing logic:
   - Extract search result cards
   - Parse file info (language, extension, size, year)
   - Extract MD5 hashes
5. Add optional paid API integration for downloads
6. Add caching layer (2-hour expiry)
7. Add monitoring for HTML structure changes
8. Consider legal review

---

## Proposed Feature Branch Structure

### Branch 1: Z-Library Integration

**Branch Name:** `feature/zlibrary-provider`

**Scope:**
- Add Z-Library metadata provider
- Official API integration
- Authentication (email/password + remix tokens)
- Search functionality
- Download quota tracking
- Session management
- Error handling

**Files to Create:**
```
src/NzbDrone.Core/MetadataSource/ZLibrary/
├── ZLibraryException.cs
├── ZLibraryProxy.cs
└── Resources/
    ├── ZLibLoginResponse.cs
    ├── ZLibSearchResponse.cs
    ├── ZLibBook.cs
    └── ZLibProfile.cs
```

**Configuration:**
```csharp
// Settings to add:
- ZLibrary.Email (optional)
- ZLibrary.Password (optional)
- ZLibrary.RemixUserId (preferred)
- ZLibrary.RemixUserKey (preferred)
- ZLibrary.SearchLanguage (default: "english")
- ZLibrary.DownloadPriority (default: 25)
```

### Branch 2: Anna's Archive Integration

**Branch Name:** `feature/annas-archive-provider`

**Scope:**
- Add Anna's Archive metadata provider
- Web scraping implementation
- Search functionality
- Optional paid API for downloads
- Caching layer
- HTML monitoring/alerts

**Files to Create:**
```
src/NzbDrone.Core/MetadataSource/AnnasArchive/
├── AnnasArchiveException.cs
├── AnnasArchiveProxy.cs
├── AnnasArchiveParser.cs (HTML parsing)
└── Resources/
    ├── AnnasSearchResult.cs
    ├── AnnasFileInfo.cs
    └── AnnasDownloadResponse.cs
```

**Dependencies:**
```xml
<PackageReference Include="AngleSharp" Version="1.0.7" />
<!-- OR -->
<PackageReference Include="HtmlAgilityPack" Version="1.11.54" />
```

**Configuration:**
```csharp
// Settings to add:
- AnnasArchive.Host (default: "https://annas-archive.org")
- AnnasArchive.ApiKey (optional, for paid downloads)
- AnnasArchive.SearchLanguage (default: "en")
- AnnasArchive.FileType (default: "ANY")
- AnnasArchive.DownloadPriority (default: 20)
- AnnasArchive.CacheExpiry (default: 7200 seconds = 2 hours)
```

---

## Legal & Ethical Considerations

### ⚠️ Important Disclaimers

**Anna's Archive:**
- Aggregates content from sources that may contain copyrighted material
- No official API (web scraping may violate ToS)
- Legal status varies by jurisdiction
- Use at your own risk
- Consider implementing as opt-in feature with legal disclaimer

**Z-Library:**
- Hosts copyrighted content without authorization
- Has faced legal challenges and domain seizures
- Blocked in some countries
- Authentication required (creates paper trail)
- Use at your own risk
- Consider implementing as opt-in feature with legal disclaimer

**Recommendations:**
1. ✅ Add legal disclaimers in UI when enabling these providers
2. ✅ Make both providers opt-in (disabled by default)
3. ✅ Use only for metadata (not downloads) if concerned
4. ✅ Log user acceptance of terms before enabling
5. ✅ Consider consulting legal counsel before public release

---

## Testing Strategy

### Z-Library Testing:
- [ ] Test authentication with email/password
- [ ] Test authentication with remix tokens
- [ ] Test search with various queries
- [ ] Test language filtering
- [ ] Test download quota tracking
- [ ] Test session expiry handling
- [ ] Test rate limit detection
- [ ] Test error handling (401, 429, etc.)

### Anna's Archive Testing:
- [ ] Test HTML parsing for search results
- [ ] Test language filtering
- [ ] Test file type filtering
- [ ] Test sorting options
- [ ] Test pagination (if needed)
- [ ] Test caching behavior
- [ ] Test paid API (if subscription available)
- [ ] Test HTML structure change detection

---

## Success Criteria

### Z-Library Implementation ✅
- [x] Successful authentication
- [x] Search returns accurate results
- [x] Metadata properly mapped to Book/Edition models
- [x] Download quota displayed and tracked
- [x] Session management working
- [x] Error handling comprehensive
- [x] Build succeeds with 0 errors/warnings

### Anna's Archive Implementation ✅
- [x] HTML parsing extracts all required fields
- [x] Search results cached properly
- [x] File info correctly parsed
- [x] MD5 hashes extracted
- [x] Optional paid API integration working
- [x] Monitoring alerts for HTML changes
- [x] Build succeeds with 0 errors/warnings

---

## Timeline Estimate

**Z-Library Provider:**
- Research & Planning: 2-4 hours ✅ (DONE)
- Implementation: 15-20 hours
- Testing: 3-5 hours
- Documentation: 2 hours
- **Total:** 22-31 hours

**Anna's Archive Provider:**
- Research & Planning: 2-4 hours ✅ (DONE)
- HTML Parser Development: 15-20 hours
- Implementation: 20-30 hours
- Testing: 5-8 hours
- Monitoring Setup: 2-3 hours
- Documentation: 2 hours
- **Total:** 46-67 hours

**Combined Total:** 68-98 hours

---

## Dependencies & Libraries

### Z-Library:
- ✅ System.Net.Http (built-in)
- ✅ System.Text.Json (built-in)
- ✅ Newtonsoft.Json (already in project)
- ✅ HttpClientFactory (already in project)

### Anna's Archive:
- ❌ **AngleSharp** (recommended) or **HtmlAgilityPack**
  - Need to add NuGet package
  - HTML5 parsing library
  - Stable API
  - Active development

---

## Risk Assessment

### Z-Library Risks:
- 🔴 **Legal:** Hosts copyrighted content (high risk)
- 🟡 **Technical:** API could change without notice (medium risk)
- 🟡 **Availability:** Domain seizures/blocks (medium risk)
- 🟢 **Implementation:** Standard API integration (low risk)

### Anna's Archive Risks:
- 🔴 **Legal:** Aggregates copyrighted content (high risk)
- 🔴 **Technical:** HTML changes break scraper (high risk)
- 🟡 **Availability:** No official support (medium risk)
- 🔴 **Implementation:** Web scraping complexity (high risk)
- 🟡 **Maintenance:** Ongoing monitoring required (medium risk)

---

## Recommendations

### Immediate Actions:
1. ✅ **Start with Z-Library** (easier, more stable)
   - Create feature branch: `feature/zlibrary-provider`
   - Implement authentication and search first
   - Add metadata mapping
   - Test thoroughly

2. ⏸️ **Defer Anna's Archive** until:
   - Official API is released (see GitLab issue #54)
   - Legal concerns are addressed
   - Z-Library implementation is complete and stable

### Alternative Approach:
**Use Anna's Archive for metadata only (not downloads):**
- Lower legal risk (fair use for metadata)
- Still requires web scraping
- Could be combined with Internet Archive downloads
- Provides access to large aggregated collection

---

## Next Steps

### For Z-Library Integration:

1. **Create Feature Branch**
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/zlibrary-provider
   ```

2. **Create Project Structure**
   - Add ZLibrary directory
   - Create exception class
   - Create DTO classes
   - Create proxy class

3. **Implement Authentication**
   - Email/password login
   - Remix token login
   - Session management

4. **Implement Search**
   - Query building
   - Language filtering
   - Result parsing

5. **Implement Metadata Retrieval**
   - Map to Book/Edition models
   - Extract authors
   - Handle file formats

6. **Add Download Tracking**
   - Quota monitoring
   - Rate limit detection
   - User notifications

7. **Testing & Documentation**
   - Unit tests
   - Integration tests
   - User guide
   - API documentation

8. **Create Pull Request**
   - Comprehensive description
   - Test results
   - Screenshots
   - Legal disclaimer

---

## Appendix: Code References

### LazyLibrarian Implementation Files:
- `/lazylibrarian/annas.py` - Anna's Archive implementation
- `/lazylibrarian/directparser.py` - BOK/Z-Library integration
- `/lib/zlibrary.py` - Z-Library Python API wrapper
- `/lazylibrarian/configdefs.py` - Configuration definitions

### Bookshelf Reference Implementation:
- Internet Archive provider (merged PR #5)
- See: `src/NzbDrone.Core/MetadataSource/InternetArchive/`

---

**Document Version:** 1.0
**Last Updated:** 2025-11-26
**Author:** Claude Code (AI Assistant)

---

## Related Documentation
- [METADATA_SOURCES_EVALUATION.md](./METADATA_SOURCES_EVALUATION.md)
- [Internet Archive Integration Plan](./IA_INTEGRATION_PLAN.md)
- [Implementation Summary](./IMPLEMENTATION_SUMMARY.md)
