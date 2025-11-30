using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    public class BookReconciliationService : IBookReconciliationService
    {
        private readonly Logger _logger;

        public BookReconciliationService(Logger logger)
        {
            _logger = logger;
        }

        public List<ReconciledBook> ReconcileBooks(List<Book> hardcoverBooks, List<Book> openLibraryBooks, List<Book> googleBooksBooks, List<Book> comicVineBooks)
        {
            _logger.Debug($"[Reconciliation] Starting book reconciliation: {hardcoverBooks?.Count ?? 0} HC, {openLibraryBooks?.Count ?? 0} OL, {googleBooksBooks?.Count ?? 0} GB, {comicVineBooks?.Count ?? 0} CV");

            var allBooks = new List<(Book book, string provider)>();

            if (hardcoverBooks != null)
            {
                allBooks.AddRange(hardcoverBooks.Select(b => (b, "hardcover")));
            }

            if (openLibraryBooks != null)
            {
                allBooks.AddRange(openLibraryBooks.Select(b => (b, "openlibrary")));
            }

            if (googleBooksBooks != null)
            {
                allBooks.AddRange(googleBooksBooks.Select(b => (b, "googlebooks")));
            }

            if (comicVineBooks != null)
            {
                allBooks.AddRange(comicVineBooks.Select(b => (b, "comicvine")));
            }

            if (allBooks.Count == 0)
            {
                _logger.Debug("[Reconciliation] No books to reconcile");
                return new List<ReconciledBook>();
            }

            var reconciledBooks = new List<ReconciledBook>();
            var processedBooks = new HashSet<Book>();

            foreach (var (book, provider) in allBooks)
            {
                if (processedBooks.Contains(book))
                {
                    continue;
                }

                var matches = FindMatchingBooks(book, allBooks.Where(b => !processedBooks.Contains(b.book)).ToList());
                var reconciledBook = MergeBookMetadata(book, matches, provider);

                reconciledBooks.Add(reconciledBook);

                processedBooks.Add(book);
                foreach (var match in matches)
                {
                    processedBooks.Add(match.book);
                }
            }

            _logger.Info($"[Reconciliation] Reconciled {allBooks.Count} books into {reconciledBooks.Count} unique books");
            return reconciledBooks;
        }

        public List<ReconciledAuthor> ReconcileAuthors(List<Author> hardcoverAuthors, List<Author> openLibraryAuthors, List<Author> googleBooksAuthors, List<Author> comicVineAuthors)
        {
            _logger.Debug($"[Reconciliation] Starting author reconciliation: {hardcoverAuthors?.Count ?? 0} HC, {openLibraryAuthors?.Count ?? 0} OL, {googleBooksAuthors?.Count ?? 0} GB, {comicVineAuthors?.Count ?? 0} CV");

            var allAuthors = new List<(Author author, string provider)>();

            if (hardcoverAuthors != null)
            {
                allAuthors.AddRange(hardcoverAuthors.Select(a => (a, "hardcover")));
            }

            if (openLibraryAuthors != null)
            {
                allAuthors.AddRange(openLibraryAuthors.Select(a => (a, "openlibrary")));
            }

            if (googleBooksAuthors != null)
            {
                allAuthors.AddRange(googleBooksAuthors.Select(a => (a, "googlebooks")));
            }

            if (comicVineAuthors != null)
            {
                allAuthors.AddRange(comicVineAuthors.Select(a => (a, "comicvine")));
            }

            if (allAuthors.Count == 0)
            {
                _logger.Debug("[Reconciliation] No authors to reconcile");
                return new List<ReconciledAuthor>();
            }

            var reconciledAuthors = new List<ReconciledAuthor>();
            var processedAuthors = new HashSet<Author>();

            foreach (var (author, provider) in allAuthors)
            {
                if (processedAuthors.Contains(author))
                {
                    continue;
                }

                var matches = FindMatchingAuthors(author, allAuthors.Where(a => !processedAuthors.Contains(a.author)).ToList());
                var reconciledAuthor = MergeAuthorMetadata(author, matches, provider);

                reconciledAuthors.Add(reconciledAuthor);

                processedAuthors.Add(author);
                foreach (var match in matches)
                {
                    processedAuthors.Add(match.author);
                }
            }

            _logger.Info($"[Reconciliation] Reconciled {allAuthors.Count} authors into {reconciledAuthors.Count} unique authors");
            return reconciledAuthors;
        }

        private List<(Book book, string provider)> FindMatchingBooks(Book targetBook, List<(Book book, string provider)> candidateBooks)
        {
            var matches = new List<(Book book, string provider)>();

            foreach (var (candidate, provider) in candidateBooks)
            {
                if (candidate == targetBook)
                {
                    continue;
                }

                // Strategy 1: ISBN matching (strongest signal)
                if (HasMatchingIsbn(targetBook, candidate))
                {
                    _logger.Debug($"[Reconciliation] ISBN match: '{targetBook.Title}' ({GetProvider(targetBook)}) <-> '{candidate.Title}' ({provider})");
                    matches.Add((candidate, provider));
                    continue;
                }

                // Strategy 2: Title + Author matching (weaker signal)
                if (HasMatchingTitleAndAuthor(targetBook, candidate))
                {
                    _logger.Debug($"[Reconciliation] Title/Author match: '{targetBook.Title}' ({GetProvider(targetBook)}) <-> '{candidate.Title}' ({provider})");
                    matches.Add((candidate, provider));
                    continue;
                }
            }

            return matches;
        }

        private List<(Author author, string provider)> FindMatchingAuthors(Author targetAuthor, List<(Author author, string provider)> candidateAuthors)
        {
            var matches = new List<(Author author, string provider)>();

            var targetName = targetAuthor.Metadata?.Value?.Name?.ToLower().Trim();
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return matches;
            }

            foreach (var (candidate, provider) in candidateAuthors)
            {
                if (candidate == targetAuthor)
                {
                    continue;
                }

                var candidateName = candidate.Metadata?.Value?.Name?.ToLower().Trim();
                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    continue;
                }

                // Exact name match
                if (targetName == candidateName)
                {
                    _logger.Debug($"[Reconciliation] Author match: '{targetName}' ({GetProvider(targetAuthor)}) <-> '{candidateName}' ({provider})");
                    matches.Add((candidate, provider));
                }
            }

            return matches;
        }

        private bool HasMatchingIsbn(Book book1, Book book2)
        {
            var isbn1 = GetIsbn13(book1);
            var isbn2 = GetIsbn13(book2);

            if (!string.IsNullOrWhiteSpace(isbn1) && !string.IsNullOrWhiteSpace(isbn2))
            {
                return isbn1 == isbn2;
            }

            return false;
        }

        private string GetIsbn13(Book book)
        {
            if (book.Editions == null || book.Editions.Value == null || !book.Editions.Value.Any())
            {
                return null;
            }

            var edition = book.Editions.Value.FirstOrDefault();
            return edition?.Isbn13;
        }

        private bool HasMatchingTitleAndAuthor(Book book1, Book book2)
        {
            var title1 = NormalizeTitle(book1.Title);
            var title2 = NormalizeTitle(book2.Title);

            if (string.IsNullOrWhiteSpace(title1) || string.IsNullOrWhiteSpace(title2))
            {
                return false;
            }

            // Title similarity
            var titleMatch = title1 == title2 || title1.Contains(title2) || title2.Contains(title1);

            if (!titleMatch)
            {
                return false;
            }

            // Author matching
            var author1 = book1.AuthorMetadata?.Value?.Name?.ToLower().Trim();
            var author2 = book2.AuthorMetadata?.Value?.Name?.ToLower().Trim();

            if (string.IsNullOrWhiteSpace(author1) || string.IsNullOrWhiteSpace(author2))
            {
                // If we have a title match but no author info, accept it (weaker match)
                return titleMatch;
            }

            return author1 == author2;
        }

        private string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            return title.ToLower()
                .Trim()
                .Replace(":", "")
                .Replace("-", "")
                .Replace("'", "")
                .Replace("\"", "");
        }

        private ReconciledBook MergeBookMetadata(Book primaryBook, List<(Book book, string provider)> matches, string primaryProvider)
        {
            var reconciledBook = new ReconciledBook
            {
                MergedBook = primaryBook,
                MatchedProviders = new List<string> { primaryProvider },
                PrimarySource = primaryProvider,
                ConfidenceScore = 1.0m
            };

            // Set primary provider ID
            SetProviderIdForBook(reconciledBook, primaryBook, primaryProvider);

            // Merge metadata from matches
            foreach (var (matchBook, matchProvider) in matches)
            {
                reconciledBook.MatchedProviders.Add(matchProvider);
                SetProviderIdForBook(reconciledBook, matchBook, matchProvider);

                // Merge additional metadata (prefer non-null values)
                if (primaryBook.ReleaseDate == null && matchBook.ReleaseDate != null)
                {
                    primaryBook.ReleaseDate = matchBook.ReleaseDate;
                }

                // Merge ratings (prefer higher vote count)
                if (matchBook.Ratings != null && matchBook.Ratings.Votes > (primaryBook.Ratings?.Votes ?? 0))
                {
                    primaryBook.Ratings = matchBook.Ratings;
                }

                // Merge links
                if (matchBook.Links != null && matchBook.Links.Any())
                {
                    foreach (var link in matchBook.Links)
                    {
                        if (!primaryBook.Links.Any(l => l.Url == link.Url))
                        {
                            primaryBook.Links.Add(link);
                        }
                    }
                }

                // Merge edition-level metadata (critical for ToResource() which reads from editions)
                if (matchBook.Editions != null && matchBook.Editions.Value != null && matchBook.Editions.Value.Any())
                {
                    var matchEdition = matchBook.Editions.Value.FirstOrDefault(e => e.Monitored);
                    if (matchEdition != null && primaryBook.Editions != null && primaryBook.Editions.Value != null)
                    {
                        var primaryEdition = primaryBook.Editions.Value.FirstOrDefault(e => e.Monitored);
                        if (primaryEdition != null)
                        {
                            // Merge ratings (prefer higher vote count)
                            if (matchEdition.Ratings != null && matchEdition.Ratings.Votes > (primaryEdition.Ratings?.Votes ?? 0))
                            {
                                primaryEdition.Ratings = matchEdition.Ratings;
                            }

                            // Merge page count (prefer non-zero values)
                            if (primaryEdition.PageCount == 0 && matchEdition.PageCount > 0)
                            {
                                primaryEdition.PageCount = matchEdition.PageCount;
                            }

                            // Merge overview (prefer longer/more detailed)
                            if (string.IsNullOrWhiteSpace(primaryEdition.Overview) && !string.IsNullOrWhiteSpace(matchEdition.Overview))
                            {
                                primaryEdition.Overview = matchEdition.Overview;
                            }

                            // Merge images
                            if (matchEdition.Images != null && matchEdition.Images.Any())
                            {
                                if (primaryEdition.Images == null)
                                {
                                    primaryEdition.Images = new List<MediaCover.MediaCover>();
                                }

                                foreach (var image in matchEdition.Images)
                                {
                                    if (!primaryEdition.Images.Any(i => i.Url == image.Url))
                                    {
                                        primaryEdition.Images.Add(image);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Calculate confidence based on number of matches and match types
            if (matches.Any())
            {
                reconciledBook.ConfidenceScore = Math.Min(1.0m, 0.7m + (0.1m * matches.Count));
            }

            return reconciledBook;
        }

        private void SetProviderIdForBook(ReconciledBook reconciledBook, Book book, string provider)
        {
            switch (provider.ToLower())
            {
                case "hardcover":
                    reconciledBook.HardcoverId = book.HardcoverBookId ?? book.ForeignBookId;
                    break;
                case "openlibrary":
                    reconciledBook.OpenLibraryId = book.OpenLibraryWorkId ?? book.ForeignBookId;
                    break;
                case "googlebooks":
                    reconciledBook.GoogleBooksId = book.GoogleBooksId ?? book.ForeignBookId;
                    break;
                case "comicvine":
                    reconciledBook.ComicVineId = book.ComicVineIssueId ?? book.ForeignBookId;
                    break;
                case "goodreads":
                    reconciledBook.GoodreadsId = book.ForeignBookId;
                    break;
            }
        }

        private ReconciledAuthor MergeAuthorMetadata(Author primaryAuthor, List<(Author author, string provider)> matches, string primaryProvider)
        {
            var reconciledAuthor = new ReconciledAuthor
            {
                MergedAuthor = primaryAuthor,
                MatchedProviders = new List<string> { primaryProvider },
                PrimarySource = primaryProvider,
                ConfidenceScore = 1.0m
            };

            // Set primary provider ID
            SetProviderIdForAuthor(reconciledAuthor, primaryAuthor, primaryProvider);

            // Merge metadata from matches
            foreach (var (matchAuthor, matchProvider) in matches)
            {
                reconciledAuthor.MatchedProviders.Add(matchProvider);
                SetProviderIdForAuthor(reconciledAuthor, matchAuthor, matchProvider);

                var primaryMetadata = primaryAuthor.Metadata?.Value;
                var matchMetadata = matchAuthor.Metadata?.Value;

                if (primaryMetadata != null && matchMetadata != null)
                {
                    // Merge bio (prefer longer/more detailed)
                    if (string.IsNullOrWhiteSpace(primaryMetadata.Overview) && !string.IsNullOrWhiteSpace(matchMetadata.Overview))
                    {
                        primaryMetadata.Overview = matchMetadata.Overview;
                    }

                    // Merge images
                    if (matchMetadata.Images != null && matchMetadata.Images.Any())
                    {
                        foreach (var image in matchMetadata.Images)
                        {
                            if (!primaryMetadata.Images.Any(i => i.Url == image.Url))
                            {
                                primaryMetadata.Images.Add(image);
                            }
                        }
                    }

                    // Merge links
                    if (matchMetadata.Links != null && matchMetadata.Links.Any())
                    {
                        foreach (var link in matchMetadata.Links)
                        {
                            if (!primaryMetadata.Links.Any(l => l.Url == link.Url))
                            {
                                primaryMetadata.Links.Add(link);
                            }
                        }
                    }
                }
            }

            // Calculate confidence
            if (matches.Any())
            {
                reconciledAuthor.ConfidenceScore = Math.Min(1.0m, 0.7m + (0.1m * matches.Count));
            }

            return reconciledAuthor;
        }

        private void SetProviderIdForAuthor(ReconciledAuthor reconciledAuthor, Author author, string provider)
        {
            var metadata = author.Metadata?.Value;
            if (metadata == null)
            {
                return;
            }

            switch (provider.ToLower())
            {
                case "hardcover":
                    reconciledAuthor.HardcoverId = metadata.HardcoverAuthorId ?? metadata.ForeignAuthorId;
                    break;
                case "openlibrary":
                    reconciledAuthor.OpenLibraryId = metadata.OpenLibraryAuthorId ?? metadata.ForeignAuthorId;
                    break;
                case "googlebooks":
                    reconciledAuthor.GoogleBooksId = metadata.GoogleBooksAuthorId ?? metadata.ForeignAuthorId;
                    break;
                case "comicvine":
                    reconciledAuthor.ComicVineId = metadata.ComicVinePersonId ?? metadata.ForeignAuthorId;
                    break;
                case "goodreads":
                    reconciledAuthor.GoodreadsId = metadata.ForeignAuthorId;
                    break;
            }
        }

        private string GetProvider(Book book)
        {
            if (!string.IsNullOrWhiteSpace(book.HardcoverBookId))
            {
                return "hardcover";
            }

            if (!string.IsNullOrWhiteSpace(book.OpenLibraryWorkId))
            {
                return "openlibrary";
            }

            if (!string.IsNullOrWhiteSpace(book.GoogleBooksId))
            {
                return "googlebooks";
            }

            if (!string.IsNullOrWhiteSpace(book.ComicVineIssueId))
            {
                return "comicvine";
            }

            return "unknown";
        }

        private string GetProvider(Author author)
        {
            var metadata = author.Metadata?.Value;
            if (metadata == null)
            {
                return "unknown";
            }

            if (!string.IsNullOrWhiteSpace(metadata.HardcoverAuthorId))
            {
                return "hardcover";
            }

            if (!string.IsNullOrWhiteSpace(metadata.OpenLibraryAuthorId))
            {
                return "openlibrary";
            }

            if (!string.IsNullOrWhiteSpace(metadata.GoogleBooksAuthorId))
            {
                return "googlebooks";
            }

            if (!string.IsNullOrWhiteSpace(metadata.ComicVinePersonId))
            {
                return "comicvine";
            }

            return "unknown";
        }
    }
}
