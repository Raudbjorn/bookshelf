using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.ComicVine
{
    public interface IComicVineSearchClient
    {
        List<ComicVineIssueResult> Search(string searchTerm);
    }
}
