using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.InternetArchive
{
    public class IAMetadataResponse
    {
        public long Created { get; set; }
        public string Dir { get; set; }
        public int Files_count { get; set; }
        public long Item_last_updated { get; set; }
        public long Item_size { get; set; }
        public IAMetadata Metadata { get; set; }
        public List<IAFile> Files { get; set; } = new List<IAFile>();
        public string Server { get; set; }
    }

    public class IAMetadata
    {
        public string Identifier { get; set; }
        public string Title { get; set; }
        public object Creator { get; set; } // Can be string or List<string>
        public object Date { get; set; } // Can be string or List<string>
        public object Publisher { get; set; } // Can be string or List<string>
        public object Description { get; set; } // Can be string or List<string>
        public object Subject { get; set; } // Can be string or List<string>
        public object Language { get; set; } // Can be string or List<string>
        public object Isbn { get; set; } // Can be string or List<string>
        public string Mediatype { get; set; }
        public object Collection { get; set; } // Can be string or List<string>
        public int? Downloads { get; set; }
        public double? Avg_rating { get; set; }
        public int? Num_reviews { get; set; }
    }

    public class IAFile
    {
        public string Name { get; set; }
        public string Format { get; set; }
        public string Size { get; set; }
        public string Md5 { get; set; }
        public string Crc32 { get; set; }
        public string Sha1 { get; set; }
    }
}
