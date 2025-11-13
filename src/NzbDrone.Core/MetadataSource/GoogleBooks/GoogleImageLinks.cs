using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public class GoogleImageLinks
    {
        [JsonPropertyName("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonPropertyName("small")]
        public string Small { get; set; }

        [JsonPropertyName("medium")]
        public string Medium { get; set; }

        [JsonPropertyName("large")]
        public string Large { get; set; }

        [JsonPropertyName("extraLarge")]
        public string ExtraLarge { get; set; }
    }
}
