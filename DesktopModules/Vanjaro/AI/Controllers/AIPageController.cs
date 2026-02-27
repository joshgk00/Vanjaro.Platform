using DotNetNuke.Entities.Tabs;
using DotNetNuke.Web.Api;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vanjaro.AI.Models;
using Vanjaro.Core.Data.Entities;

namespace Vanjaro.AI.Controllers
{
    [AllowAnonymous]
    [RequireAdmin]
    public class AIPageController : DnnApiController
    {
        [HttpGet]
        public HttpResponseMessage List(int skip = 0, int take = 50)
        {
            if (take > 200) take = 200;
            if (take < 1) take = 1;
            if (skip < 0) skip = 0;

            int portalId = PortalSettings.PortalId;

            var allTabs = TabController.Instance.GetTabsByPortal(portalId).Values
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.TabOrder)
                .ToList();

            var publishedTabIds = new HashSet<int>(
                Vanjaro.Core.Managers.PageManager.GetAllTabIdByPortalID(portalId, true));

            var allTabIds = new HashSet<int>(
                Vanjaro.Core.Managers.PageManager.GetAllTabIdByPortalID(portalId, false));

            int total = allTabs.Count;

            var pageItems = allTabs
                .Skip(skip)
                .Take(take)
                .Select(tab => new PageListItem
                {
                    TabId = tab.TabID,
                    Name = tab.TabName,
                    Title = tab.Title,
                    Path = tab.TabPath.Replace("//", "/"),
                    IsVisible = tab.IsVisible,
                    IsDeleted = tab.IsDeleted,
                    HasVanjaroContent = allTabIds.Contains(tab.TabID),
                    IsPublished = publishedTabIds.Contains(tab.TabID)
                })
                .ToArray();

            var result = new PageListResponse
            {
                Total = total,
                Skip = skip,
                Take = take,
                Pages = pageItems
            };

            return Request.CreateResponse(HttpStatusCode.OK, result);
        }

        [HttpGet]
        public HttpResponseMessage Get(int pageId, bool includeDraft = false)
        {
            try
            {
                var tab = TabController.Instance.GetTab(pageId, PortalSettings.PortalId, false);

                if (tab == null || tab.IsDeleted)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound,
                        "Page not found or has been deleted.");
                }

                if (tab.PortalID != PortalSettings.PortalId)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden,
                        "Page does not belong to the current portal.");
                }

                string locale = PortalSettings.CultureCode;
                bool ignoreDraft = !includeDraft;

                Pages page = Vanjaro.Core.Managers.PageManager.GetLatestVersion(
                    pageId, ignoreDraft, locale, true, true);

                if (page == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound,
                        "No Vanjaro content found for this page.");
                }

                var detail = new PageDetailResponse
                {
                    TabId = tab.TabID,
                    Name = tab.TabName,
                    Title = tab.Title,
                    Description = tab.Description,
                    Path = tab.TabPath.Replace("//", "/"),
                    Version = page.Version,
                    IsPublished = page.IsPublished,
                    Locale = page.Locale,
                    CreatedOn = page.CreatedOn.ToString("o"),
                    UpdatedOn = page.UpdatedOn.HasValue ? page.UpdatedOn.Value.ToString("o") : null,
                    ContentJSON = SafeParseJson(page.ContentJSON),
                    StyleJSON = SafeParseJson(page.StyleJSON),
                    ContentHtml = page.Content
                };

                return Request.CreateResponse(HttpStatusCode.OK, detail);
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                    "An error occurred retrieving the page.");
            }
        }

        private static JToken SafeParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JToken.Parse(json);
            }
            catch
            {
                return new JValue(json);
            }
        }
    }
}
