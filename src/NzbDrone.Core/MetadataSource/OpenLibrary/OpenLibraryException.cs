using System;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.OpenLibrary
{
    public class OpenLibraryException : NzbDroneException
    {
        public OpenLibraryException(string message)
            : base(message)
        {
        }

        public OpenLibraryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
