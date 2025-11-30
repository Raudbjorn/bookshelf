using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// Metadata from Libgen.rs (Library Genesis)
    /// Used for both non-fiction (lgrsnf_book) and fiction (lgrsfic_book)
    /// </summary>
    public class AALibgenBook
    {
        [JsonPropertyName("Title")]
        public string Title { get; set; }

        [JsonPropertyName("Author")]
        public string Author { get; set; }

        [JsonPropertyName("ISBN")]
        public string ISBN { get; set; }

        [JsonPropertyName("Publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("Year")]
        public string Year { get; set; }

        [JsonPropertyName("Pages")]
        public string Pages { get; set; }

        [JsonPropertyName("Language")]
        public string Language { get; set; }

        [JsonPropertyName("Topic")]
        public string Topic { get; set; }

        [JsonPropertyName("Library")]
        public string Library { get; set; }

        [JsonPropertyName("Issue")]
        public string Issue { get; set; }

        [JsonPropertyName("Series")]
        public string Series { get; set; }

        [JsonPropertyName("Edition")]
        public string Edition { get; set; }

        [JsonPropertyName("VolumeInfo")]
        public string VolumeInfo { get; set; }

        [JsonPropertyName("Descr")]
        public string Description { get; set; }
    }
}
