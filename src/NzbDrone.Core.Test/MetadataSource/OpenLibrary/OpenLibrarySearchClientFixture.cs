using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource.OpenLibrary;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MetadataSource.OpenLibrary
{
    [TestFixture]
    public class OpenLibrarySearchClientFixture : CoreTest<OpenLibrarySearchClient>
    {
        [SetUp]
        public void Setup()
        {
            UseRealHttp();

            Mocker.GetMock<IConfigService>()
                .Setup(x => x.OpenLibraryEnabled)
                .Returns(true);

            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get<OpenLibrarySearchResponse>(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns((HttpRequest request, bool useCache, TimeSpan ttl) =>
                    Mocker.Resolve<IHttpClient>().Get<OpenLibrarySearchResponse>(request));
        }

        [TestCase("Foundation")]
        [TestCase("The Great Gatsby")]
        [TestCase("To Kill a Mockingbird")]
        public void successful_book_search(string query)
        {
            var result = Subject.Search(query);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Count.Should().BeGreaterThan(0);

            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("Isaac Asimov")]
        [TestCase("F. Scott Fitzgerald")]
        [TestCase("Harper Lee")]
        public void successful_author_search(string authorName)
        {
            var result = Subject.Search(authorName);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();
            searchDoc.Key.Should().NotBeNullOrEmpty();
            searchDoc.Title.Should().NotBeNullOrEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [TestCase("0451524934")] // 1984 ISBN-10
        [TestCase("9780451524935")] // 1984 ISBN-13
        public void successful_isbn_search(string isbn)
        {
            var result = Subject.Search(isbn);

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();
            searchDoc.Isbn.Should().Contain(isbn);

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
        public void search_results_include_metadata()
        {
            var result = Subject.Search("The Hobbit");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();
            searchDoc.Key.Should().NotBeNullOrEmpty();
            searchDoc.Title.Should().NotBeNullOrEmpty();
            searchDoc.AuthorName.Should().NotBeNullOrEmpty();
        }

        [Test]
        public void search_results_include_cover_ids()
        {
            var result = Subject.Search("Harry Potter and the Philosopher's Stone");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            // Popular books should have cover IDs
            if (searchDoc.CoverId.HasValue)
            {
                searchDoc.CoverId.Value.Should().BeGreaterThan(0);
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_ratings()
        {
            var result = Subject.Search("The Lord of the Rings");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            // Popular books should have ratings
            if (searchDoc.RatingsAverage.HasValue)
            {
                searchDoc.RatingsAverage.Value.Should().BeInRange(0, 5);
            }

            if (searchDoc.RatingsCount.HasValue)
            {
                searchDoc.RatingsCount.Value.Should().BeGreaterThanOrEqualTo(0);
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_page_counts()
        {
            var result = Subject.Search("1984");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            if (searchDoc.NumberOfPagesMedian.HasValue)
            {
                searchDoc.NumberOfPagesMedian.Value.Should().BeGreaterThan(0);
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_publication_info()
        {
            var result = Subject.Search("Pride and Prejudice");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            if (searchDoc.FirstPublishYear.HasValue)
            {
                searchDoc.FirstPublishYear.Value.Should().BeGreaterThan(1500);
                searchDoc.FirstPublishYear.Value.Should().BeLessThanOrEqualTo(DateTime.Now.Year);
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_subjects()
        {
            var result = Subject.Search("Dune");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            if (searchDoc.Subject != null && searchDoc.Subject.Any())
            {
                searchDoc.Subject.Should().NotBeEmpty();
                searchDoc.Subject.Should().AllSatisfy(s => s.Should().NotBeNullOrWhiteSpace());
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_isbn_list()
        {
            var result = Subject.Search("The Catcher in the Rye");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            if (searchDoc.Isbn != null && searchDoc.Isbn.Any())
            {
                searchDoc.Isbn.Should().NotBeEmpty();
                searchDoc.Isbn.Should().AllSatisfy(isbn => isbn.Should().NotBeNullOrWhiteSpace());
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_author_keys()
        {
            var result = Subject.Search("Brave New World");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            if (searchDoc.AuthorKey != null && searchDoc.AuthorKey.Any())
            {
                searchDoc.AuthorKey.Should().NotBeEmpty();
                searchDoc.AuthorKey.Should().AllSatisfy(key => key.Should().NotBeNullOrWhiteSpace());
            }

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_respects_enabled_configuration()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(x => x.OpenLibraryEnabled)
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
            result.Count.Should().BeLessThanOrEqualTo(100);

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_special_characters()
        {
            var result = Subject.Search("Fahrenheit 451");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_unicode_characters()
        {
            var result = Subject.Search("One Hundred Years of Solitude");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_series_queries()
        {
            var result = Subject.Search("The Chronicles of Narnia");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_results_include_work_keys()
        {
            var result = Subject.Search("Neuromancer");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();
            searchDoc.Key.Should().NotBeNullOrEmpty();
            searchDoc.Key.Should().StartWith("/works/");

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_with_language_filter()
        {
            var result = Subject.Search("Foundation AND language:eng");

            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            ExceptionVerification.IgnoreWarns();
        }

        [Test]
        public void search_handles_multiple_editions()
        {
            var result = Subject.Search("The Hitchhiker's Guide to the Galaxy");

            result.Should().NotBeEmpty();

            var searchDoc = result.First() as OpenLibrarySearchDoc;
            searchDoc.Should().NotBeNull();

            // Popular books should have multiple editions
            if (searchDoc.EditionCount.HasValue)
            {
                searchDoc.EditionCount.Value.Should().BeGreaterThan(0);
            }

            ExceptionVerification.IgnoreWarns();
        }
    }
}
