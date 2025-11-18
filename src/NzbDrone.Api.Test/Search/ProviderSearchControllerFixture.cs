using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.GoogleBooks;
using NzbDrone.Core.MetadataSource.Hardcover;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.Organizer;
using NzbDrone.Test.Common;
using Readarr.Api.V1.Search;

namespace NzbDrone.Api.Test.Search
{
    [TestFixture]
    public class ProviderSearchControllerFixture : TestBase
    {
        private ProviderSearchController _controller;
        private Mock<IHardcoverSearchClient> _hardcoverClientMock;
        private Mock<IOpenLibrarySearchClient> _openLibraryClientMock;
        private Mock<IGoogleBooksSearchClient> _googleBooksClientMock;
        private Mock<IBookReconciliationService> _reconciliationServiceMock;
        private Mock<IBuildFileNames> _fileNameBuilderMock;
        private Mock<IMapCoversToLocal> _coverMapperMock;
        private Mock<Logger> _loggerMock;

        [SetUp]
        public void Setup()
        {
            _hardcoverClientMock = new Mock<IHardcoverSearchClient>();
            _openLibraryClientMock = new Mock<IOpenLibrarySearchClient>();
            _googleBooksClientMock = new Mock<IGoogleBooksSearchClient>();
            _reconciliationServiceMock = new Mock<IBookReconciliationService>();
            _fileNameBuilderMock = new Mock<IBuildFileNames>();
            _coverMapperMock = new Mock<IMapCoversToLocal>();
            _loggerMock = new Mock<Logger>();

            _controller = new ProviderSearchController(
                _hardcoverClientMock.Object,
                _openLibraryClientMock.Object,
                _googleBooksClientMock.Object,
                _reconciliationServiceMock.Object,
                _fileNameBuilderMock.Object,
                _coverMapperMock.Object,
                _loggerMock.Object
            );
        }

        [Test]
        public void search_hardcover_returns_results()
        {
            // Arrange
            var mockResults = new List<object>
            {
                new HardcoverBookResult
                {
                    Id = "hc123",
                    Title = "Foundation",
                    Description = "Test description",
                    Pages = 255,
                    Rating = 4.5,
                    ImageUrl = "https://example.com/image.jpg"
                }
            };

            _hardcoverClientMock
                .Setup(x => x.Search("Foundation"))
                .Returns(mockResults);

            // Act
            var result = _controller.SearchHardcover("Foundation");

            // Assert
            result.Should().NotBeNull();
            var searchResults = result as List<SearchResource>;
            searchResults.Should().NotBeNull();
            searchResults.Should().NotBeEmpty();

            _hardcoverClientMock.Verify(x => x.Search("Foundation"), Times.Once);
        }

        [Test]
        public void search_hardcover_empty_term_returns_empty()
        {
            // Act
            var result = _controller.SearchHardcover("");

            // Assert
            result.Should().NotBeNull();
            var searchResults = result as List<SearchResource>;
            searchResults.Should().NotBeNull();
            searchResults.Should().BeEmpty();

            _hardcoverClientMock.Verify(x => x.Search(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void search_hardcover_null_results_returns_empty()
        {
            // Arrange
            _hardcoverClientMock
                .Setup(x => x.Search("NonExistent"))
                .Returns((List<object>)null);

            // Act
            var result = _controller.SearchHardcover("NonExistent");

            // Assert
            result.Should().NotBeNull();
            var searchResults = result as List<SearchResource>;
            searchResults.Should().NotBeNull();
            searchResults.Should().BeEmpty();
        }

        [Test]
        public void search_openlibrary_returns_results()
        {
            // Arrange
            var mockResults = new List<object>
            {
                new OpenLibrarySearchDoc
                {
                    Key = "/works/OL123",
                    Title = "1984",
                    AuthorName = new List<string> { "George Orwell" },
                    CoverId = 12345,
                    NumberOfPagesMedian = 328,
                    RatingsAverage = 4.3,
                    RatingsCount = 1000
                }
            };

            _openLibraryClientMock
                .Setup(x => x.Search("1984"))
                .Returns(mockResults);

            // Act
            var result = _controller.SearchOpenLibrary("1984");

            // Assert
            result.Should().NotBeNull();
            var searchResults = result as List<SearchResource>;
            searchResults.Should().NotBeNull();
            searchResults.Should().NotBeEmpty();

            _openLibraryClientMock.Verify(x => x.Search("1984"), Times.Once);
        }

        [Test]
        public void search_googlebooks_returns_results()
        {
            // Arrange
            var mockResults = new List<object>
            {
                new GoogleBookItem
                {
                    Id = "gb123",
                    VolumeInfo = new GoogleBookVolumeInfo
                    {
                        Title = "Dune",
                        Authors = new List<string> { "Frank Herbert" },
                        Description = "A classic science fiction novel",
                        PageCount = 688,
                        AverageRating = 4.6,
                        RatingsCount = 5000,
                        ImageLinks = new GoogleBookImageLinks
                        {
                            Thumbnail = "https://example.com/thumb.jpg"
                        }
                    }
                }
            };

            _googleBooksClientMock
                .Setup(x => x.Search("Dune"))
                .Returns(mockResults);

            // Act
            var result = _controller.SearchGoogleBooks("Dune");

            // Assert
            result.Should().NotBeNull();
            var searchResults = result as List<SearchResource>;
            searchResults.Should().NotBeNull();
            searchResults.Should().NotBeEmpty();

            _googleBooksClientMock.Verify(x => x.Search("Dune"), Times.Once);
        }

        [Test]
        public void search_all_calls_multiple_providers()
        {
            // Arrange
            var hardcoverResults = new List<object> { new HardcoverBookResult { Id = "hc1", Title = "Test" } };
            var openLibraryResults = new List<object> { new OpenLibrarySearchDoc { Key = "/works/OL1", Title = "Test" } };
            var googleBooksResults = new List<object> { new GoogleBookItem { Id = "gb1", VolumeInfo = new GoogleBookVolumeInfo { Title = "Test" } } };

            _hardcoverClientMock.Setup(x => x.Search("Test")).Returns(hardcoverResults);
            _openLibraryClientMock.Setup(x => x.Search("Test")).Returns(openLibraryResults);
            _googleBooksClientMock.Setup(x => x.Search("Test")).Returns(googleBooksResults);

            // Act
            var result = _controller.SearchAll("Test", "hardcover,openlibrary,googlebooks");

            // Assert
            result.Should().NotBeNull();
            var multiResult = result as MultiProviderSearchResource;
            multiResult.Should().NotBeNull();
            multiResult.Query.Should().Be("Test");
            multiResult.Hardcover.Should().NotBeEmpty();
            multiResult.OpenLibrary.Should().NotBeEmpty();
            multiResult.GoogleBooks.Should().NotBeEmpty();

            _hardcoverClientMock.Verify(x => x.Search("Test"), Times.Once);
            _openLibraryClientMock.Verify(x => x.Search("Test"), Times.Once);
            _googleBooksClientMock.Verify(x => x.Search("Test"), Times.Once);
        }

        [Test]
        public void search_all_respects_provider_selection()
        {
            // Arrange
            var hardcoverResults = new List<object> { new HardcoverBookResult { Id = "hc1", Title = "Test" } };

            _hardcoverClientMock.Setup(x => x.Search("Test")).Returns(hardcoverResults);

            // Act - only request hardcover
            var result = _controller.SearchAll("Test", "hardcover");

            // Assert
            result.Should().NotBeNull();
            var multiResult = result as MultiProviderSearchResource;
            multiResult.Should().NotBeNull();
            multiResult.Hardcover.Should().NotBeEmpty();
            multiResult.OpenLibrary.Should().BeEmpty();
            multiResult.GoogleBooks.Should().BeEmpty();

            _hardcoverClientMock.Verify(x => x.Search("Test"), Times.Once);
            _openLibraryClientMock.Verify(x => x.Search(It.IsAny<string>()), Times.Never);
            _googleBooksClientMock.Verify(x => x.Search(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void search_all_empty_term_returns_empty_results()
        {
            // Act
            var result = _controller.SearchAll("", "hardcover,openlibrary,googlebooks");

            // Assert
            result.Should().NotBeNull();
            var multiResult = result as MultiProviderSearchResource;
            multiResult.Should().NotBeNull();
            multiResult.Hardcover.Should().BeEmpty();
            multiResult.OpenLibrary.Should().BeEmpty();
            multiResult.GoogleBooks.Should().BeEmpty();

            _hardcoverClientMock.Verify(x => x.Search(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void search_reconciled_calls_reconciliation_service()
        {
            // Arrange
            var hardcoverBooks = new List<Book> { new Book { Title = "Foundation", HardcoverBookId = "hc123" } };
            var openLibraryBooks = new List<Book> { new Book { Title = "Foundation", OpenLibraryWorkId = "OL123" } };
            var googleBooksBooks = new List<Book> { new Book { Title = "Foundation", GoogleBooksId = "gb123" } };

            _hardcoverClientMock.Setup(x => x.Search("Foundation")).Returns(new List<object>());
            _openLibraryClientMock.Setup(x => x.Search("Foundation")).Returns(new List<object>());
            _googleBooksClientMock.Setup(x => x.Search("Foundation")).Returns(new List<object>());

            var reconciledBooks = new List<ReconciledBook>
            {
                new ReconciledBook
                {
                    MergedBook = new Book { Title = "Foundation" },
                    HardcoverId = "hc123",
                    OpenLibraryId = "OL123",
                    GoogleBooksId = "gb123",
                    MatchedProviders = new List<string> { "hardcover", "openlibrary", "googlebooks" },
                    ConfidenceScore = 0.95m,
                    PrimarySource = "hardcover"
                }
            };

            _reconciliationServiceMock
                .Setup(x => x.ReconcileBooks(
                    It.IsAny<List<Book>>(),
                    It.IsAny<List<Book>>(),
                    It.IsAny<List<Book>>()))
                .Returns(reconciledBooks);

            _reconciliationServiceMock
                .Setup(x => x.ReconcileAuthors(
                    It.IsAny<List<Author>>(),
                    It.IsAny<List<Author>>(),
                    It.IsAny<List<Author>>()))
                .Returns(new List<ReconciledAuthor>());

            // Act
            var result = _controller.SearchReconciled("Foundation", "hardcover,openlibrary,googlebooks");

            // Assert
            result.Should().NotBeNull();
            var reconciledResult = result as ReconciledSearchResource;
            reconciledResult.Should().NotBeNull();
            reconciledResult.Query.Should().Be("Foundation");
            reconciledResult.Books.Should().NotBeEmpty();

            _reconciliationServiceMock.Verify(x => x.ReconcileBooks(
                It.IsAny<List<Book>>(),
                It.IsAny<List<Book>>(),
                It.IsAny<List<Book>>()), Times.Once);
        }

        [Test]
        public void search_reconciled_empty_term_returns_empty()
        {
            // Act
            var result = _controller.SearchReconciled("", "hardcover");

            // Assert
            result.Should().NotBeNull();
            var reconciledResult = result as ReconciledSearchResource;
            reconciledResult.Should().NotBeNull();
            reconciledResult.Books.Should().BeEmpty();
            reconciledResult.Authors.Should().BeEmpty();

            _reconciliationServiceMock.Verify(x => x.ReconcileBooks(
                It.IsAny<List<Book>>(),
                It.IsAny<List<Book>>(),
                It.IsAny<List<Book>>()), Times.Never);
        }

        [Test]
        public void search_reconciled_respects_provider_selection()
        {
            // Arrange
            _hardcoverClientMock.Setup(x => x.Search("Test")).Returns(new List<object>());
            _reconciliationServiceMock
                .Setup(x => x.ReconcileBooks(It.IsAny<List<Book>>(), It.IsAny<List<Book>>(), It.IsAny<List<Book>>()))
                .Returns(new List<ReconciledBook>());
            _reconciliationServiceMock
                .Setup(x => x.ReconcileAuthors(It.IsAny<List<Author>>(), It.IsAny<List<Author>>(), It.IsAny<List<Author>>()))
                .Returns(new List<ReconciledAuthor>());

            // Act
            var result = _controller.SearchReconciled("Test", "hardcover");

            // Assert
            _hardcoverClientMock.Verify(x => x.Search("Test"), Times.Once);
            _openLibraryClientMock.Verify(x => x.Search(It.IsAny<string>()), Times.Never);
            _googleBooksClientMock.Verify(x => x.Search(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void search_reconciled_includes_author_results()
        {
            // Arrange
            _hardcoverClientMock.Setup(x => x.Search("Isaac Asimov")).Returns(new List<object>());
            _openLibraryClientMock.Setup(x => x.Search("Isaac Asimov")).Returns(new List<object>());
            _googleBooksClientMock.Setup(x => x.Search("Isaac Asimov")).Returns(new List<object>());

            _reconciliationServiceMock
                .Setup(x => x.ReconcileBooks(It.IsAny<List<Book>>(), It.IsAny<List<Book>>(), It.IsAny<List<Book>>()))
                .Returns(new List<ReconciledBook>());

            var reconciledAuthors = new List<ReconciledAuthor>
            {
                new ReconciledAuthor
                {
                    MergedAuthor = new Author { Metadata = new AuthorMetadata { Name = "Isaac Asimov" } },
                    HardcoverId = "hc123",
                    MatchedProviders = new List<string> { "hardcover" },
                    ConfidenceScore = 0.9m,
                    PrimarySource = "hardcover"
                }
            };

            _reconciliationServiceMock
                .Setup(x => x.ReconcileAuthors(It.IsAny<List<Author>>(), It.IsAny<List<Author>>(), It.IsAny<List<Author>>()))
                .Returns(reconciledAuthors);

            // Act
            var result = _controller.SearchReconciled("Isaac Asimov", "hardcover");

            // Assert
            var reconciledResult = result as ReconciledSearchResource;
            reconciledResult.Should().NotBeNull();
            reconciledResult.Authors.Should().NotBeEmpty();
            reconciledResult.Authors.First().Author.AuthorName.Should().Be("Isaac Asimov");
        }
    }
}
