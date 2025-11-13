using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public interface IGoogleBooksSearchClient
    {
        List<object> Search(string query);
    }
}
