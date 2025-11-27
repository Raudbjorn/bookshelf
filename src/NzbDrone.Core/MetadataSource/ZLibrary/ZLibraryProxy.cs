using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.ZLibrary.Resources;

namespace NzbDrone.Core.MetadataSource.ZLibrary
{
    public class ZLibraryProxy : ISearchForNewBook, IProvideBookInfo
    {
        private const string BaseUrl = "https://1lib.sk";
        private const string ApiUrl = BaseUrl + "/eapi";

        private readonly IHttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public ZLibraryProxy(IHttpClient httpClient, IConfigService configService, Logger logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public List<Book> SearchForNewBook(string title, string author = null, bool getAllEditions = true)
        {
            if (!_configService.ZLibraryEnabled)
            {
                return new List<Book>();
            }

            _logger.Debug("Searching Z-Library for: title={0}, author={1}", title, author);

            try
            {
                EnsureAuthenticated();

                var query = $"{title} {author}".Trim();
                var results = Search(query);

                return results.Select(MapToBook).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error searching Z-Library for title={0}, author={1}", title, author);
                return new List<Book>();
            }
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            return SearchForNewBook(isbn);
        }

        public List<Book> SearchByAsin(string asin)
        {
             return SearchForNewBook(asin);
        }

        public List<Book> SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions)
        {
            return new List<Book>();
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
        {
            if (!_configService.ZLibraryEnabled)
            {
                throw new ZLibraryException("Z-Library provider is disabled.");
            }

            var parts = foreignBookId.Split(':');
            if (parts.Length < 3)
            {
                 throw new ZLibraryException($"Invalid Z-Library ID: {foreignBookId}");
            }

            var id = parts[1];
            var hash = parts[2];

            try
            {
                EnsureAuthenticated();
                var book = GetBookDetails(id, hash);

                if (book == null)
                {
                    throw new BookNotFoundException(foreignBookId);
                }

                var mappedBook = MapToBook(book);
                var authors = new List<AuthorMetadata>();
                if (!string.IsNullOrWhiteSpace(book.Author))
                {
                    authors.Add(new AuthorMetadata { Name = book.Author, ForeignAuthorId = book.Author });
                }

                return new Tuple<string, Book, List<AuthorMetadata>>(book.Id, mappedBook, authors);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting book info from Z-Library for: {0}", foreignBookId);
                throw;
            }
        }

        private ZLibBook GetBookDetails(string id, string hash)
        {
            var builder = BuildRequestBuilder($"/book/{id}/{hash}");
            var request = builder.Build();
            var response = _httpClient.Get<ZLibBookDetailResponse>(request);

            if (!response.Resource.Success || response.Resource.Book == null)
            {
                _logger.Warn("Z-Library get book details failed: success=false or book is null");
                return null;
            }

            return response.Resource.Book;
        }

        private List<ZLibBook> Search(string query)
        {
            var builder = BuildRequestBuilder("/book/search")
                .Post()
                .AddFormParameter("message", query)
                .AddFormParameter("limit", "50")
                .AddFormParameter("languages[0]", "english"); // Make configurable

            var request = builder.Build();
            var response = _httpClient.Post<ZLibSearchResponse>(request);

            if (!response.Resource.Success)
            {
                _logger.Warn("Z-Library search failed: success=false");
                return new List<ZLibBook>();
            }

            return response.Resource.Books ?? new List<ZLibBook>();
        }

        private void EnsureAuthenticated()
        {
            var userId = _configService.ZLibraryRemixUserId;
            var userKey = _configService.ZLibraryRemixUserKey;

            if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(userKey))
            {
                return;
            }

            var email = _configService.ZLibraryEmail;
            var password = _configService.ZLibraryPassword;

            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                Login(email, password);
            }
            else
            {
                _logger.Warn("Z-Library credentials not found. Please configure Email/Password or Remix Tokens.");
            }
        }

        private void Login(string email, string password)
        {
            var builder = new HttpRequestBuilder($"{ApiUrl}/user/login")
                .SetHeader("User-Agent", "Bookshelf/1.0")
                .Post()
                .AddFormParameter("email", email)
                .AddFormParameter("password", password);

            var request = builder.Build();
            var response = _httpClient.Post<ZLibLoginResponse>(request);

            if (response.Resource.Success && response.Resource.User != null)
            {
                _configService.ZLibraryRemixUserId = response.Resource.User.RemixUserId;
                _configService.ZLibraryRemixUserKey = response.Resource.User.RemixUserKey;
                _logger.Info("Successfully logged into Z-Library as {0}", response.Resource.User.Email);
            }
            else
            {
                _logger.Error("Z-Library login failed.");
                throw new ZLibraryException("Z-Library login failed.");
            }
        }

        private HttpRequestBuilder BuildRequestBuilder(string endpoint)
        {
            var url = $"{ApiUrl}{endpoint}";
            var builder = new HttpRequestBuilder(url)
                .SetHeader("User-Agent", "Bookshelf/1.0");

            var userId = _configService.ZLibraryRemixUserId;
            var userKey = _configService.ZLibraryRemixUserKey;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                builder.SetCookie("remix_userid", userId);
            }

            if (!string.IsNullOrWhiteSpace(userKey))
            {
                builder.SetCookie("remix_userkey", userKey);
            }

            builder.SetCookie("siteLanguageV2", "en");

            return builder;
        }

        private Book MapToBook(ZLibBook zBook)
        {
            var book = new Book
            {
                ForeignBookId = $"zlib:{zBook.Id}:{zBook.Hash}",
                Title = zBook.Title?.CleanSpaces() ?? "Unknown Title",
                TitleSlug = zBook.Title?.CleanSpaces().ToLower().Replace(" ", "-"),
                ReleaseDate = ParseYear(zBook.Year),
                Links = new List<Links>
                {
                    new Links { Url = $"{BaseUrl}{zBook.Href}", Name = "Z-Library" }
                },
                AnyEditionOk = true
            };

            var edition = new Edition
            {
                ForeignEditionId = $"zlib:{zBook.Id}:{zBook.Hash}",
                Title = zBook.Title?.CleanSpaces() ?? "Unknown Title",
                TitleSlug = book.TitleSlug,
                ReleaseDate = book.ReleaseDate,
                Language = zBook.Language,
                Publisher = zBook.Publisher,
                Monitored = true,
                Images = new List<MediaCover.MediaCover>()
            };

            if (!string.IsNullOrEmpty(zBook.Cover))
            {
                 edition.Images.Add(new MediaCover.MediaCover { Url = zBook.Cover, CoverType = MediaCoverTypes.Cover });
            }

            book.Editions = new List<Edition> { edition };
            return book;
        }

        private DateTime? ParseYear(string year)
        {
             if (int.TryParse(year, out var y) && y > 0 && y < 3000)
             {
                 return new DateTime(y, 1, 1);
             }

             return null;
        }
    }
}
