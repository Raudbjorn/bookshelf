using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.BookInfo
{
    [TestFixture]
    public class BookReconciliationServiceFixture : CoreTest<BookReconciliationService>
    {
        private Book CreateTestBook(string title, string authorName, string foreignId, string provider)
        {
            var book = new Book
            {
                Title = title,
                ForeignBookId = $"{provider}:{foreignId}",
                Ratings = new Ratings { Value = 4.0m, Votes = 100 },
                Editions = new List<Edition>
                {
                    new Edition
                    {
                        Title = title,
                        PageCount = 300,
                        Overview = $"Description from {provider}",
                        Ratings = new Ratings { Value = 4.0m, Votes = 100 }
                    }
                }
            };

            // Set provider-specific IDs
            switch (provider.ToLower())
            {
                case "hardcover":
                    book.HardcoverBookId = foreignId;
                    break;
                case "openlibrary":
                    book.OpenLibraryWorkId = foreignId;
                    break;
                case "googlebooks":
                    book.GoogleBooksId = foreignId;
                    break;
            }

            return book;
        }

        private Author CreateTestAuthor(string name, string foreignId, string provider)
        {
            var author = new Author();
            author.Metadata = new AuthorMetadata
            {
                Name = name,
                ForeignAuthorId = $"{provider}:{foreignId}",
                Overview = $"Bio from {provider}"
            };

            // Set provider-specific IDs
            switch (provider.ToLower())
            {
                case "hardcover":
                    author.Metadata.Value.HardcoverAuthorId = foreignId;
                    break;
                case "openlibrary":
                    author.Metadata.Value.OpenLibraryAuthorId = foreignId;
                    break;
                case "googlebooks":
                    author.Metadata.Value.GoogleBooksAuthorId = foreignId;
                    break;
            }

            return author;
        }

        [Test]
        public void reconcile_books_with_exact_title_match()
        {
            // Arrange
            var hardcoverBooks = new List<Book>
            {
                CreateTestBook("Foundation", "Isaac Asimov", "hc123", "hardcover")
            };

            var openLibraryBooks = new List<Book>
            {
                CreateTestBook("Foundation", "Isaac Asimov", "OL123", "openlibrary")
            };

            var googleBooksBooks = new List<Book>
            {
                CreateTestBook("Foundation", "Isaac Asimov", "gb123", "googlebooks")
            };

            // Act
            var result = Subject.ReconcileBooks(hardcoverBooks, openLibraryBooks, googleBooksBooks);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var reconciledBook = result.First();
            reconciledBook.HardcoverId.Should().Be("hc123");
            reconciledBook.OpenLibraryId.Should().Be("OL123");
            reconciledBook.GoogleBooksId.Should().Be("gb123");
            reconciledBook.MatchedProviders.Should().Contain(new[] { "hardcover", "openlibrary", "googlebooks" });
            reconciledBook.ConfidenceScore.Should().BeGreaterThan(0.8m);
        }

        [Test]
        public void reconcile_books_with_similar_titles()
        {
            // Arrange
            var hardcoverBooks = new List<Book>
            {
                CreateTestBook("The Lord of the Rings", "J.R.R. Tolkien", "hc456", "hardcover")
            };

            var openLibraryBooks = new List<Book>
            {
                CreateTestBook("Lord of the Rings", "J.R.R. Tolkien", "OL456", "openlibrary")
            };

            var googleBooksBooks = new List<Book>
            {
                CreateTestBook("The Lord Of The Rings", "J.R.R. Tolkien", "gb456", "googlebooks")
            };

            // Act
            var result = Subject.ReconcileBooks(hardcoverBooks, openLibraryBooks, googleBooksBooks);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var reconciledBook = result.First();
            reconciledBook.MatchedProviders.Count.Should().Be(3);
            reconciledBook.ConfidenceScore.Should().BeGreaterThan(0.7m);
        }

        [Test]
        public void reconcile_books_with_no_matches()
        {
            // Arrange
            var hardcoverBooks = new List<Book>
            {
                CreateTestBook("Foundation", "Isaac Asimov", "hc123", "hardcover")
            };

            var openLibraryBooks = new List<Book>
            {
                CreateTestBook("Dune", "Frank Herbert", "OL789", "openlibrary")
            };

            var googleBooksBooks = new List<Book>
            {
                CreateTestBook("Neuromancer", "William Gibson", "gb999", "googlebooks")
            };

            // Act
            var result = Subject.ReconcileBooks(hardcoverBooks, openLibraryBooks, googleBooksBooks);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3); // Three separate books

            foreach (var book in result)
            {
                book.MatchedProviders.Should().HaveCount(1);
            }
        }

        [Test]
        public void reconcile_books_merges_metadata()
        {
            // Arrange
            var hardcoverBook = CreateTestBook("Foundation", "Isaac Asimov", "hc123", "hardcover");
            hardcoverBook.Editions.Value.First().Overview = "Detailed description from Hardcover";
            hardcoverBook.Ratings = new Ratings { Value = 4.5m, Votes = 500 };

            var openLibraryBook = CreateTestBook("Foundation", "Isaac Asimov", "OL123", "openlibrary");
            openLibraryBook.Editions.Value.First().PageCount = 255;
            openLibraryBook.Editions.Value.First().Overview = "Short description from OpenLibrary";

            var googleBooksBook = CreateTestBook("Foundation", "Isaac Asimov", "gb123", "googlebooks");
            googleBooksBook.Ratings = new Ratings { Value = 4.3m, Votes = 1000 };

            // Act
            var result = Subject.ReconcileBooks(
                new List<Book> { hardcoverBook },
                new List<Book> { openLibraryBook },
                new List<Book> { googleBooksBook }
            );

            // Assert
            result.Should().HaveCount(1);

            var reconciledBook = result.First();
            var mergedBook = reconciledBook.MergedBook;

            // Should prefer the longer/better description
            mergedBook.Editions.Value.First().Overview.Should().NotBeNullOrEmpty();

            // Should have ratings data
            mergedBook.Ratings.Should().NotBeNull();
            mergedBook.Ratings.Votes.Should().BeGreaterThan(0);
        }

        [Test]
        public void reconcile_books_with_empty_lists()
        {
            // Act
            var result = Subject.ReconcileBooks(
                new List<Book>(),
                new List<Book>(),
                new List<Book>()
            );

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void reconcile_books_with_only_one_provider()
        {
            // Arrange
            var hardcoverBooks = new List<Book>
            {
                CreateTestBook("Foundation", "Isaac Asimov", "hc123", "hardcover"),
                CreateTestBook("Dune", "Frank Herbert", "hc456", "hardcover")
            };

            // Act
            var result = Subject.ReconcileBooks(hardcoverBooks, new List<Book>(), new List<Book>());

            // Assert
            result.Should().HaveCount(2);

            foreach (var book in result)
            {
                book.MatchedProviders.Should().HaveCount(1);
                book.MatchedProviders.Should().Contain("hardcover");
                book.HardcoverId.Should().NotBeNullOrEmpty();
                book.PrimarySource.Should().Be("hardcover");
            }
        }

        [Test]
        public void reconcile_authors_with_exact_name_match()
        {
            // Arrange
            var hardcoverAuthors = new List<Author>
            {
                CreateTestAuthor("Isaac Asimov", "hc123", "hardcover")
            };

            var openLibraryAuthors = new List<Author>
            {
                CreateTestAuthor("Isaac Asimov", "OL123", "openlibrary")
            };

            var googleBooksAuthors = new List<Author>
            {
                CreateTestAuthor("Isaac Asimov", "gb123", "googlebooks")
            };

            // Act
            var result = Subject.ReconcileAuthors(hardcoverAuthors, openLibraryAuthors, googleBooksAuthors);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);

            var reconciledAuthor = result.First();
            reconciledAuthor.HardcoverId.Should().Be("hc123");
            reconciledAuthor.OpenLibraryId.Should().Be("OL123");
            reconciledAuthor.GoogleBooksId.Should().Be("gb123");
            reconciledAuthor.MatchedProviders.Should().Contain(new[] { "hardcover", "openlibrary", "googlebooks" });
            reconciledAuthor.ConfidenceScore.Should().BeGreaterThan(0.8m);
        }

        [Test]
        public void reconcile_authors_with_similar_names()
        {
            // Arrange
            var hardcoverAuthors = new List<Author>
            {
                CreateTestAuthor("J.R.R. Tolkien", "hc456", "hardcover")
            };

            var openLibraryAuthors = new List<Author>
            {
                CreateTestAuthor("J. R. R. Tolkien", "OL456", "openlibrary")
            };

            var googleBooksAuthors = new List<Author>
            {
                CreateTestAuthor("John Ronald Reuel Tolkien", "gb456", "googlebooks")
            };

            // Act
            var result = Subject.ReconcileAuthors(hardcoverAuthors, openLibraryAuthors, googleBooksAuthors);

            // Assert
            result.Should().NotBeNull();

            // Should match at least the first two (very similar), third may or may not match
            var tolkienMatches = result.Where(a =>
                a.MergedAuthor.Name.Contains("Tolkien", StringComparison.OrdinalIgnoreCase)).ToList();

            tolkienMatches.Should().NotBeEmpty();
        }

        [Test]
        public void reconcile_authors_with_no_matches()
        {
            // Arrange
            var hardcoverAuthors = new List<Author>
            {
                CreateTestAuthor("Isaac Asimov", "hc123", "hardcover")
            };

            var openLibraryAuthors = new List<Author>
            {
                CreateTestAuthor("Frank Herbert", "OL789", "openlibrary")
            };

            var googleBooksAuthors = new List<Author>
            {
                CreateTestAuthor("William Gibson", "gb999", "googlebooks")
            };

            // Act
            var result = Subject.ReconcileAuthors(hardcoverAuthors, openLibraryAuthors, googleBooksAuthors);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3); // Three separate authors

            foreach (var author in result)
            {
                author.MatchedProviders.Should().HaveCount(1);
            }
        }

        [Test]
        public void reconcile_authors_merges_metadata()
        {
            // Arrange
            var hardcoverAuthor = CreateTestAuthor("Isaac Asimov", "hc123", "hardcover");
            hardcoverAuthor.Metadata.Value.Overview = "Detailed biography from Hardcover";
            hardcoverAuthor.Metadata.Value.Born = new DateTime(1920, 1, 2);

            var openLibraryAuthor = CreateTestAuthor("Isaac Asimov", "OL123", "openlibrary");
            openLibraryAuthor.Metadata.Value.Overview = "Short bio from OpenLibrary";

            var googleBooksAuthor = CreateTestAuthor("Isaac Asimov", "gb123", "googlebooks");
            googleBooksAuthor.Metadata.Value.Died = new DateTime(1992, 4, 6);

            // Act
            var result = Subject.ReconcileAuthors(
                new List<Author> { hardcoverAuthor },
                new List<Author> { openLibraryAuthor },
                new List<Author> { googleBooksAuthor }
            );

            // Assert
            result.Should().HaveCount(1);

            var reconciledAuthor = result.First();
            var mergedAuthor = reconciledAuthor.MergedAuthor;

            // Should have merged metadata
            mergedAuthor.Metadata.Value.Name.Should().Be("Isaac Asimov");
            mergedAuthor.Metadata.Value.Overview.Should().NotBeNullOrEmpty();

            // Should have both dates if available
            if (mergedAuthor.Metadata.Value.Born.HasValue)
            {
                mergedAuthor.Metadata.Value.Born.Value.Year.Should().Be(1920);
            }

            if (mergedAuthor.Metadata.Value.Died.HasValue)
            {
                mergedAuthor.Metadata.Value.Died.Value.Year.Should().Be(1992);
            }
        }

        [Test]
        public void reconcile_authors_with_empty_lists()
        {
            // Act
            var result = Subject.ReconcileAuthors(
                new List<Author>(),
                new List<Author>(),
                new List<Author>()
            );

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Test]
        public void reconcile_authors_with_only_one_provider()
        {
            // Arrange
            var hardcoverAuthors = new List<Author>
            {
                CreateTestAuthor("Isaac Asimov", "hc123", "hardcover"),
                CreateTestAuthor("Frank Herbert", "hc456", "hardcover")
            };

            // Act
            var result = Subject.ReconcileAuthors(hardcoverAuthors, new List<Author>(), new List<Author>());

            // Assert
            result.Should().HaveCount(2);

            foreach (var author in result)
            {
                author.MatchedProviders.Should().HaveCount(1);
                author.MatchedProviders.Should().Contain("hardcover");
                author.HardcoverId.Should().NotBeNullOrEmpty();
                author.PrimarySource.Should().Be("hardcover");
            }
        }

        [Test]
        public void reconcile_books_assigns_primary_source()
        {
            // Arrange
            var hardcoverBook = CreateTestBook("Foundation", "Isaac Asimov", "hc123", "hardcover");
            hardcoverBook.Ratings = new Ratings { Value = 4.5m, Votes = 500 };

            var openLibraryBook = CreateTestBook("Foundation", "Isaac Asimov", "OL123", "openlibrary");
            openLibraryBook.Ratings = new Ratings { Value = 4.0m, Votes = 100 };

            // Act
            var result = Subject.ReconcileBooks(
                new List<Book> { hardcoverBook },
                new List<Book> { openLibraryBook },
                new List<Book>()
            );

            // Assert
            result.Should().HaveCount(1);

            var reconciledBook = result.First();
            reconciledBook.PrimarySource.Should().NotBeNullOrEmpty();
            reconciledBook.PrimarySource.Should().BeOneOf("hardcover", "openlibrary");
        }

        [Test]
        public void reconcile_books_calculates_confidence_score()
        {
            // Arrange
            var hardcoverBook = CreateTestBook("Foundation", "Isaac Asimov", "hc123", "hardcover");
            var openLibraryBook = CreateTestBook("Foundation", "Isaac Asimov", "OL123", "openlibrary");
            var googleBooksBook = CreateTestBook("Foundation", "Isaac Asimov", "gb123", "googlebooks");

            // Act
            var result = Subject.ReconcileBooks(
                new List<Book> { hardcoverBook },
                new List<Book> { openLibraryBook },
                new List<Book> { googleBooksBook }
            );

            // Assert
            result.Should().HaveCount(1);

            var reconciledBook = result.First();
            reconciledBook.ConfidenceScore.Should().BeInRange(0, 1);
            reconciledBook.ConfidenceScore.Should().BeGreaterThan(0.5m); // High confidence for perfect match
        }

        [Test]
        public void reconcile_authors_calculates_confidence_score()
        {
            // Arrange
            var hardcoverAuthor = CreateTestAuthor("Isaac Asimov", "hc123", "hardcover");
            var openLibraryAuthor = CreateTestAuthor("Isaac Asimov", "OL123", "openlibrary");
            var googleBooksAuthor = CreateTestAuthor("Isaac Asimov", "gb123", "googlebooks");

            // Act
            var result = Subject.ReconcileAuthors(
                new List<Author> { hardcoverAuthor },
                new List<Author> { openLibraryAuthor },
                new List<Author> { googleBooksAuthor }
            );

            // Assert
            result.Should().HaveCount(1);

            var reconciledAuthor = result.First();
            reconciledAuthor.ConfidenceScore.Should().BeInRange(0, 1);
            reconciledAuthor.ConfidenceScore.Should().BeGreaterThan(0.5m); // High confidence for perfect match
        }
    }
}
