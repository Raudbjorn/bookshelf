using System;
using System.Net;
using NzbDrone.Core.Exceptions;

namespace NzbDrone.Core.MetadataSource.InternetArchive
{
    public class InternetArchiveException : NzbDroneClientException
    {
        public InternetArchiveException(string message)
            : base(HttpStatusCode.ServiceUnavailable, message)
        {
        }

        public InternetArchiveException(string message, params object[] args)
            : base(HttpStatusCode.ServiceUnavailable, message, args)
        {
        }

        public InternetArchiveException(string message, Exception innerException, params object[] args)
            : base(HttpStatusCode.ServiceUnavailable, message, innerException, args)
        {
        }
    }
}
