namespace NzbDrone.Core.MetadataSource.Hardcover
{
    public class HardcoverBookResult
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Description { get; set; }
        public string[] AuthorNames { get; set; }
        public string[] AuthorIds { get; set; }
        public string[] SeriesNames { get; set; }
        public string[] Isbns { get; set; }
        public float Rating { get; set; }
        public int Pages { get; set; }
        public string ReleaseDate { get; set; }
        public string ImageUrl { get; set; }
    }
}
