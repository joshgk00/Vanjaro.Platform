using DotNetNuke.Web.Api;

namespace Vanjaro.AI.Controllers
{
    public class ServiceRouteMapper : IServiceRouteMapper
    {
        public void RegisterRoutes(IMapRoute mapRouteManager)
        {
            mapRouteManager.MapHttpRoute("VanjaroAI", "default", "{controller}/{action}",
                new[] { "Vanjaro.AI.Controllers" });
        }
    }
}
