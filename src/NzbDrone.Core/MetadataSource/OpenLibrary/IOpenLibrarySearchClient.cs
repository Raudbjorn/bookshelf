using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public interface IOpenLibrarySearchClient
    {
        List<object> Search(string query);
    }
}
