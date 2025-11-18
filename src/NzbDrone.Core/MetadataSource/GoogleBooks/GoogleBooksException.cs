using System;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.GoogleBooks
{
    public class GoogleBooksException : NzbDroneException
    {
        public GoogleBooksException(string message)
            : base(message)
        {
        }

        public GoogleBooksException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
