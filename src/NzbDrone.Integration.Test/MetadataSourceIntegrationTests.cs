using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Readarr.Api.V1.Search;

namespace NzbDrone.Integration.Test
{
    [TestFixture]
    [Category("Integration")]
    public class MetadataSourceIntegrationTests : IntegrationTest
    {
        [Test]
        [Order(1)]
        public void should_search_googlebooks_for_book()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/googlebooks?term=Foundation");

            // Assert
            results.Should().NotBeNullOrEmpty();
            results.First().Book.Should().NotBeNull();
            results.First().Book.Title.Should().NotBeNullOrEmpty();
        }

        [Test]
        [Order(2)]
        public void should_search_hardcover_for_book()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/hardcover?term=Mistborn");

            // Assert
            results.Should().NotBeNullOrEmpty();
            results.Any(r => r.Book != null).Should().BeTrue();
        }

        [Test]
        [Order(3)]
        public void should_search_openlibrary_for_book()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/openlibrary?term=1984");

            // Assert
            results.Should().NotBeNullOrEmpty();
            results.First().Book.Should().NotBeNull();
            results.First().Book.Title.Should().Contain("1984");
        }

        [Test]
        [Order(4)]
        public void should_search_hardcover_for_author()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/hardcover?term=Brandon+Sanderson");

            // Assert
            results.Should().NotBeNullOrEmpty();
            // May contain both books and authors
            (results.Any(r => r.Author != null) || results.Any(r => r.Book != null)).Should().BeTrue();
        }

        [Test]
        [Order(5)]
        public void should_search_all_providers()
        {
            // Act
            var results = ProviderSearch.Get<MultiProviderSearchResource>($"/search/provider?term=Foundation&providers=hardcover,openlibrary,googlebooks");

            // Assert
            results.Should().NotBeNull();
            results.Query.Should().Be("Foundation");

            // At least one provider should have results
            var totalResults = results.Hardcover.Count + results.OpenLibrary.Count + results.GoogleBooks.Count;
            totalResults.Should().BeGreaterThan(0);
        }

        [Test]
        [Order(6)]
        public void should_reconcile_search_results()
        {
            // Act
            var results = ProviderSearch.Get<ReconciledSearchResource>($"/search/provider/reconcile?term=Foundation&providers=hardcover,openlibrary,googlebooks");

            // Assert
            results.Should().NotBeNull();
            results.Query.Should().Be("Foundation");

            // Should have reconciled books or authors
            (results.Books.Any() || results.Authors.Any()).Should().BeTrue();
        }

        [Test]
        [Order(7)]
        public void reconciled_results_should_have_matched_providers()
        {
            // Act
            var results = ProviderSearch.Get<ReconciledSearchResource>($"/search/provider/reconcile?term=The+Lord+of+the+Rings&providers=hardcover,openlibrary,googlebooks");

            // Assert
            results.Should().NotBeNull();

            if (results.Books.Any())
            {
                var firstBook = results.Books.First();
                firstBook.MatchedProviders.Should().NotBeNullOrEmpty();
                firstBook.ConfidenceScore.Should().BeInRange(0, 1);
                firstBook.PrimarySource.Should().NotBeNullOrEmpty();
            }
        }

        [Test]
        [Order(8)]
        public void reconciled_results_should_include_all_provider_ids()
        {
            // Act
            var results = ProviderSearch.Get<ReconciledSearchResource>($"/search/provider/reconcile?term=Harry+Potter&providers=hardcover,openlibrary,googlebooks");

            // Assert
            results.Should().NotBeNull();

            if (results.Books.Any())
            {
                var booksWithMultipleProviders = results.Books
                    .Where(b => b.MatchedProviders.Count > 1)
                    .ToList();

                if (booksWithMultipleProviders.Any())
                {
                    var firstMatch = booksWithMultipleProviders.First();

                    // Should have at least 2 provider IDs
                    var providerIdCount = new[]
                    {
                        firstMatch.HardcoverId,
                        firstMatch.OpenLibraryId,
                        firstMatch.GoogleBooksId,
                        firstMatch.GoodreadsId
                    }.Count(id => !string.IsNullOrEmpty(id));

                    providerIdCount.Should().BeGreaterThanOrEqualTo(2);
                }
            }
        }

        [Test]
        [Order(9)]
        public void search_with_isbn_should_return_specific_book()
        {
            // Act - Search for 1984 by ISBN
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/googlebooks?term=9780451524935");

            // Assert
            results.Should().NotBeNullOrEmpty();
            results.First().Book.Should().NotBeNull();
            // ISBN searches should return very specific matches
            results.Count.Should().BeLessThanOrEqualTo(5);
        }

        [Test]
        [Order(10)]
        public void empty_search_should_return_empty_results()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/googlebooks?term=");

            // Assert
            results.Should().NotBeNull();
            results.Should().BeEmpty();
        }

        [Test]
        [Order(11)]
        public void nonexistent_book_search_should_return_empty()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/hardcover?term=ThisBookDefinitelyDoesNotExist999999999");

            // Assert
            results.Should().NotBeNull();
            results.Should().BeEmpty();
        }

        [Test]
        [Order(12)]
        public void search_should_include_metadata()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/googlebooks?term=The+Hobbit");

            // Assert
            results.Should().NotBeNullOrEmpty();
            var book = results.First().Book;
            book.Should().NotBeNull();
            book.Title.Should().NotBeNullOrEmpty();
            book.Overview.Should().NotBeNullOrEmpty();
            book.PageCount.Should().BeGreaterThan(0);
        }

        [Test]
        [Order(13)]
        public void search_should_include_cover_images()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/openlibrary?term=Dune");

            // Assert
            results.Should().NotBeNullOrEmpty();
            var book = results.First().Book;
            book.Should().NotBeNull();

            if (!string.IsNullOrEmpty(book.RemoteCover))
            {
                book.RemoteCover.Should().StartWith("http");
            }
        }

        [Test]
        [Order(14)]
        public void search_should_handle_special_characters()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/googlebooks?term=Guns,+Germs,+and+Steel");

            // Assert
            results.Should().NotBeNullOrEmpty();
        }

        [Test]
        [Order(15)]
        public void search_provider_selection_should_work()
        {
            // Act - Request only Hardcover
            var results = ProviderSearch.Get<MultiProviderSearchResource>($"/search/provider?term=Foundation&providers=hardcover");

            // Assert
            results.Should().NotBeNull();
            results.OpenLibrary.Should().BeEmpty();
            results.GoogleBooks.Should().BeEmpty();
            // Hardcover may or may not have results depending on availability
        }

        [Test]
        [Order(16)]
        public void reconciled_alias_endpoint_should_work()
        {
            // Act - Test the /reconciled alias
            var results = ProviderSearch.Get<ReconciledSearchResource>($"/search/provider/reconciled?term=Foundation&providers=hardcover,openlibrary");

            // Assert
            results.Should().NotBeNull();
            results.Query.Should().Be("Foundation");
        }

        [Test]
        [Order(17)]
        public void search_results_should_have_ratings()
        {
            // Act
            var results = ProviderSearch.Get<SearchResource[]>($"/search/provider/googlebooks?term=The+Lord+of+the+Rings");

            // Assert
            results.Should().NotBeNullOrEmpty();
            var book = results.First().Book;
            book.Should().NotBeNull();

            if (book.Ratings != null)
            {
                book.Ratings.Value.Should().BeGreaterThanOrEqualTo(0);
                book.Ratings.Votes.Should().BeGreaterThanOrEqualTo(0);
            }
        }

        private static ProviderSearchApiClient ProviderSearch => new ProviderSearchApiClient(RestClient);

        private class ProviderSearchApiClient : ClientBase
        {
            public ProviderSearchApiClient(IRestClient restClient)
                : base(restClient, ApiKey)
            {
            }

            protected override string ApiUrl => "/api/v1/search/provider";

            public T Get<T>(string resource) where T : class, new()
            {
                var request = BuildRequest(resource);
                return Get<T>(request);
            }
        }
    }
}
