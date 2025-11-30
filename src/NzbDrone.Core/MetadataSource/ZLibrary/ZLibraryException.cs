using System;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.ZLibrary
{
    public class ZLibraryException : NzbDroneException
    {
        public ZLibraryException(string message, params object[] args)
            : base(message, args)
        {
        }

        public ZLibraryException(string message, Exception innerException, params object[] args)
            : base(message, innerException, args)
        {
        }
    }
}
