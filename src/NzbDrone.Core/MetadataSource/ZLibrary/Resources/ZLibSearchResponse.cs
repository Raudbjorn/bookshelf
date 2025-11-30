using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.ZLibrary.Resources
{
    public class ZLibSearchResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("books")]
        public List<ZLibBook> Books { get; set; }
    }
}
