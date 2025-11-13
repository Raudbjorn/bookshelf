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

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public class GoogleBooksSearchClient : IGoogleBooksSearchClient
    {
        private const string GOOGLE_BOOKS_ENDPOINT = "https://www.googleapis.com/books/v1";
        private const int TIMEOUT_SECONDS = 10;
        private const int MAX_RESULTS = 10;
        private const int RATE_LIMIT_MS = 100;
        private const int DAILY_LIMIT_WITHOUT_KEY = 1000;
        private const int DAILY_LIMIT_WITH_KEY = 90000;

        private static readonly object _rateLimitLock = new object();
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static int _requestCount = 0;
        private static DateTime _dailyResetTime = DateTime.UtcNow.Date.AddDays(1);

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public GoogleBooksSearchClient(IHttpClient httpClient, IConfigService configService)
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

            if (!_configService.GoogleBooksEnabled)
            {
                _logger.Debug("GoogleBooks search skipped - not enabled");
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
                    _logger.Info($"GoogleBooks search successful: {results.Count} results for '{trimmed}'");
                    return results;
                }

                _logger.Warn($"GoogleBooks search returned no results for '{trimmed}'");
                return new List<object>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"GoogleBooks search failed for '{searchTerm}' - falling back to next provider");
                return null;
            }
        }

        private List<object> ExecuteSearch(string query)
        {
            // Check rate limit
            if (!RateLimitRequest())
            {
                _logger.Warn("GoogleBooks daily limit exceeded");
                return null;
            }

            var encodedQuery = Uri.EscapeDataString(query);
            var requestUrl = $"volumes?q={encodedQuery}&maxResults={MAX_RESULTS}";

            // Add API key if configured
            var apiKey = _configService.GoogleBooksApiKey;
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                requestUrl += $"&key={apiKey}";
                _logger.Debug("Using GoogleBooks API key");
            }
            else
            {
                _logger.Debug("Using GoogleBooks without API key (1k/day limit)");
            }

            var request = new HttpRequestBuilder(GOOGLE_BOOKS_ENDPOINT)
                .Resource(requestUrl)
                .SetHeader("Accept", "application/json")
                .SetHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
                .Build();

            request.Method = HttpMethod.Get;

            var response = ExecuteWithRetry(request);
            if (response == null)
            {
                return null;
            }

            return ParseSearchResponse(response.Content);
        }

        private bool RateLimitRequest()
        {
            lock (_rateLimitLock)
            {
                var now = DateTime.UtcNow;

                // Reset daily counter if new day
                if (now >= _dailyResetTime)
                {
                    _requestCount = 0;
                    _dailyResetTime = now.Date.AddDays(1);
                    _logger.Debug("GoogleBooks daily request counter reset");
                }

                // Check daily limit
                var hasApiKey = !string.IsNullOrWhiteSpace(_configService.GoogleBooksApiKey);
                var dailyLimit = hasApiKey ? DAILY_LIMIT_WITH_KEY : DAILY_LIMIT_WITHOUT_KEY;

                if (_requestCount >= dailyLimit)
                {
                    _logger.Warn($"GoogleBooks daily limit ({dailyLimit}) reached");
                    return false;
                }

                // Enforce minimum time between requests
                var timeSinceLastRequest = now - _lastRequestTime;
                if (timeSinceLastRequest < TimeSpan.FromMilliseconds(RATE_LIMIT_MS))
                {
                    var waitTime = TimeSpan.FromMilliseconds(RATE_LIMIT_MS) - timeSinceLastRequest;
                    Thread.Sleep(waitTime);
                }

                _lastRequestTime = DateTime.UtcNow;
                _requestCount++;
                return true;
            }
        }

        private HttpResponse ExecuteWithRetry(HttpRequest request)
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    var response = _httpClient.Execute(request);

                    if (!response.HasHttpError)
                    {
                        return response;
                    }

                    // Handle 429 Too Many Requests - rate limiting
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        _logger.Warn($"GoogleBooks API rate limit hit (429) - attempt {attempt}/2");

                        if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(2000 + new Random().Next(1000)));
                            continue;
                        }
                    }

                    // Handle 403 Forbidden (quota exceeded)
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.Warn("GoogleBooks API quota exceeded (403)");
                        return null;
                    }

                    // Handle 5xx server errors
                    if (response.StatusCode >= HttpStatusCode.InternalServerError)
                    {
                        _logger.Warn($"GoogleBooks API server error ({response.StatusCode}) - attempt {attempt}/2");

                        if (attempt == 1)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(1000 + new Random().Next(500)));
                            continue;
                        }
                    }

                    _logger.Error($"GoogleBooks API error: {response.StatusCode}");
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"GoogleBooks API request failed (attempt {attempt}/2): {ex.Message}");

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

                var searchResult = JsonSerializer.Deserialize<GoogleBooksResponse>(responseContent, jsonOptions);

                if (searchResult?.Items == null || !searchResult.Items.Any())
                {
                    _logger.Debug("GoogleBooks response contained no items");
                    return new List<object>();
                }

                var results = new List<object>();

                foreach (var item in searchResult.Items)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.Id))
                    {
                        results.Add(item);
                    }
                }

                _logger.Debug($"Parsed {results.Count} GoogleBooks search results");
                return results;
            }
            catch (JsonException ex)
            {
                _logger.Error(ex, $"Failed to parse GoogleBooks response: {ex.Message}");
                return null;
            }
        }
    }
}
