using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.ZLibrary;
using NzbDrone.Core.MetadataSource.ZLibrary.Resources;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.ZLibrary
{
    [TestFixture]
    public class ZLibraryProxyFixture : CoreTest<ZLibraryProxy>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ZLibraryEnabled)
                .Returns(true);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ZLibraryRemixUserId)
                .Returns("12345");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ZLibraryRemixUserKey)
                .Returns("abcdef");
        }

        [Test]
        public void SearchForNewBook_should_return_results_when_api_succeeds()
        {
            var searchResponse = new ZLibSearchResponse
            {
                Success = true,
                Books = new List<ZLibBook>
                {
                    new ZLibBook
                    {
                        Id = "1",
                        Hash = "hash1",
                        Title = "Test Book",
                        Author = "Test Author",
                        Year = "2023",
                        Language = "english",
                        Extension = "epub"
                    }
                }
            };

            var json = searchResponse.ToJson();
            var httpResponse = new HttpResponse(new HttpRequest("http://test"), new HttpHeader(), Encoding.UTF8.GetBytes(json));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Post<ZLibSearchResponse>(It.IsAny<HttpRequest>()))
                .Returns(new HttpResponse<ZLibSearchResponse>(httpResponse));

            var results = Subject.SearchForNewBook("Test Book");

            results.Should().HaveCount(1);
            results[0].Title.Should().Be("Test Book");
            results[0].ForeignBookId.Should().Be("zlib:1:hash1");
        }

        [Test]
        public void GetBookInfo_should_return_book_details_when_api_succeeds()
        {
            var bookResponse = new ZLibBookDetailResponse
            {
                Success = true,
                Book = new ZLibBook
                {
                    Id = "1",
                    Hash = "hash1",
                    Title = "Test Book",
                    Author = "Test Author",
                    Year = "2023",
                    Language = "english",
                    Extension = "epub"
                }
            };

            var json = bookResponse.ToJson();
            var httpResponse = new HttpResponse(new HttpRequest("http://test"), new HttpHeader(), Encoding.UTF8.GetBytes(json));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Get<ZLibBookDetailResponse>(It.IsAny<HttpRequest>()))
                .Returns(new HttpResponse<ZLibBookDetailResponse>(httpResponse));

            var result = Subject.GetBookInfo("zlib:1:hash1");

            result.Item2.Title.Should().Be("Test Book");
            result.Item2.ForeignBookId.Should().Be("zlib:1:hash1");
        }

        [Test]
        public void should_throw_if_provider_disabled()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ZLibraryEnabled)
                .Returns(false);

            var results = Subject.SearchForNewBook("Test Book");
            results.Should().BeEmpty();
        }

        [Test]
        public void should_authenticate_with_credentials_if_tokens_missing()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ZLibraryRemixUserId)
                .Returns((string)null);

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ZLibraryEmail)
                .Returns("user@example.com");
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.ZLibraryPassword)
                .Returns("password");

            var loginResponse = new ZLibLoginResponse
            {
                Success = true,
                User = new ZLibUser
                {
                    Email = "user@example.com",
                    RemixUserId = "new_id",
                    RemixUserKey = "new_key"
                }
            };

            var loginJson = loginResponse.ToJson();
            var loginHttpResponse = new HttpResponse(new HttpRequest("http://test"), new HttpHeader(), Encoding.UTF8.GetBytes(loginJson));

            var searchJson = new ZLibSearchResponse { Success = true, Books = new List<ZLibBook>() }.ToJson();
            var searchHttpResponse = new HttpResponse(new HttpRequest("http://test"), new HttpHeader(), Encoding.UTF8.GetBytes(searchJson));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Post<ZLibLoginResponse>(It.Is<HttpRequest>(r => r.Url.ToString().Contains("/user/login"))))
                .Returns(new HttpResponse<ZLibLoginResponse>(loginHttpResponse));

            Mocker.GetMock<IHttpClient>()
                .Setup(c => c.Post<ZLibSearchResponse>(It.Is<HttpRequest>(r => r.Url.ToString().Contains("/book/search"))))
                .Returns(new HttpResponse<ZLibSearchResponse>(searchHttpResponse));

            Subject.SearchForNewBook("Test Book");

            Mocker.GetMock<IConfigService>()
                .VerifySet(s => s.ZLibraryRemixUserId = "new_id", Times.Once());
        }
    }
}
