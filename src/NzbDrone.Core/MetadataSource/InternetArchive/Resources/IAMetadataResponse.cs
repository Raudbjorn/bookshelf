using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.InternetArchive
{
    public class IAMetadataResponse
    {
        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("dir")]
        public string Dir { get; set; }

        [JsonPropertyName("files_count")]
        public int FilesCount { get; set; }

        [JsonPropertyName("item_last_updated")]
        public long ItemLastUpdated { get; set; }

        [JsonPropertyName("item_size")]
        public long ItemSize { get; set; }

        [JsonPropertyName("metadata")]
        public IAMetadata Metadata { get; set; }

        [JsonPropertyName("files")]
        public List<IAFile> Files { get; set; } = new List<IAFile>();

        [JsonPropertyName("server")]
        public string Server { get; set; }
    }

    public class IAMetadata
    {
        [JsonPropertyName("identifier")]
        public string Identifier { get; set; }

        [JsonPropertyName("title")]
        public object Title { get; set; } // Can be string or List<string>

        [JsonPropertyName("creator")]
        public object Creator { get; set; } // Can be string or List<string>

        [JsonPropertyName("date")]
        public object Date { get; set; } // Can be string or List<string>

        [JsonPropertyName("publisher")]
        public object Publisher { get; set; } // Can be string or List<string>

        [JsonPropertyName("description")]
        public object Description { get; set; } // Can be string or List<string>

        [JsonPropertyName("subject")]
        public object Subject { get; set; } // Can be string or List<string>

        [JsonPropertyName("language")]
        public object Language { get; set; } // Can be string or List<string>

        [JsonPropertyName("isbn")]
        public object Isbn { get; set; } // Can be string or List<string>

        [JsonPropertyName("mediatype")]
        public string Mediatype { get; set; }

        [JsonPropertyName("collection")]
        public object Collection { get; set; } // Can be string or List<string>

        [JsonPropertyName("downloads")]
        public int? Downloads { get; set; }

        [JsonPropertyName("avg_rating")]
        public double? AvgRating { get; set; }

        [JsonPropertyName("num_reviews")]
        public int? NumReviews { get; set; }
    }

    public class IAFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; }

        [JsonPropertyName("md5")]
        public string Md5 { get; set; }

        [JsonPropertyName("crc32")]
        public string Crc32 { get; set; }

        [JsonPropertyName("sha1")]
        public string Sha1 { get; set; }
    }
}
