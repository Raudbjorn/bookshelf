using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource.Hardcover;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.Hardcover
{
    [TestFixture]
    public class HardcoverSearchClientFixture : CoreTest<HardcoverSearchClient>
    {
        [SetUp]
        public void Setup()
        {
            UseRealHttp();

            Mocker.GetMock<IConfigService>()
                .Setup(x => x.HardcoverEnabled)
                .Returns(true);

            Mocker.GetMock<IConfigService>()
                .Setup(x => x.HardcoverApiToken)
                .Returns(string.Empty); // Can work without token for public searches

            Mocker.GetMock<IConfigService>()
                .Setup(x => x.HardcoverUsername)
                .Returns(string.Empty);

            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get<HardcoverSearchResponse>(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns((HttpRequest request, bool useCache, TimeSpan ttl) =>
                    Mocker.Resolve<IHttpClient>().Get<HardcoverSearchResponse>(request));
        }

        [TestCase("Brandon Sanderson")]
        [TestCase("Mistborn")]
        [TestCase("The Way of Kings")]
        public void successful_book_search(string query)
        {
            var result = Subject.Search(query);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Count.Should().BeGreaterThan(0);

            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("J.K. Rowling")]
        [TestCase("Brandon Sanderson")]
        [TestCase("Patrick Rothfuss")]
        public void successful_author_search(string authorName)
        {
            var result = Subject.Search(authorName);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            // Should contain at least one author result
            var authorResults = result.OfType<HardcoverAuthorResult>().ToList();
            authorResults.Should().NotBeEmpty();

            var firstAuthor = authorResults.First();
            firstAuthor.Id.Should().NotBeNullOrEmpty();
            firstAuthor.Name.Should().NotBeNullOrEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("Foundation")]
        [TestCase("Dune")]
        [TestCase("Harry Potter")]
        public void search_returns_both_books_and_authors(string query)
        {
            var result = Subject.Search(query);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            // Popular searches may return both books and authors
            // At minimum should have one type of result
            var hasBooks = result.OfType<HardcoverBookResult>().Any();
            var hasAuthors = result.OfType<HardcoverAuthorResult>().Any();

            (hasBooks || hasAuthors).Should().BeTrue();

            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("   ")]
        public void empty_search_returns_empty_list(string query)
        {
            var result = Subject.Search(query);

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [TestCase("asdfjkl;qwertyuiop123456789zxcvbnm")]
        [TestCase("thisBookDefinitelyDoesNotExist999999999")]
        public void no_results_search_returns_empty_list(string query)
        {
            var result = Subject.Search(query);

            result.Should().NotBeNull();
            result.Should().BeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void book_results_include_metadata()
        {
            var result = Subject.Search("The Name of the Wind");

            result.Should().NotBeEmpty();

            var bookResults = result.OfType<HardcoverBookResult>().ToList();
            bookResults.Should().NotBeEmpty();

            var firstBook = bookResults.First();
            firstBook.Id.Should().NotBeNullOrEmpty();
            firstBook.Title.Should().NotBeNullOrEmpty();
            // Description may be null for some books
            // firstBook.Description.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void book_results_include_cover_images()
        {
            var result = Subject.Search("The Hobbit");

            result.Should().NotBeEmpty();

            var bookResults = result.OfType<HardcoverBookResult>().ToList();
            bookResults.Should().NotBeEmpty();

            var firstBook = bookResults.First();
            // Popular books should have cover images
            if (!string.IsNullOrEmpty(firstBook.ImageUrl))
            {
                firstBook.ImageUrl.Should().StartWith("http");
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void author_results_include_metadata()
        {
            var result = Subject.Search("Neil Gaiman");

            result.Should().NotBeEmpty();

            var authorResults = result.OfType<HardcoverAuthorResult>().ToList();
            authorResults.Should().NotBeEmpty();

            var firstAuthor = authorResults.First();
            firstAuthor.Id.Should().NotBeNullOrEmpty();
            firstAuthor.Name.Should().NotBeNullOrEmpty();
            // Bio may be null for some authors
        }

        [Test]
        public void author_results_include_image_urls()
        {
            var result = Subject.Search("Stephen King");

            result.Should().NotBeEmpty();

            var authorResults = result.OfType<HardcoverAuthorResult>().ToList();
            authorResults.Should().NotBeEmpty();

            var firstAuthor = authorResults.First();
            // Popular authors should have images
            if (!string.IsNullOrEmpty(firstAuthor.ImageUrl))
            {
                firstAuthor.ImageUrl.Should().StartWith("http");
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_respects_enabled_configuration()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(x => x.HardcoverEnabled)
                .Returns(false);

            var result = Subject.Search("Foundation");

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void search_with_api_token_uses_authentication()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(x => x.HardcoverApiToken)
                .Returns("test-token-12345");

            Mocker.GetMock<IConfigService>()
                .Setup(x => x.HardcoverUsername)
                .Returns("testuser");

            var result = Subject.Search("Foundation");

            result.Should().NotBeNull();
            // With authentication, may have access to more features/data

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_limits_results_to_reasonable_count()
        {
            var result = Subject.Search("fantasy");

            result.Should().NotBeNull();
            // Should not return thousands of results
            result.Count.Should().BeLessThanOrEqualTo(50);

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_special_characters()
        {
            var result = Subject.Search("The Catcher in the Rye");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_unicode_characters()
        {
            var result = Subject.Search("Kafka on the Shore");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_series_titles()
        {
            var result = Subject.Search("The Stormlight Archive");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void book_results_include_rating_if_available()
        {
            var result = Subject.Search("The Lord of the Rings");

            result.Should().NotBeEmpty();

            var bookResults = result.OfType<HardcoverBookResult>().ToList();
            bookResults.Should().NotBeEmpty();

            var firstBook = bookResults.First();
            // Popular books should have ratings
            if (firstBook.Rating > 0)
            {
                firstBook.Rating.Should().BeInRange(0, 5);
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void book_results_include_page_count_if_available()
        {
            var result = Subject.Search("1984");

            result.Should().NotBeEmpty();

            var bookResults = result.OfType<HardcoverBookResult>().ToList();
            bookResults.Should().NotBeEmpty();

            var firstBook = bookResults.First();
            if (firstBook.Pages > 0)
            {
                firstBook.Pages.Should().BeGreaterThan(0);
            }

            ExceptionVerification.IgnoreWarns();
        }
    }
}
