using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.ComicVine
{
    public class ComicVineSearchResponse
    {
        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("number_of_page_results")]
        public int NumberOfPageResults { get; set; }

        [JsonPropertyName("number_of_total_results")]
        public int NumberOfTotalResults { get; set; }

        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }

        [JsonPropertyName("results")]
        public ComicVineIssueResult[] Results { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}
