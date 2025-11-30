using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// Metadata from ISBNdb
    /// Typically has high-quality, structured metadata
    /// </summary>
    public class AAIsbndb
    {
        [JsonPropertyName("isbn13")]
        public string Isbn13 { get; set; }

        [JsonPropertyName("isbn10")]
        public string Isbn10 { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("authors")]
        public List<string> Authors { get; set; } = new List<string>();

        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("date_published")]
        public string DatePublished { get; set; }

        [JsonPropertyName("edition")]
        public string Edition { get; set; }

        [JsonPropertyName("pages")]
        public int? Pages { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("synopsis")]
        public string Synopsis { get; set; }

        [JsonPropertyName("binding")]
        public string Binding { get; set; }
    }
}
