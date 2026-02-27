using DotNetNuke.Entities.Users;
using DotNetNuke.Security.Roles;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Vanjaro.AI.Controllers
{
    public class RequireAdminAttribute : ActionFilterAttribute
    {
        private static readonly ConcurrentDictionary<string, CacheEntry> RoleCache
            = new ConcurrentDictionary<string, CacheEntry>();

        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var controller = actionContext.ControllerContext.Controller as DnnApiController;
            if (controller == null)
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError, "Controller context unavailable.");
                return;
            }

            var user = controller.UserInfo;
            if (user == null || user.UserID < 0)
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Unauthorized, "Authentication required.");
                return;
            }

            if (user.IsSuperUser)
                return;

            int portalId = controller.PortalSettings.PortalId;

            if (!IsAdminCached(portalId, user.UserID))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Forbidden, "Access restricted to Administrators.");
                return;
            }

            base.OnActionExecuting(actionContext);
        }

        private static bool IsAdminCached(int portalId, int userId)
        {
            string cacheKey = $"{portalId}:{userId}";

            if (RoleCache.TryGetValue(cacheKey, out CacheEntry entry) && !entry.IsExpired)
                return entry.IsAdmin;

            bool isAdmin = CheckAdminRole(portalId, userId);

            RoleCache[cacheKey] = new CacheEntry
            {
                IsAdmin = isAdmin,
                ExpiresUtc = DateTime.UtcNow.Add(CacheDuration)
            };

            return isAdmin;
        }

        private static bool CheckAdminRole(int portalId, int userId)
        {
            var fullUser = UserController.GetUserById(portalId, userId);
            if (fullUser == null)
                return false;

            var userRoles = RoleController.Instance.GetUserRoles(fullUser, true);
            return userRoles != null && userRoles.Any(r =>
                r.RoleName == "Administrators" && r.Status == RoleStatus.Approved);
        }

        private class CacheEntry
        {
            public bool IsAdmin { get; set; }
            public DateTime ExpiresUtc { get; set; }
            public bool IsExpired => DateTime.UtcNow > ExpiresUtc;
        }
    }
}
