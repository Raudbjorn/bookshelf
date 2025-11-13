using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(45)]
    public class AddMultiProviderIdsToSeries : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add provider ID columns to Series table
            Alter.Table("Series")
                .AddColumn("HardcoverSeriesId").AsString().Nullable()
                .AddColumn("OpenLibrarySeriesId").AsString().Nullable();

            // Create indexes for fast lookups
            Create.Index("IX_Series_HardcoverSeriesId")
                .OnTable("Series")
                .OnColumn("HardcoverSeriesId");

            Create.Index("IX_Series_OpenLibrarySeriesId")
                .OnTable("Series")
                .OnColumn("OpenLibrarySeriesId");
        }
    }
}
