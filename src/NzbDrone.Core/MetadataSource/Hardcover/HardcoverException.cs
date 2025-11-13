using System;
using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.Hardcover
{
    public class HardcoverException : NzbDroneException
    {
        public HardcoverException(string message)
            : base(message)
        {
        }

        public HardcoverException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
