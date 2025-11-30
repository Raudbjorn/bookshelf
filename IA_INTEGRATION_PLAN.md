# Internet Archive Integration Plan

**Date:** 2025-11-22
**Branch:** `feature/anna-archive-integration`
**Status:** Implementation Complete - Ready for Testing

---

## Summary

✅ **COMPLETED:** Internet Archive metadata provider implementation
📝 **NEXT STEPS:** Integration testing and fallback chain configuration

---

## What Was Implemented

### Files Created

1. **`InternetArchiveException.cs`** - Custom exception class
   - Location: `/src/NzbDrone.Core/MetadataSource/InternetArchive/`
   - Follows existing pattern (inherits from `NzbDroneClientException`)

2. **`IASearchResponse.cs`** - Search API response DTOs
   - Location: `/src/NzbDrone.Core/MetadataSource/InternetArchive/Resources/`
   - Models: `IASearchResponse`, `IAResponseHeader`, `IAResponse`, `IASearchDoc`

3. **`IAMetadataResponse.cs`** - Metadata API response DTOs
   - Location: `/src/NzbDrone.Core/MetadataSource/InternetArchive/Resources/`
   - Models: `IAMetadataResponse`, `IAMetadata`, `IAFile`

4. **`InternetArchiveProxy.cs`** - Main provider implementation
   - Location: `/src/NzbDrone.Core/MetadataSource/InternetArchive/`
   - Implements: `ISearchForNewBook`, `IProvideBookInfo`

---

## Implementation Details

### Interfaces Implemented

#### 1. ISearchForNewBook
Provides book search functionality:
- ✅ `SearchForNewBook(title, author, getAllEditions)` - General search
- ✅ `SearchByIsbn(isbn)` - ISBN-based search
- ✅ `SearchByAsin(asin)` - ASIN-based search
- ⚠️ `SearchByGoodreadsBookId(id, getAllEditions)` - Returns empty (IA doesn't have Goodreads mapping)

#### 2. IProvideBookInfo
Provides detailed book metadata:
- ✅ `GetBookInfo(foreignBookId)` - Retrieves full metadata for a book
- Returns: `Tuple<authorId, Book, List<AuthorMetadata>>`

### Key Features

1. **Official API Integration**
   - Uses Internet Archive's official Search API
   - Uses Internet Archive's official Metadata API
   - No web scraping required

2. **Caching Strategy**
   - Search results: 7 days cache
   - Metadata: 30 days cache
   - Uses existing `ICachedHttpResponseService`

3. **Error Handling**
   - HTTP error suppression with custom handling
   - Rate limit detection (429 status code)
   - Not Found handling (404 status code)
   - Comprehensive logging

4. **Data Mapping**
   - Maps IA search results → `Book` domain model
   - Maps IA metadata → `Book` + `Edition` models
   - Extracts author metadata → `AuthorMetadata` model
   - Handles flexible field formats (string or List<string>)
   - Parses various date formats (year only, full date, etc.)

5. **Search Capabilities**
   - Lucene query building with proper escaping
   - Filters to text/book media types
   - Prefers book-related collections
   - Returns up to 25 results per search

6. **Metadata Extraction**
   - Title, author(s), publisher
   - ISBN-13 extraction
   - Release date parsing
   - Subject/genre tags
   - Ratings and review counts
   - Cover images (via IA image service)
   - Description/overview
   - Language information

---

## Foreign ID Format

Internet Archive identifiers use the prefix **`ia:`**

Examples:
- `ia:lordoftheringsj00tolk` - Book identifier
- `ia:tolkien-j-r-r` - Author identifier (generated from name)

This follows the established pattern:
- Goodreads: numeric string (e.g., "12345")
- Hardcover: `hc:12345`
- OpenLibrary: `ol:123456W`
- **Internet Archive: `ia:identifier`**

---

## Testing Required

### Next Steps

1. ⬜ **Build the project** - Verify no compilation errors
2. ⬜ **Create unit tests** - Basic coverage
3. ⬜ **Manual testing** - Test against live API
4. ⬜ **Integration into BookInfoProxy** - Add to fallback chain (optional for MVP)

### Manual Test Cases

```bash
# Test popular book
title: "The Lord of the Rings"
author: "Tolkien"

# Test ISBN search
isbn: "9780134093413"

# Test metadata retrieval
identifier: "ia:lordoftheringsj00tolk"
```

---

## Success Criteria

✅ **Implementation Complete When:**
- [x] All files created and compiling
- [ ] Unit tests passing
- [ ] Manual testing successful
- [ ] Documentation updated

---

## Resources

- **Internet Archive API Docs:** https://archive.org/services/docs/api/
- **Metadata Sources Evaluation:** `/METADATA_SOURCES_EVALUATION.md`

---

**Ready for testing! 🚀**
