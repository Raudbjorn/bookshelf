using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.Goodreads;
using NzbDrone.Core.MetadataSource.GoogleBooks;
using NzbDrone.Core.MetadataSource.Hardcover;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    public class BookInfoProxy : IProvideAuthorInfo, IProvideBookInfo, ISearchForNewBook, ISearchForNewAuthor, ISearchForNewEntity
    {
        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            Converters = { new STJUtcConverter() }
        };

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly IGoodreadsSearchProxy _goodreadsSearchProxy;
        private readonly IHardcoverSearchClient _hardcoverSearchClient;
        private readonly IOpenLibrarySearchClient _openLibrarySearchClient;
        private readonly IGoogleBooksSearchClient _googleBooksSearchClient;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly Logger _logger;
        private readonly IMetadataRequestBuilder _requestBuilder;
        private readonly ICached<HashSet<string>> _cache;
        private readonly CachingService _authorCache;

        public BookInfoProxy(IHttpClient httpClient,
                             ICachedHttpResponseService cachedHttpClient,
                             IGoodreadsSearchProxy goodreadsSearchProxy,
                             IHardcoverSearchClient hardcoverSearchClient,
                             IOpenLibrarySearchClient openLibrarySearchClient,
                             IGoogleBooksSearchClient googleBooksSearchClient,
                             IAuthorService authorService,
                             IBookService bookService,
                             IEditionService editionService,
                             IMetadataRequestBuilder requestBuilder,
                             Logger logger,
                             ICacheManager cacheManager)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _goodreadsSearchProxy = goodreadsSearchProxy;
            _hardcoverSearchClient = hardcoverSearchClient;
            _openLibrarySearchClient = openLibrarySearchClient;
            _googleBooksSearchClient = googleBooksSearchClient;
            _authorService = authorService;
            _bookService = bookService;
            _editionService = editionService;
            _requestBuilder = requestBuilder;
            _cache = cacheManager.GetCache<HashSet<string>>(GetType());
            _logger = logger;

            _authorCache = new CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 })));
            _authorCache.DefaultCachePolicy = new CacheDefaults
            {
                DefaultCacheDurationSeconds = 60
            };
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            var httpRequest = _requestBuilder.GetRequestBuilder().Create()
                .SetSegment("route", "author/changed")
                .AddQueryParam("since", startTime.ToString("o"))
                .Build();

            httpRequest.SuppressHttpError = true;

            var httpResponse = _httpClient.Get<RecentUpdatesResource>(httpRequest);

            if (httpResponse.Resource == null || httpResponse.Resource.Limited)
            {
                return null;
            }

            return new HashSet<string>(httpResponse.Resource.Ids.Select(x => x.ToString()));
        }

        public Author GetAuthorInfo(string foreignAuthorId, bool useCache = false)
        {
            _logger.Debug("Getting Author details GoodreadsId of {0}", foreignAuthorId);

            try
            {
                if (useCache)
                {
                    return PollAuthor(foreignAuthorId);
                }

                return PollAuthorUncached(foreignAuthorId);
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Unexpected error getting author info: {foreignAuthorId}", foreignAuthorId);
                throw;
            }
        }

        public HashSet<string> GetChangedBooks(DateTime startTime)
        {
            return _cache.Get("ChangedBooks", () => GetChangedBooksUncached(startTime), TimeSpan.FromMinutes(30));
        }

        private HashSet<string> GetChangedBooksUncached(DateTime startTime)
        {
            return null;
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
        {
            try
            {
                return PollBook(foreignBookId);
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Unexpected error getting book info: {foreignBookId}", foreignBookId);
                throw;
            }
        }

        public List<object> SearchForNewEntity(string title)
        {
            _logger.Info($"[BookInfoProxy] SearchForNewEntity called with title: '{title}'");

            // Try Hardcover first if enabled
            try
            {
                _logger.Info("[BookInfoProxy] Trying Hardcover search first for: '" + title + "'");
                var hardcoverResults = _hardcoverSearchClient?.Search(title);

                if (hardcoverResults == null)
                {
                    _logger.Debug("[BookInfoProxy] Hardcover search unavailable or failed, falling back to Goodreads");
                }
                else
                {
                    var mappedResults = MapHardcoverResultsToDomain(hardcoverResults);
                    _logger.Info($"[BookInfoProxy] Hardcover mapped results: {mappedResults.Count}");

                    if (mappedResults.Count > 0)
                    {
                        _logger.Info($"[BookInfoProxy] Returning {mappedResults.Count} results from Hardcover");
                        return mappedResults;
                    }

                    _logger.Info("[BookInfoProxy] Hardcover returned no results, falling back to Goodreads");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "[BookInfoProxy] Hardcover search threw exception for '" + title + "', falling back to next provider");
            }

            // Try OpenLibrary second if enabled
            try
            {
                _logger.Info("[BookInfoProxy] Trying OpenLibrary search for: '" + title + "'");
                var openLibraryResults = _openLibrarySearchClient?.Search(title);

                if (openLibraryResults == null)
                {
                    _logger.Debug("[BookInfoProxy] OpenLibrary search unavailable or failed, falling back to Goodreads");
                }
                else
                {
                    var mappedResults = MapOpenLibraryResultsToDomain(openLibraryResults);
                    _logger.Info($"[BookInfoProxy] OpenLibrary mapped results: {mappedResults.Count}");

                    if (mappedResults.Count > 0)
                    {
                        _logger.Info($"[BookInfoProxy] Returning {mappedResults.Count} results from OpenLibrary");
                        return mappedResults;
                    }

                    _logger.Info("[BookInfoProxy] OpenLibrary returned no results, falling back to Goodreads");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "[BookInfoProxy] OpenLibrary search threw exception for '" + title + "', falling back to next provider");
            }

            // Try GoogleBooks third if enabled
            try
            {
                _logger.Info("[BookInfoProxy] Trying GoogleBooks search for: '" + title + "'");
                var googleBooksResults = _googleBooksSearchClient?.Search(title);

                if (googleBooksResults == null)
                {
                    _logger.Debug("[BookInfoProxy] GoogleBooks search unavailable or failed, falling back to Goodreads");
                }
                else
                {
                    var mappedResults = MapGoogleBooksResultsToDomain(googleBooksResults);
                    _logger.Info($"[BookInfoProxy] GoogleBooks mapped results: {mappedResults.Count}");

                    if (mappedResults.Count > 0)
                    {
                        _logger.Info($"[BookInfoProxy] Returning {mappedResults.Count} results from GoogleBooks");
                        return mappedResults;
                    }

                    _logger.Info("[BookInfoProxy] GoogleBooks returned no results, falling back to Goodreads");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "[BookInfoProxy] GoogleBooks search threw exception for '" + title + "', falling back to Goodreads");
            }

            // Fall back to existing Goodreads search
            _logger.Info("[BookInfoProxy] Using Goodreads search for: " + title);
            var books = SearchForNewBook(title, null, false);

            var result = new List<object>();
            foreach (var book in books)
            {
                var author = book.Author.Value;

                if (!result.Contains(author))
                {
                    result.Add(author);
                }

                result.Add(book);
            }

            return result;
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            var books = SearchForNewBook(title, null);

            return books
                .Select(x => x.Author.Value)
                .DistinctBy(x => x.ForeignAuthorId)
                .ToList();
        }

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            var q = title.ToLower().Trim();
            if (author != null)
            {
                q += " " + author;
            }

            try
            {
                var lowerTitle = title.ToLowerInvariant();

                var split = lowerTitle.Split(':');
                var prefix = split[0];

                if (split.Length == 2 && new[] { "author", "work", "edition", "isbn", "asin" }.Contains(prefix))
                {
                    var slug = split[1].Trim();

                    if (slug.IsNullOrWhiteSpace() || slug.Any(char.IsWhiteSpace))
                    {
                        return new List<Book>();
                    }

                    if (prefix == "author" || prefix == "work" || prefix == "edition")
                    {
                        var isValid = int.TryParse(slug, out var searchId);
                        if (!isValid)
                        {
                            return new List<Book>();
                        }

                        if (prefix == "author")
                        {
                            return SearchByGoodreadsAuthorId(searchId);
                        }

                        if (prefix == "work")
                        {
                            return SearchByGoodreadsWorkId(searchId);
                        }

                        if (prefix == "edition")
                        {
                            return SearchByGoodreadsBookId(searchId, getAllEditions);
                        }
                    }

                    // to handle isbn / asin
                    q = slug;
                }

                return Search(q, getAllEditions);
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex, ex.Message);
                throw new GoodreadsException("Search for '{0}' failed. Unable to communicate with Goodreads.", ex, title);
            }
            catch (Exception ex) when (ex is not BookInfoException)
            {
                _logger.Warn(ex, ex.Message);
                throw new GoodreadsException("Search for '{0}' failed. Invalid response received from Goodreads.", ex, title);
            }
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            return Search(isbn, true);
        }

        public List<Book> SearchByAsin(string asin)
        {
            return Search(asin, true);
        }

        private List<Book> Search(string query, bool getAllEditions)
        {
            List<SearchJsonResource> result;
            try
            {
                result = _goodreadsSearchProxy.Search(query);
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Error searching for {0}", query);
                return new List<Book>();
            }

            var books = new List<Book>();

            if (getAllEditions)
            {
                // Slower but more exhaustive, less intensive on metadata API
                var bookIds = result.Select(x => x.WorkId).ToList();

                var idMap = result.Select(x => new { AuthorId = x.Author.Id, BookId = x.WorkId })
                    .GroupBy(x => x.AuthorId)
                    .ToDictionary(x => x.Key, x => x.Select(i => i.BookId.ToString()).ToList());

                List<Book> authorBooks;
                foreach (var author in idMap.Keys)
                {
                    authorBooks = SearchByGoodreadsAuthorId(author);
                    books.AddRange(authorBooks.Where(b => idMap[author].Contains(b.ForeignBookId)));
                }

                var missingBooks = bookIds.ExceptBy(x => x.ToString(), books, x => x.ForeignBookId, StringComparer.Ordinal).ToList();
                foreach (var book in missingBooks)
                {
                    books.AddRange(SearchByGoodreadsWorkId(book));
                }

                return books;
            }
            else
            {
                // Use sparingly, hits metadata API quite hard
                var ids = result.Select(x => x.BookId).ToList();

                if (ids.Count == 0)
                {
                    return new List<Book>();
                }

                if (ids.Count == 1)
                {
                    return SearchByGoodreadsBookId(ids[0], false);
                }

                try
                {
                    return MapSearchResult(ids);
                }
                catch (HttpException ex)
                {
                    _logger.Warn(ex);
                    throw new BookInfoException("Search for '{0}' failed. Unable to communicate with ReadarrAPI, returning status code: {1}.", ex, query, ex.Response.StatusCode);
                }
                catch (Exception e)
                {
                    _logger.Warn(e, "Error mapping search results");

                    return new List<Book>();
                }
            }
        }

        private List<Book> SearchByGoodreadsAuthorId(int id)
        {
            try
            {
                var authorId = id.ToString();
                var result = GetAuthorInfo(authorId);
                var books = result.Books.Value;
                var authors = new Dictionary<string, AuthorMetadata> { { authorId, result.Metadata.Value } };

                foreach (var book in books)
                {
                    AddDbIds(authorId, book, authors);
                }

                return books;
            }
            catch (AuthorNotFoundException)
            {
                return new List<Book>();
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Error searching by author id");
                return new List<Book>();
            }
        }

        public List<Book> SearchByGoodreadsWorkId(int id)
        {
            try
            {
                var tuple = GetBookInfo(id.ToString());
                AddDbIds(tuple.Item1, tuple.Item2, tuple.Item3.ToDictionary(x => x.ForeignAuthorId));
                return new List<Book> { tuple.Item2 };
            }
            catch (BookNotFoundException)
            {
                return new List<Book>();
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Error searching by work id");
                return new List<Book>();
            }
        }

        public List<Book> SearchByGoodreadsBookId(int id, bool getAllEditions)
        {
            try
            {
                var book = GetEditionInfo(id, getAllEditions);

                return new List<Book> { book };
            }
            catch (AuthorNotFoundException)
            {
                return new List<Book>();
            }
            catch (BookNotFoundException)
            {
                return new List<Book>();
            }
            catch (EditionNotFoundException)
            {
                return new List<Book>();
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Error searching by book id");
                return new List<Book>();
            }
        }

        private Book GetEditionInfo(int id, bool getAllEditions)
        {
            HttpRequest httpRequest;
            HttpResponse httpResponse;

            while (true)
            {
                httpRequest = _requestBuilder.GetRequestBuilder().Create()
                    .SetSegment("route", $"book/{id}")
                    .Build();

                httpRequest.SuppressHttpError = true;

                // we expect a redirect
                httpResponse = _httpClient.Get(httpRequest);

                if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    WaitUntilRetry(httpResponse);
                }
                else
                {
                    break;
                }
            }

            if (httpResponse.StatusCode == HttpStatusCode.NotFound)
            {
                throw new EditionNotFoundException(id.ToString());
            }

            if (!httpResponse.HasHttpRedirect)
            {
                throw new BookInfoException($"Unexpected response from {httpRequest.Url}");
            }

            var location = httpResponse.Headers.GetSingleValue("Location");
            var split = location.Split('/').Reverse().ToList();
            var newId = split[0];
            var type = split[1];

            Book book;
            List<AuthorMetadata> authors;

            if (type == "author")
            {
                var author = PollAuthor(newId);

                book = author.Books.Value.FirstOrDefault(b => b.Editions.Value.Any(e => e.ForeignEditionId == id.ToString()));
                authors = new List<AuthorMetadata> { author.Metadata.Value };
            }
            else if (type == "work")
            {
                var tuple = PollBook(newId);

                book = tuple.Item2;
                authors = tuple.Item3;
            }
            else
            {
                throw new NotImplementedException($"Unexpected response from {httpResponse.Request.Url}");
            }

            if (book == null || book.Editions.Value.All(e => e.ForeignEditionId != id.ToString()))
            {
                throw new EditionNotFoundException(id.ToString());
            }

            if (!getAllEditions)
            {
                var trimmed = new Book();
                trimmed.UseMetadataFrom(book);
                trimmed.Author.Value.Metadata = book.AuthorMetadata.Value;
                trimmed.AuthorMetadata = book.AuthorMetadata.Value;
                trimmed.SeriesLinks = book.SeriesLinks;
                var edition = book.Editions.Value.SingleOrDefault(e => e.ForeignEditionId == id.ToString());
                if (edition != null)
                {
                    edition.Monitored = true;
                }

                trimmed.Editions = new List<Edition> { edition };
                book = trimmed;
            }

            var authorDict = authors.ToDictionary(x => x.ForeignAuthorId);
            AddDbIds(book.AuthorMetadata.Value.ForeignAuthorId, book, authorDict);

            return book;
        }

        private List<Book> MapSearchResult(List<int> ids)
        {
            HttpResponse<BulkBookResource> httpResponse;

            while (true)
            {
                var httpRequest = _requestBuilder.GetRequestBuilder().Create()
                    .SetSegment("route", "book/bulk")
                    .SetHeader("Content-Type", "application/json")
                    .Build();

                httpRequest.SetContent(ids.ToJson());
                httpRequest.ContentSummary = ids.ToJson(Formatting.None);

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpErrorStatusCodes = new[] { HttpStatusCode.TooManyRequests };

                httpResponse = _httpClient.Post<BulkBookResource>(httpRequest);

                if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    WaitUntilRetry(httpResponse);
                }
                else
                {
                    break;
                }
            }

            return MapBulkBook(httpResponse.Resource);
        }

        private List<Book> MapBulkBook(BulkBookResource resource)
        {
            var books = new List<Book>();

            if (resource == null)
            {
                return books;
            }

            var authors = resource.Authors.Select(MapAuthorMetadata).ToDictionary(x => x.ForeignAuthorId, x => x);
            var series = resource.Series.Select(MapSeries).ToList();

            foreach (var work in resource.Works)
            {
                var book = MapBook(work);
                var authorId = work.Books.OrderByDescending(b => b.AverageRating * b.RatingCount).First().Contributors.First().ForeignId.ToString();

                AddDbIds(authorId, book, authors);

                books.Add(book);
            }

            MapSeriesLinks(series, books, resource.Series);

            return books;
        }

        private void AddDbIds(string authorId, Book book, Dictionary<string, AuthorMetadata> authors)
        {
            var dbBook = _bookService.FindById(book.ForeignBookId);
            if (dbBook != null)
            {
                book.UseDbFieldsFrom(dbBook);

                var editions = _editionService.GetEditionsByBook(dbBook.Id).ToDictionary(x => x.ForeignEditionId);

                // If we have any database editions, exactly one will be monitored.
                // So unmonitor all the found editions and let the UseDbFieldsFrom set
                // the monitored status
                foreach (var edition in book.Editions.Value)
                {
                    edition.Monitored = false;
                    if (editions.TryGetValue(edition.ForeignEditionId, out var dbEdition))
                    {
                        edition.UseDbFieldsFrom(dbEdition);
                    }
                }

                // Double check at least one edition is monitored
                if (book.Editions.Value.Any() && !book.Editions.Value.Any(x => x.Monitored))
                {
                    var mostPopular = book.Editions.Value.OrderByDescending(x => x.Ratings.Popularity).First();
                    mostPopular.Monitored = true;
                }
            }

            var author = _authorService.FindById(authorId);

            if (author == null)
            {
                if (!authors.TryGetValue(authorId, out var metadata))
                {
                    throw new BookInfoException(string.Format("Expected author metadata for id [{0}] in book data {1}", authorId, book));
                }

                author = new Author
                {
                    CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                    Metadata = metadata
                };
            }

            book.Author = author;
            book.AuthorMetadata = author.Metadata.Value;
            book.AuthorMetadataId = author.AuthorMetadataId;
        }

        private Author PollAuthor(string foreignAuthorId)
        {
            return _authorCache.GetOrAdd(foreignAuthorId,
                () => PollAuthorUncached(foreignAuthorId),
                new LazyCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                    ImmediateAbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1),
                    Size = 1,
                    SlidingExpiration = TimeSpan.FromMinutes(1),
                    ExpirationMode = ExpirationMode.ImmediateEviction
                }.RegisterPostEvictionCallback((key, value, reason, state) => _logger.Debug($"Clearing cache for {key} due to {reason}")));
        }

        private Author PollAuthorUncached(string foreignAuthorId)
        {
            AuthorResource resource = null;

            for (var i = 0; i < 60; i++)
            {
                var httpRequest = _requestBuilder.GetRequestBuilder().Create()
                    .SetSegment("route", $"author/{foreignAuthorId}")
                    .Build();

                httpRequest.AllowAutoRedirect = true;
                httpRequest.SuppressHttpError = true;

                var httpResponse = _cachedHttpClient.Get(httpRequest, false, TimeSpan.FromMinutes(30));

                if (httpResponse.HasHttpError)
                {
                    if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        WaitUntilRetry(httpResponse);
                        continue;
                    }
                    else if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new AuthorNotFoundException(foreignAuthorId);
                    }
                    else if (httpResponse.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new BadRequestException(foreignAuthorId);
                    }
                    else
                    {
                        throw new BookInfoException("Unexpected error fetching author data");
                    }
                }

                resource = JsonSerializer.Deserialize<AuthorResource>(httpResponse.Content, SerializerSettings);

                if (resource.Works != null)
                {
                    resource.Works ??= new List<WorkResource>();
                    resource.Series ??= new List<SeriesResource>();
                    break;
                }

                Thread.Sleep(2000);
            }

            if (resource?.Works == null)
            {
                throw new BookInfoException($"Failed to get works for {foreignAuthorId}");
            }

            return MapAuthor(resource);
        }

        private Tuple<string, Book, List<AuthorMetadata>> PollBook(string foreignBookId)
        {
            WorkResource resource = null;

            for (var i = 0; i < 60; i++)
            {
                var httpRequest = _requestBuilder.GetRequestBuilder().Create()
                    .SetSegment("route", $"work/{foreignBookId}")
                    .Build();

                httpRequest.SuppressHttpError = true;

                // this may redirect to an author
                var httpResponse = _httpClient.Get(httpRequest);

                if (httpResponse.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    WaitUntilRetry(httpResponse);
                    continue;
                }

                if (httpResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BookNotFoundException(foreignBookId);
                }

                if (httpResponse.HasHttpRedirect)
                {
                    var location = httpResponse.Headers.GetSingleValue("Location");
                    var split = location.Split('/').Reverse().ToList();
                    var newId = split[0];
                    var type = split[1];

                    if (type == "author")
                    {
                        var author = PollAuthor(newId);
                        var authorBook = author.Books.Value.SingleOrDefault(x => x.ForeignBookId == foreignBookId);

                        if (authorBook == null)
                        {
                            throw new BookNotFoundException(foreignBookId);
                        }

                        var authorMetadata = new List<AuthorMetadata> { author.Metadata.Value };

                        return Tuple.Create(author.ForeignAuthorId, authorBook, authorMetadata);
                    }
                    else
                    {
                        throw new NotImplementedException($"Unexpected response from {httpResponse.Request.Url}");
                    }
                }

                if (httpResponse.HasHttpError)
                {
                    if (httpResponse.StatusCode == HttpStatusCode.BadRequest)
                    {
                        throw new BadRequestException(foreignBookId);
                    }
                    else
                    {
                        throw new BookInfoException("Unexpected response fetching book data");
                    }
                }

                resource = JsonSerializer.Deserialize<WorkResource>(httpResponse.Content, SerializerSettings);

                if (resource.Books != null)
                {
                    break;
                }

                Thread.Sleep(2000);
            }

            if (resource?.Books == null || resource?.Authors == null || (!resource?.Authors?.Any() ?? false))
            {
                throw new BookInfoException($"Failed to get books for {foreignBookId}");
            }

            var book = MapBook(resource);
            var authorId = GetAuthorId(resource).ToString();
            var metadata = resource.Authors.Select(MapAuthorMetadata).ToList();

            var series = resource.Series.Select(MapSeries).ToList();
            MapSeriesLinks(series, new List<Book> { book }, resource.Series);

            return Tuple.Create(authorId, book, metadata);
        }

        private void WaitUntilRetry(HttpResponse response)
        {
            var seconds = 5;

            if (response.Headers.ContainsKey("Retry-After"))
            {
                var retryAfter = response.Headers["Retry-After"];

                if (!int.TryParse(retryAfter, out seconds))
                {
                    seconds = 5;
                }
            }

            _logger.Info("BookInfo returned 429, backing off for {0}s", seconds);

            Thread.Sleep(TimeSpan.FromSeconds(seconds));
        }

        private static AuthorMetadata MapAuthorMetadata(AuthorResource resource)
        {
            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = resource.ForeignId.ToString(),
                TitleSlug = resource.ForeignId.ToString(),
                Name = resource.Name.CleanSpaces(),
                Overview = resource.Description,
                Ratings = new Ratings { Votes = resource.RatingCount, Value = (decimal)resource.AverageRating },
                Status = AuthorStatusType.Continuing
            };

            metadata.SortName = metadata.Name.ToLower();
            metadata.NameLastFirst = metadata.Name.ToLastFirst();
            metadata.SortNameLastFirst = metadata.NameLastFirst.ToLower();

            if (resource.ImageUrl.IsNotNullOrWhiteSpace())
            {
                metadata.Images.Add(new MediaCover.MediaCover
                {
                    Url = resource.ImageUrl,
                    CoverType = MediaCoverTypes.Poster
                });
            }

            if (resource.Url.IsNotNullOrWhiteSpace())
            {
                metadata.Links.Add(new Links { Url = resource.Url, Name = "Goodreads" });
            }

            return metadata;
        }

        private static Author MapAuthor(AuthorResource resource)
        {
            var metadata = MapAuthorMetadata(resource);

            var books = resource.Works
                .Where(x => x.ForeignId > 0 && GetAuthorId(x) == resource.ForeignId)
                .Select(MapBook)
                .ToList();

            books.ForEach(x => x.AuthorMetadata = metadata);

            var series = resource.Series.Select(MapSeries).ToList();

            MapSeriesLinks(series, books, resource.Series);

            var result = new Author
            {
                Metadata = metadata,
                CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                Books = books,
                Series = series
            };

            return result;
        }

        private static void MapSeriesLinks(List<Series> series, List<Book> books, List<SeriesResource> resource)
        {
            var bookDict = books.ToDictionary(x => x.ForeignBookId);
            var seriesDict = series.ToDictionary(x => x.ForeignSeriesId);

            foreach (var book in books)
            {
                book.SeriesLinks = new List<SeriesBookLink>();
            }

            // only take series where there are some works
            foreach (var s in resource.Where(x => x.LinkItems.Any()))
            {
                if (seriesDict.TryGetValue(s.ForeignId.ToString(), out var curr))
                {
                    curr.LinkItems = s.LinkItems.Where(x => x.ForeignWorkId != 0 && bookDict.ContainsKey(x.ForeignWorkId.ToString())).Select(l => new SeriesBookLink
                    {
                        Book = bookDict[l.ForeignWorkId.ToString()],
                        Series = curr,
                        IsPrimary = l.Primary,
                        Position = l.PositionInSeries,
                        SeriesPosition = l.SeriesPosition
                    }).ToList();

                    foreach (var l in curr.LinkItems.Value)
                    {
                        l.Book.Value.SeriesLinks.Value.Add(l);
                    }
                }
            }
        }

        private static Series MapSeries(SeriesResource resource)
        {
            var series = new Series
            {
                ForeignSeriesId = resource.ForeignId.ToString(),
                Title = resource.Title,
                Description = resource.Description
            };

            return series;
        }

        private static Book MapBook(WorkResource resource)
        {
            var book = new Book
            {
                ForeignBookId = resource.ForeignId.ToString(),
                Title = resource.Title,
                TitleSlug = resource.ForeignId.ToString(),
                CleanTitle = Parser.Parser.CleanAuthorName(resource.Title),
                ReleaseDate = resource.ReleaseDate,
                Genres = resource.Genres,
                RelatedBooks = resource.RelatedWorks
            };

            book.Links.Add(new Links { Url = resource.Url, Name = "Goodreads Editions" });

            if (resource.Books != null)
            {
                book.Editions = resource.Books.Select(x => MapEdition(x)).ToList();

                // monitor the most popular release
                var mostPopular = book.Editions.Value.MaxBy(x => x.Ratings.Popularity);
                if (mostPopular != null)
                {
                    mostPopular.Monitored = true;

                    // fix work title if missing
                    if (book.Title.IsNullOrWhiteSpace())
                    {
                        book.Title = mostPopular.Title;
                    }
                }
            }
            else
            {
                book.Editions = new List<Edition>();
            }

            // If we are missing the book release date, set as the earliest edition release date
            if (!book.ReleaseDate.HasValue)
            {
                var editionReleases = book.Editions.Value
                    .Where(x => x.ReleaseDate.HasValue && x.ReleaseDate.Value.Month != 1 && x.ReleaseDate.Value.Day != 1)
                    .ToList();

                if (editionReleases.Any())
                {
                    book.ReleaseDate = editionReleases.Min(x => x.ReleaseDate.Value);
                }
                else
                {
                    editionReleases = book.Editions.Value.Where(x => x.ReleaseDate.HasValue).ToList();
                    if (editionReleases.Any())
                    {
                        book.ReleaseDate = editionReleases.Min(x => x.ReleaseDate.Value);
                    }
                }
            }

            Debug.Assert(!book.Editions.Value.Any() || book.Editions.Value.Count(x => x.Monitored) == 1, "one edition monitored");

            book.AnyEditionOk = true;

            var ratingCount = book.Editions.Value.Sum(x => x.Ratings.Votes);

            if (ratingCount > 0)
            {
                book.Ratings = new Ratings
                {
                    Votes = ratingCount,
                    Value = book.Editions.Value.Sum(x => x.Ratings.Votes * x.Ratings.Value) / ratingCount
                };
            }
            else
            {
                book.Ratings = new Ratings { Votes = 0, Value = 0 };
            }

            return book;
        }

        private static Edition MapEdition(BookResource resource)
        {
            var edition = new Edition
            {
                ForeignEditionId = resource.ForeignId.ToString(),
                TitleSlug = resource.ForeignId.ToString(),
                Isbn13 = resource.Isbn13,
                Asin = resource.Asin,
                Title = resource.Title.CleanSpaces(),
                Language = resource.Language,
                Overview = resource.Description,
                Format = resource.Format,
                IsEbook = resource.IsEbook,
                Disambiguation = resource.EditionInformation,
                Publisher = resource.Publisher,
                PageCount = resource.NumPages ?? 0,
                ReleaseDate = resource.ReleaseDate,
                Ratings = new Ratings { Votes = resource.RatingCount, Value = (decimal)resource.AverageRating }
            };

            if (resource.ImageUrl.IsNotNullOrWhiteSpace())
            {
                edition.Images.Add(new MediaCover.MediaCover
                {
                    Url = resource.ImageUrl,
                    CoverType = MediaCoverTypes.Cover
                });
            }

            edition.Links.Add(new Links { Url = resource.Url, Name = "Goodreads Book" });

            return edition;
        }

        private static int GetAuthorId(WorkResource b)
        {
            return b.Books.OrderByDescending(x => x.RatingCount * x.AverageRating).FirstOrDefault(x => x.Contributors.Any())?.Contributors.First().ForeignId ?? 0;
        }

        // Hardcover conversion methods
        public List<object> MapHardcoverResultsToDomain(List<object> raw)
        {
            var list = new List<object>();
            foreach (var item in raw)
            {
                if (item is HardcoverAuthorResult a)
                {
                    list.Add(ConvertHardcoverAuthor(a));
                }
                else if (item is HardcoverBookResult b)
                {
                    list.Add(ConvertHardcoverBook(b));
                }
                else if (item is HardcoverSeriesResult s)
                {
                    list.Add(ConvertHardcoverSeries(s));
                }
                else
                {
                    _logger.Debug("[BookInfoProxy] Unknown Hardcover result type: " + item?.GetType().Name);
                }
            }

            return list;
        }

        private Author ConvertHardcoverAuthor(HardcoverAuthorResult a)
        {
            var name = a?.Name ?? string.Empty;
            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = "hc:" + a?.Id,
                TitleSlug = (!string.IsNullOrWhiteSpace(a?.Slug)) ? a.Slug : ("hc:" + a?.Id),
                Name = name,
                Overview = a?.Bio ?? string.Empty,
                Status = AuthorStatusType.Continuing,
                Ratings = new Ratings
                {
                    Value = 0m,
                    Votes = 0
                },
                HardcoverAuthorId = "hc:" + a?.Id
            };

            metadata.SortName = name.ToLower();
            metadata.NameLastFirst = name.ToLastFirst();
            metadata.SortNameLastFirst = metadata.NameLastFirst.ToLower();

            if (!string.IsNullOrWhiteSpace(a?.ImageUrl))
            {
                metadata.Images.Add(new MediaCover.MediaCover
                {
                    Url = a.ImageUrl,
                    CoverType = MediaCoverTypes.Poster
                });
            }

            if (!string.IsNullOrWhiteSpace(a?.Slug))
            {
                metadata.Links.Add(new Links
                {
                    Url = "https://hardcover.app/authors/" + a.Slug,
                    Name = "Hardcover"
                });
            }

            var author = new Author
            {
                Metadata = metadata,
                CleanName = Parser.Parser.CleanAuthorName(name),
                Monitored = false
            };

            return author;
        }

        private Book ConvertHardcoverBook(HardcoverBookResult b)
        {
            var title = b?.Title ?? string.Empty;
            var book = new Book
            {
                ForeignBookId = "hc:" + b?.Id,
                HardcoverBookId = "hc:" + b?.Id,
                Title = title,
                TitleSlug = "hc:" + b?.Id,
                CleanTitle = Parser.Parser.CleanAuthorName(title),
                AnyEditionOk = true,
                Ratings = new Ratings
                {
                    Value = (decimal)(b?.Rating ?? 0f),
                    Votes = 0
                }
            };

            if (!string.IsNullOrWhiteSpace(b?.ReleaseDate) && DateTime.TryParse(b.ReleaseDate, out var result))
            {
                book.ReleaseDate = result;
            }

            book.Links.Add(new Links
            {
                Url = "https://hardcover.app/books/" + b?.Id,
                Name = "Hardcover"
            });

            var authorName = b?.AuthorNames?.FirstOrDefault();
            var authorId = b?.AuthorIds?.FirstOrDefault();
            _logger.Debug($"ConvertHardcoverBook: Book '{b?.Title}' has author name: '{authorName}', author ID: '{authorId}'");

            if (!string.IsNullOrWhiteSpace(authorName))
            {
                var authorMetadata = new AuthorMetadata
                {
                    Name = authorName,
                    ForeignAuthorId = !string.IsNullOrWhiteSpace(authorId) ? ("hc:" + authorId) : "hc-author-unknown",
                    HardcoverAuthorId = !string.IsNullOrWhiteSpace(authorId) ? ("hc:" + authorId) : null
                };

                authorMetadata.SortName = authorName.ToLower();
                authorMetadata.NameLastFirst = authorName.ToLastFirst();
                authorMetadata.SortNameLastFirst = authorMetadata.NameLastFirst.ToLower();

                book.AuthorMetadata = authorMetadata;

                if (!string.IsNullOrWhiteSpace(authorId))
                {
                    _logger.Debug("Set HardcoverAuthorId to: " + authorMetadata.HardcoverAuthorId);
                }
                else
                {
                    _logger.Debug("No author ID to set for book '" + b?.Title + "'");
                }
            }

            var edition = new Edition
            {
                ForeignEditionId = "hc:" + b?.Id,
                Title = title,
                TitleSlug = "hc:" + b?.Id,
                Monitored = true,
                ManualAdd = false,
                PageCount = b?.Pages ?? 0,
                Ratings = new Ratings
                {
                    Value = (decimal)(b?.Rating ?? 0f),
                    Votes = 0
                }
            };

            if (!string.IsNullOrWhiteSpace(b?.ImageUrl))
            {
                edition.Images.Add(new MediaCover.MediaCover
                {
                    Url = b.ImageUrl,
                    CoverType = MediaCoverTypes.Cover
                });
            }

            if (b?.Isbns != null && b.Isbns.Length != 0)
            {
                var isbn = b.Isbns.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x.Length == 13);
                if (!string.IsNullOrWhiteSpace(isbn))
                {
                    edition.Isbn13 = isbn;
                }
            }

            book.Editions = new List<Edition> { edition };
            return book;
        }

        private Series ConvertHardcoverSeries(HardcoverSeriesResult s)
        {
            var series = new Series
            {
                ForeignSeriesId = "hc:" + s?.Id,
                HardcoverSeriesId = "hc:" + s?.Id,
                Title = s?.Name ?? string.Empty,
                Description = s?.Description ?? string.Empty
            };

            return series;
        }

        // OpenLibrary Conversion Methods
        private List<object> MapOpenLibraryResultsToDomain(List<object> results)
        {
            var mapped = new List<object>();

            foreach (var result in results)
            {
                if (result is OpenLibrarySearchDoc doc)
                {
                    var book = ConvertOpenLibrarySearchDoc(doc);
                    if (book != null)
                    {
                        // Add author if present
                        if (book.AuthorMetadata != null && book.AuthorMetadata.Value != null)
                        {
                            var authorMetadata = book.AuthorMetadata.Value;
                            var authorName = authorMetadata.Name;
                            var author = new Author
                            {
                                Metadata = authorMetadata,
                                CleanName = Parser.Parser.CleanAuthorName(authorName),
                                Monitored = false
                            };

                            // Check if we already added an author with the same name
                            var existingAuthor = mapped.OfType<Author>().FirstOrDefault(a =>
                                a.Metadata.Value?.Name == authorName);

                            if (existingAuthor == null)
                            {
                                mapped.Add(author);
                            }
                        }

                        mapped.Add(book);
                    }
                }
            }

            return mapped;
        }

        private Book ConvertOpenLibrarySearchDoc(OpenLibrarySearchDoc doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Key))
            {
                return null;
            }

            var workId = doc.Key?.Replace("/works/", "");
            var title = doc.Title ?? "Unknown Title";

            var book = new Book
            {
                ForeignBookId = "ol:" + workId,
                OpenLibraryWorkId = "ol:" + workId,
                Title = title,
                TitleSlug = "ol:" + workId,
                CleanTitle = Parser.Parser.CleanAuthorName(title),
                AnyEditionOk = true,
                Ratings = new Ratings
                {
                    Value = (decimal)(doc.RatingsAverage ?? 0f),
                    Votes = doc.RatingsCount ?? 0
                }
            };

            if (doc.FirstPublishYear.HasValue)
            {
                book.ReleaseDate = new DateTime(doc.FirstPublishYear.Value, 1, 1);
            }

            book.Links.Add(new Links
            {
                Url = $"https://openlibrary.org{doc.Key}",
                Name = "OpenLibrary"
            });

            // Create author metadata if author information is available
            if (doc.AuthorName != null && doc.AuthorName.Any())
            {
                var authorName = doc.AuthorName.First();
                var authorKey = doc.AuthorKey?.FirstOrDefault();
                var authorId = authorKey?.Replace("/authors/", "");

                var authorMetadata = new AuthorMetadata
                {
                    Name = authorName,
                    ForeignAuthorId = !string.IsNullOrWhiteSpace(authorId) ? ("ol:" + authorId) : ("ol-author-" + authorName.Replace(" ", "_").ToLower()),
                    OpenLibraryAuthorId = !string.IsNullOrWhiteSpace(authorId) ? ("ol:" + authorId) : null
                };

                authorMetadata.SortName = authorName.ToLower();
                authorMetadata.NameLastFirst = authorName.ToLastFirst();
                authorMetadata.SortNameLastFirst = authorMetadata.NameLastFirst.ToLower();

                book.AuthorMetadata = authorMetadata;
            }

            // Create edition
            var edition = new Edition
            {
                ForeignEditionId = "ol:" + workId,
                Title = title,
                TitleSlug = "ol:" + workId,
                Monitored = true,
                ManualAdd = false,
                PageCount = doc.NumberOfPagesMedian ?? 0,
                Ratings = new Ratings
                {
                    Value = (decimal)(doc.RatingsAverage ?? 0f),
                    Votes = doc.RatingsCount ?? 0
                }
            };

            if (doc.Language != null && doc.Language.Any())
            {
                edition.Language = doc.Language.First();
            }

            if (doc.Publisher != null && doc.Publisher.Any())
            {
                edition.Publisher = doc.Publisher.First();
            }

            if (doc.Isbn != null && doc.Isbn.Any())
            {
                var isbn = doc.Isbn.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x.Length == 13);
                if (!string.IsNullOrWhiteSpace(isbn))
                {
                    edition.Isbn13 = isbn;
                }
            }

            if (doc.CoverId.HasValue)
            {
                edition.Images.Add(new MediaCover.MediaCover
                {
                    Url = $"https://covers.openlibrary.org/b/id/{doc.CoverId}-L.jpg",
                    CoverType = MediaCoverTypes.Cover
                });
            }

            if (doc.FirstPublishYear.HasValue)
            {
                edition.ReleaseDate = new DateTime(doc.FirstPublishYear.Value, 1, 1);
            }

            book.Editions = new List<Edition> { edition };
            return book;
        }

        // GoogleBooks Conversion Methods
        private List<object> MapGoogleBooksResultsToDomain(List<object> results)
        {
            var mapped = new List<object>();

            foreach (var result in results)
            {
                if (result is GoogleBookItem item)
                {
                    var book = ConvertGoogleBookItem(item);
                    if (book != null)
                    {
                        // Add author if present
                        if (book.AuthorMetadata != null && book.AuthorMetadata.Value != null)
                        {
                            var authorMetadata = book.AuthorMetadata.Value;
                            var authorName = authorMetadata.Name;
                            var author = new Author
                            {
                                Metadata = authorMetadata,
                                CleanName = Parser.Parser.CleanAuthorName(authorName),
                                Monitored = false
                            };

                            // Check if we already added an author with the same name
                            var existingAuthor = mapped.OfType<Author>().FirstOrDefault(a =>
                                a.Metadata.Value?.Name == authorName);

                            if (existingAuthor == null)
                            {
                                mapped.Add(author);
                            }
                        }

                        mapped.Add(book);
                    }
                }
            }

            return mapped;
        }

        private Book ConvertGoogleBookItem(GoogleBookItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || item.VolumeInfo == null)
            {
                return null;
            }

            var volumeId = item.Id;
            var title = item.VolumeInfo.Title ?? "Unknown Title";

            var book = new Book
            {
                ForeignBookId = "gb:" + volumeId,
                GoogleBooksId = "gb:" + volumeId,
                Title = title,
                TitleSlug = "gb:" + volumeId,
                CleanTitle = Parser.Parser.CleanAuthorName(title),
                AnyEditionOk = true
            };

            // Add subtitle if present
            if (!string.IsNullOrWhiteSpace(item.VolumeInfo.Subtitle))
            {
                book.Title = $"{title}: {item.VolumeInfo.Subtitle}";
            }

            // Add ratings
            if (item.VolumeInfo.AverageRating.HasValue)
            {
                book.Ratings = new Ratings
                {
                    Value = (decimal)item.VolumeInfo.AverageRating.Value,
                    Votes = item.VolumeInfo.RatingsCount ?? 0
                };
            }

            // Parse published date
            if (!string.IsNullOrWhiteSpace(item.VolumeInfo.PublishedDate))
            {
                if (DateTime.TryParse(item.VolumeInfo.PublishedDate, out var publishedDate))
                {
                    book.ReleaseDate = publishedDate;
                }
            }

            // Add link to GoogleBooks
            if (!string.IsNullOrWhiteSpace(item.VolumeInfo.InfoLink))
            {
                book.Links.Add(new Links
                {
                    Url = item.VolumeInfo.InfoLink,
                    Name = "GoogleBooks"
                });
            }

            // Create author metadata if author information is available
            if (item.VolumeInfo.Authors != null && item.VolumeInfo.Authors.Any())
            {
                var authorName = item.VolumeInfo.Authors.First();

                var authorMetadata = new AuthorMetadata
                {
                    Name = authorName,
                    ForeignAuthorId = "gb-author-" + authorName.Replace(" ", "_").ToLower(),
                    GoogleBooksAuthorId = "gb-author-" + authorName.Replace(" ", "_").ToLower()
                };

                authorMetadata.SortName = authorName.ToLower();
                authorMetadata.NameLastFirst = authorName.ToLastFirst();
                authorMetadata.SortNameLastFirst = authorMetadata.NameLastFirst.ToLower();

                book.AuthorMetadata = authorMetadata;
            }

            // Create edition
            var edition = new Edition
            {
                ForeignEditionId = "gb:" + volumeId,
                Title = title,
                TitleSlug = "gb:" + volumeId,
                Monitored = true,
                ManualAdd = false,
                PageCount = item.VolumeInfo.PageCount ?? 0
            };

            // Add subtitle to edition if present
            if (!string.IsNullOrWhiteSpace(item.VolumeInfo.Subtitle))
            {
                edition.Title = $"{title}: {item.VolumeInfo.Subtitle}";
            }

            // Add ratings to edition
            if (item.VolumeInfo.AverageRating.HasValue)
            {
                edition.Ratings = new Ratings
                {
                    Value = (decimal)item.VolumeInfo.AverageRating.Value,
                    Votes = item.VolumeInfo.RatingsCount ?? 0
                };
            }

            // Add language
            if (!string.IsNullOrWhiteSpace(item.VolumeInfo.Language))
            {
                edition.Language = item.VolumeInfo.Language;
            }

            // Add publisher
            if (!string.IsNullOrWhiteSpace(item.VolumeInfo.Publisher))
            {
                edition.Publisher = item.VolumeInfo.Publisher;
            }

            // Extract ISBN from industry identifiers
            if (item.VolumeInfo.IndustryIdentifiers != null && item.VolumeInfo.IndustryIdentifiers.Any())
            {
                var isbn13 = item.VolumeInfo.IndustryIdentifiers
                    .FirstOrDefault(x => x.Type == "ISBN_13")?.Identifier;

                if (!string.IsNullOrWhiteSpace(isbn13))
                {
                    edition.Isbn13 = isbn13;
                }
                else
                {
                    // Fall back to ISBN_10 if ISBN_13 not available
                    var isbn10 = item.VolumeInfo.IndustryIdentifiers
                        .FirstOrDefault(x => x.Type == "ISBN_10")?.Identifier;

                    if (!string.IsNullOrWhiteSpace(isbn10))
                    {
                        edition.Isbn13 = isbn10;
                    }
                }
            }

            // Add cover images
            if (item.VolumeInfo.ImageLinks != null)
            {
                // Try to get the largest available image
                var imageUrl = item.VolumeInfo.ImageLinks.ExtraLarge
                    ?? item.VolumeInfo.ImageLinks.Large
                    ?? item.VolumeInfo.ImageLinks.Medium
                    ?? item.VolumeInfo.ImageLinks.Small
                    ?? item.VolumeInfo.ImageLinks.Thumbnail;

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    // GoogleBooks returns http URLs, upgrade to https
                    imageUrl = imageUrl.Replace("http://", "https://");

                    edition.Images.Add(new MediaCover.MediaCover
                    {
                        Url = imageUrl,
                        CoverType = MediaCoverTypes.Cover
                    });
                }
            }

            // Add published date to edition
            if (!string.IsNullOrWhiteSpace(item.VolumeInfo.PublishedDate))
            {
                if (DateTime.TryParse(item.VolumeInfo.PublishedDate, out var publishedDate))
                {
                    edition.ReleaseDate = publishedDate;
                }
            }

            book.Editions = new List<Edition> { edition };
            return book;
        }
    }
}
