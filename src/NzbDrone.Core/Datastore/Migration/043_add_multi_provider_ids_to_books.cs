using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(43)]
    public class AddMultiProviderIdsToBooks : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add provider ID columns to Books table
            Alter.Table("Books")
                .AddColumn("HardcoverBookId").AsString().Nullable()
                .AddColumn("OpenLibraryWorkId").AsString().Nullable()
                .AddColumn("GoogleBooksId").AsString().Nullable();

            // Create indexes for fast lookups
            Create.Index("IX_Books_HardcoverBookId")
                .OnTable("Books")
                .OnColumn("HardcoverBookId");

            Create.Index("IX_Books_OpenLibraryWorkId")
                .OnTable("Books")
                .OnColumn("OpenLibraryWorkId");

            Create.Index("IX_Books_GoogleBooksId")
                .OnTable("Books")
                .OnColumn("GoogleBooksId");
        }
    }
}
