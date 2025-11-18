using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public class GoogleBooksResponse
    {
        [JsonPropertyName("items")]
        public List<GoogleBookItem> Items { get; set; }

        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }
    }
}
