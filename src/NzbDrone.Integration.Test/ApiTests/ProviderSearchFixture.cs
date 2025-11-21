using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Readarr.Api.V1.Config;

namespace NzbDrone.Integration.Test.ApiTests
{
    [TestFixture]
    public class ProviderSearchFixture : IntegrationTest
    {
        private MetadataProviderConfigResource _originalConfig;

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            // Save original configuration
            _originalConfig = GetMetadataProviderConfig();
        }

        [OneTimeTearDown]
        public void FixtureTeardown()
        {
            // Restore original configuration
            if (_originalConfig != null)
            {
                UpdateMetadataProviderConfig(_originalConfig);
            }
        }

        private MetadataProviderConfigResource GetMetadataProviderConfig()
        {
            var request = ProviderSearch.BuildRequest("../../config/metadataProvider");
            return ProviderSearch.Get<MetadataProviderConfigResource>(request);
        }

        private void UpdateMetadataProviderConfig(MetadataProviderConfigResource config)
        {
            var request = ProviderSearch.BuildRequest("../../config/metadataProvider");
            request.AddJsonBody(config);
            ProviderSearch.Put<MetadataProviderConfigResource>(request);
        }

        private string GetComicVineApiKeyOrIgnore()
        {
            var apiKey = Environment.GetEnvironmentVariable("COMICVINE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Assert.Ignore("COMICVINE_API_KEY environment variable not set - skipping ComicVine test");
            }

            return apiKey;
        }

        [Test]
        public void hardcover_search_should_return_results()
        {
            // Arrange
            var config = GetMetadataProviderConfig();
            config.HardcoverEnabled = true;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchHardcover("Harry Potter");

            // Assert
            results.Should().NotBeNull();
            results.Should().NotBeEmpty("Hardcover should return results for popular query");
            results.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.ForeignId));
        }

        [Test]
        public void openlibrary_search_should_return_results()
        {
            // Arrange
            var config = GetMetadataProviderConfig();
            config.OpenLibraryEnabled = true;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchOpenLibrary("The Hobbit");

            // Assert
            results.Should().NotBeNull();
            results.Should().NotBeEmpty("OpenLibrary should return results for popular query");
            results.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.ForeignId));
        }

        [Test]
        public void googlebooks_search_should_return_results()
        {
            // Arrange
            var config = GetMetadataProviderConfig();
            config.GoogleBooksEnabled = true;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchGoogleBooks("1984 Orwell");

            // Assert
            results.Should().NotBeNull();
            results.Should().NotBeEmpty("Google Books should return results for popular query");
            results.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.ForeignId));
        }

        [Test]
        public void multi_provider_search_should_return_from_all_enabled_providers()
        {
            // Arrange
            var config = GetMetadataProviderConfig();
            config.HardcoverEnabled = true;
            config.OpenLibraryEnabled = true;
            config.GoogleBooksEnabled = true;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchAll("Foundation Asimov");

            // Assert
            results.Should().NotBeNull();
            results.Query.Should().Be("Foundation Asimov");
            results.Hardcover.Should().NotBeNull();
            results.OpenLibrary.Should().NotBeNull();
            results.GoogleBooks.Should().NotBeNull();

            // At least one provider should return results
            var totalResults = results.Hardcover.Count + results.OpenLibrary.Count + results.GoogleBooks.Count;
            totalResults.Should().BeGreaterThan(0, "At least one provider should return results");
        }

        [Test]
        public void search_with_empty_term_should_return_empty_results()
        {
            // Act
            var results = ProviderSearch.SearchHardcover("");

            // Assert
            results.Should().NotBeNull();
            results.Should().BeEmpty("Empty search term should return no results");
        }

        [Test]
        public void search_results_should_have_required_fields()
        {
            // Arrange
            var config = GetMetadataProviderConfig();
            config.OpenLibraryEnabled = true;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchOpenLibrary("Dune Herbert");

            // Assert
            results.Should().NotBeEmpty();

            foreach (var result in results.Take(3))
            {
                result.Id.Should().BeGreaterThan(0);
                result.ForeignId.Should().NotBeNullOrWhiteSpace();

                if (result.Book != null)
                {
                    result.Book.Title.Should().NotBeNullOrWhiteSpace();
                    result.Book.Author.Should().NotBeNull();
                    result.Book.Author.AuthorName.Should().NotBeNullOrWhiteSpace();
                }

                if (result.Author != null)
                {
                    result.Author.AuthorName.Should().NotBeNullOrWhiteSpace();
                }
            }
        }

        [Test]
        public void comicvine_search_should_return_results()
        {
            // Arrange
            var apiKey = GetComicVineApiKeyOrIgnore();

            var config = GetMetadataProviderConfig();
            config.ComicVineEnabled = true;
            config.ComicVineApiKey = apiKey;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchComicVine("Batman");

            // Assert
            results.Should().NotBeNull();
            results.Should().NotBeEmpty("ComicVine should return results for popular comic query");
            results.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.ForeignId));
            results.Should().OnlyContain(r => r.ForeignId.StartsWith("comicvine:"));
        }

        [Test]
        public void comicvine_search_with_missing_api_key_should_return_empty()
        {
            // Arrange
            var config = GetMetadataProviderConfig();
            config.ComicVineEnabled = true;
            config.ComicVineApiKey = null;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchComicVine("Spider-Man");

            // Assert
            results.Should().NotBeNull();
            results.Should().BeEmpty("ComicVine without API key should return empty results");
        }

        [Test]
        public void comicvine_search_with_disabled_provider_should_return_empty()
        {
            // Arrange
            var apiKey = GetComicVineApiKeyOrIgnore();

            var config = GetMetadataProviderConfig();
            config.ComicVineEnabled = false;
            config.ComicVineApiKey = apiKey;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchComicVine("X-Men");

            // Assert
            results.Should().NotBeNull();
            results.Should().BeEmpty("Disabled ComicVine provider should return empty results");
        }

        [Test]
        public void comicvine_results_should_have_comic_specific_fields()
        {
            // Arrange
            var apiKey = GetComicVineApiKeyOrIgnore();

            var config = GetMetadataProviderConfig();
            config.ComicVineEnabled = true;
            config.ComicVineApiKey = apiKey;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchComicVine("Watchmen");

            // Assert
            results.Should().NotBeEmpty();

            foreach (var result in results.Take(3))
            {
                result.ForeignId.Should().NotBeNullOrWhiteSpace();
                result.ForeignId.Should().StartWith("comicvine:");

                if (result.Book != null)
                {
                    // Comic titles should include volume information
                    result.Book.Title.Should().NotBeNullOrWhiteSpace();

                    // Should have author (writer/creator credits)
                    result.Book.Author.Should().NotBeNull();
                    result.Book.Author.AuthorName.Should().NotBeNullOrWhiteSpace();

                    // Should have ComicVine link
                    if (result.Book.Editions != null && result.Book.Editions.Any())
                    {
                        var edition = result.Book.Editions.First();
                        edition.Links.Should().NotBeNull();
                        edition.Links.Should().Contain(l => l.Name == "ComicVine");
                    }
                }
            }
        }

        [Test]
        public void comicvine_search_in_multi_provider_should_be_included()
        {
            // Arrange
            var apiKey = GetComicVineApiKeyOrIgnore();

            var config = GetMetadataProviderConfig();
            config.HardcoverEnabled = false;
            config.OpenLibraryEnabled = false;
            config.GoogleBooksEnabled = false;
            config.ComicVineEnabled = true;
            config.ComicVineApiKey = apiKey;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchAll("Amazing Spider-Man");

            // Assert
            results.Should().NotBeNull();
            results.Query.Should().Be("Amazing Spider-Man");
            results.ComicVine.Should().NotBeNull();
            results.ComicVine.Should().NotBeEmpty("ComicVine should return results in multi-provider search");
        }

        [Test]
        public void comicvine_search_should_handle_special_characters()
        {
            // Arrange
            var apiKey = GetComicVineApiKeyOrIgnore();

            var config = GetMetadataProviderConfig();
            config.ComicVineEnabled = true;
            config.ComicVineApiKey = apiKey;
            UpdateMetadataProviderConfig(config);

            // Act
            var results = ProviderSearch.SearchComicVine("Y: The Last Man");

            // Assert
            results.Should().NotBeNull();

            // Should not throw exception on special characters
        }
    }
}
