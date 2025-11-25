using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.InternetArchive
{
    public class IASearchResponse
    {
        public IAResponseHeader ResponseHeader { get; set; }
        public IAResponse Response { get; set; }
    }

    public class IAResponseHeader
    {
        public int Status { get; set; }
        public int QTime { get; set; }
    }

    public class IAResponse
    {
        public int NumFound { get; set; }
        public int Start { get; set; }
        public List<IASearchDoc> Docs { get; set; } = new List<IASearchDoc>();
    }

    public class IASearchDoc
    {
        public string Identifier { get; set; }
        public string Title { get; set; }
        public List<string> Creator { get; set; } = new List<string>();
        public string Date { get; set; }
        public List<string> Subject { get; set; } = new List<string>();
        public string Description { get; set; }
        public string Publisher { get; set; }
        public List<string> Language { get; set; } = new List<string>();
        public string Mediatype { get; set; }
        public int? Downloads { get; set; }
        public List<string> Collection { get; set; } = new List<string>();
        public double? Avg_rating { get; set; }
        public int? Num_reviews { get; set; }
    }
}
