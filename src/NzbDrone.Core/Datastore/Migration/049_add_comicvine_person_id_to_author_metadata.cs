using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(49)]
    public class AddComicVinePersonIdToAuthorMetadata : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("AuthorMetadata")
                .AddColumn("ComicVinePersonId").AsString().Nullable();

            Create.Index("IX_AuthorMetadata_ComicVinePersonId")
                .OnTable("AuthorMetadata")
                .OnColumn("ComicVinePersonId");
        }
    }
}
