using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(46)]
    public class AddProviderConfigDefaults : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Insert default values for provider settings if they don't exist
            // Note: The ConfigService already handles defaults, but this makes them explicit
            Execute.Sql(@"
                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'hardcoverenabled', 'False'
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'hardcoverenabled');

                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'hardcoverapitoken', ''
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'hardcoverapitoken');

                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'hardcoverusername', ''
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'hardcoverusername');

                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'openlibraryenabled', 'True'
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'openlibraryenabled');

                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'googlebooksenabled', 'False'
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'googlebooksenabled');

                INSERT INTO ""Config"" (""Key"", ""Value"")
                SELECT 'googlebooksapikey', ''
                WHERE NOT EXISTS (SELECT 1 FROM ""Config"" WHERE ""Key"" = 'googlebooksapikey');
            ");
        }
    }
}
