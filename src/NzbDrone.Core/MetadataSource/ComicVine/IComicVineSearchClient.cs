using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.ComicVine
{
    public interface IComicVineSearchClient
    {
        List<object> Search(string searchTerm);
    }
}
