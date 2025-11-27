using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// Unified file data from Anna's Archive aggregation
    /// This is the most reliable source of metadata
    /// </summary>
    public class AAFileUnifiedData
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("year")]
        public string Year { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("filesize")]
        public long? Filesize { get; set; }

        [JsonPropertyName("extension")]
        public string Extension { get; set; }

        [JsonPropertyName("md5")]
        public string Md5 { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("edition_varia")]
        public string EditionVaria { get; set; }
    }
}
