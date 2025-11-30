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

namespace NzbDrone.Core.MetadataSource.ComicVine
{
    public class ComicVineSearchClient : IComicVineSearchClient
    {
        private const string COMICVINE_ENDPOINT = "https://comicvine.gamespot.com/api";
        private const int TIMEOUT_SECONDS = 10;
        private const int MAX_RESULTS = 10;
        private const int RATE_LIMIT_MS = 1000;

        private static readonly object _rateLimitLock = new object();
        private static DateTime _lastRequestTime = DateTime.MinValue;

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public ComicVineSearchClient(IHttpClient httpClient, ICachedHttpResponseService cachedHttpClient, IConfigService configService)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _configService = configService;
            _logger = LogManager.GetCurrentClassLogger();
        }

        public List<ComicVineIssueResult> Search(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<ComicVineIssueResult>();
            }

            if (!_configService.ComicVineEnabled || string.IsNullOrEmpty(_configService.ComicVineApiKey))
            {
                _logger.Debug("ComicVine search skipped - not enabled or no API key configured");
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
                    _logger.Info($"ComicVine search successful: {results.Count} results for '{trimmed}'");
                    return results;
                }

                _logger.Warn($"ComicVine search returned no results for '{trimmed}'");
                return new List<ComicVineIssueResult>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"ComicVine search failed for '{searchTerm}' - falling back to next provider");
                return null;
            }
        }

        private List<ComicVineIssueResult> ExecuteSearch(string query)
        {
            var encodedQuery = Uri.EscapeDataString(query);
            var request = new HttpRequestBuilder(COMICVINE_ENDPOINT)
                .Resource($"search/?api_key={_configService.ComicVineApiKey}&format=json&resources=issue&query={encodedQuery}&limit={MAX_RESULTS}")
                .SetHeader("Accept", "application/json")
                .SetHeader("User-Agent", "Bookshelf/1.0 (Readarr Fork)")
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
            var cacheTtl = TimeSpan.FromHours(_configService.ComicVineCacheTtlHours);

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

                    // Handle 401 Unauthorized
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        _logger.Warn("ComicVine API returned 401 - invalid API key");
                        return null;
                    }

                    // Handle 429 Too Many Requests - rate limiting
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.Warn($"ComicVine API rate limit hit (429) - attempt {attempt}/2");

                        if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(2000 + Random.Shared.Next(1000)));
                            continue;
                        }
                    }

                    // Handle 5xx server errors
                    if (response.StatusCode >= HttpStatusCode.InternalServerError)
                    {
                        _logger.Warn($"ComicVine API server error ({response.StatusCode}) - attempt {attempt}/2");

                        if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(1000 + Random.Shared.Next(500)));
                            continue;
                        }
                    }

                    _logger.Error($"ComicVine API error: {response.StatusCode}");
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"ComicVine API request failed (attempt {attempt}/2): {ex.Message}");

                    if (attempt == 1 && IsRetryableException(ex))
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(1000 + Random.Shared.Next(500)));
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

        private List<ComicVineIssueResult> ParseSearchResponse(string responseContent)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var searchResult = JsonSerializer.Deserialize<ComicVineSearchResponse>(responseContent, jsonOptions);

                if (searchResult == null)
                {
                    _logger.Error("ComicVine response is null");
                    return null;
                }

                // Check for API errors
                if (searchResult.StatusCode != 1)
                {
                    _logger.Error($"ComicVine API returned error status: {searchResult.StatusCode} - {searchResult.Error}");
                    return null;
                }

                if (searchResult.Results == null || !searchResult.Results.Any())
                {
                    _logger.Debug("ComicVine response contained no results");
                    return new List<ComicVineIssueResult>();
                }

                var results = new List<ComicVineIssueResult>();

                foreach (var issue in searchResult.Results)
                {
                    if (issue != null)
                    {
                        results.Add(issue);
                    }
                }

                _logger.Debug($"Parsed {results.Count} ComicVine search results");
                return results;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, $"Failed to parse ComicVine response: {ex.Message}");
                return null;
            }
        }
    }
}
