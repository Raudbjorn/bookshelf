namespace NzbDrone.Core.MetadataSource.Hardcover
{
    public class HardcoverSeriesResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public string Description { get; set; }
        public int BooksCount { get; set; }
        public int PrimaryBooksCount { get; set; }
        public int ReadersCount { get; set; }
        public string AuthorName { get; set; }
    }
}
