using System;

namespace NzbDrone.Core.MetadataSource.AnnasArchive
{
    public class AnnasArchiveException : Exception
    {
        public AnnasArchiveException(string message)
            : base(message)
        {
        }

        public AnnasArchiveException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public AnnasArchiveException(string message, params object[] args)
            : base(string.Format(message, args))
        {
        }
    }
}
