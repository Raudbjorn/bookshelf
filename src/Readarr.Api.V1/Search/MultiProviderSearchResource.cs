using System.Collections.Generic;

namespace Readarr.Api.V1.Search
{
    public class MultiProviderSearchResource
    {
        public string Query { get; set; }
        public List<SearchResource> Hardcover { get; set; }
        public List<SearchResource> OpenLibrary { get; set; }
        public List<SearchResource> GoogleBooks { get; set; }
        public List<SearchResource> ComicVine { get; set; }
    }
}
