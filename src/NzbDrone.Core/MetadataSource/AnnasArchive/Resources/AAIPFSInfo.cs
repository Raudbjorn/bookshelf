using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// IPFS download information for decentralized file access
    /// </summary>
    public class AAIPFSInfo
    {
        [JsonPropertyName("cid")]
        public string Cid { get; set; }

        [JsonPropertyName("filename")]
        public string Filename { get; set; }

        [JsonPropertyName("from")]
        public string From { get; set; }
    }
}
