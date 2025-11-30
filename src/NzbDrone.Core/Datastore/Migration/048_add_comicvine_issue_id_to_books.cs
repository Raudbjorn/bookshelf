using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(48)]
    public class AddComicVineIssueIdToBooks : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Books")
                .AddColumn("ComicVineIssueId").AsString().Nullable();

            Create.Index("IX_Books_ComicVineIssueId")
                .OnTable("Books")
                .OnColumn("ComicVineIssueId");
        }
    }
}
