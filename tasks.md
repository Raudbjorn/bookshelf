# Anna's Archive Provider - Task Breakdown

**Project:** Bookshelf (Readarr fork)
**Feature:** Anna's Archive metadata provider integration
**Version:** 1.0
**Date:** 2025-11-27
**Status:** In Development 🚧

---

## Task Overview

**Total Estimated Time**: ~10 hours (Phase 1 MVP)
**Completed**: 3/30 tasks (10%)
**In Progress**: 1/30 tasks
**Remaining**: 26/30 tasks

---

## Phase 0: Spec-Driven Development ✅

### Task 0.1: Create Requirements Document ✅
- **Status**: COMPLETED
- **Estimated**: 1 hour
- **Actual**: 1 hour
- **Assignee**: Claude Code
- **Description**: Create comprehensive requirements.md with functional and non-functional requirements
- **Deliverables**:
  - [x] requirements.md created (13KB, 800+ lines)
  - [x] 8 functional requirements defined
  - [x] 8 non-functional requirements defined
  - [x] Success criteria documented
  - [x] Acceptance tests defined

### Task 0.2: Create Design Document ✅
- **Status**: COMPLETED
- **Estimated**: 1 hour
- **Actual**: 1 hour
- **Assignee**: Claude Code
- **Description**: Create comprehensive design.md with architecture and data flow
- **Deliverables**:
  - [x] design.md created (22KB, 1000+ lines)
  - [x] Architecture diagrams documented
  - [x] Data flow diagrams created
  - [x] Class design specified
  - [x] Caching strategy defined
  - [x] Error handling designed

### Task 0.3: Create Work Repository Clone ✅
- **Status**: COMPLETED
- **Estimated**: 5 minutes
- **Actual**: 5 minutes
- **Assignee**: Claude Code
- **Description**: Clone repository to isolated work directory
- **Deliverables**:
  - [x] Repository cloned to `/home/svnbjrn/projects/bookshelf/wrk/annas-archive-provider`
  - [x] Feature branch created: `feature/annas-archive-provider`
  - [x] Clean working tree verified

### Task 0.4: Create Task Breakdown (This Document) 🚧
- **Status**: IN PROGRESS
- **Estimated**: 30 minutes
- **Actual**: TBD
- **Assignee**: Claude Code
- **Description**: Create comprehensive tasks.md with all implementation tasks
- **Deliverables**:
  - [ ] tasks.md created with all Phase 1 tasks
  - [ ] Time estimates for each task
  - [ ] Dependencies mapped
  - [ ] Success criteria per task

---

## Phase 1: Foundation & Setup

### Task 1.1: Copy Existing DTO Files to Work Directory
- **Status**: PENDING
- **Estimated**: 10 minutes
- **Priority**: HIGH
- **Dependencies**: Task 0.3
- **Description**: Copy already-created DTO files from main bookshelf directory
- **Steps**:
  1. Copy `src/NzbDrone.Core/MetadataSource/AnnasArchive/` directory
  2. Verify all 9 files present
  3. Check file integrity
- **Deliverables**:
  - [ ] AnnasArchiveException.cs copied
  - [ ] AnnasArchiveProxy.cs copied (with errors - will fix)
  - [ ] All 8 Resource DTO files copied
- **Success Criteria**:
  - All files present in work directory
  - No corruption during copy

### Task 1.2: Create Helper Method - CreateSlug()
- **Status**: PENDING
- **Estimated**: 15 minutes
- **Priority**: HIGH
- **Dependencies**: Task 1.1
- **Description**: Create slug generation method (like IA's CreateAuthorSlug)
- **Implementation**:
```csharp
private string CreateSlug(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return "unknown";

    // Normalize whitespace
    var normalized = System.Text.RegularExpressions.Regex.Replace(
        name.Trim(), @"\s+", " ");

    // Convert to lowercase, replace spaces with hyphens, remove punctuation
    return normalized.ToLowerInvariant()
        .Replace(" ", "-")
        .Replace(".", "")
        .Replace(",", "")
        .Replace("'", "")
        .Replace("\"", "")
        .Replace("(", "")
        .Replace(")", "");
}
```
- **Deliverables**:
  - [ ] CreateSlug() method added to AnnasArchiveProxy
  - [ ] Unit test for CreateSlug() created
- **Success Criteria**:
  - "Michele Boldrin" → "michele-boldrin"
  - "David K. Levine" → "david-k-levine"
  - "  Spaces  " → "spaces"

### Task 1.3: Fix HTTP Request Pattern
- **Status**: PENDING
- **Estimated**: 20 minutes
- **Priority**: HIGH
- **Dependencies**: Task 1.1
- **Description**: Replace HttpRequest with HttpRequestBuilder pattern
- **Changes**:
```csharp
// OLD (incorrect):
var httpRequest = new HttpRequest(url);
httpRequest.Headers.UserAgent = UserAgent;

// NEW (correct):
var httpRequest = new HttpRequestBuilder(url)
    .SetHeader("User-Agent", UserAgent)
    .Build();
```
- **Deliverables**:
  - [ ] GetBookByMd5() uses HttpRequestBuilder
  - [ ] Proper error handling with HasHttpError
- **Success Criteria**:
  - Compiles without errors
  - Follows same pattern as InternetArchiveProxy

### Task 1.4: Fix Book Model Mapping
- **Status**: PENDING
- **Estimated**: 30 minutes
- **Priority**: HIGH
- **Dependencies**: Task 1.1
- **Description**: Remove references to non-existent Book properties
- **Changes Required**:
  1. Remove `Book.Authors` (doesn't exist)
  2. Remove `Book.Overview` (only in Edition)
  3. Remove `Book.AuthorMetadata` assignment (LazyLoaded, can't set directly)
  4. Remove `Author.NameLastFirst` (only in AuthorMetadata)
- **Correct Pattern**:
```csharp
var book = new Book
{
    ForeignBookId = $"aa:{md5}",
    Title = GetBestTitle(record),
    TitleSlug = md5,
    CleanTitle = Parser.Parser.CleanAuthorName(GetBestTitle(record)),
    ReleaseDate = ParseReleaseDate(record),
    Links = GetLinks(record),
    Genres = new List<string>(),
    Ratings = new Ratings { Votes = 0, Value = 0 },
    Editions = new List<Edition> { CreateEdition(record) },
    AnyEditionOk = true
};
```
- **Deliverables**:
  - [ ] MapRecordToBook() fixed
  - [ ] CreateEdition() fixed
  - [ ] GetAuthorMetadata() returns correct list
- **Success Criteria**:
  - Compiles without model errors
  - Book object properly constructed

### Task 1.5: Fix Edition PageCount Type
- **Status**: PENDING
- **Estimated**: 10 minutes
- **Priority**: MEDIUM
- **Dependencies**: Task 1.4
- **Description**: Handle PageCount as `int` (not `int?`)
- **Changes**:
```csharp
// OLD:
PageCount = GetBestPageCount(record)  // Returns int?

// NEW:
PageCount = GetBestPageCount(record) ?? 0  // Convert to int with default
```
- **Deliverables**:
  - [ ] GetBestPageCount() updated to return int
  - [ ] Null coalescing added where needed
- **Success Criteria**:
  - PageCount compiles without warnings
  - Defaults to 0 when no page count available

### Task 1.6: Remove Unused Using Directives
- **Status**: PENDING
- **Estimated**: 5 minutes
- **Priority**: LOW
- **Dependencies**: Task 1.3, Task 1.4
- **Description**: Clean up unused imports flagged by compiler
- **Files to Check**:
  - AnnasArchiveProxy.cs
  - AAInternetArchive.cs
- **Deliverables**:
  - [ ] All unused using directives removed
  - [ ] IDE0005 errors resolved
- **Success Criteria**:
  - 0 IDE0005 warnings
  - Clean build output

---

## Phase 2: Core Implementation Fixes

### Task 2.1: Implement GetAuthorMetadata() Correctly
- **Status**: PENDING
- **Estimated**: 20 minutes
- **Priority**: HIGH
- **Dependencies**: Task 1.2, Task 1.4
- **Description**: Return AuthorMetadata list (not populate Book.AuthorMetadata directly)
- **Implementation**:
```csharp
private List<AuthorMetadata> GetAuthorMetadata(AARecord record)
{
    var authorNames = GetAuthorNames(record);

    if (!authorNames.Any())
    {
        return new List<AuthorMetadata>
        {
            new AuthorMetadata
            {
                ForeignAuthorId = "aa-author:unknown",
                Name = "Unknown Author"
            }
        };
    }

    return authorNames.Select(name => new AuthorMetadata
    {
        ForeignAuthorId = $"aa-author:{CreateSlug(name)}",
        Name = name
    }).ToList();
}
```
- **Deliverables**:
  - [ ] GetAuthorMetadata() implemented
  - [ ] Returns List<AuthorMetadata>
  - [ ] Uses CreateSlug() for IDs
- **Success Criteria**:
  - Returns valid AuthorMetadata list
  - Handles empty author lists gracefully

### Task 2.2: Fix GetBookInfo() Return Type
- **Status**: PENDING
- **Estimated**: 10 minutes
- **Priority**: HIGH
- **Dependencies**: Task 2.1
- **Description**: Return Tuple<string, Book, List<AuthorMetadata>> correctly
- **Implementation**:
```csharp
public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
{
    var md5 = ExtractMd5FromForeignId(foreignBookId);
    if (string.IsNullOrEmpty(md5))
        throw new AnnasArchiveException("Invalid foreign book ID format");

    var book = GetBookByMd5(md5);
    var authors = GetAuthorMetadata(record);  // Need to pass record!
    var authorId = authors.FirstOrDefault()?.ForeignAuthorId ?? "unknown";

    return Tuple.Create(authorId, book, authors);
}
```
- **NOTE**: Need to refactor to pass AARecord to avoid double deserialization
- **Deliverables**:
  - [ ] GetBookInfo() returns correct tuple
  - [ ] Refactored to avoid double API call
- **Success Criteria**:
  - Implements IProvideBookInfo correctly
  - Compiles without errors

### Task 2.3: Refactor to Avoid Double Deserialization
- **Status**: PENDING
- **Estimated**: 30 minutes
- **Priority**: MEDIUM
- **Dependencies**: Task 2.2
- **Description**: GetBookByMd5 should return AARecord, separate method creates Book
- **New Structure**:
```csharp
// 1. Fetch and deserialize
private AARecord FetchRecordByMd5(string md5);

// 2. Map to models
private Book MapRecordToBook(AARecord record);
private List<AuthorMetadata> MapRecordToAuthors(AARecord record);

// 3. Public method combines them
public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
{
    var md5 = ExtractMd5FromForeignId(foreignBookId);
    var record = FetchRecordByMd5(md5);
    var book = MapRecordToBook(record);
    var authors = MapRecordToAuthors(record);
    var authorId = authors.FirstOrDefault()?.ForeignAuthorId ?? "unknown";

    return Tuple.Create(authorId, book, authors);
}
```
- **Deliverables**:
  - [ ] FetchRecordByMd5() method created
  - [ ] MapRecordToBook() refactored
  - [ ] MapRecordToAuthors() created
  - [ ] GetBookInfo() refactored
- **Success Criteria**:
  - Only one API call per request
  - Clean separation of concerns

### Task 2.4: Add GetStringValue() Helper
- **Status**: PENDING
- **Estimated**: 15 minutes
- **Priority**: MEDIUM
- **Dependencies**: None
- **Description**: Add helper for extracting strings from JsonElement or object
- **Implementation**: Copy from InternetArchiveProxy.cs
- **Deliverables**:
  - [ ] GetStringValue(object) method added
  - [ ] Handles string, JsonElement, IEnumerable
- **Success Criteria**:
  - Works with all value types
  - Returns null for unexpected types

### Task 2.5: Add GetListValue() Helper
- **Status**: PENDING
- **Estimated**: 15 minutes
- **Priority**: MEDIUM
- **Dependencies**: None
- **Description**: Add helper for extracting string lists from JsonElement or object
- **Implementation**: Copy from InternetArchiveProxy.cs
- **Deliverables**:
  - [ ] GetListValue(object) method added
  - [ ] Handles string, JsonElement array, IEnumerable
- **Success Criteria**:
  - Works with all value types
  - Returns empty list for unexpected types

---

## Phase 3: Testing

### Task 3.1: Build and Fix Compilation Errors
- **Status**: PENDING
- **Estimated**: 1 hour
- **Priority**: CRITICAL
- **Dependencies**: Tasks 1.1-1.6, Tasks 2.1-2.5
- **Description**: Build project and fix all remaining errors
- **Steps**:
  1. Run `dotnet build src/NzbDrone.Core/Readarr.Core.csproj`
  2. Review all errors
  3. Fix each error systematically
  4. Re-build until 0 errors
- **Deliverables**:
  - [ ] Build succeeds with 0 errors
  - [ ] Build succeeds with 0 warnings
- **Success Criteria**:
  - Clean build output
  - All tests pass (if any)

### Task 3.2: Integration Test - Valid MD5
- **Status**: PENDING
- **Estimated**: 30 minutes
- **Priority**: HIGH
- **Dependencies**: Task 3.1
- **Description**: Test with real AA API call using valid MD5
- **Test Steps**:
  1. Create simple console app or unit test
  2. Call `GetBookInfo("aa:8336332bf5877e3adbfb60ac70720cd5")`
  3. Verify response
- **Expected Results**:
  - Book.Title = "Against intellectual monopoly"
  - Book.ForeignBookId = "aa:8336332bf5877e3adbfb60ac70720cd5"
  - Authors includes "Michele Boldrin" and "David K. Levine"
  - Edition.Isbn13 is valid
  - Edition.Links includes AA link
  - Edition.Links includes IPFS link (if available)
- **Deliverables**:
  - [ ] Integration test created
  - [ ] Test passes with real API
  - [ ] Response logged for inspection
- **Success Criteria**:
  - Test passes
  - All expected fields populated

### Task 3.3: Integration Test - Invalid MD5
- **Status**: PENDING
- **Estimated**: 15 minutes
- **Priority**: MEDIUM
- **Dependencies**: Task 3.1
- **Description**: Test error handling with invalid MD5
- **Test Cases**:
  1. Short MD5: "abc123"
  2. Invalid chars: "gggggggggggggggggggggggggggggggg"
  3. Not found: "00000000000000000000000000000000"
- **Expected Results**:
  - Case 1: AnnasArchiveException("Invalid MD5 hash")
  - Case 2: AnnasArchiveException("Invalid MD5 hash")
  - Case 3: BookNotFoundException
- **Deliverables**:
  - [ ] Error handling test created
  - [ ] All error cases tested
- **Success Criteria**:
  - Correct exceptions thrown
  - Error messages are clear

### Task 3.4: Unit Test - Metadata Aggregation
- **Status**: PENDING
- **Estimated**: 45 minutes
- **Priority**: MEDIUM
- **Dependencies**: Task 3.1
- **Description**: Test metadata priority logic with mocked AARecord
- **Test Cases**:
  1. Title: ISBNdb present → use ISBNdb
  2. Title: ISBNdb null, Libgen present → use Libgen
  3. Authors: ISBNdb list present → use list
  4. Authors: ISBNdb null, string present → split string
  5. ISBN: Validate checksum
- **Deliverables**:
  - [ ] GetBestTitle() unit test
  - [ ] GetBestPublisher() unit test
  - [ ] GetAuthorNames() unit test
  - [ ] ExtractBestIsbn13() unit test
  - [ ] IsValidIsbn13() unit test
- **Success Criteria**:
  - All tests pass
  - Coverage >80%

### Task 3.5: Unit Test - Author Splitting
- **Status**: PENDING
- **Estimated**: 20 minutes
- **Priority**: LOW
- **Dependencies**: Task 3.1
- **Description**: Test SplitAuthors() with various formats
- **Test Cases**:
  1. Semicolon: "Author A; Author B" → ["Author A", "Author B"]
  2. Comma: "Author A, Author B" → ["Author A", "Author B"]
  3. " and ": "Author A and Author B" → ["Author A", "Author B"]
  4. " & ": "Author A & Author B" → ["Author A", "Author B"]
  5. Single: "Author A" → ["Author A"]
  6. Trim: " Author A ; Author B " → ["Author A", "Author B"]
- **Deliverables**:
  - [ ] SplitAuthors() unit test with 6+ cases
- **Success Criteria**:
  - All test cases pass
  - Handles edge cases (null, empty)

### Task 3.6: Unit Test - ISBN Validation
- **Status**: PENDING
- **Estimated**: 30 minutes
- **Priority**: MEDIUM
- **Dependencies**: Task 3.1
- **Description**: Test ISBN-13 checksum validation
- **Test Cases**:
  1. Valid: "9780511410840" → true
  2. Invalid checksum: "9780511410841" → false
  3. Too short: "978051141084" → false
  4. Too long: "97805114108400" → false
  5. Non-digits: "978051141084X" → false
  6. Null/empty: "" → false
- **Deliverables**:
  - [ ] IsValidIsbn13() unit test
  - [ ] CleanIsbn() unit test
- **Success Criteria**:
  - All test cases pass
  - Correct checksum algorithm

### Task 3.7: Unit Test - Date Parsing
- **Status**: PENDING
- **Estimated**: 20 minutes
- **Priority**: LOW
- **Dependencies**: Task 3.1
- **Description**: Test ParseReleaseDate() with various formats
- **Test Cases**:
  1. Full date: "2008-01-15" → DateTime(2008, 1, 15)
  2. Year only: "2008" → DateTime(2008, 1, 1)
  3. Year-month: "2008-05" → DateTime(2008, 5, 1)
  4. Invalid: "abc" → null
  5. Empty: "" → null
  6. Future year: "2050" → DateTime(2050, 1, 1)
- **Deliverables**:
  - [ ] ParseReleaseDate() unit test
- **Success Criteria**:
  - All formats parsed correctly
  - Invalid inputs return null

---

## Phase 4: Documentation & PR

### Task 4.1: Add XML Documentation Comments
- **Status**: PENDING
- **Estimated**: 30 minutes
- **Priority**: MEDIUM
- **Dependencies**: Task 3.1
- **Description**: Add XML doc comments to all public methods
- **Files to Document**:
  - AnnasArchiveProxy.cs (all public methods)
  - All DTO classes (class-level comments)
- **Deliverables**:
  - [ ] All public methods documented
  - [ ] All classes documented
  - [ ] All parameters documented
- **Success Criteria**:
  - IntelliSense shows documentation
  - No missing doc warnings

### Task 4.2: Create IMPLEMENTATION_NOTES.md
- **Status**: PENDING
- **Estimated**: 20 minutes
- **Priority**: LOW
- **Dependencies**: Task 3.1
- **Description**: Document implementation details for future maintainers
- **Contents**:
  - How to add new metadata sources
  - How priority system works
  - How to update DTOs for AA API changes
  - Known limitations
- **Deliverables**:
  - [ ] IMPLEMENTATION_NOTES.md created
- **Success Criteria**:
  - Clear, actionable documentation

### Task 4.3: Update ANNAS_ARCHIVE_STATUS.md
- **Status**: PENDING
- **Estimated**: 15 minutes
- **Priority**: LOW
- **Dependencies**: Task 3.1
- **Description**: Update status document with final results
- **Updates**:
  - Build status: PASSING
  - Test results: X/Y passing
  - Known issues: None (or list remaining)
  - Next steps: Phase 2 planning
- **Deliverables**:
  - [ ] ANNAS_ARCHIVE_STATUS.md updated
- **Success Criteria**:
  - Accurate status reflection

### Task 4.4: Create PR Description
- **Status**: PENDING
- **Estimated**: 30 minutes
- **Priority**: HIGH
- **Dependencies**: All Phase 3 tasks
- **Description**: Write comprehensive PR description
- **Contents**:
  - **Summary**: What this adds
  - **Motivation**: Why this is valuable
  - **Implementation**: How it works
  - **Testing**: What was tested
  - **Screenshots**: Example output
  - **Checklist**: All PR requirements met
- **Deliverables**:
  - [ ] PR description drafted
  - [ ] Screenshots captured
  - [ ] Checklist completed
- **Success Criteria**:
  - Clear, comprehensive description
  - Reviewer can understand without reading code

### Task 4.5: Commit and Push Changes
- **Status**: PENDING
- **Estimated**: 15 minutes
- **Priority**: CRITICAL
- **Dependencies**: Task 4.4
- **Description**: Commit all changes and push to fork
- **Steps**:
  1. Review all changes: `git status`
  2. Stage changes: `git add .`
  3. Commit with message: `git commit -m "Add Anna's Archive metadata provider"`
  4. Push to fork: `git push -u origin feature/annas-archive-provider`
- **Deliverables**:
  - [ ] All files committed
  - [ ] Pushed to remote
  - [ ] Branch visible on GitHub
- **Success Criteria**:
  - Clean commit history
  - All changes included

### Task 4.6: Create Pull Request
- **Status**: PENDING
- **Estimated**: 10 minutes
- **Priority**: CRITICAL
- **Dependencies**: Task 4.5
- **Description**: Create PR from feature branch to main
- **Steps**:
  1. Navigate to GitHub
  2. Click "New Pull Request"
  3. Select: base: `main` ← compare: `feature/annas-archive-provider`
  4. Paste PR description from Task 4.4
  5. Submit PR
- **Deliverables**:
  - [ ] PR created
  - [ ] Linked to relevant issues (if any)
  - [ ] Reviewers assigned (if required)
- **Success Criteria**:
  - PR is visible and ready for review
  - CI checks pass (if present)

---

## Phase 5: Code Review & Refinement

### Task 5.1: Address Code Review Comments
- **Status**: PENDING (blocked until PR created)
- **Estimated**: 2-4 hours (variable)
- **Priority**: HIGH
- **Dependencies**: Task 4.6
- **Description**: Respond to and address all code review feedback
- **Process**:
  1. Read all comments
  2. Prioritize by severity
  3. Fix bugs first
  4. Implement suggestions
  5. Respond to each comment
  6. Request re-review
- **Deliverables**:
  - [ ] All review comments addressed
  - [ ] Code quality improved
  - [ ] Tests updated if needed
- **Success Criteria**:
  - All reviewers approve
  - No unresolved conversations

### Task 5.2: Merge to Main
- **Status**: PENDING (blocked until approval)
- **Estimated**: 5 minutes
- **Priority**: CRITICAL
- **Dependencies**: Task 5.1
- **Description**: Merge approved PR to main branch
- **Steps**:
  1. Ensure all checks pass
  2. Ensure all approvals received
  3. Squash and merge (if project uses squash)
  4. Delete feature branch
- **Deliverables**:
  - [ ] PR merged
  - [ ] Feature branch deleted
  - [ ] Main branch updated
- **Success Criteria**:
  - Clean merge (no conflicts)
  - Main branch builds successfully

---

## Summary of Task Dependencies

```
Phase 0 (Spec)
    └─→ Phase 1 (Foundation)
            ├─→ Task 1.1 (Copy files)
            ├─→ Task 1.2 (CreateSlug) ─┐
            ├─→ Task 1.3 (HTTP pattern) │
            ├─→ Task 1.4 (Book mapping) ├─→ Phase 2 (Core Impl)
            ├─→ Task 1.5 (PageCount)    │       ├─→ Task 2.1 (AuthorMetadata)
            └─→ Task 1.6 (Cleanup)      │       ├─→ Task 2.2 (GetBookInfo)
                                         │       ├─→ Task 2.3 (Refactor)
                                         │       ├─→ Task 2.4 (GetStringValue)
                                         │       └─→ Task 2.5 (GetListValue)
                                         │               ↓
                                         └───────→ Phase 3 (Testing)
                                                       ├─→ Task 3.1 (Build)
                                                       ├─→ Task 3.2 (Integration test - valid)
                                                       ├─→ Task 3.3 (Integration test - invalid)
                                                       ├─→ Task 3.4 (Unit test - aggregation)
                                                       ├─→ Task 3.5 (Unit test - authors)
                                                       ├─→ Task 3.6 (Unit test - ISBN)
                                                       └─→ Task 3.7 (Unit test - dates)
                                                               ↓
                                                       Phase 4 (Documentation)
                                                           ├─→ Task 4.1 (XML docs)
                                                           ├─→ Task 4.2 (Implementation notes)
                                                           ├─→ Task 4.3 (Update status)
                                                           ├─→ Task 4.4 (PR description)
                                                           ├─→ Task 4.5 (Commit & push)
                                                           └─→ Task 4.6 (Create PR)
                                                                   ↓
                                                               Phase 5 (Review)
                                                                   ├─→ Task 5.1 (Address comments)
                                                                   └─→ Task 5.2 (Merge)
```

---

## Time Tracking

| Phase | Tasks | Estimated | Actual | Status |
|-------|-------|-----------|--------|--------|
| Phase 0: Spec | 4 | 2.5 hrs | 2.5 hrs | ✅ DONE |
| Phase 1: Foundation | 6 | 1.5 hrs | TBD | ⏳ PENDING |
| Phase 2: Core Impl | 5 | 2 hrs | TBD | ⏳ PENDING |
| Phase 3: Testing | 7 | 3 hrs | TBD | ⏳ PENDING |
| Phase 4: Documentation | 6 | 2 hrs | TBD | ⏳ PENDING |
| Phase 5: Review | 2 | 2-4 hrs | TBD | ⏳ PENDING |
| **TOTAL** | **30** | **13-15 hrs** | **2.5 hrs** | **17% DONE** |

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation | Owner |
|------|------------|--------|------------|-------|
| Compilation errors persist | Medium | High | Incremental testing, follow IA pattern | Dev |
| AA API changes during dev | Low | Medium | Use versioned endpoint, comprehensive tests | Dev |
| ISBN validation bugs | Low | Low | Extensive unit tests | Dev |
| Performance issues | Low | Low | Caching (24hr TTL) | Dev |
| Code review delays | Medium | Low | Clear documentation, good tests | Dev |

---

## Daily Standup Template

### Today's Focus:
- [ ] Task X.Y: [Task Name]
- [ ] Task X.Z: [Task Name]

### Blockers:
- None / [Describe blocker]

### Completed Yesterday:
- [x] Task X.Y: [Task Name]

### Notes:
- [Any relevant notes]

---

## Definition of Done

A task is considered "done" when ALL of the following are true:

- [ ] Code compiles with 0 errors
- [ ] Code compiles with 0 warnings
- [ ] Unit tests written and passing (if applicable)
- [ ] Integration tests written and passing (if applicable)
- [ ] Code reviewed by peer (Phase 5 only)
- [ ] Documentation updated (if applicable)
- [ ] No regression in existing functionality

---

## Next Steps (Immediate)

1. **Start Task 1.1**: Copy existing DTO files to work directory
2. **Start Task 1.2**: Create CreateSlug() helper method
3. **Start Task 1.3**: Fix HTTP request pattern
4. **Continue systematically** through Phase 1 tasks

---

**Document Version**: 1.0
**Last Updated**: 2025-11-27
**Author**: Claude Code (AI Assistant)
**Next Review**: After Phase 1 completion
