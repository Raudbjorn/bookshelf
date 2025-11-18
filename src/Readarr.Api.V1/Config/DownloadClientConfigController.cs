using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;

namespace Readarr.Api.V1.Config
{
    [V1ApiController("config/downloadclient")]
    public class DownloadClientConfigController : ConfigController<DownloadClientConfigResource>
    {
        public DownloadClientConfigController(IConfigService configService)
            : base(configService)
        {
        }

        [RestPutById]
        public override ActionResult<DownloadClientConfigResource> SaveConfig([FromBody] DownloadClientConfigResource resource)
        {
            return base.SaveConfig(resource);
        }

        protected override DownloadClientConfigResource ToResource(IConfigService model)
        {
            return DownloadClientConfigResourceMapper.ToResource(model);
        }
    }
}
