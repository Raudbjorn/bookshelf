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
                var cleanIsbn = isbn.Replace("-", "").Replace(" ", "");
                var query = $"isbn:{cleanIsbn}";
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
                var query = $"asin:{asin}";
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
                .SetHeader("User-Agent", "Bookshelf/1.0")
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
                .SetHeader("User-Agent", "Bookshelf/1.0")
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

            return response.Resource;
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
            var specialChars = new[] { '+', '-', '&', '|', '!', '(', ')', '{', '}', '[', ']', '^', '"', '~', '*', '?', ':', '\\', '/' };
            var result = input;

            foreach (var ch in specialChars)
            {
                result = result.Replace(ch.ToString(), "\\" + ch);
            }

            return result;
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
                        Url = $"https://archive.org/details/{doc.Identifier}",
                        Name = "Internet Archive"
                    }
                },
                Genres = doc.Subject ?? new List<string>(),
                Ratings = new Ratings
                {
                    Value = doc.Avg_rating.HasValue ? (decimal)doc.Avg_rating.Value : 0m,
                    Votes = doc.Num_reviews ?? 0
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
                        Url = $"https://archive.org/details/{doc.Identifier}",
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
                        Url = $"https://archive.org/details/{identifier}",
                        Name = "Internet Archive"
                    }
                },
                Genres = GetListValue(metadata.Subject),
                Ratings = new Ratings
                {
                    Value = metadata.Avg_rating.HasValue ? (decimal)metadata.Avg_rating.Value : 0m,
                    Votes = metadata.Num_reviews ?? 0
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
                Isbn13 = isbns.FirstOrDefault(isbn => isbn?.Length == 13),
                Monitored = true,
                Links = new List<Links>
                {
                    new Links
                    {
                        Url = $"https://archive.org/details/{identifier}",
                        Name = "Internet Archive"
                    }
                },
                Images = new List<MediaCover.MediaCover>()
            };

            // Add cover image if available
            var coverUrl = $"https://archive.org/services/img/{identifier}";
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

            return authorName.ToLower()
                .Replace(" ", "-")
                .Replace(".", "")
                .Replace(",", "");
        }

        private DateTime? ParseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                return null;
            }

            // Internet Archive dates can be in various formats: "2020", "2020-01-01", "2020-01", etc.
            if (DateTime.TryParse(dateString, out var result))
            {
                return result;
            }

            // Try parsing just the year
            if (int.TryParse(dateString.Substring(0, Math.Min(4, dateString.Length)), out var year))
            {
                return new DateTime(year, 1, 1);
            }

            return null;
        }

        private string GetStringValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            // Handle both string and List<string> cases
            if (value is string str)
            {
                return str;
            }

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

            return value.ToString();
        }

        private List<string> GetListValue(object value)
        {
            if (value == null)
            {
                return new List<string>();
            }

            // Handle both string and List<string> cases
            if (value is string str)
            {
                return new List<string> { str };
            }

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

            return new List<string> { value.ToString() };
        }
    }
}
