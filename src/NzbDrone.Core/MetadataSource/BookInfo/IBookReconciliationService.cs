using System.Collections.Generic;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    public interface IBookReconciliationService
    {
        /// <summary>
        /// Reconciles books from multiple providers by matching them and merging metadata
        /// </summary>
        List<ReconciledBook> ReconcileBooks(List<Book> hardcoverBooks, List<Book> openLibraryBooks, List<Book> googleBooksBooks);

        /// <summary>
        /// Reconciles authors from multiple providers by matching them and merging metadata
        /// </summary>
        List<ReconciledAuthor> ReconcileAuthors(List<Author> hardcoverAuthors, List<Author> openLibraryAuthors, List<Author> googleBooksAuthors);
    }

    /// <summary>
    /// A book that has been matched across multiple providers with merged metadata
    /// </summary>
    public class ReconciledBook
    {
        public Book MergedBook { get; set; }
        public string HardcoverId { get; set; }
        public string OpenLibraryId { get; set; }
        public string GoogleBooksId { get; set; }
        public string GoodreadsId { get; set; }
        public List<string> MatchedProviders { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string PrimarySource { get; set; }
    }

    /// <summary>
    /// An author that has been matched across multiple providers with merged metadata
    /// </summary>
    public class ReconciledAuthor
    {
        public Author MergedAuthor { get; set; }
        public string HardcoverId { get; set; }
        public string OpenLibraryId { get; set; }
        public string GoogleBooksId { get; set; }
        public string GoodreadsId { get; set; }
        public List<string> MatchedProviders { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string PrimarySource { get; set; }
    }
}
