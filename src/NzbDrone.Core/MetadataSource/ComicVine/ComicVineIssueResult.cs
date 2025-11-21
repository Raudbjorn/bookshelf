using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.ComicVine
{
    public class ComicVineIssueResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("issue_number")]
        public string IssueNumber { get; set; }

        [JsonPropertyName("volume")]
        public ComicVineVolume Volume { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("image")]
        public ComicVineImage Image { get; set; }

        [JsonPropertyName("store_date")]
        public string StoreDate { get; set; }

        [JsonPropertyName("cover_date")]
        public string CoverDate { get; set; }

        [JsonPropertyName("person_credits")]
        public ComicVinePersonCredit[] PersonCredits { get; set; }
    }

    public class ComicVineVolume
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("api_detail_url")]
        public string ApiDetailUrl { get; set; }
    }

    public class ComicVineImage
    {
        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; }

        [JsonPropertyName("medium_url")]
        public string MediumUrl { get; set; }

        [JsonPropertyName("screen_url")]
        public string ScreenUrl { get; set; }

        [JsonPropertyName("screen_large_url")]
        public string ScreenLargeUrl { get; set; }

        [JsonPropertyName("small_url")]
        public string SmallUrl { get; set; }

        [JsonPropertyName("super_url")]
        public string SuperUrl { get; set; }

        [JsonPropertyName("thumb_url")]
        public string ThumbUrl { get; set; }

        [JsonPropertyName("tiny_url")]
        public string TinyUrl { get; set; }

        [JsonPropertyName("original_url")]
        public string OriginalUrl { get; set; }

        [JsonPropertyName("image_tags")]
        public string ImageTags { get; set; }
    }

    public class ComicVinePersonCredit
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }
    }
}
