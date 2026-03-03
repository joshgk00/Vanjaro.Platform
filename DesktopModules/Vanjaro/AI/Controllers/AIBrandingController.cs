using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.UI.Internals;
using DotNetNuke.Web.Api;
using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vanjaro.AI.Models;

namespace Vanjaro.AI.Controllers
{
    [AllowAnonymous]
    [RequireAdmin]
    public class AIBrandingController : DnnApiController
    {
        [HttpGet]
        public HttpResponseMessage GetBranding()
        {
            try
            {
                int portalId = PortalSettings.PortalId;
                var portal = PortalController.Instance.GetPortal(portalId);

                if (portal == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Portal not found.");

                AssetFileItem logoItem = null;
                if (!string.IsNullOrEmpty(portal.LogoFile))
                {
                    var logoFile = FileManager.Instance.GetFile(portalId, portal.LogoFile);
                    logoItem = AssetFileItem.FromDnn(logoFile);
                }

                AssetFileItem favItem = null;
                try
                {
                    var favIcon = new FavIcon(portalId);
                    string favPath = favIcon.GetSettingPath();
                    if (!string.IsNullOrEmpty(favPath))
                    {
                        var favFile = FileManager.Instance.GetFile(portalId, favPath);
                        favItem = AssetFileItem.FromDnn(favFile);
                    }
                }
                catch
                {
                    // FavIcon may not be configured
                }

                return Request.CreateResponse(HttpStatusCode.OK, new BrandingResponse
                {
                    SiteName = portal.PortalName,
                    Description = portal.Description,
                    Keywords = portal.KeyWords,
                    FooterText = portal.FooterText,
                    Logo = logoItem,
                    Favicon = favItem
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to get branding: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage UpdateBranding(BrandingUpdateRequest request)
        {
            try
            {
                if (request == null)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Request body is required.");

                int portalId = PortalSettings.PortalId;
                var portal = PortalController.Instance.GetPortal(portalId);

                if (portal == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Portal not found.");

                bool portalUpdated = false;

                if (request.SiteName != null)
                {
                    portal.PortalName = request.SiteName;
                    portalUpdated = true;
                }

                if (request.Description != null)
                {
                    portal.Description = request.Description;
                    portalUpdated = true;
                }

                if (request.Keywords != null)
                {
                    portal.KeyWords = request.Keywords;
                    portalUpdated = true;
                }

                if (request.FooterText != null)
                {
                    portal.FooterText = request.FooterText;
                    portalUpdated = true;
                }

                if (request.LogoFileId.HasValue)
                {
                    var logoFile = FileManager.Instance.GetFile(request.LogoFileId.Value);
                    if (logoFile == null)
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Logo file not found for fileId " + request.LogoFileId.Value);

                    if (logoFile.PortalId != portalId)
                        return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Logo file does not belong to this portal.");

                    string relativePath = logoFile.RelativePath;
                    if (relativePath != null && relativePath.Length > 50)
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, $"Logo file path is {relativePath.Length} characters, but DNN limits LogoFile to 50 characters. Use a shorter folder path or file name.");

                    portal.LogoFile = relativePath;
                    portalUpdated = true;
                }

                if (request.FaviconFileId.HasValue)
                {
                    var favFile = FileManager.Instance.GetFile(request.FaviconFileId.Value);
                    if (favFile == null)
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Favicon file not found for fileId " + request.FaviconFileId.Value);

                    if (favFile.PortalId != portalId)
                        return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Favicon file does not belong to this portal.");

                    new FavIcon(portalId).Update(request.FaviconFileId.Value);
                }

                if (portalUpdated)
                    PortalController.Instance.UpdatePortalInfo(portal);

                return GetBranding();
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to update branding: " + ex.Message);
            }
        }

    }
}
