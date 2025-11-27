using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.ZLibrary.Resources
{
    public class ZLibBook
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("extension")]
        public string Extension { get; set; }

        [JsonPropertyName("filesize")]
        public object Filesize { get; set; } // Can be string or number

        [JsonPropertyName("href")]
        public string Href { get; set; }

        [JsonPropertyName("year")]
        public string Year { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("cover")]
        public string Cover { get; set; }
    }
}
