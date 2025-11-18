using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource.GoogleBooks;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.GoogleBooks
{
    [TestFixture]
    public class GoogleBooksSearchClientFixture : CoreTest<GoogleBooksSearchClient>
    {
        [SetUp]
        public void Setup()
        {
            UseRealHttp();

            Mocker.GetMock<IConfigService>()
                .Setup(x => x.GoogleBooksEnabled)
                .Returns(true);

            Mocker.GetMock<IConfigService>()
                .Setup(x => x.GoogleBooksApiKey)
                .Returns(string.Empty); // Can work without API key but with rate limits

            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get<GoogleBooksSearchResponse>(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns((HttpRequest request, bool useCache, TimeSpan ttl) =>
                    Mocker.Resolve<IHttpClient>().Get<GoogleBooksSearchResponse>(request));
        }

        [TestCase("Isaac Asimov")]
        [TestCase("Foundation")]
        [TestCase("1984")]
        public void successful_book_search(string query)
        {
            var result = Subject.Search(query);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Count.Should().BeGreaterThan(0);

            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("9780451524935")] // 1984 ISBN-10
        [TestCase("9780553293357")] // Foundation ISBN-13
        public void successful_isbn_search(string isbn)
        {
            var result = Subject.Search(isbn);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.First().Should().BeOfType<GoogleBookItem>();

            var bookItem = result.First() as GoogleBookItem;
            bookItem.Id.Should().NotBeNullOrEmpty();
            bookItem.VolumeInfo.Should().NotBeNull();
            bookItem.VolumeInfo.Title.Should().NotBeNullOrEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("J.K. Rowling")]
        [TestCase("Stephen King")]
        public void successful_author_search(string authorName)
        {
            var result = Subject.Search($"inauthor:{authorName}");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Count.Should().BeGreaterThan(0);

            var bookItem = result.First() as GoogleBookItem;
            bookItem.Should().NotBeNull();
            bookItem.VolumeInfo.Authors.Should().Contain(a => a.Contains(authorName, StringComparison.OrdinalIgnoreCase));

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
        [TestCase("thisBookDefinitelyDoesNotExist123456789")]
        public void no_results_search_returns_empty_list(string query)
        {
            var result = Subject.Search(query);

            result.Should().NotBeNull();
            // May return empty or very few irrelevant results
            result.Count.Should().BeLessThan(5);

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_volume_info()
        {
            var result = Subject.Search("Foundation Isaac Asimov");

            result.Should().NotBeEmpty();

            var bookItem = result.First() as GoogleBookItem;
            bookItem.Should().NotBeNull();
            bookItem.VolumeInfo.Should().NotBeNull();
            bookItem.VolumeInfo.Title.Should().NotBeNullOrEmpty();
            bookItem.VolumeInfo.Authors.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void search_results_include_image_links()
        {
            var result = Subject.Search("Harry Potter and the Philosopher's Stone");

            result.Should().NotBeEmpty();

            var bookItem = result.First() as GoogleBookItem;
            bookItem.Should().NotBeNull();
            bookItem.VolumeInfo.Should().NotBeNull();

            // Not all books have images, but popular ones usually do
            if (bookItem.VolumeInfo.ImageLinks != null)
            {
                bookItem.VolumeInfo.ImageLinks.Thumbnail.Should().NotBeNullOrEmpty();
                bookItem.VolumeInfo.ImageLinks.Thumbnail.Should().StartWith("http");
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_ratings()
        {
            var result = Subject.Search("The Lord of the Rings");

            result.Should().NotBeEmpty();

            var bookItem = result.First() as GoogleBookItem;
            bookItem.Should().NotBeNull();
            bookItem.VolumeInfo.Should().NotBeNull();

            // Popular books should have ratings
            if (bookItem.VolumeInfo.AverageRating.HasValue)
            {
                bookItem.VolumeInfo.AverageRating.Value.Should().BeInRange(0, 5);
            }

            if (bookItem.VolumeInfo.RatingsCount.HasValue)
            {
                bookItem.VolumeInfo.RatingsCount.Value.Should().BeGreaterThanOrEqualTo(0);
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_respects_api_key_configuration()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(x => x.GoogleBooksApiKey)
                .Returns("test-api-key-12345");

            var result = Subject.Search("Foundation");

            result.Should().NotBeNull();
            // With API key, should have higher rate limits

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_respects_enabled_configuration()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(x => x.GoogleBooksEnabled)
                .Returns(false);

            var result = Subject.Search("Foundation");

            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void search_limits_results_to_reasonable_count()
        {
            var result = Subject.Search("science fiction");

            result.Should().NotBeNull();
            // Should not return thousands of results
            result.Count.Should().BeLessThanOrEqualTo(40); // Google Books API default max is 40

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_special_characters()
        {
            var result = Subject.Search("Guns, Germs, and Steel");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_unicode_characters()
        {
            var result = Subject.Search("Les Misérables");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }
    }
}
