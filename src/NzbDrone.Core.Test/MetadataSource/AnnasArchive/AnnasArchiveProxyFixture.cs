using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource.AnnasArchive;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.AnnasArchive
{
    [TestFixture]
    public class AnnasArchiveProxyFixture : CoreTest<AnnasArchiveProxy>
    {
        [SetUp]
        public void Setup()
        {
            UseRealHttp();
        }

        [TestCase("aa:8336332bf5877e3adbfb60ac70720cd5")]
        public void should_be_able_to_get_book_detail(string foreignBookId)
        {
            var details = Subject.GetBookInfo(foreignBookId);

            ValidateBookInfo(details);

            details.Item2.Title.Should().NotBeNullOrWhiteSpace();
            details.Item2.ForeignBookId.Should().Be(foreignBookId);
        }

        [Test]
        public void getting_details_of_invalid_foreign_id()
        {
            Assert.Throws<AnnasArchiveException>(() => Subject.GetBookInfo("invalid"));
        }

        [Test]
        public void getting_details_of_nonexistent_md5()
        {
            Assert.Throws<BookNotFoundException>(() => Subject.GetBookInfo("aa:00000000000000000000000000000000"));
        }

        [TestCase("8336332bf5877e3adbfb60ac70720cd5", "Against intellectual monopoly")]
        public void should_fetch_book_by_md5(string md5, string expectedTitle)
        {
            var book = Subject.GetBookByMd5(md5);

            ValidateBook(book);
            book.Title.Should().Contain(expectedTitle);
        }

        [Test]
        public void should_validate_md5_format()
        {
            Assert.Throws<AnnasArchiveException>(() => Subject.GetBookByMd5("invalid"));
            Assert.Throws<AnnasArchiveException>(() => Subject.GetBookByMd5("abc123"));
            Assert.Throws<AnnasArchiveException>(() => Subject.GetBookByMd5(""));
            Assert.Throws<AnnasArchiveException>(() => Subject.GetBookByMd5(null));
        }

        private void ValidateBookInfo(Tuple<string, Book, List<AuthorMetadata>> details)
        {
            details.Should().NotBeNull();
            details.Item1.Should().NotBeNullOrWhiteSpace(); // Author ID
            details.Item2.Should().NotBeNull(); // Book
            details.Item3.Should().NotBeNull(); // Authors list
            details.Item3.Should().NotBeEmpty();

            ValidateBook(details.Item2);
            ValidateAuthors(details.Item3);
        }

        private void ValidateBook(Book book)
        {
            book.Should().NotBeNull();
            book.Title.Should().NotBeNullOrWhiteSpace();
            book.ForeignBookId.Should().NotBeNullOrWhiteSpace();
            book.ForeignBookId.Should().StartWith("aa:");
            book.TitleSlug.Should().NotBeNullOrWhiteSpace();
            book.CleanTitle.Should().NotBeNullOrWhiteSpace();
            book.Links.Should().NotBeNull();
            book.Links.Should().Contain(l => l.Name == "Anna's Archive");

            // Validate edition
            book.Editions.Value.Should().NotBeNull();
            book.Editions.Value.Should().NotBeEmpty();
            var edition = book.Editions.Value.First();
            edition.ForeignEditionId.Should().NotBeNullOrWhiteSpace();
            edition.ForeignEditionId.Should().StartWith("aa:");
            edition.Title.Should().NotBeNullOrWhiteSpace();
            edition.Monitored.Should().BeTrue();
        }

        private void ValidateAuthors(List<AuthorMetadata> authors)
        {
            authors.Should().NotBeNull();
            authors.Should().NotBeEmpty();

            foreach (var author in authors)
            {
                author.ForeignAuthorId.Should().NotBeNullOrWhiteSpace();
                author.ForeignAuthorId.Should().StartWith("aa-author:");
                author.Name.Should().NotBeNullOrWhiteSpace();
            }
        }
    }
}
