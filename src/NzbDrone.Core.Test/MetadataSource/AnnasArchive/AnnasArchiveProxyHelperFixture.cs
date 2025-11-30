using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.AnnasArchive;
using NzbDrone.Core.MetadataSource.AnnasArchive.Resources;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.AnnasArchive
{
    [TestFixture]
    public class AnnasArchiveProxyHelperFixture : CoreTest<AnnasArchiveProxy>
    {
        [SetUp]
        public void Setup()
        {
            UseRealHttp();
        }

        [TestCase("978-0-511-41084-0", "9780511410840", true)]
        [TestCase("9780511410840", "9780511410840", true)]
        [TestCase("9780511410841", null, false)] // Invalid checksum
        [TestCase("978051141084", null, false)] // Too short
        [TestCase("invalid", null, false)]
        [TestCase("", null, false)]
        [TestCase(null, null, false)]
        public void should_validate_isbn13(string input, string expected, bool shouldBeValid)
        {
            // Using reflection to test private method
            var method = typeof(AnnasArchiveProxy).GetMethod("CleanIsbn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cleaned = (string)method.Invoke(Subject, new object[] { input });

            var validateMethod = typeof(AnnasArchiveProxy).GetMethod("IsValidIsbn13", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isValid = (bool)validateMethod.Invoke(Subject, new object[] { cleaned });

            if (shouldBeValid)
            {
                cleaned.Should().Be(expected);
                isValid.Should().BeTrue();
            }
            else
            {
                isValid.Should().BeFalse();
            }
        }

        [TestCase("Michele Boldrin; David K. Levine", new[] { "Michele Boldrin", "David K. Levine" })]
        [TestCase("Michele Boldrin, David K. Levine", new[] { "Michele Boldrin", "David K. Levine" })]
        [TestCase("Michele Boldrin and David K. Levine", new[] { "Michele Boldrin", "David K. Levine" })]
        [TestCase("Michele Boldrin & David K. Levine", new[] { "Michele Boldrin", "David K. Levine" })]
        [TestCase("Single Author", new[] { "Single Author" })]
        [TestCase("  Trimmed  ; Author  ", new[] { "Trimmed", "Author" })]
        public void should_split_authors(string input, string[] expected)
        {
            var method = typeof(AnnasArchiveProxy).GetMethod("SplitAuthors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (System.Collections.Generic.List<string>)method.Invoke(Subject, new object[] { input });

            result.Should().BeEquivalentTo(expected);
        }

        [TestCase("Michele Boldrin", "michele-boldrin")]
        [TestCase("David K. Levine", "david-k-levine")]
        [TestCase("J.R.R. Tolkien", "jrr-tolkien")]
        [TestCase("Author, Name", "author-name")]
        [TestCase("Author's Name", "authors-name")]
        [TestCase("  Multiple   Spaces  ", "multiple-spaces")]
        [TestCase("Name (Pseudonym)", "name-pseudonym")]
        [TestCase("", "unknown")]
        [TestCase(null, "unknown")]
        public void should_create_slug(string input, string expected)
        {
            var method = typeof(AnnasArchiveProxy).GetMethod("CreateSlug", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (string)method.Invoke(Subject, new object[] { input });

            result.Should().Be(expected);
        }

        [TestCase("aa:8336332bf5877e3adbfb60ac70720cd5", "8336332bf5877e3adbfb60ac70720cd5")]
        [TestCase("8336332bf5877e3adbfb60ac70720cd5", null)] // Missing prefix
        [TestCase("invalid:hash", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void should_extract_md5_from_foreign_id(string input, string expected)
        {
            var method = typeof(AnnasArchiveProxy).GetMethod("ExtractMd5FromForeignId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (string)method.Invoke(Subject, new object[] { input });

            result.Should().Be(expected);
        }

        [TestCase("2008", 2008, 1, 1)]
        [TestCase("2008-01-15", 2008, 1, 15)]
        [TestCase("invalid", null, null, null)]
        [TestCase("", null, null, null)]
        [TestCase(null, null, null, null)]
        public void should_parse_release_date(string input, int? year, int? month, int? day)
        {
            var record = new AARecord
            {
                FileUnifiedData = new AAFileUnifiedData { Year = input, Md5 = "test" }
            };

            var method = typeof(AnnasArchiveProxy).GetMethod("ParseReleaseDate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (System.DateTime?)method.Invoke(Subject, new object[] { record });

            if (year.HasValue)
            {
                result.Should().NotBeNull();
                result.Value.Year.Should().Be(year.Value);
                result.Value.Month.Should().Be(month.Value);
                result.Value.Day.Should().Be(day.Value);
            }
            else
            {
                result.Should().BeNull();
            }
        }
    }
}
