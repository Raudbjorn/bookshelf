using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(44)]
    public class AddMultiProviderIdsToEditions : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add provider ID columns to Editions table
            Alter.Table("Editions")
                .AddColumn("HardcoverEditionId").AsString().Nullable()
                .AddColumn("OpenLibraryEditionId").AsString().Nullable()
                .AddColumn("GoogleBooksEditionId").AsString().Nullable()
                .AddColumn("AudibleASIN").AsString().Nullable();

            // Create indexes for fast lookups
            Create.Index("IX_Editions_HardcoverEditionId")
                .OnTable("Editions")
                .OnColumn("HardcoverEditionId");

            Create.Index("IX_Editions_OpenLibraryEditionId")
                .OnTable("Editions")
                .OnColumn("OpenLibraryEditionId");

            Create.Index("IX_Editions_GoogleBooksEditionId")
                .OnTable("Editions")
                .OnColumn("GoogleBooksEditionId");

            // Create partial index for AudibleASIN (only non-null values)
            Create.Index("IX_Editions_AudibleASIN")
                .OnTable("Editions")
                .OnColumn("AudibleASIN");
        }
    }
}
