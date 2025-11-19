using System.Collections.Generic;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    public interface IBookReconciliationService
    {
        List<ReconciledBook> ReconcileBooks(List<Book> hardcoverBooks, List<Book> openLibraryBooks, List<Book> googleBooksBooks);
        List<ReconciledAuthor> ReconcileAuthors(List<Author> hardcoverAuthors, List<Author> openLibraryAuthors, List<Author> googleBooksAuthors);
    }

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
