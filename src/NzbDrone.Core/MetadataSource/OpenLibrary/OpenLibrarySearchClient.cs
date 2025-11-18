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
using NzbDrone.Core.Http;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public class OpenLibrarySearchClient : IOpenLibrarySearchClient
    {
        private const string OPENLIBRARY_ENDPOINT = "https://openlibrary.org";
        private const int TIMEOUT_SECONDS = 10;
        private const int MAX_RESULTS = 10;
        private const int RATE_LIMIT_MS = 1000;

        private static readonly object _rateLimitLock = new object();
        private static DateTime _lastRequestTime = DateTime.MinValue;

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public OpenLibrarySearchClient(IHttpClient httpClient, ICachedHttpResponseService cachedHttpClient, IConfigService configService)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _configService = configService;
            _logger = LogManager.GetCurrentClassLogger();
        }

        public List<object> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<object>();
            }

            if (!_configService.OpenLibraryEnabled)
            {
                _logger.Debug("OpenLibrary search skipped - not enabled");
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
                if (results != null && results.Count > 0)
                {
                    _logger.Info($"OpenLibrary search successful: {results.Count} results for '{trimmed}'");
                    return results;
                }

                _logger.Warn($"OpenLibrary search returned no results for '{trimmed}'");
                return new List<object>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"OpenLibrary search failed for '{searchTerm}' - falling back to next provider");
                return null;
            }
        }

        private List<object> ExecuteSearch(string query)
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var request = new HttpRequestBuilder(OPENLIBRARY_ENDPOINT)
                .Resource($"search.json?q={encodedQuery}&limit={MAX_RESULTS}")
                .SetHeader("Accept", "application/json")
                .SetHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
                .Build();

            request.Method = HttpMethod.Get;

            RateLimitRequest();

            var response = ExecuteWithRetry(request);
            if (response == null)
            {
                return null;
            }

            return ParseSearchResponse(response.Content);
        }

        private static void RateLimitRequest()
        {
            lock (_rateLimitLock)
            {
                var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
                if (timeSinceLastRequest < TimeSpan.FromMilliseconds(RATE_LIMIT_MS))
                {
                    var waitTime = TimeSpan.FromMilliseconds(RATE_LIMIT_MS) - timeSinceLastRequest;
                    Thread.Sleep(waitTime);
                }

                _lastRequestTime = DateTime.UtcNow;
            }
        }

        private HttpResponse ExecuteWithRetry(HttpRequest request)
        {
            // Use cached response if available (1 hour TTL by default)
            var useCache = true;
            var cacheTtl = TimeSpan.FromHours(_configService.OpenLibraryCacheTtlHours);

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    // Use cached HTTP client for GET requests
                    var response = _cachedHttpClient.Get(request, useCache, cacheTtl);

                    if (!response.HasHttpError)
                    {
                        return response;
                    }

                    // Handle 429 Too Many Requests - rate limiting
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.Warn($"OpenLibrary API rate limit hit (429) - attempt {attempt}/2");

                        if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(2000 + new Random().Next(1000)));
                            continue;
                        }
                    }

                    // Handle 5xx server errors
                    if (response.StatusCode >= HttpStatusCode.InternalServerError)
                    {
                        _logger.Warn($"OpenLibrary API server error ({response.StatusCode}) - attempt {attempt}/2");

                        if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(1000 + new Random().Next(500)));
                            continue;
                        }
                    }

                    _logger.Error($"OpenLibrary API error: {response.StatusCode}");
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"OpenLibrary API request failed (attempt {attempt}/2): {ex.Message}");

                    if (attempt == 1 && IsRetryableException(ex))
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(1000 + new Random().Next(500)));
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

        private List<object> ParseSearchResponse(string responseContent)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var searchResult = JsonSerializer.Deserialize<OpenLibrarySearchResult>(responseContent, jsonOptions);

                if (searchResult?.Docs == null || !searchResult.Docs.Any())
                {
                    _logger.Debug("OpenLibrary response contained no documents");
                    return new List<object>();
                }

                var results = new List<object>();

                foreach (var doc in searchResult.Docs)
                {
                    if (doc != null && !string.IsNullOrWhiteSpace(doc.Key))
                    {
                        results.Add(doc);
                    }
                }

                _logger.Debug($"Parsed {results.Count} OpenLibrary search results");
                return results;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, $"Failed to parse OpenLibrary response: {ex.Message}");
                return null;
            }
        }
    }
}
