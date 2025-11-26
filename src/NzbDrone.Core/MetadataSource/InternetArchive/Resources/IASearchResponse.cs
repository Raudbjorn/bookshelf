using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.InternetArchive
{
    public class IASearchResponse
    {
        [JsonPropertyName("responseHeader")]
        public IAResponseHeader ResponseHeader { get; set; }

        [JsonPropertyName("response")]
        public IAResponse Response { get; set; }
    }

    public class IAResponseHeader
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("QTime")]
        public int QTime { get; set; }
    }

    public class IAResponse
    {
        [JsonPropertyName("numFound")]
        public int NumFound { get; set; }

        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("docs")]
        public List<IASearchDoc> Docs { get; set; } = new List<IASearchDoc>();
    }

    public class IASearchDoc
    {
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("creator")]
        public List<string> Creator { get; set; } = new List<string>();

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("subject")]
        public List<string> Subject { get; set; } = new List<string>();

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("publisher")]
        public string Publisher { get; set; }

        [JsonPropertyName("language")]
        public List<string> Language { get; set; } = new List<string>();

        [JsonPropertyName("mediatype")]
        public string Mediatype { get; set; }

        [JsonPropertyName("downloads")]
        public int? Downloads { get; set; }

        [JsonPropertyName("collection")]
        public List<string> Collection { get; set; } = new List<string>();

        [JsonPropertyName("avg_rating")]
        public double? AvgRating { get; set; }

        [JsonPropertyName("num_reviews")]
        public int? NumReviews { get; set; }
    }
}
