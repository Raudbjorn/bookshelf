using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.ZLibrary.Resources
{
    public class ZLibBookDetailResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("book")]
        public ZLibBook Book { get; set; }
    }
}
