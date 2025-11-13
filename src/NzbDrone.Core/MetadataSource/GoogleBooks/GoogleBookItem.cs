using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public class GoogleBookItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("volumeInfo")]
        public GoogleVolumeInfo VolumeInfo { get; set; }
    }
}
