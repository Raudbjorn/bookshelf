using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.AnnasArchive.Resources
{
    /// <summary>
    /// Metadata from OpenLibrary (ol)
    /// </summary>
    public class AAOpenLibrary
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("authors")]
        public List<AAOpenLibraryAuthor> Authors { get; set; } = new List<AAOpenLibraryAuthor>();

        [JsonPropertyName("publishers")]
        public List<string> Publishers { get; set; } = new List<string>();

        [JsonPropertyName("publish_date")]
        public string PublishDate { get; set; }

        [JsonPropertyName("isbn_13")]
        public List<string> Isbn13 { get; set; } = new List<string>();

        [JsonPropertyName("isbn_10")]
        public List<string> Isbn10 { get; set; } = new List<string>();

        [JsonPropertyName("number_of_pages")]
        public int? NumberOfPages { get; set; }

        [JsonPropertyName("languages")]
        public List<string> Languages { get; set; } = new List<string>();

        [JsonPropertyName("subjects")]
        public List<string> Subjects { get; set; } = new List<string>();

        [JsonPropertyName("description")]
        public object Description { get; set; } // Can be string or object with 'value' field
    }

    public class AAOpenLibraryAuthor
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
