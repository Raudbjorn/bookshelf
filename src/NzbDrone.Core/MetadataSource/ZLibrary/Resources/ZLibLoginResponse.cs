using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.ZLibrary.Resources
{
    public class ZLibLoginResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("user")]
        public ZLibUser User { get; set; }
    }

    public class ZLibUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("remix_userid")]
        public string RemixUserId { get; set; }

        [JsonPropertyName("remix_userkey")]
        public string RemixUserKey { get; set; }

        [JsonPropertyName("downloads_limit")]
        public int DownloadsLimit { get; set; }

        [JsonPropertyName("downloads_today")]
        public int DownloadsToday { get; set; }
    }
}
