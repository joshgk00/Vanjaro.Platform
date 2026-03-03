using DotNetNuke.Common.Utilities;
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
using static Vanjaro.Core.Factories;

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

        [HttpPost]
        public HttpResponseMessage Update(PageUpdateRequest request)
        {
            try
            {
                // Validate: at least one of ContentJSON or StyleJSON must be provided
                if (string.IsNullOrEmpty(request.ContentJSON) && string.IsNullOrEmpty(request.StyleJSON))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        "At least one of contentJSON or styleJSON is required.");
                }

                // Verify tab exists and belongs to current portal
                var tab = TabController.Instance.GetTab(request.PageId, PortalSettings.PortalId, false);
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

                // Get current version (include drafts so we get the latest)
                string locale = PortalSettings.CultureCode;
                Pages current = Vanjaro.Core.Managers.PageManager.GetLatestVersion(
                    request.PageId, false, locale, true, true);

                if (current == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound,
                        "No Vanjaro content found for this page.");
                }

                // Optimistic concurrency check
                if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != current.Version)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.Conflict,
                        $"Version conflict. Expected version {request.ExpectedVersion.Value} but current is {current.Version}.");
                }

                // Create new version
                var newPage = new Pages
                {
                    ID = 0,
                    PortalID = PortalSettings.PortalId,
                    TabID = request.PageId,
                    Version = current.Version + 1,
                    ContentJSON = request.ContentJSON ?? current.ContentJSON,
                    StyleJSON = request.StyleJSON ?? current.StyleJSON,
                    Content = current.Content,
                    Style = current.Style,
                    IsPublished = false,
                    Locale = locale,
                    StateID = current.StateID
                };

                // Save via PageFactory
                PageFactory.Update(newPage, UserInfo.UserID);

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    pageId = request.PageId,
                    version = newPage.Version
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                    "An error occurred updating the page.");
            }
        }

        [HttpPost]
        public HttpResponseMessage Publish(PagePublishRequest request)
        {
            try
            {
                // Verify tab exists and belongs to current portal
                var tab = TabController.Instance.GetTab(request.PageId, PortalSettings.PortalId, false);
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

                // Get latest version (include drafts)
                string locale = PortalSettings.CultureCode;
                Pages page = Vanjaro.Core.Managers.PageManager.GetLatestVersion(
                    request.PageId, false, locale, true, true);

                if (page == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound,
                        "No Vanjaro content found for this page.");
                }

                // If specific version requested, validate it matches
                if (request.Version.HasValue && request.Version.Value != page.Version)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound,
                        $"Version {request.Version.Value} not found. Latest version is {page.Version}.");
                }

                // Set publish fields (ID > 0 so PageFactory.Update does an update, not insert)
                page.IsPublished = true;
                page.PublishedBy = UserInfo.UserID;
                page.PublishedOn = DateTime.UtcNow;

                // Save
                PageFactory.Update(page, UserInfo.UserID);

                // Return result
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    pageId = request.PageId,
                    version = page.Version,
                    isPublished = true,
                    publishedOn = page.PublishedOn.Value.ToString("o")
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                    "An error occurred publishing the page.");
            }
        }

        [HttpPost]
        public HttpResponseMessage Create(PageCreateRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest,
                        "Page name is required.");
                }

                int portalId = PortalSettings.PortalId;

                var tab = new TabInfo
                {
                    PortalID = portalId,
                    TabName = request.Name,
                    Title = request.Title ?? request.Name,
                    Description = request.Description ?? "",
                    IsVisible = request.IsVisible,
                    ParentId = request.ParentId ?? Null.NullInteger,
                    SkinSrc = "[g]skins/vanjaro/base.ascx",
                    ContainerSrc = "[g]containers/vanjaro/base.ascx"
                };

                int tabId = TabController.Instance.AddTab(tab);

                // Get StateID — required or PageFactory.Update silently skips
                int workflowId = Vanjaro.Core.Managers.WorkflowManager.GetDefaultWorkflow(tabId);
                var firstState = Vanjaro.Core.Managers.WorkflowManager.GetFirstStateID(workflowId);
                if (firstState == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                        "Could not determine workflow state for new page.");
                }

                // Bootstrap Vanjaro Pages row
                var page = new Pages
                {
                    ID = 0,
                    PortalID = portalId,
                    TabID = tabId,
                    Version = 1,
                    ContentJSON = request.ContentJSON ?? "[]",
                    StyleJSON = request.StyleJSON ?? "{}",
                    Content = "",
                    Style = "",
                    IsPublished = false,
                    Locale = PortalSettings.CultureCode,
                    StateID = firstState.StateID
                };

                PageFactory.Update(page, UserInfo.UserID);

                // Reload tab to get generated path
                tab = TabController.Instance.GetTab(tabId, portalId, false);

                return Request.CreateResponse(HttpStatusCode.Created, new
                {
                    pageId = tabId,
                    name = tab.TabName,
                    version = 1,
                    path = tab.TabPath.Replace("//", "/")
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                    "An error occurred creating the page.");
            }
        }

        [HttpPost]
        public HttpResponseMessage Delete(PageDeleteRequest request)
        {
            try
            {
                int portalId = PortalSettings.PortalId;
                var tab = TabController.Instance.GetTab(request.PageId, portalId, false);

                if (tab == null || tab.IsDeleted)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound,
                        "Page not found or has been deleted.");
                }

                if (tab.PortalID != portalId)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden,
                        "Page does not belong to the current portal.");
                }

                // Delete Vanjaro content rows (Pages.Delete is PetaPoco record method)
                Pages.Delete("Where TabID=@0", tab.TabID);

                // Delete DNN tab
                TabController.Instance.DeleteTab(tab.TabID, portalId);

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    pageId = request.PageId,
                    deleted = true
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError,
                    "An error occurred deleting the page.");
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
