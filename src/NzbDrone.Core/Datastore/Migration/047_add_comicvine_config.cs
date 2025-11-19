using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(47)]
    public class AddComicVineConfig : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add ComicVine configuration fields
            Execute.Sql(@"
                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'comicvineenabled', 'False'
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'comicvineenabled');

                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'comicvineapikey', ''
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'comicvineapikey');

                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'comicvinecachettlhours', '1'
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'comicvinecachettlhours');
            ");
        }
    }
}
