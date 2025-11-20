using System.Collections.Generic;
using Readarr.Api.V1.Author;
using Readarr.Api.V1.Books;

namespace Readarr.Api.V1.Search
{
    public class ReconciledSearchResource
    {
        public string Query { get; set; }
        public List<ReconciledBookResource> Books { get; set; }
        public List<ReconciledAuthorResource> Authors { get; set; }
    }

    public class ReconciledBookResource
    {
        public BookResource Book { get; set; }
        public string HardcoverId { get; set; }
        public string OpenLibraryId { get; set; }
        public string GoogleBooksId { get; set; }
        public string ComicVineId { get; set; }
        public string GoodreadsId { get; set; }
        public List<string> MatchedProviders { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string PrimarySource { get; set; }
    }

    public class ReconciledAuthorResource
    {
        public AuthorResource Author { get; set; }
        public string HardcoverId { get; set; }
        public string OpenLibraryId { get; set; }
        public string GoogleBooksId { get; set; }
        public string ComicVineId { get; set; }
        public string GoodreadsId { get; set; }
        public List<string> MatchedProviders { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string PrimarySource { get; set; }
    }
}
