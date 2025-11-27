using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// Main Anna's Archive record structure
    /// Aggregates metadata from multiple sources: Libgen, Z-Library, Internet Archive, ISBNdb, OpenLibrary, etc.
    /// </summary>
    public class AARecord
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } // Format: "md5:hash"

        /// <summary>
        /// Unified file data - most reliable source, aggregated from all sources
        /// </summary>
        [JsonPropertyName("file_unified_data")]
        public AAFileUnifiedData FileUnifiedData { get; set; }

        /// <summary>
        /// IPFS download information for decentralized access
        /// </summary>
        [JsonPropertyName("ipfs_infos")]
        public List<AAIPFSInfo> IpfsInfos { get; set; }

        /// <summary>
        /// Libgen.rs non-fiction data
        /// </summary>
        [JsonPropertyName("lgrsnf_book")]
        public AALibgenBook LibgenNonFiction { get; set; }

        /// <summary>
        /// Libgen.rs fiction data
        /// </summary>
        [JsonPropertyName("lgrsfic_book")]
        public AALibgenBook LibgenFiction { get; set; }

        /// <summary>
        /// Z-Library data
        /// </summary>
        [JsonPropertyName("zlib_book")]
        public AAZLibBook ZLibrary { get; set; }

        /// <summary>
        /// ISBNdb metadata
        /// </summary>
        [JsonPropertyName("isbndb")]
        public AAIsbndb IsbnDb { get; set; }

        /// <summary>
        /// OpenLibrary metadata
        /// </summary>
        [JsonPropertyName("ol")]
        public AAOpenLibrary OpenLibrary { get; set; }

        /// <summary>
        /// Internet Archive metadata
        /// </summary>
        [JsonPropertyName("ia_record")]
        public AAInternetArchive InternetArchive { get; set; }

        /// <summary>
        /// Additional search-only fields
        /// </summary>
        [JsonPropertyName("search_only_fields")]
        public Dictionary<string, object> SearchOnlyFields { get; set; }
    }
}
