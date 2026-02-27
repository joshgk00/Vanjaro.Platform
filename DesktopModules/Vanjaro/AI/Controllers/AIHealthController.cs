using DotNetNuke.Application;
using DotNetNuke.Web.Api;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Web.Http;
using Vanjaro.AI.Models;

namespace Vanjaro.AI.Controllers
{
    [AllowAnonymous]
    [RequireAdmin]
    public class AIHealthController : DnnApiController
    {
        [HttpGet]
        public HttpResponseMessage Check()
        {
            string vanjaroVersion = "unknown";
            try
            {
                var coreAssembly = Assembly.GetAssembly(typeof(Vanjaro.Core.Managers));
                if (coreAssembly != null)
                    vanjaroVersion = coreAssembly.GetName().Version.ToString();
            }
            catch
            {
                // Fall back to "unknown" if reflection fails
            }

            var response = new HealthCheckResponse
            {
                Status = "ok",
                DnnVersion = DotNetNukeContext.Current.Application.Version.ToString(3),
                VanjaroVersion = vanjaroVersion,
                UserId = UserInfo.UserID,
                UserName = UserInfo.Username,
                PortalId = PortalSettings.PortalId,
                Timestamp = System.DateTime.UtcNow.ToString("o")
            };

            return Request.CreateResponse(HttpStatusCode.OK, response);
        }
    }
}
