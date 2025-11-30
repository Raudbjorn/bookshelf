using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource.AnnasArchive.Resources;

namespace NzbDrone.Core.MetadataSource.AnnasArchive
{
    /// <summary>
    /// Anna's Archive metadata provider.
    /// Aggregates metadata from 165M+ files across 11+ sources including Libgen, Z-Library, ISBNdb, OpenLibrary, and Internet Archive.
    /// Uses official JSON API for individual record lookups by MD5 hash.
    /// </summary>
    /// <remarks>
    /// Phase 1 implementation provides metadata retrieval by MD5 hash only.
    /// Search functionality (by title, author, ISBN) is deferred to Phase 2.
    /// Foreign ID format: aa:{md5hash}
    /// Author ID format: aa-author:{slug}
    /// </remarks>
    public class AnnasArchiveProxy : ISearchForNewBook, IProvideBookInfo
    {
        private const string JsonApiUrl = "https://annas-archive.org/db/aarecord_elasticsearch/md5:{0}.json.html";
        private const string SearchUrl = "https://annas-archive.org/search";
        private const int MaxSearchResults = 25;
        private const string UserAgent = "Bookshelf/1.0";

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly Logger _logger;

        public AnnasArchiveProxy(
            IHttpClient httpClient,
            ICachedHttpResponseService cachedHttpClient,
            Logger logger)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _logger = logger;
        }

        /// <summary>
        /// Search for books by title and author.
        /// </summary>
        /// <param name="title">Book title to search for</param>
        /// <param name="author">Optional author name</param>
        /// <param name="getAllEditions">Whether to retrieve all editions (ignored for Anna's Archive)</param>
        /// <returns>Empty list - search functionality not yet implemented (Phase 2)</returns>
        /// <remarks>
        /// Phase 2 feature: Requires HTML parsing to extract MD5 hashes from search results.
        /// Current implementation returns empty list gracefully.
        /// </remarks>
        public List<Book> SearchForNewBook(string title, string author = null, bool getAllEditions = true)
        {
            _logger.Debug("Searching Anna's Archive for: title={0}, author={1}", title, author);

            try
            {
                // TODO: Implement light HTML parsing to extract MD5 hashes from search results
                // For now, return empty list - Phase 2 implementation
                _logger.Warn("Anna's Archive search not yet implemented - requires HTML parsing");
                return new List<Book>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Anna's Archive for title={0}, author={1}", title, author);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Search for books by ISBN.
        /// </summary>
        /// <param name="isbn">ISBN-10 or ISBN-13 to search for</param>
        /// <returns>Empty list - ISBN search not yet implemented (Phase 2)</returns>
        /// <remarks>
        /// Phase 2 feature: Requires ISBN to MD5 hash mapping.
        /// Current implementation returns empty list gracefully.
        /// </remarks>
        public List<Book> SearchByIsbn(string isbn)
        {
            _logger.Debug("Searching Anna's Archive by ISBN: {0}", isbn);

            try
            {
                // TODO: Implement ISBN → MD5 lookup
                // For now, return empty list - Phase 2 implementation
                _logger.Warn("Anna's Archive ISBN search not yet implemented");
                return new List<Book>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Anna's Archive by ISBN: {0}", isbn);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Search for books by Amazon ASIN.
        /// </summary>
        /// <param name="asin">Amazon Standard Identification Number</param>
        /// <returns>Empty list - ASIN search not supported by Anna's Archive</returns>
        /// <remarks>
        /// Anna's Archive does not track Amazon ASINs.
        /// This method returns empty list gracefully.
        /// </remarks>
        public List<Book> SearchByAsin(string asin)
        {
            _logger.Debug("Searching Anna's Archive by ASIN: {0}", asin);

            try
            {
                // TODO: Implement ASIN → MD5 lookup
                // For now, return empty list - Phase 2 implementation
                _logger.Warn("Anna's Archive ASIN search not yet implemented");
                return new List<Book>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Anna's Archive by ASIN: {0}", asin);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Search for books by Goodreads book ID.
        /// </summary>
        /// <param name="goodreadsId">Goodreads book identifier</param>
        /// <param name="getAllEditions">Whether to retrieve all editions</param>
        /// <returns>Empty list - Goodreads ID mapping not supported</returns>
        /// <remarks>
        /// Anna's Archive does not track Goodreads IDs.
        /// This method returns empty list gracefully.
        /// </remarks>
        public List<Book> SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions)
        {
            // Anna's Archive doesn't have direct Goodreads ID mapping
            return new List<Book>();
        }

        /// <summary>
        /// Retrieve book information by Anna's Archive foreign ID.
        /// </summary>
        /// <param name="foreignBookId">Foreign book ID in format "aa:{md5hash}"</param>
        /// <returns>Tuple containing: author foreign ID, Book object, and list of AuthorMetadata</returns>
        /// <exception cref="AnnasArchiveException">Thrown when foreign ID format is invalid</exception>
        /// <exception cref="BookNotFoundException">Thrown when book with specified MD5 is not found</exception>
        /// <remarks>
        /// Fetches metadata from Anna's Archive JSON API and aggregates data from multiple sources.
        /// Priority order: ISBNdb → Libgen → Z-Library → file_unified_data
        /// Results are cached for 24 hours.
        /// </remarks>
        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
        {
            _logger.Debug("Getting book info from Anna's Archive for foreign ID: {0}", foreignBookId);

            try
            {
                // Extract MD5 hash from foreign ID (format: "aa:md5hash")
                var md5 = ExtractMd5FromForeignId(foreignBookId);
                if (string.IsNullOrEmpty(md5))
                {
                    throw new AnnasArchiveException("Invalid foreign book ID format. Expected: aa:md5hash");
                }

                // Fetch record and map to models
                var record = FetchRecordByMd5(md5);
                var book = MapRecordToBook(record);
                var authors = GetAuthorMetadata(record);
                var authorId = authors.FirstOrDefault()?.ForeignAuthorId ?? "unknown";

                return Tuple.Create(authorId, book, authors);
            }
            catch (AnnasArchiveException)
            {
                throw;
            }
            catch (BookNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting book info from Anna's Archive for ID: {0}", foreignBookId);
                throw new AnnasArchiveException("Failed to retrieve book information from Anna's Archive", ex);
            }
        }

        /// <summary>
        /// Get book metadata by MD5 hash using Anna's Archive JSON API.
        /// </summary>
        /// <param name="md5">32-character hexadecimal MD5 hash</param>
        /// <returns>Book object with aggregated metadata and single edition</returns>
        /// <exception cref="AnnasArchiveException">Thrown when MD5 format is invalid or API error occurs</exception>
        /// <exception cref="BookNotFoundException">Thrown when book with specified MD5 is not found</exception>
        /// <remarks>
        /// Endpoint: https://annas-archive.org/db/aarecord_elasticsearch/md5:{hash}.json.html
        /// Response is cached for 24 hours.
        /// Aggregates metadata from ISBNdb, Libgen, Z-Library, OpenLibrary, and other sources.
        /// </remarks>
        public Book GetBookByMd5(string md5)
        {
            try
            {
                var record = FetchRecordByMd5(md5);
                return MapRecordToBook(record);
            }
            catch (BookNotFoundException)
            {
                throw;
            }
            catch (AnnasArchiveException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching metadata for MD5: {0}", md5);
                throw new AnnasArchiveException($"Failed to fetch metadata for MD5 {md5}", ex);
            }
        }

        /// <summary>
        /// Fetch Anna's Archive record by MD5 hash.
        /// </summary>
        /// <param name="md5">32-character hexadecimal MD5 hash</param>
        /// <returns>Deserialized AARecord object</returns>
        /// <exception cref="AnnasArchiveException">Thrown when MD5 format is invalid or API error occurs</exception>
        /// <exception cref="BookNotFoundException">Thrown when book with specified MD5 is not found</exception>
        private AARecord FetchRecordByMd5(string md5)
        {
            if (string.IsNullOrWhiteSpace(md5) || md5.Length != 32)
            {
                throw new AnnasArchiveException("Invalid MD5 hash. Must be 32 characters");
            }

            _logger.Debug("Fetching metadata for MD5: {0}", md5);

            var url = string.Format(JsonApiUrl, md5.ToLower());
            var httpRequest = new HttpRequestBuilder(url)
                .SetHeader("User-Agent", UserAgent)
                .Build();

            httpRequest.AllowAutoRedirect = true;
            httpRequest.SuppressHttpError = true;

            var response = _cachedHttpClient.Get(httpRequest, true, TimeSpan.FromHours(24));

            if (response.HasHttpError)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BookNotFoundException($"Book with MD5 {md5} not found in Anna's Archive");
                }

                throw new AnnasArchiveException($"API error: HTTP {response.StatusCode}");
            }

            var record = JsonSerializer.Deserialize<AARecord>(response.Content);
            if (record == null)
            {
                throw new AnnasArchiveException("Failed to deserialize Anna's Archive response");
            }

            return record;
        }

        private Book MapRecordToBook(AARecord record)
        {
            if (record?.FileUnifiedData == null)
            {
                throw new AnnasArchiveException("Invalid record: missing file_unified_data");
            }

            var md5 = record.FileUnifiedData.Md5;
            if (string.IsNullOrEmpty(md5))
            {
                throw new AnnasArchiveException("Invalid record: missing MD5 hash");
            }

            _logger.Debug("Mapping AA record to Book: MD5={0}, Title={1}", md5, record.FileUnifiedData.Title);

            var title = GetBestTitle(record);
            var book = new Book
            {
                ForeignBookId = $"aa:{md5}",
                Title = title,
                TitleSlug = md5,
                CleanTitle = Parser.Parser.CleanAuthorName(title),
                ReleaseDate = ParseReleaseDate(record),
                Links = GetLinks(record),
                Genres = new List<string>(),
                Ratings = new Ratings { Votes = 0, Value = 0 },
                AnyEditionOk = true
            };

            // Create and add edition
            var edition = CreateEdition(record);
            book.Editions = new List<Edition> { edition };

            return book;
        }

        private Edition CreateEdition(AARecord record)
        {
            var md5 = record.FileUnifiedData.Md5;
            var title = GetBestTitle(record);
            var edition = new Edition
            {
                ForeignEditionId = $"aa:{md5}",
                Title = title,
                TitleSlug = md5,
                Publisher = GetBestPublisher(record),
                ReleaseDate = ParseReleaseDate(record),
                Isbn13 = ExtractBestIsbn13(record),
                Asin = null, // AA doesn't track ASINs
                Overview = GetBestDescription(record),
                Format = record.FileUnifiedData.Extension?.ToUpper(),
                PageCount = GetBestPageCount(record) ?? 0,
                Language = record.FileUnifiedData.Language,
                Ratings = new Ratings { Votes = 0, Value = 0 },
                Monitored = true,
                Links = GetLinks(record),
                Images = new List<MediaCover.MediaCover>()
            };

            return edition;
        }

        private string GetBestTitle(AARecord record)
        {
            // Priority: ISBNdb > Libgen > Z-Library > file_unified_data
            return record.IsbnDb?.Title
                ?? record.LibgenNonFiction?.Title
                ?? record.LibgenFiction?.Title
                ?? record.ZLibrary?.Title
                ?? record.FileUnifiedData.Title;
        }

        private string GetBestPublisher(AARecord record)
        {
            // Priority: ISBNdb > Libgen > Z-Library > file_unified_data
            return record.IsbnDb?.Publisher
                ?? record.LibgenNonFiction?.Publisher
                ?? record.LibgenFiction?.Publisher
                ?? record.ZLibrary?.Publisher
                ?? record.FileUnifiedData.Publisher;
        }

        private string GetBestDescription(AARecord record)
        {
            // Priority: ISBNdb > OpenLibrary > Libgen > Z-Library
            var description = record.IsbnDb?.Synopsis;

            if (string.IsNullOrEmpty(description) && record.OpenLibrary?.Description != null)
            {
                description = ExtractStringFromJsonElement(record.OpenLibrary.Description);
            }

            if (string.IsNullOrEmpty(description))
            {
                description = record.LibgenNonFiction?.Description
                    ?? record.LibgenFiction?.Description
                    ?? record.ZLibrary?.Description
                    ?? record.FileUnifiedData.Description;
            }

            return description;
        }

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

        private List<string> GetAuthorNames(AARecord record)
        {
            List<string> authors;

            // Priority: ISBNdb (structured) > Libgen > Z-Library > file_unified_data
            if (record.IsbnDb?.Authors != null && record.IsbnDb.Authors.Any())
            {
                authors = record.IsbnDb.Authors;
            }
            else
            {
                var authorString = record.LibgenNonFiction?.Author
                    ?? record.LibgenFiction?.Author
                    ?? record.ZLibrary?.Author
                    ?? record.FileUnifiedData.Author;

                authors = SplitAuthors(authorString);
            }

            return authors.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList();
        }

        private List<string> SplitAuthors(string authorString)
        {
            if (string.IsNullOrWhiteSpace(authorString))
            {
                return new List<string>();
            }

            // Common author separators
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

        private DateTime? ParseReleaseDate(AARecord record)
        {
            var yearStr = record.IsbnDb?.DatePublished
                ?? record.LibgenNonFiction?.Year
                ?? record.LibgenFiction?.Year
                ?? record.ZLibrary?.Year?.ToString()
                ?? record.FileUnifiedData.Year;

            if (string.IsNullOrEmpty(yearStr))
            {
                return null;
            }

            // Try to extract year from various formats
            if (DateTime.TryParse(yearStr, out var date))
            {
                return date;
            }

            // Try to extract just the year
            if (int.TryParse(yearStr.Substring(0, Math.Min(4, yearStr.Length)), out var year))
            {
                if (year >= 1000 && year <= DateTime.Now.Year + 10)
                {
                    return new DateTime(year, 1, 1);
                }
            }

            return null;
        }

        private string ExtractBestIsbn13(AARecord record)
        {
            // Priority: ISBNdb > Libgen > Z-Library
            var candidates = new List<string>();

            if (!string.IsNullOrEmpty(record.IsbnDb?.Isbn13))
            {
                candidates.Add(record.IsbnDb.Isbn13);
            }

            if (!string.IsNullOrEmpty(record.LibgenNonFiction?.ISBN))
            {
                candidates.AddRange(ExtractIsbn13sFromString(record.LibgenNonFiction.ISBN));
            }

            if (!string.IsNullOrEmpty(record.LibgenFiction?.ISBN))
            {
                candidates.AddRange(ExtractIsbn13sFromString(record.LibgenFiction.ISBN));
            }

            if (!string.IsNullOrEmpty(record.ZLibrary?.Isbn))
            {
                candidates.AddRange(ExtractIsbn13sFromString(record.ZLibrary.Isbn));
            }

            // Return first valid ISBN-13
            foreach (var isbn in candidates)
            {
                var cleaned = CleanIsbn(isbn);
                if (IsValidIsbn13(cleaned))
                {
                    return cleaned;
                }
            }

            return null;
        }

        private List<string> ExtractIsbn13sFromString(string isbnString)
        {
            if (string.IsNullOrWhiteSpace(isbnString))
            {
                return new List<string>();
            }

            // ISBNs can be separated by commas, semicolons, or spaces
            return isbnString.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CleanIsbn)
                .Where(isbn => isbn.Length == 13)
                .ToList();
        }

        private string CleanIsbn(string isbn)
        {
            if (string.IsNullOrEmpty(isbn))
            {
                return string.Empty;
            }

            // Remove hyphens and spaces
            return new string(isbn.Where(char.IsDigit).ToArray());
        }

        private bool IsValidIsbn13(string isbn)
        {
            if (string.IsNullOrEmpty(isbn) || isbn.Length != 13 || !isbn.All(char.IsDigit))
            {
                return false;
            }

            // Validate ISBN-13 checksum
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

        private int? GetBestPageCount(AARecord record)
        {
            if (record.IsbnDb?.Pages != null)
            {
                return record.IsbnDb.Pages;
            }

            if (record.ZLibrary?.Pages != null)
            {
                return record.ZLibrary.Pages;
            }

            if (!string.IsNullOrEmpty(record.LibgenNonFiction?.Pages) && int.TryParse(record.LibgenNonFiction.Pages, out var pages1))
            {
                return pages1;
            }

            if (!string.IsNullOrEmpty(record.LibgenFiction?.Pages) && int.TryParse(record.LibgenFiction.Pages, out var pages2))
            {
                return pages2;
            }

            return null;
        }

        private List<Links> GetLinks(AARecord record)
        {
            var links = new List<Links>();
            var md5 = record.FileUnifiedData.Md5;

            // Link to Anna's Archive page
            links.Add(new Links
            {
                Url = $"https://annas-archive.org/md5/{md5}",
                Name = "Anna's Archive"
            });

            // IPFS links (if available)
            if (record.IpfsInfos != null && record.IpfsInfos.Any())
            {
                var firstIpfs = record.IpfsInfos.First();
                links.Add(new Links
                {
                    Url = $"https://ipfs.io/ipfs/{firstIpfs.Cid}",
                    Name = "IPFS"
                });
            }

            return links;
        }

        private string ExtractMd5FromForeignId(string foreignId)
        {
            if (string.IsNullOrEmpty(foreignId))
            {
                return null;
            }

            // Format: "aa:md5hash"
            var parts = foreignId.Split(':');
            if (parts.Length != 2 || parts[0] != "aa")
            {
                return null;
            }

            return parts[1];
        }

        private string ExtractStringFromJsonElement(object value)
        {
            if (value == null)
            {
                return null;
            }

            // Handle JsonElement for description fields that can be string or object
            if (value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    return element.GetString();
                }

                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("value", out var valueProperty))
                {
                    return valueProperty.GetString();
                }
            }

            return value.ToString();
        }

        private string CreateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unknown";
            }

            // Trim and normalize whitespace, then convert to slug format
            var normalized = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");

            return normalized.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("(", "")
                .Replace(")", "");
        }
    }
}
