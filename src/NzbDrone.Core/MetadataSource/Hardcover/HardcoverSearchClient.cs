using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource.Hardcover
{
    public class HardcoverSearchClient : IHardcoverSearchClient
    {
        private const string HARDCOVER_ENDPOINT = "https://api.hardcover.app/v1/graphql";
        private const int TIMEOUT_SECONDS = 8;
        private const int MAX_RESULTS_PER_TYPE = 10;
        private const string BATCHED_SEARCH_QUERY = @"
            query BatchedSearch($q: String!, $limit: Int!, $page: Int!) {
                authors: search(query: $q, query_type: ""Author"", per_page: $limit, page: $page) {
                    results
                }
                books: search(query: $q, query_type: ""Book"", per_page: $limit, page: $page) {
                    results
                }
                series: search(query: $q, query_type: ""Series"", per_page: $limit, page: $page) {
                    results
                }
            }";

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public HardcoverSearchClient(IHttpClient httpClient, IConfigService configService)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = LogManager.GetCurrentClassLogger();
        }

        public List<object> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<object>();
            }

            if (!_configService.HardcoverEnabled || string.IsNullOrEmpty(_configService.HardcoverApiToken))
            {
                _logger.Debug("Hardcover search skipped - not enabled or no token configured");
                return null;
            }

            try
            {
                var trimmed = searchTerm.Trim();
                if (trimmed.Length > 200)
                {
                    trimmed = trimmed.Substring(0, 200);
                }

                var results = ExecuteSearch(trimmed);
                if (results != null)
                {
                    _logger.Info($"Hardcover search successful: {results.Count} total results for '{trimmed}'");
                    return results;
                }

                _logger.Warn($"Hardcover search returned no results for '{trimmed}'");
                return new List<object>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Hardcover search failed for '{searchTerm}' - falling back to proxy");
                return null;
            }
        }

        private List<object> ExecuteSearch(string query)
        {
            var requestBody = new
            {
                query = BATCHED_SEARCH_QUERY,
                variables = new
                {
                    q = query,
                    limit = MAX_RESULTS_PER_TYPE,
                    page = 1
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var content = JsonSerializer.Serialize(requestBody, jsonOptions);

            var request = new HttpRequestBuilder(HARDCOVER_ENDPOINT)
                .SetHeader("Content-Type", "application/json")
                .SetHeader("Accept", "application/json")
                .SetHeader("User-Agent", "Bookshelf/1.0")
                .Build();

            request.Method = HttpMethod.Post;
            request.SetContent(content);
            request.Headers.Add("Authorization", $"Bearer {_configService.HardcoverApiToken}");

            var response = ExecuteWithRetry(request);
            if (response == null)
            {
                return null;
            }

            return ParseSearchResponse(response.Content, jsonOptions);
        }

        private HttpResponse ExecuteWithRetry(HttpRequest request)
        {
            var timeout = TimeSpan.FromSeconds(TIMEOUT_SECONDS);

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var response = _httpClient.Execute(request);

                    if (!response.HasHttpError)
                    {
                        return response;
                    }

                    // Handle 401 Unauthorized - invalid/expired token
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.Warn("Hardcover API returned 401 - invalid or expired token, falling back to V5");
                        return null;
                    }

                    // Handle 429 Too Many Requests - rate limiting
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.Warn($"Hardcover API rate limit hit (429) - attempt {attempt}/2");

                        if (response.Headers.ContainsKey("Retry-After") &&
                            int.TryParse(response.Headers["Retry-After"], out var retryAfter))
                        {
                            if (retryAfter <= 10 && attempt == 1)
                            {
                                Thread.Sleep(TimeSpan.FromSeconds(retryAfter + 1));
                                continue;
                            }
                        }
                        else if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(1000 + new Random().Next(500)));
                            continue;
                        }
                    }

                    // Handle 5xx server errors
                    if (response.StatusCode >= HttpStatusCode.InternalServerError)
                    {
                        _logger.Warn($"Hardcover API server error ({response.StatusCode}) - attempt {attempt}/2");

                        if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(500 + new Random().Next(500)));
                            continue;
                        }
                    }

                    _logger.Error($"Hardcover API error: {response.StatusCode}");
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Hardcover API request failed (attempt {attempt}/2): {ex.Message}");

                    if (attempt == 1 && IsRetryableException(ex))
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(500 + new Random().Next(500)));
                    }
                }
            }

            return null;
        }

        private bool IsRetryableException(Exception ex)
        {
            return ex is TimeoutException ||
                   ex is HttpRequestException ||
                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        }

        private List<object> ParseSearchResponse(string responseContent, JsonSerializerOptions jsonOptions)
        {
            try
            {
                using var jsonDocument = JsonDocument.Parse(responseContent);
                var root = jsonDocument.RootElement;

                // Check for GraphQL errors
                if (root.TryGetProperty("errors", out var errors))
                {
                    var isAuthError = false;
                    foreach (var error in errors.EnumerateArray())
                    {
                        var message = error.GetProperty("message").GetString();
                        _logger.Error($"Hardcover GraphQL error: {message}");

                        if (error.TryGetProperty("extensions", out var extensions) &&
                            extensions.TryGetProperty("code", out var code) &&
                            code.GetString() == "UNAUTHENTICATED")
                        {
                            _logger.Warn("Hardcover GraphQL authentication failed - token invalid, falling back to V5");
                            isAuthError = true;
                        }
                    }

                    if (isAuthError)
                    {
                        return null;
                    }
                }

                if (!root.TryGetProperty("data", out var data))
                {
                    _logger.Error("Hardcover response missing data field");
                    return null;
                }

                var results = new List<object>();

                // Parse authors and sort by books count
                var authors = new List<HardcoverAuthorResult>();
                if (data.TryGetProperty("authors", out var authorsData) &&
                    authorsData.TryGetProperty("results", out var authorResults) &&
                    authorResults.TryGetProperty("hits", out var authorHits))
                {
                    foreach (var hit in authorHits.EnumerateArray())
                    {
                        if (hit.TryGetProperty("document", out var doc))
                        {
                            var author = ParseAuthor(doc);
                            if (author != null)
                            {
                                authors.Add(author);
                            }
                        }
                    }
                }

                // Add authors ordered by popularity (books count)
                var sortedAuthors = authors.OrderByDescending(a => a.BooksCount).ToList();
                foreach (var author in sortedAuthors)
                {
                    results.Add(author);
                }

                // Parse books
                if (data.TryGetProperty("books", out var booksData) &&
                    booksData.TryGetProperty("results", out var bookResults) &&
                    bookResults.TryGetProperty("hits", out var bookHits))
                {
                    foreach (var hit in bookHits.EnumerateArray())
                    {
                        if (hit.TryGetProperty("document", out var doc))
                        {
                            var book = ParseBook(doc);
                            if (book != null)
                            {
                                results.Add(book);
                            }
                        }
                    }
                }

                // Parse series
                if (data.TryGetProperty("series", out var seriesData) &&
                    seriesData.TryGetProperty("results", out var seriesResults) &&
                    seriesResults.TryGetProperty("hits", out var seriesHits))
                {
                    foreach (var hit in seriesHits.EnumerateArray())
                    {
                        if (hit.TryGetProperty("document", out var doc))
                        {
                            var series = ParseSeries(doc);
                            if (series != null)
                            {
                                results.Add(series);
                            }
                        }
                    }
                }

                return results;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, $"Failed to parse Hardcover response: {ex.Message}");
                return null;
            }
        }

        private HardcoverAuthorResult ParseAuthor(JsonElement element)
        {
            try
            {
                return new HardcoverAuthorResult
                {
                    Id = GetStringValue(element, "id"),
                    Name = GetStringValue(element, "name"),
                    AlternateNames = GetStringArrayValue(element, "alternate_names"),
                    Bio = GetStringValue(element, "bio"),
                    BooksCount = GetIntValue(element, "books_count"),
                    Slug = GetStringValue(element, "slug"),
                    BornDate = GetStringValue(element, "born_date"),
                    DeathDate = GetStringValue(element, "death_date"),
                    ImageUrl = GetImageUrl(element)
                };
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to parse Hardcover author");
                return null;
            }
        }

        private HardcoverBookResult ParseBook(JsonElement element)
        {
            try
            {
                var authorIds = GetAuthorIdsFromContributions(element);
                var title = GetStringValue(element, "title");

                if (authorIds != null && authorIds.Length > 0)
                {
                    _logger.Debug($"Hardcover book '{title}' has author IDs: {string.Join(", ", authorIds)}");
                }
                else
                {
                    _logger.Debug($"Hardcover book '{title}' has no author IDs extracted");
                }

                return new HardcoverBookResult
                {
                    Id = GetStringValue(element, "id"),
                    Title = title,
                    Subtitle = GetStringValue(element, "subtitle"),
                    Description = GetStringValue(element, "description"),
                    AuthorNames = GetStringArrayValue(element, "author_names"),
                    AuthorIds = authorIds,
                    SeriesNames = GetStringArrayValue(element, "series_names"),
                    Isbns = GetStringArrayValue(element, "isbns"),
                    Rating = GetFloatValue(element, "rating"),
                    Pages = GetIntValue(element, "pages"),
                    ReleaseDate = GetStringValue(element, "release_date"),
                    ImageUrl = GetImageUrl(element)
                };
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to parse Hardcover book");
                return null;
            }
        }

        private HardcoverSeriesResult ParseSeries(JsonElement element)
        {
            try
            {
                return new HardcoverSeriesResult
                {
                    Id = GetStringValue(element, "id"),
                    Name = GetStringValue(element, "name"),
                    Slug = GetStringValue(element, "slug"),
                    Description = GetStringValue(element, "description"),
                    BooksCount = GetIntValue(element, "books_count"),
                    PrimaryBooksCount = GetIntValue(element, "primary_books_count"),
                    ReadersCount = GetIntValue(element, "readers_count"),
                    AuthorName = GetStringValue(element, "author_name")
                };
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to parse Hardcover series");
                return null;
            }
        }

        private string GetStringValue(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null)
            {
                return value.GetString();
            }

            return null;
        }

        private string[] GetStringArrayValue(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .ToArray();
            }

            return Array.Empty<string>();
        }

        private int GetIntValue(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
            {
                return value.GetInt32();
            }

            return 0;
        }

        private float GetFloatValue(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number)
            {
                return value.GetSingle();
            }

            return 0f;
        }

        private string GetImageUrl(JsonElement element)
        {
            if (element.TryGetProperty("image", out var image) &&
                image.ValueKind == JsonValueKind.Object &&
                image.TryGetProperty("url", out var url))
            {
                return url.GetString();
            }

            return null;
        }

        private string[] GetAuthorIdsFromContributions(JsonElement element)
        {
            var authorIds = new List<string>();

            if (element.TryGetProperty("contributions", out var contributions) &&
                contributions.ValueKind == JsonValueKind.Array)
            {
                foreach (var contribution in contributions.EnumerateArray())
                {
                    if (contribution.TryGetProperty("author", out var author) &&
                        author.TryGetProperty("id", out var id))
                    {
                        var authorId = (string)null;

                        if (id.ValueKind == JsonValueKind.Number)
                        {
                            authorId = id.GetInt32().ToString();
                        }
                        else if (id.ValueKind == JsonValueKind.String)
                        {
                            authorId = id.GetString();
                        }

                        if (!string.IsNullOrEmpty(authorId) && !authorIds.Contains(authorId))
                        {
                            authorIds.Add(authorId);
                        }
                    }
                }
            }

            return authorIds.ToArray();
        }
    }
}
