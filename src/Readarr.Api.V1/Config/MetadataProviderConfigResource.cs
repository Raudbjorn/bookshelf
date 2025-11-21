using NzbDrone.Core.Configuration;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Config
{
    public class MetadataProviderConfigResource : RestResource
    {
        public WriteAudioTagsType WriteAudioTags { get; set; }
        public bool ScrubAudioTags { get; set; }
        public WriteBookTagsType WriteBookTags { get; set; }
        public bool UpdateCovers { get; set; }
        public bool EmbedMetadata { get; set; }
        public bool HardcoverEnabled { get; set; }
        public string HardcoverApiToken { get; set; }
        public bool GoogleBooksEnabled { get; set; }
        public bool OpenLibraryEnabled { get; set; }
        public bool ComicVineEnabled { get; set; }
        public string ComicVineApiKey { get; set; }
    }

    public static class MetadataProviderConfigResourceMapper
    {
        public static MetadataProviderConfigResource ToResource(IConfigService model)
        {
            return new MetadataProviderConfigResource
            {
                WriteAudioTags = model.WriteAudioTags,
                ScrubAudioTags = model.ScrubAudioTags,
                WriteBookTags = model.WriteBookTags,
                UpdateCovers = model.UpdateCovers,
                EmbedMetadata = model.EmbedMetadata,
                HardcoverEnabled = model.HardcoverEnabled,
                HardcoverApiToken = model.HardcoverApiToken,
                GoogleBooksEnabled = model.GoogleBooksEnabled,
                OpenLibraryEnabled = model.OpenLibraryEnabled,
                ComicVineEnabled = model.ComicVineEnabled,
                ComicVineApiKey = model.ComicVineApiKey
            };
        }
    }
}
