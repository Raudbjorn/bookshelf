using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public class GoogleIndustryIdentifier
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("identifier")]
        public string Identifier { get; set; }
    }
}
