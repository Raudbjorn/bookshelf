using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Web;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MediaCover;

namespace NzbDrone.Core.MetadataSource.InternetArchive
{
    public class InternetArchiveProxy : ISearchForNewBook, IProvideBookInfo
    {
        private const string SearchUrl = "https://archive.org/advancedsearch.php";
        private const string MetadataUrl = "https://archive.org/metadata";
        private const int MaxSearchResults = 25;
        private const string UserAgent = "Bookshelf/1.0";

        private static readonly char[] LuceneSpecialChars = { '\\', '+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '/' };

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly Logger _logger;

        public InternetArchiveProxy(
            IHttpClient httpClient,
            ICachedHttpResponseService cachedHttpClient,
            Logger logger)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _logger = logger;
        }

        public List<Book> SearchForNewBook(string title, string author = null, bool getAllEditions = true)
        {
            _logger.Debug("Searching Internet Archive for: title={0}, author={1}", title, author);

            try
            {
                var query = BuildLuceneQuery(title, author);
                var searchResults = ExecuteSearch(query);

                return searchResults.Select(MapSearchDocToBook).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Internet Archive for title={0}, author={1}", title, author);
                return new List<Book>();
            }
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            _logger.Debug("Searching Internet Archive by ISBN: {0}", isbn);

            try
            {
                var cleanIsbn = NormalizeIsbn(isbn);
                var query = BuildIsbnQuery(cleanIsbn);
                var searchResults = ExecuteSearch(query);

                return searchResults.Select(MapSearchDocToBook).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Internet Archive by ISBN: {0}", isbn);
                return new List<Book>();
            }
        }

        public List<Book> SearchByAsin(string asin)
        {
            _logger.Debug("Searching Internet Archive by ASIN: {0}", asin);

            try
            {
                var query = BuildAsinQuery(asin);
                var searchResults = ExecuteSearch(query);

                return searchResults.Select(MapSearchDocToBook).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Internet Archive by ASIN: {0}", asin);
                return new List<Book>();
            }
        }

        public List<Book> SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions)
        {
            // Internet Archive doesn't have direct Goodreads ID mapping
            // Return empty list - let other providers handle this
            return new List<Book>();
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
        {
            _logger.Debug("Getting book info from Internet Archive for identifier: {0}", foreignBookId);

            try
            {
                // foreignBookId for IA is in format "ia:identifier"
                var identifier = foreignBookId.StartsWith("ia:") ? foreignBookId.Substring(3) : foreignBookId;

                var metadata = GetMetadata(identifier);
                var book = MapMetadataToBook(metadata, identifier);
                var authors = ExtractAuthors(metadata);

                var authorId = authors.FirstOrDefault()?.ForeignAuthorId ?? "unknown";

                return Tuple.Create(authorId, book, authors);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting book info from Internet Archive for: {0}", foreignBookId);
                throw new InternetArchiveException($"Failed to get book info for {foreignBookId}", ex);
            }
        }

        private List<IASearchDoc> ExecuteSearch(string query)
        {
            var fields = new[] { "identifier", "title", "creator", "date", "subject", "description", "publisher", "language", "avg_rating", "num_reviews" };
            var fieldsParam = string.Join("&", fields.Select(f => $"fl[]={f}"));

            var url = $"{SearchUrl}?q={HttpUtility.UrlEncode(query)}&{fieldsParam}&output=json&rows={MaxSearchResults}";

            var httpRequest = new HttpRequestBuilder(url)
                .SetHeader("User-Agent", UserAgent)
                .Build();

            httpRequest.SuppressHttpError = true;

            var response = _cachedHttpClient.Get<IASearchResponse>(
                httpRequest,
                true,
                TimeSpan.FromDays(7));

            if (response.HasHttpError)
            {
                _logger.Warn("Internet Archive search returned status code: {0}", response.StatusCode);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    throw new InternetArchiveException("Rate limited by Internet Archive");
                }

                throw new InternetArchiveException($"Search failed with status code: {response.StatusCode}");
            }

            return response.Resource?.Response?.Docs ?? new List<IASearchDoc>();
        }

        private IAMetadataResponse GetMetadata(string identifier)
        {
            var url = $"{MetadataUrl}/{identifier}";

            var httpRequest = new HttpRequestBuilder(url)
                .SetHeader("User-Agent", UserAgent)
                .Build();

            httpRequest.SuppressHttpError = true;

            var response = _cachedHttpClient.Get<IAMetadataResponse>(
                httpRequest,
                true,
                TimeSpan.FromDays(30));

            if (response.HasHttpError)
            {
                _logger.Warn("Internet Archive metadata API returned status code: {0} for identifier: {1}", response.StatusCode, identifier);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BookNotFoundException($"Book not found in Internet Archive: {identifier}");
                }

                throw new InternetArchiveException($"Metadata retrieval failed with status code: {response.StatusCode}");
            }

            if (response.Resource == null)
            {
                throw new InternetArchiveException($"Metadata response was null for identifier: {identifier}");
            }

            return response.Resource;
        }

        private string NormalizeIsbn(string isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn))
            {
                return isbn;
            }

            // Remove hyphens, spaces, and other common separators
            return isbn.Replace("-", "").Replace(" ", "").Replace(".", "").Trim();
        }

        private bool IsValidIsbn13(string isbn)
        {
            if (string.IsNullOrEmpty(isbn) || isbn.Length != 13)
            {
                return false;
            }

            // Ensure all characters are digits
            if (!isbn.All(char.IsDigit))
            {
                return false;
            }

            // Validate ISBN-13 checksum
            // Algorithm: Multiply each digit alternately by 1 and 3, sum them, check if divisible by 10
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

        private string ExtractValidIsbn13(List<string> isbns)
        {
            if (isbns == null || !isbns.Any())
            {
                return null;
            }

            // Try to find a valid ISBN-13 by normalizing and validating each ISBN
            foreach (var isbn in isbns)
            {
                if (string.IsNullOrWhiteSpace(isbn))
                {
                    continue;
                }

                // Normalize by removing non-digit characters
                var normalized = new string(isbn.Where(char.IsDigit).ToArray());

                // Check if it's a valid ISBN-13
                if (IsValidIsbn13(normalized))
                {
                    return normalized;
                }
            }

            return null;
        }

        private string BuildIsbnQuery(string isbn)
        {
            var queryParts = new List<string>
            {
                $"isbn:{isbn}",
                "mediatype:texts",
                "(collection:printdisabled OR collection:inlibrary OR collection:books OR collection:opensource)"
            };

            return string.Join(" AND ", queryParts);
        }

        private string BuildAsinQuery(string asin)
        {
            var queryParts = new List<string>
            {
                $"asin:{asin}",
                "mediatype:texts",
                "(collection:printdisabled OR collection:inlibrary OR collection:books OR collection:opensource)"
            };

            return string.Join(" AND ", queryParts);
        }

        private string BuildItemUrl(string identifier)
        {
            return $"https://archive.org/details/{identifier}";
        }

        private string BuildCoverUrl(string identifier)
        {
            return $"https://archive.org/services/img/{identifier}";
        }

        private string BuildLuceneQuery(string title, string author)
        {
            var queryParts = new List<string>();

            // Add title search
            if (!string.IsNullOrWhiteSpace(title))
            {
                var escapedTitle = EscapeLucene(title);
                queryParts.Add($"title:({escapedTitle})");
            }

            // Add author search
            if (!string.IsNullOrWhiteSpace(author))
            {
                var escapedAuthor = EscapeLucene(author);
                queryParts.Add($"creator:({escapedAuthor})");
            }

            // Limit to books/texts
            queryParts.Add("mediatype:texts");

            // Prefer items in book-related collections
            queryParts.Add("(collection:printdisabled OR collection:inlibrary OR collection:books OR collection:opensource)");

            return string.Join(" AND ", queryParts);
        }

        private string EscapeLucene(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Escape special Lucene characters
            // Backslash must be escaped first to avoid double-escaping
            var result = new System.Text.StringBuilder(input.Length * 2);

            foreach (var ch in input)
            {
                if (Array.IndexOf(LuceneSpecialChars, ch) >= 0)
                {
                    result.Append('\\');
                }

                result.Append(ch);
            }

            return result.ToString();
        }

        private Book MapSearchDocToBook(IASearchDoc doc)
        {
            var book = new Book
            {
                ForeignBookId = $"ia:{doc.Identifier}",
                Title = doc.Title?.CleanSpaces() ?? "Unknown Title",
                TitleSlug = doc.Identifier,
                ReleaseDate = ParseDate(doc.Date),
                Links = new List<Links>
                {
                    new Links
                    {
                        Url = BuildItemUrl(doc.Identifier),
                        Name = "Internet Archive"
                    }
                },
                Genres = doc.Subject ?? new List<string>(),
                Ratings = new Ratings
                {
                    Value = doc.AvgRating.HasValue ? (decimal)doc.AvgRating.Value : 0m,
                    Votes = doc.NumReviews ?? 0
                },
                AnyEditionOk = true
            };

            // Create a single edition for the search result
            var edition = new Edition
            {
                ForeignEditionId = $"ia:{doc.Identifier}",
                TitleSlug = doc.Identifier,
                Title = doc.Title?.CleanSpaces() ?? "Unknown Title",
                Publisher = doc.Publisher?.CleanSpaces(),
                ReleaseDate = ParseDate(doc.Date),
                Overview = doc.Description?.CleanSpaces(),
                Language = doc.Language?.FirstOrDefault() ?? "eng",
                Monitored = true,
                Links = new List<Links>
                {
                    new Links
                    {
                        Url = BuildItemUrl(doc.Identifier),
                        Name = "Internet Archive"
                    }
                }
            };

            book.Editions = new List<Edition> { edition };

            return book;
        }

        private Book MapMetadataToBook(IAMetadataResponse response, string identifier)
        {
            var metadata = response.Metadata;

            var book = new Book
            {
                ForeignBookId = $"ia:{identifier}",
                Title = GetStringValue(metadata.Title)?.CleanSpaces() ?? "Unknown Title",
                TitleSlug = identifier,
                ReleaseDate = ParseDate(GetStringValue(metadata.Date)),
                Links = new List<Links>
                {
                    new Links
                    {
                        Url = BuildItemUrl(identifier),
                        Name = "Internet Archive"
                    }
                },
                Genres = GetListValue(metadata.Subject),
                Ratings = new Ratings
                {
                    Value = metadata.AvgRating.HasValue ? (decimal)metadata.AvgRating.Value : 0m,
                    Votes = metadata.NumReviews ?? 0
                },
                AnyEditionOk = true
            };

            // Create edition from metadata
            var edition = MapMetadataToEdition(response, identifier);
            book.Editions = new List<Edition> { edition };

            return book;
        }

        private Edition MapMetadataToEdition(IAMetadataResponse response, string identifier)
        {
            var metadata = response.Metadata;
            var isbns = GetListValue(metadata.Isbn);

            var edition = new Edition
            {
                ForeignEditionId = $"ia:{identifier}",
                TitleSlug = identifier,
                Title = GetStringValue(metadata.Title)?.CleanSpaces() ?? "Unknown Title",
                Publisher = GetStringValue(metadata.Publisher)?.CleanSpaces(),
                ReleaseDate = ParseDate(GetStringValue(metadata.Date)),
                Overview = GetStringValue(metadata.Description)?.CleanSpaces(),
                Language = GetListValue(metadata.Language).FirstOrDefault() ?? "eng",
                Isbn13 = ExtractValidIsbn13(isbns),
                Monitored = true,
                Links = new List<Links>
                {
                    new Links
                    {
                        Url = BuildItemUrl(identifier),
                        Name = "Internet Archive"
                    }
                },
                Images = new List<MediaCover.MediaCover>()
            };

            // Add cover image if available
            var coverUrl = BuildCoverUrl(identifier);
            edition.Images.Add(new MediaCover.MediaCover
            {
                Url = coverUrl,
                CoverType = MediaCoverTypes.Cover
            });

            return edition;
        }

        private List<AuthorMetadata> ExtractAuthors(IAMetadataResponse response)
        {
            var metadata = response.Metadata;
            var creators = GetListValue(metadata.Creator);

            if (!creators.Any())
            {
                return new List<AuthorMetadata>
                {
                    new AuthorMetadata
                    {
                        ForeignAuthorId = "ia:unknown",
                        Name = "Unknown Author"
                    }
                };
            }

            return creators.Select(creator => new AuthorMetadata
            {
                ForeignAuthorId = $"ia:{CreateAuthorSlug(creator)}",
                Name = creator?.CleanSpaces() ?? "Unknown Author"
            }).ToList();
        }

        private string CreateAuthorSlug(string authorName)
        {
            if (string.IsNullOrWhiteSpace(authorName))
            {
                return "unknown";
            }

            // Trim and normalize whitespace, then convert to slug format
            var normalized = System.Text.RegularExpressions.Regex.Replace(authorName.Trim(), @"\s+", " ");

            return normalized.ToLowerInvariant()
                .Replace(" ", "-")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("'", "")
                .Replace("\"", "");
        }

        private DateTime? ParseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return null;
            }

            var trimmed = dateString.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            // Internet Archive dates can be in various formats: "2020", "2020-01-01", "2020-01", etc.
            if (DateTime.TryParse(trimmed, out var result))
            {
                return result;
            }

            // Try parsing just the year (must have at least 4 characters)
            if (trimmed.Length >= 4 && int.TryParse(trimmed.Substring(0, 4), out var year))
            {
                if (year >= 1 && year <= 9999)
                {
                    return new DateTime(year, 1, 1);
                }
            }

            return null;
        }

        private string GetStringValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            // Handle string
            if (value is string str)
            {
                return str;
            }

            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return jsonElement.GetString();
                }

                if (jsonElement.ValueKind == JsonValueKind.Array && jsonElement.GetArrayLength() > 0)
                {
                    return jsonElement[0].GetString();
                }
            }

            // Handle IEnumerable<string>
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                var firstItem = enumerable.Cast<object>().FirstOrDefault();
                return firstItem?.ToString();
            }

            // Last resort - log warning and return null
            _logger.Warn("Unexpected value type in GetStringValue: {0}", value.GetType().Name);
            return null;
        }

        private List<string> GetListValue(object value)
        {
            if (value == null)
            {
                return new List<string>();
            }

            // Handle string
            if (value is string str)
            {
                return new List<string> { str };
            }

            // Handle JsonElement
            if (value is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return new List<string> { jsonElement.GetString() };
                }

                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    return jsonElement.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString())
                        .ToList();
                }
            }

            // Handle IEnumerable<string>
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                return enumerable.Cast<object>()
                    .Select(item => item?.ToString())
                    .Where(s => s != null)
                    .ToList();
            }

            // Last resort - log warning and return empty list
            _logger.Warn("Unexpected value type in GetListValue: {0}", value.GetType().Name);
            return new List<string>();
        }
    }
}
