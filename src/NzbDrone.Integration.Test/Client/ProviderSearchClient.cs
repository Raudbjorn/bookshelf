using System.Collections.Generic;
using Readarr.Api.V1.Search;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class ProviderSearchClient : ClientBase<SearchResource>
    {
        public ProviderSearchClient(IRestClient restClient, string apiKey)
            : base(restClient, apiKey, "search/provider")
        {
        }

        public List<SearchResource> SearchHardcover(string term)
        {
            var request = BuildRequest("hardcover");
            request.AddParameter("term", term);
            return Get<List<SearchResource>>(request);
        }

        public List<SearchResource> SearchOpenLibrary(string term)
        {
            var request = BuildRequest("openlibrary");
            request.AddParameter("term", term);
            return Get<List<SearchResource>>(request);
        }

        public List<SearchResource> SearchGoogleBooks(string term)
        {
            var request = BuildRequest("googlebooks");
            request.AddParameter("term", term);
            return Get<List<SearchResource>>(request);
        }

        public List<SearchResource> SearchComicVine(string term)
        {
            var request = BuildRequest("comicvine");
            request.AddParameter("term", term);
            return Get<List<SearchResource>>(request);
        }

        public MultiProviderSearchResource SearchAll(string term, string providers = null)
        {
            var request = BuildRequest();
            request.AddParameter("term", term);
            if (!string.IsNullOrEmpty(providers))
            {
                request.AddParameter("providers", providers);
            }

            return Get<MultiProviderSearchResource>(request);
        }

        public ReconciledSearchResource SearchReconciled(string term, string providers = null)
        {
            var request = BuildRequest("reconcile");
            request.AddParameter("term", term);
            if (!string.IsNullOrEmpty(providers))
            {
                request.AddParameter("providers", providers);
            }

            return Get<ReconciledSearchResource>(request);
        }
    }
}
