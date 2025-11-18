namespace NzbDrone.Core.MetadataSource.Hardcover
{
    public class HardcoverAuthorResult
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string[] AlternateNames { get; set; }
        public string Bio { get; set; }
        public int BooksCount { get; set; }
        public string Slug { get; set; }
        public string BornDate { get; set; }
        public string DeathDate { get; set; }
        public string ImageUrl { get; set; }
    }
}
