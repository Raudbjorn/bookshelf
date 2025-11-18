using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public class GoogleVolumeInfo
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("subtitle")]
        public string Subtitle { get; set; }

        [JsonPropertyName("authors")]
        public List<string> Authors { get; set; }

        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("publishedDate")]
        public string PublishedDate { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("industryIdentifiers")]
        public List<GoogleIndustryIdentifier> IndustryIdentifiers { get; set; }

        [JsonPropertyName("pageCount")]
        public int? PageCount { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("averageRating")]
        public double? AverageRating { get; set; }

        [JsonPropertyName("ratingsCount")]
        public int? RatingsCount { get; set; }

        [JsonPropertyName("imageLinks")]
        public GoogleImageLinks ImageLinks { get; set; }

        [JsonPropertyName("infoLink")]
        public string InfoLink { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }
    }
}
