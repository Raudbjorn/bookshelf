using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public class OpenLibrarySearchResult
    {
        [JsonPropertyName("docs")]
        public List<OpenLibrarySearchDoc> Docs { get; set; }

        [JsonPropertyName("numFound")]
        public int NumFound { get; set; }

        [JsonPropertyName("start")]
        public int Start { get; set; }
    }
}
