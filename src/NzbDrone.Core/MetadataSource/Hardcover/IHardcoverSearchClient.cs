using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.Hardcover
{
    public interface IHardcoverSearchClient
    {
        List<object> Search(string query);
    }
}
