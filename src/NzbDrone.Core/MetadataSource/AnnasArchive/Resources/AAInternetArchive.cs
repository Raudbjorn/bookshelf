using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// Metadata from Internet Archive (ia_record)
    /// </summary>
    public class AAInternetArchive
    {
        [JsonPropertyName("aa_ia_file")]
        public AAInternetArchiveFile File { get; set; }

        [JsonPropertyName("metadata")]
        public AAInternetArchiveMetadata Metadata { get; set; }
    }

    public class AAInternetArchiveFile
    {
        [JsonPropertyName("md5")]
        public string Md5 { get; set; }

        [JsonPropertyName("ia_id")]
        public string IaId { get; set; }

        [JsonPropertyName("extension")]
        public string Extension { get; set; }

        [JsonPropertyName("filesize")]
        public long? Filesize { get; set; }
    }

    public class AAInternetArchiveMetadata
    {
        [JsonPropertyName("title")]
        public object Title { get; set; } // Can be string or array

        [JsonPropertyName("creator")]
        public object Creator { get; set; } // Can be string or array

        [JsonPropertyName("publisher")]
        public object Publisher { get; set; } // Can be string or array

        [JsonPropertyName("date")]
        public object Date { get; set; } // Can be string or array

        [JsonPropertyName("language")]
        public object Language { get; set; } // Can be string or array

        [JsonPropertyName("description")]
        public object Description { get; set; } // Can be string or array

        [JsonPropertyName("isbn")]
        public object Isbn { get; set; } // Can be string or array
    }
}
