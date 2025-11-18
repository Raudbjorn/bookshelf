using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(42)]
    public class AddMultiProviderIdsToAuthors : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add provider ID columns to AuthorMetadata table
            Alter.Table("AuthorMetadata")
                .AddColumn("HardcoverAuthorId").AsString().Nullable()
                .AddColumn("OpenLibraryAuthorId").AsString().Nullable()
                .AddColumn("GoogleBooksAuthorId").AsString().Nullable();

            // Create indexes for fast lookups
            Create.Index("IX_AuthorMetadata_HardcoverAuthorId")
                .OnTable("AuthorMetadata")
                .OnColumn("HardcoverAuthorId");

            Create.Index("IX_AuthorMetadata_OpenLibraryAuthorId")
                .OnTable("AuthorMetadata")
                .OnColumn("OpenLibraryAuthorId");

            Create.Index("IX_AuthorMetadata_GoogleBooksAuthorId")
                .OnTable("AuthorMetadata")
                .OnColumn("GoogleBooksAuthorId");
        }
    }
}
