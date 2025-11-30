using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// Metadata from Z-Library (b-ok.cc / 1lib.sk)
    /// </summary>
    public class AAZLibBook
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("extension")]
        public string Extension { get; set; }

        [JsonPropertyName("filesize")]
        public long? Filesize { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("pages")]
        public int? Pages { get; set; }

        [JsonPropertyName("isbn")]
        public string Isbn { get; set; }
    }
}
