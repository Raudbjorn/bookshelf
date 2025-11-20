using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.ComicVine;
using NzbDrone.Core.MetadataSource.GoogleBooks;
using NzbDrone.Core.MetadataSource.Hardcover;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.Organizer;
using Readarr.Api.V1.Author;
using Readarr.Api.V1.Books;
using Readarr.Http;

namespace Readarr.Api.V1.Search
{
    [V1ApiController("search/provider")]
    public class ProviderSearchController : Controller
    {
        private readonly IHardcoverSearchClient _hardcoverSearchClient;
        private readonly IOpenLibrarySearchClient _openLibrarySearchClient;
        private readonly IGoogleBooksSearchClient _googleBooksSearchClient;
        private readonly IComicVineSearchClient _comicVineSearchClient;
        private readonly IBookReconciliationService _reconciliationService;
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly Logger _logger;

        public ProviderSearchController(
            IHardcoverSearchClient hardcoverSearchClient,
            IOpenLibrarySearchClient openLibrarySearchClient,
            IGoogleBooksSearchClient googleBooksSearchClient,
            IComicVineSearchClient comicVineSearchClient,
            IBookReconciliationService reconciliationService,
            IBuildFileNames fileNameBuilder,
            IMapCoversToLocal coverMapper,
            Logger logger)
        {
            _hardcoverSearchClient = hardcoverSearchClient;
            _openLibrarySearchClient = openLibrarySearchClient;
            _googleBooksSearchClient = googleBooksSearchClient;
            _comicVineSearchClient = comicVineSearchClient;
            _reconciliationService = reconciliationService;
            _fileNameBuilder = fileNameBuilder;
            _coverMapper = coverMapper;
            _logger = logger;
        }

        [HttpGet("hardcover")]
        public object SearchHardcover([FromQuery] string term)
        {
            _logger.Info($"[ProviderSearch] Hardcover search requested for: '{term}'");

            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<SearchResource>();
            }

            var results = _hardcoverSearchClient?.Search(term);

            if (results == null || results.Count == 0)
            {
                _logger.Info($"[ProviderSearch] Hardcover returned no results for: '{term}'");
                return new List<SearchResource>();
            }

            _logger.Info($"[ProviderSearch] Hardcover returned {results.Count} results for: '{term}'");
            return MapToResource(results, "hardcover").ToList();
        }

        [HttpGet("openlibrary")]
        public object SearchOpenLibrary([FromQuery] string term)
        {
            _logger.Info($"[ProviderSearch] OpenLibrary search requested for: '{term}'");

            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<SearchResource>();
            }

            var results = _openLibrarySearchClient?.Search(term);

            if (results == null || results.Count == 0)
            {
                _logger.Info($"[ProviderSearch] OpenLibrary returned no results for: '{term}'");
                return new List<SearchResource>();
            }

            _logger.Info($"[ProviderSearch] OpenLibrary returned {results.Count} results for: '{term}'");
            return MapToResource(results, "openlibrary").ToList();
        }

        [HttpGet("googlebooks")]
        public object SearchGoogleBooks([FromQuery] string term)
        {
            _logger.Info($"[ProviderSearch] GoogleBooks search requested for: '{term}'");

            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<SearchResource>();
            }

            var results = _googleBooksSearchClient?.Search(term);

            if (results == null || results.Count == 0)
            {
                _logger.Info($"[ProviderSearch] GoogleBooks returned no results for: '{term}'");
                return new List<SearchResource>();
            }

            _logger.Info($"[ProviderSearch] GoogleBooks returned {results.Count} results for: '{term}'");
            return MapToResource(results, "googlebooks").ToList();
        }

        [HttpGet("comicvine")]
        public object SearchComicVine([FromQuery] string term)
        {
            _logger.Info($"[ProviderSearch] ComicVine search requested for: '{term}'");

            if (string.IsNullOrWhiteSpace(term))
            {
                return new List<SearchResource>();
            }

            var results = _comicVineSearchClient?.Search(term);

            if (results == null || results.Count == 0)
            {
                _logger.Info($"[ProviderSearch] ComicVine returned no results for: '{term}'");
                return new List<SearchResource>();
            }

            _logger.Info($"[ProviderSearch] ComicVine returned {results.Count} results for: '{term}'");
            return MapToResource(results, "comicvine").ToList();
        }

        [HttpGet]
        public object SearchAll([FromQuery] string term, [FromQuery] string providers = "hardcover,openlibrary,googlebooks,comicvine")
        {
            _logger.Info($"[ProviderSearch] Multi-provider search requested for: '{term}' (providers: {providers})");

            if (string.IsNullOrWhiteSpace(term))
            {
                return new MultiProviderSearchResource
                {
                    Query = term,
                    Hardcover = new List<SearchResource>(),
                    OpenLibrary = new List<SearchResource>(),
                    GoogleBooks = new List<SearchResource>(),
                    ComicVine = new List<SearchResource>()
                };
            }

            var providerList = providers.Split(',').Select(p => p.Trim().ToLower()).ToList();
            var response = new MultiProviderSearchResource
            {
                Query = term,
                Hardcover = new List<SearchResource>(),
                OpenLibrary = new List<SearchResource>(),
                GoogleBooks = new List<SearchResource>(),
                ComicVine = new List<SearchResource>()
            };

            // Search each requested provider
            if (providerList.Contains("hardcover"))
            {
                var hardcoverResults = _hardcoverSearchClient?.Search(term);
                if (hardcoverResults != null && hardcoverResults.Count > 0)
                {
                    response.Hardcover = MapToResource(hardcoverResults, "hardcover").ToList();
                    _logger.Info($"[ProviderSearch] Hardcover: {response.Hardcover.Count} results");
                }
            }

            if (providerList.Contains("openlibrary"))
            {
                var openLibraryResults = _openLibrarySearchClient?.Search(term);
                if (openLibraryResults != null && openLibraryResults.Count > 0)
                {
                    response.OpenLibrary = MapToResource(openLibraryResults, "openlibrary").ToList();
                    _logger.Info($"[ProviderSearch] OpenLibrary: {response.OpenLibrary.Count} results");
                }
            }

            if (providerList.Contains("googlebooks"))
            {
                var googleBooksResults = _googleBooksSearchClient?.Search(term);
                if (googleBooksResults != null && googleBooksResults.Count > 0)
                {
                    response.GoogleBooks = MapToResource(googleBooksResults, "googlebooks").ToList();
                    _logger.Info($"[ProviderSearch] GoogleBooks: {response.GoogleBooks.Count} results");
                }
            }

            if (providerList.Contains("comicvine"))
            {
                var comicVineResults = _comicVineSearchClient?.Search(term);
                if (comicVineResults != null && comicVineResults.Count > 0)
                {
                    response.ComicVine = MapToResource(comicVineResults, "comicvine").ToList();
                    _logger.Info($"[ProviderSearch] ComicVine: {response.ComicVine.Count} results");
                }
            }

            var totalResults = response.Hardcover.Count + response.OpenLibrary.Count + response.GoogleBooks.Count + response.ComicVine.Count;
            _logger.Info($"[ProviderSearch] Multi-provider search complete: {totalResults} total results");

            return response;
        }

        [HttpGet("reconcile")]
        public object SearchReconciled([FromQuery] string term, [FromQuery] string providers = "hardcover,openlibrary,googlebooks,comicvine")
        {
            _logger.Info($"[ProviderSearch] Reconciled search requested for: '{term}' (providers: {providers})");

            if (string.IsNullOrWhiteSpace(term))
            {
                return new ReconciledSearchResource
                {
                    Query = term,
                    Books = new List<ReconciledBookResource>(),
                    Authors = new List<ReconciledAuthorResource>()
                };
            }

            var providerList = providers.Split(',').Select(p => p.Trim().ToLower()).ToList();

            // Collect results from each provider
            var hardcoverBooks = new List<Book>();
            var openLibraryBooks = new List<Book>();
            var googleBooksBooks = new List<Book>();
            var comicVineBooks = new List<Book>();
            var hardcoverAuthors = new List<NzbDrone.Core.Books.Author>();
            var openLibraryAuthors = new List<NzbDrone.Core.Books.Author>();
            var googleBooksAuthors = new List<NzbDrone.Core.Books.Author>();
            var comicVineAuthors = new List<NzbDrone.Core.Books.Author>();

            if (providerList.Contains("hardcover"))
            {
                var results = _hardcoverSearchClient?.Search(term);
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        if (result is NzbDrone.Core.MetadataSource.Hardcover.HardcoverAuthorResult hardcoverAuthor)
                        {
                            var author = ConvertHardcoverAuthorToDomain(hardcoverAuthor);
                            if (author != null)
                            {
                                hardcoverAuthors.Add(author);
                            }
                        }
                        else if (result is NzbDrone.Core.MetadataSource.Hardcover.HardcoverBookResult hardcoverBook)
                        {
                            var book = ConvertHardcoverBookToDomain(hardcoverBook);
                            if (book != null)
                            {
                                hardcoverBooks.Add(book);
                            }
                        }
                    }

                    _logger.Info($"[Reconcile] Hardcover: {hardcoverBooks.Count} books, {hardcoverAuthors.Count} authors");
                }
            }

            if (providerList.Contains("openlibrary"))
            {
                var results = _openLibrarySearchClient?.Search(term);
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        if (result is NzbDrone.Core.MetadataSource.OpenLibrary.OpenLibrarySearchDoc openLibraryDoc)
                        {
                            var book = ConvertOpenLibraryDocToDomain(openLibraryDoc);
                            if (book != null)
                            {
                                openLibraryBooks.Add(book);
                            }
                        }
                    }

                    _logger.Info($"[Reconcile] OpenLibrary: {openLibraryBooks.Count} books, {openLibraryAuthors.Count} authors");
                }
            }

            if (providerList.Contains("googlebooks"))
            {
                var results = _googleBooksSearchClient?.Search(term);
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        if (result is NzbDrone.Core.MetadataSource.GoogleBooks.GoogleBookItem googleBookItem)
                        {
                            var book = ConvertGoogleBookItemToDomain(googleBookItem);
                            if (book != null)
                            {
                                googleBooksBooks.Add(book);
                            }
                        }
                    }

                    _logger.Info($"[Reconcile] GoogleBooks: {googleBooksBooks.Count} books, {googleBooksAuthors.Count} authors");
                }
            }

            if (providerList.Contains("comicvine"))
            {
                var results = _comicVineSearchClient?.Search(term);
                if (results != null)
                {
                    foreach (var result in results)
                    {
                        if (result is NzbDrone.Core.MetadataSource.ComicVine.ComicVineIssueResult comicVineIssue)
                        {
                            var book = ConvertComicVineIssueToDomain(comicVineIssue);
                            if (book != null)
                            {
                                comicVineBooks.Add(book);
                            }
                        }
                    }

                    _logger.Info($"[Reconcile] ComicVine: {comicVineBooks.Count} books, {comicVineAuthors.Count} authors");
                }
            }

            // Reconcile books and authors
            var reconciledBooks = _reconciliationService.ReconcileBooks(hardcoverBooks, openLibraryBooks, googleBooksBooks, comicVineBooks);
            var reconciledAuthors = _reconciliationService.ReconcileAuthors(hardcoverAuthors, openLibraryAuthors, googleBooksAuthors, comicVineAuthors);

            _logger.Info($"[Reconcile] Reconciliation complete: {reconciledBooks.Count} unique books, {reconciledAuthors.Count} unique authors");

            // Convert to API resources
            var response = new ReconciledSearchResource
            {
                Query = term,
                Books = reconciledBooks.Select(rb => new ReconciledBookResource
                {
                    Book = rb.MergedBook.ToResource(),
                    HardcoverId = rb.HardcoverId,
                    OpenLibraryId = rb.OpenLibraryId,
                    GoogleBooksId = rb.GoogleBooksId,
                    ComicVineId = rb.ComicVineId,
                    GoodreadsId = rb.GoodreadsId,
                    MatchedProviders = rb.MatchedProviders,
                    ConfidenceScore = rb.ConfidenceScore,
                    PrimarySource = rb.PrimarySource
                }).ToList(),
                Authors = reconciledAuthors.Select(ra => new ReconciledAuthorResource
                {
                    Author = ra.MergedAuthor.ToResource(),
                    HardcoverId = ra.HardcoverId,
                    OpenLibraryId = ra.OpenLibraryId,
                    GoogleBooksId = ra.GoogleBooksId,
                    ComicVineId = ra.ComicVineId,
                    GoodreadsId = ra.GoodreadsId,
                    MatchedProviders = ra.MatchedProviders,
                    ConfidenceScore = ra.ConfidenceScore,
                    PrimarySource = ra.PrimarySource
                }).ToList()
            };

            return response;
        }

        private IEnumerable<SearchResource> MapToResource(IEnumerable<object> results, string provider)
        {
            var id = 1;
            foreach (var result in results)
            {
                var resource = new SearchResource();
                resource.Id = id++;

                if (result is NzbDrone.Core.Books.Author author)
                {
                    resource.Author = author.ToResource();
                    resource.ForeignId = author.ForeignAuthorId;

                    _coverMapper.ConvertToLocalUrls(resource.Author.Id, MediaCoverEntity.Author, resource.Author.Images);

                    var poster = resource.Author.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Poster);

                    if (poster != null)
                    {
                        resource.Author.RemotePoster = poster.RemoteUrl;
                    }

                    resource.Author.Folder = _fileNameBuilder.GetAuthorFolder(author);
                }
                else if (result is NzbDrone.Core.Books.Book book)
                {
                    resource.Book = book.ToResource();

                    // Handle editions safely
                    if (book.Editions != null && book.Editions.Value != null && book.Editions.Value.Any())
                    {
                        var monitoredEdition = book.Editions.Value.FirstOrDefault(x => x.Monitored);
                        if (monitoredEdition != null && !string.IsNullOrWhiteSpace(monitoredEdition.Overview))
                        {
                            resource.Book.Overview = monitoredEdition.Overview;
                        }

                        resource.Book.Editions = book.Editions.Value.ToResource();
                    }

                    // Handle author safely
                    if (book.Author != null && book.Author.Value != null)
                    {
                        resource.Book.Author = book.Author.Value.ToResource();
                        resource.Book.Author.Folder = _fileNameBuilder.GetAuthorFolder(book.Author);
                    }

                    resource.ForeignId = book.ForeignBookId;

                    _coverMapper.ConvertToLocalUrls(resource.Book.Id, MediaCoverEntity.Book, resource.Book.Images);

                    var cover = resource.Book.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Cover);

                    if (cover != null)
                    {
                        resource.Book.RemoteCover = cover.RemoteUrl;
                    }
                }
                else if (result is NzbDrone.Core.MetadataSource.GoogleBooks.GoogleBookItem googleBookItem)
                {
                    // Handle raw Google Books API items
                    var bookResource = new Readarr.Api.V1.Books.BookResource
                    {
                        Title = googleBookItem.VolumeInfo?.Title ?? "Unknown",
                        TitleSlug = GenerateTitleSlug(googleBookItem.VolumeInfo?.Title),
                        ForeignBookId = $"googlebooks:{googleBookItem.Id}",
                        Overview = googleBookItem.VolumeInfo?.Description,
                        PageCount = googleBookItem.VolumeInfo?.PageCount ?? 0,
                        Monitored = false,
                        AnyEditionOk = false,
                        Ratings = new Ratings
                        {
                            Value = (decimal)(googleBookItem.VolumeInfo?.AverageRating ?? 0),
                            Votes = googleBookItem.VolumeInfo?.RatingsCount ?? 0
                        },
                        Images = new List<MediaCover>(),
                        Grabbed = false
                    };

                    // Add author info (always set to avoid frontend crashes)
                    var authorName = (googleBookItem.VolumeInfo?.Authors != null && googleBookItem.VolumeInfo.Authors.Any())
                        ? string.Join(", ", googleBookItem.VolumeInfo.Authors)
                        : "Unknown";

                    bookResource.Author = new Readarr.Api.V1.Author.AuthorResource
                    {
                        AuthorName = authorName,
                        TitleSlug = GenerateTitleSlug(authorName),
                        Status = AuthorStatusType.Continuing,
                        Monitored = false,
                        MonitorNewItems = NewItemMonitorTypes.All,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>(),
                        Ratings = new Ratings { Votes = 0, Value = 0 }
                    };

                    // Add edition with Google Books link
                    var edition = new Readarr.Api.V1.Books.EditionResource
                    {
                        Title = googleBookItem.VolumeInfo?.Title,
                        ForeignEditionId = $"googlebooks:{googleBookItem.Id}",
                        Monitored = false,
                        ManualAdd = false,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>
                        {
                            new Links
                            {
                                Url = $"https://books.google.com/books?id={googleBookItem.Id}",
                                Name = "Google Books"
                            }
                        }
                    };

                    bookResource.Editions = new List<Readarr.Api.V1.Books.EditionResource> { edition };

                    // Add cover image if available
                    if (!string.IsNullOrWhiteSpace(googleBookItem.VolumeInfo?.ImageLinks?.Thumbnail))
                    {
                        bookResource.RemoteCover = googleBookItem.VolumeInfo.ImageLinks.Thumbnail;
                        bookResource.Images.Add(new MediaCover
                        {
                            CoverType = MediaCoverTypes.Cover,
                            Url = googleBookItem.VolumeInfo.ImageLinks.Thumbnail,
                            RemoteUrl = googleBookItem.VolumeInfo.ImageLinks.Thumbnail
                        });
                    }

                    resource.Book = bookResource;
                    resource.ForeignId = $"googlebooks:{googleBookItem.Id}";
                }
                else if (result is NzbDrone.Core.MetadataSource.OpenLibrary.OpenLibrarySearchDoc openLibraryDoc)
                {
                    // Handle raw Open Library API documents
                    var bookResource = new Readarr.Api.V1.Books.BookResource
                    {
                        Title = openLibraryDoc.Title ?? "Unknown",
                        TitleSlug = GenerateTitleSlug(openLibraryDoc.Title),
                        ForeignBookId = $"openlibrary:{openLibraryDoc.Key}",
                        Overview = openLibraryDoc.Subject != null && openLibraryDoc.Subject.Any()
                            ? string.Join(", ", openLibraryDoc.Subject.Take(5))
                            : null,
                        PageCount = openLibraryDoc.NumberOfPagesMedian ?? 0,
                        Monitored = false,
                        AnyEditionOk = false,
                        Ratings = new Ratings
                        {
                            Value = (decimal)(openLibraryDoc.RatingsAverage ?? 0),
                            Votes = openLibraryDoc.RatingsCount ?? 0
                        },
                        Images = new List<MediaCover>(),
                        Grabbed = false
                    };

                    // Add author info (always set to avoid frontend crashes)
                    var authorName = (openLibraryDoc.AuthorName != null && openLibraryDoc.AuthorName.Any())
                        ? string.Join(", ", openLibraryDoc.AuthorName)
                        : "Unknown";

                    bookResource.Author = new Readarr.Api.V1.Author.AuthorResource
                    {
                        AuthorName = authorName,
                        TitleSlug = GenerateTitleSlug(authorName),
                        Status = AuthorStatusType.Continuing,
                        Monitored = false,
                        MonitorNewItems = NewItemMonitorTypes.All,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>(),
                        Ratings = new Ratings { Votes = 0, Value = 0 }
                    };

                    // Add edition with Open Library link
                    var edition = new Readarr.Api.V1.Books.EditionResource
                    {
                        Title = openLibraryDoc.Title,
                        ForeignEditionId = $"openlibrary:{openLibraryDoc.Key}",
                        Monitored = false,
                        ManualAdd = false,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>
                        {
                            new Links
                            {
                                Url = $"https://openlibrary.org{openLibraryDoc.Key}",
                                Name = "Open Library"
                            }
                        }
                    };

                    bookResource.Editions = new List<Readarr.Api.V1.Books.EditionResource> { edition };

                    // Add cover image if available
                    if (openLibraryDoc.CoverId != null && openLibraryDoc.CoverId > 0)
                    {
                        var coverUrl = $"https://covers.openlibrary.org/b/id/{openLibraryDoc.CoverId}-L.jpg";
                        bookResource.RemoteCover = coverUrl;
                        bookResource.Images.Add(new MediaCover
                        {
                            CoverType = MediaCoverTypes.Cover,
                            Url = coverUrl,
                            RemoteUrl = coverUrl
                        });
                    }

                    resource.Book = bookResource;
                    resource.ForeignId = $"openlibrary:{openLibraryDoc.Key}";
                }
                else if (result is NzbDrone.Core.MetadataSource.Hardcover.HardcoverBookResult hardcoverBook)
                {
                    // Handle raw Hardcover book results
                    var bookResource = new Readarr.Api.V1.Books.BookResource
                    {
                        Title = hardcoverBook.Title ?? "Unknown",
                        TitleSlug = GenerateTitleSlug(hardcoverBook.Title),
                        ForeignBookId = $"hardcover:{hardcoverBook.Id}",
                        Overview = hardcoverBook.Description,
                        PageCount = hardcoverBook.Pages,
                        Monitored = false,
                        AnyEditionOk = false,
                        Ratings = new Ratings
                        {
                            Value = (decimal)hardcoverBook.Rating,
                            Votes = 0
                        },
                        Images = new List<MediaCover>(),
                        Grabbed = false
                    };

                    // Add author info (always set to avoid frontend crashes)
                    var authorName = (hardcoverBook.AuthorNames != null && hardcoverBook.AuthorNames.Any())
                        ? string.Join(", ", hardcoverBook.AuthorNames)
                        : "Unknown";

                    bookResource.Author = new Readarr.Api.V1.Author.AuthorResource
                    {
                        AuthorName = authorName,
                        TitleSlug = GenerateTitleSlug(authorName),
                        Status = AuthorStatusType.Continuing,
                        Monitored = false,
                        MonitorNewItems = NewItemMonitorTypes.All,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>(),
                        Ratings = new Ratings { Votes = 0, Value = 0 }
                    };

                    // Add edition with Hardcover link
                    var edition = new Readarr.Api.V1.Books.EditionResource
                    {
                        Title = hardcoverBook.Title,
                        ForeignEditionId = $"hardcover:{hardcoverBook.Id}",
                        Monitored = false,
                        ManualAdd = false,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>
                        {
                            new Links
                            {
                                Url = $"https://hardcover.app/books/{hardcoverBook.Id}",
                                Name = "Hardcover"
                            }
                        }
                    };

                    bookResource.Editions = new List<Readarr.Api.V1.Books.EditionResource> { edition };

                    // Add cover image if available
                    if (!string.IsNullOrWhiteSpace(hardcoverBook.ImageUrl))
                    {
                        bookResource.RemoteCover = hardcoverBook.ImageUrl;
                        bookResource.Images.Add(new MediaCover
                        {
                            CoverType = MediaCoverTypes.Cover,
                            Url = hardcoverBook.ImageUrl,
                            RemoteUrl = hardcoverBook.ImageUrl
                        });
                    }

                    resource.Book = bookResource;
                    resource.ForeignId = $"hardcover:{hardcoverBook.Id}";
                }
                else if (result is NzbDrone.Core.MetadataSource.Hardcover.HardcoverAuthorResult hardcoverAuthor)
                {
                    // Handle raw Hardcover author results
                    var authorResource = new Readarr.Api.V1.Author.AuthorResource
                    {
                        AuthorName = hardcoverAuthor.Name ?? "Unknown",
                        TitleSlug = GenerateTitleSlug(hardcoverAuthor.Slug ?? hardcoverAuthor.Name),
                        ForeignAuthorId = $"hardcover:{hardcoverAuthor.Id}",
                        Overview = hardcoverAuthor.Bio,
                        Status = AuthorStatusType.Continuing,
                        Monitored = false,
                        MonitorNewItems = NewItemMonitorTypes.All,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>
                        {
                            new Links
                            {
                                Url = $"https://hardcover.app/authors/{hardcoverAuthor.Slug ?? hardcoverAuthor.Id.ToString()}",
                                Name = "Hardcover"
                            }
                        },
                        Ratings = new Ratings { Votes = 0, Value = 0 }
                    };

                    // Add author image if available
                    if (!string.IsNullOrWhiteSpace(hardcoverAuthor.ImageUrl))
                    {
                        authorResource.RemotePoster = hardcoverAuthor.ImageUrl;
                        authorResource.Images.Add(new MediaCover
                        {
                            CoverType = MediaCoverTypes.Poster,
                            Url = hardcoverAuthor.ImageUrl,
                            RemoteUrl = hardcoverAuthor.ImageUrl
                        });
                    }

                    resource.Author = authorResource;
                    resource.ForeignId = $"hardcover:{hardcoverAuthor.Id}";
                }
                else if (result is NzbDrone.Core.MetadataSource.Hardcover.HardcoverSeriesResult hardcoverSeries)
                {
                    // Handle raw Hardcover series results
                    // Note: Series don't map directly to Book or Author, so we'll treat them as books for now
                    var bookResource = new Readarr.Api.V1.Books.BookResource
                    {
                        Title = hardcoverSeries.Name ?? "Unknown",
                        TitleSlug = GenerateTitleSlug(hardcoverSeries.Slug ?? hardcoverSeries.Name),
                        ForeignBookId = $"hardcover:series:{hardcoverSeries.Id}",
                        SeriesTitle = hardcoverSeries.Name,
                        Overview = hardcoverSeries.Description,
                        PageCount = 0,
                        Monitored = false,
                        AnyEditionOk = false,
                        Ratings = new Ratings { Votes = 0, Value = 0 },
                        Images = new List<MediaCover>(),
                        Grabbed = false
                    };

                    // Add author info (always set to avoid frontend crashes)
                    var seriesAuthorName = !string.IsNullOrWhiteSpace(hardcoverSeries.AuthorName)
                        ? hardcoverSeries.AuthorName
                        : "Unknown";

                    bookResource.Author = new Readarr.Api.V1.Author.AuthorResource
                    {
                        AuthorName = seriesAuthorName,
                        TitleSlug = GenerateTitleSlug(seriesAuthorName),
                        Status = AuthorStatusType.Continuing,
                        Monitored = false,
                        MonitorNewItems = NewItemMonitorTypes.All,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>(),
                        Ratings = new Ratings { Votes = 0, Value = 0 }
                    };

                    resource.Book = bookResource;
                    resource.ForeignId = $"hardcover:series:{hardcoverSeries.Id}";
                }
                else if (result is NzbDrone.Core.MetadataSource.ComicVine.ComicVineIssueResult comicVineIssue)
                {
                    // Handle raw ComicVine issue results - map to books (comics)
                    var volumeName = comicVineIssue.Volume?.Name ?? "Unknown Volume";
                    var issueNumber = !string.IsNullOrWhiteSpace(comicVineIssue.IssueNumber) ? $" #{comicVineIssue.IssueNumber}" : "";
                    var issueName = !string.IsNullOrWhiteSpace(comicVineIssue.Name) ? $": {comicVineIssue.Name}" : "";
                    var title = $"{volumeName}{issueNumber}{issueName}";

                    var bookResource = new Readarr.Api.V1.Books.BookResource
                    {
                        Title = title,
                        TitleSlug = GenerateTitleSlug(title),
                        ForeignBookId = $"comicvine:{comicVineIssue.Id}",
                        Overview = comicVineIssue.Description,
                        PageCount = 0,
                        Monitored = false,
                        AnyEditionOk = false,
                        Ratings = new Ratings { Votes = 0, Value = 0 },
                        Images = new List<MediaCover>(),
                        Grabbed = false
                    };

                    // For comics, use the series/volume name as the "author" since individual writers change frequently
                    // and ComicVine search API doesn't return person_credits without fetching full issue details
                    var authorName = volumeName;

                    bookResource.Author = new Readarr.Api.V1.Author.AuthorResource
                    {
                        AuthorName = authorName,
                        TitleSlug = GenerateTitleSlug(authorName),
                        Status = AuthorStatusType.Continuing,
                        Monitored = false,
                        MonitorNewItems = NewItemMonitorTypes.All,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>(),
                        Ratings = new Ratings { Votes = 0, Value = 0 }
                    };

                    // Add edition with ComicVine link
                    var edition = new Readarr.Api.V1.Books.EditionResource
                    {
                        Title = title,
                        ForeignEditionId = $"comicvine:{comicVineIssue.Id}",
                        Monitored = false,
                        ManualAdd = false,
                        Images = new List<MediaCover>(),
                        Links = new List<Links>
                        {
                            new Links
                            {
                                Url = $"https://comicvine.gamespot.com/issue/4000-{comicVineIssue.Id}",
                                Name = "ComicVine"
                            }
                        }
                    };

                    bookResource.Editions = new List<Readarr.Api.V1.Books.EditionResource> { edition };

                    // Add cover image if available (use medium or screen size)
                    var coverUrl = comicVineIssue.Image?.MediumUrl ?? comicVineIssue.Image?.ScreenUrl;
                    if (!string.IsNullOrWhiteSpace(coverUrl))
                    {
                        bookResource.RemoteCover = coverUrl;
                        bookResource.Images.Add(new MediaCover
                        {
                            CoverType = MediaCoverTypes.Cover,
                            Url = coverUrl,
                            RemoteUrl = coverUrl
                        });
                    }

                    // Add release date if available
                    if (!string.IsNullOrWhiteSpace(comicVineIssue.StoreDate))
                    {
                        if (global::System.DateTime.TryParse(comicVineIssue.StoreDate, out var storeDate))
                        {
                            bookResource.ReleaseDate = storeDate;
                        }
                    }

                    resource.Book = bookResource;
                    resource.ForeignId = $"comicvine:{comicVineIssue.Id}";
                }

                yield return resource;
            }
        }

        // Conversion methods to transform raw API objects into domain objects for reconciliation
        private Book ConvertGoogleBookItemToDomain(NzbDrone.Core.MetadataSource.GoogleBooks.GoogleBookItem googleBookItem)
        {
            if (googleBookItem == null || string.IsNullOrWhiteSpace(googleBookItem.Id))
            {
                return null;
            }

            // Create edition with detailed metadata
            var edition = new Edition
            {
                Title = googleBookItem.VolumeInfo?.Title ?? "Unknown",
                Overview = googleBookItem.VolumeInfo?.Description,
                PageCount = googleBookItem.VolumeInfo?.PageCount ?? 0,
                GoogleBooksEditionId = googleBookItem.Id,
                Monitored = true,  // Mark as monitored so ToResource() will use this edition
                Ratings = new Ratings
                {
                    Value = (decimal)(googleBookItem.VolumeInfo?.AverageRating ?? 0),
                    Votes = googleBookItem.VolumeInfo?.RatingsCount ?? 0
                }
            };

            // Add cover image to edition
            if (!string.IsNullOrWhiteSpace(googleBookItem.VolumeInfo?.ImageLinks?.Thumbnail))
            {
                edition.Images.Add(new MediaCover
                {
                    CoverType = MediaCoverTypes.Cover,
                    Url = googleBookItem.VolumeInfo.ImageLinks.Thumbnail,
                    RemoteUrl = googleBookItem.VolumeInfo.ImageLinks.Thumbnail
                });
            }

            // Create book with edition
            var book = new Book
            {
                Title = googleBookItem.VolumeInfo?.Title ?? "Unknown",
                GoogleBooksId = googleBookItem.Id,
                ForeignBookId = googleBookItem.Id,  // Use GoogleBooks ID as foreign ID
                TitleSlug = GenerateTitleSlug(googleBookItem.VolumeInfo?.Title),
                Ratings = edition.Ratings,
                Editions = new List<Edition> { edition }
            };

            return book;
        }

        private Book ConvertOpenLibraryDocToDomain(NzbDrone.Core.MetadataSource.OpenLibrary.OpenLibrarySearchDoc openLibraryDoc)
        {
            if (openLibraryDoc == null || string.IsNullOrWhiteSpace(openLibraryDoc.Key))
            {
                return null;
            }

            // Create edition with detailed metadata
            var edition = new Edition
            {
                Title = openLibraryDoc.Title ?? "Unknown",
                Overview = openLibraryDoc.Subject != null && openLibraryDoc.Subject.Any()
                    ? string.Join(", ", openLibraryDoc.Subject.Take(5))
                    : null,
                PageCount = openLibraryDoc.NumberOfPagesMedian ?? 0,
                OpenLibraryEditionId = openLibraryDoc.Key,
                Monitored = true,  // Mark as monitored so ToResource() will use this edition
                Ratings = new Ratings
                {
                    Value = (decimal)(openLibraryDoc.RatingsAverage ?? 0),
                    Votes = openLibraryDoc.RatingsCount ?? 0
                }
            };

            // Add cover image to edition
            if (openLibraryDoc.CoverId != null && openLibraryDoc.CoverId > 0)
            {
                var coverUrl = $"https://covers.openlibrary.org/b/id/{openLibraryDoc.CoverId}-L.jpg";
                edition.Images.Add(new MediaCover
                {
                    CoverType = MediaCoverTypes.Cover,
                    Url = coverUrl,
                    RemoteUrl = coverUrl
                });
            }

            // Create book with edition
            var book = new Book
            {
                Title = openLibraryDoc.Title ?? "Unknown",
                OpenLibraryWorkId = openLibraryDoc.Key,
                ForeignBookId = openLibraryDoc.Key,  // Use OpenLibrary ID as foreign ID
                TitleSlug = GenerateTitleSlug(openLibraryDoc.Title),
                Ratings = edition.Ratings,
                Editions = new List<Edition> { edition }
            };

            return book;
        }

        private Book ConvertHardcoverBookToDomain(NzbDrone.Core.MetadataSource.Hardcover.HardcoverBookResult hardcoverBook)
        {
            if (hardcoverBook == null || string.IsNullOrWhiteSpace(hardcoverBook.Id))
            {
                return null;
            }

            // Create edition with detailed metadata
            var edition = new Edition
            {
                Title = hardcoverBook.Title ?? "Unknown",
                Overview = hardcoverBook.Description,
                PageCount = hardcoverBook.Pages,
                HardcoverEditionId = hardcoverBook.Id,
                Monitored = true,  // Mark as monitored so ToResource() will use this edition
                Ratings = new Ratings
                {
                    Value = (decimal)hardcoverBook.Rating,
                    Votes = 0  // HardcoverBookResult doesn't have RatingsCount
                }
            };

            // Add cover image to edition
            if (!string.IsNullOrWhiteSpace(hardcoverBook.ImageUrl))
            {
                edition.Images.Add(new MediaCover
                {
                    CoverType = MediaCoverTypes.Cover,
                    Url = hardcoverBook.ImageUrl,
                    RemoteUrl = hardcoverBook.ImageUrl
                });
            }

            // Create book with edition
            var book = new Book
            {
                Title = hardcoverBook.Title ?? "Unknown",
                HardcoverBookId = hardcoverBook.Id,
                ForeignBookId = hardcoverBook.Id,  // Use Hardcover ID as foreign ID
                TitleSlug = GenerateTitleSlug(hardcoverBook.Title),
                Ratings = edition.Ratings,
                Editions = new List<Edition> { edition }
            };

            return book;
        }

        private NzbDrone.Core.Books.Author ConvertHardcoverAuthorToDomain(NzbDrone.Core.MetadataSource.Hardcover.HardcoverAuthorResult hardcoverAuthor)
        {
            if (hardcoverAuthor == null || string.IsNullOrWhiteSpace(hardcoverAuthor.Id))
            {
                return null;
            }

            var author = new NzbDrone.Core.Books.Author();

            // Initialize metadata with author information
            author.Metadata.Value.Name = hardcoverAuthor.Name ?? "Unknown";
            author.Metadata.Value.HardcoverAuthorId = hardcoverAuthor.Id;
            author.Metadata.Value.Overview = hardcoverAuthor.Bio;

            // Add author image to metadata
            if (!string.IsNullOrWhiteSpace(hardcoverAuthor.ImageUrl))
            {
                author.Metadata.Value.Images.Add(new MediaCover
                {
                    CoverType = MediaCoverTypes.Poster,
                    Url = hardcoverAuthor.ImageUrl,
                    RemoteUrl = hardcoverAuthor.ImageUrl
                });
            }

            return author;
        }

        private Book ConvertComicVineIssueToDomain(NzbDrone.Core.MetadataSource.ComicVine.ComicVineIssueResult comicVineIssue)
        {
            if (comicVineIssue == null || comicVineIssue.Id == 0)
            {
                return null;
            }

            // Build title from volume name and issue number
            var title = comicVineIssue.Volume?.Name ?? "Unknown";
            if (!string.IsNullOrWhiteSpace(comicVineIssue.IssueNumber))
            {
                title += $" #{comicVineIssue.IssueNumber}";
            }

            if (!string.IsNullOrWhiteSpace(comicVineIssue.Name))
            {
                title += $" - {comicVineIssue.Name}";
            }

            // Create edition with detailed metadata
            var edition = new Edition
            {
                Title = title,
                Overview = comicVineIssue.Description,
                Monitored = true,  // Mark as monitored so ToResource() will use this edition
                Ratings = new Ratings
                {
                    Value = 0,
                    Votes = 0
                }
            };

            // Add cover image to edition (prefer super_url for highest quality)
            if (comicVineIssue.Image != null)
            {
                var imageUrl = comicVineIssue.Image.SuperUrl
                    ?? comicVineIssue.Image.OriginalUrl
                    ?? comicVineIssue.Image.ScreenLargeUrl
                    ?? comicVineIssue.Image.MediumUrl;

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    edition.Images.Add(new MediaCover
                    {
                        CoverType = MediaCoverTypes.Cover,
                        Url = imageUrl,
                        RemoteUrl = imageUrl
                    });
                }
            }

            // Create book with edition
            var book = new Book
            {
                Title = title,
                ComicVineIssueId = comicVineIssue.Id.ToString(),
                ForeignBookId = comicVineIssue.Id.ToString(),  // Use ComicVine ID as foreign ID
                TitleSlug = GenerateTitleSlug(title),
                Ratings = edition.Ratings,
                Editions = new List<Edition> { edition }
            };

            // Parse release date (prefer cover_date over store_date)
            var releaseDate = comicVineIssue.CoverDate ?? comicVineIssue.StoreDate;
            if (!string.IsNullOrWhiteSpace(releaseDate) && DateTime.TryParse(releaseDate, out var parsedDate))
            {
                book.ReleaseDate = parsedDate;
            }

            return book;
        }

        private string GenerateTitleSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "unknown";
            }

            return title.ToLower()
                .Replace(" ", "-")
                .Replace("'", "")
                .Replace("#", "")
                .Replace(":", "");
        }
    }
}
