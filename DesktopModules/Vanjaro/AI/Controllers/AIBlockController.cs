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
    public class AIBlockController : DnnApiController
    {
        private static readonly HashSet<string> BlockTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "blockwrapper",
            "globalblockwrapper"
        };

        [HttpGet]
        public HttpResponseMessage List(int pageId)
        {
            try
            {
                var tab = TabController.Instance.GetTab(pageId, PortalSettings.PortalId, false);
                if (tab == null || tab.IsDeleted)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Page not found or has been deleted.");
                if (tab.PortalID != PortalSettings.PortalId)
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Page does not belong to the current portal.");

                string locale = PortalSettings.CultureCode;
                Pages page = Vanjaro.Core.Managers.PageManager.GetLatestVersion(pageId, false, locale, true, true);
                if (page == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No Vanjaro content found for this page.");

                JArray components = ParseContentJson(page.ContentJSON);
                if (components == null)
                    return Request.CreateResponse(HttpStatusCode.OK, new BlockListResponse
                    {
                        PageId = pageId,
                        Version = page.Version,
                        Total = 0,
                        Blocks = new BlockListItem[0]
                    });

                var blocks = new List<BlockListItem>();
                CollectBlocks(components, blocks);

                return Request.CreateResponse(HttpStatusCode.OK, new BlockListResponse
                {
                    PageId = pageId,
                    Version = page.Version,
                    Total = blocks.Count,
                    Blocks = blocks.ToArray()
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred listing blocks.");
            }
        }

        [HttpGet]
        public HttpResponseMessage Get(int pageId, string componentId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(componentId))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "componentId is required.");

                var tab = TabController.Instance.GetTab(pageId, PortalSettings.PortalId, false);
                if (tab == null || tab.IsDeleted)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Page not found or has been deleted.");
                if (tab.PortalID != PortalSettings.PortalId)
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Page does not belong to the current portal.");

                string locale = PortalSettings.CultureCode;
                Pages page = Vanjaro.Core.Managers.PageManager.GetLatestVersion(pageId, false, locale, true, true);
                if (page == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No Vanjaro content found for this page.");

                JArray components = ParseContentJson(page.ContentJSON);
                if (components == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Block not found.");

                JObject found = FindComponentById(components, componentId);
                if (found == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Block not found.");

                var subtreeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectComponentIds(found, subtreeIds);

                JArray filteredStyles = FilterStyles(page.StyleJSON, subtreeIds);

                var detail = new BlockDetailResponse
                {
                    PageId = pageId,
                    Version = page.Version,
                    ComponentId = GetAttributeValue(found, "id"),
                    Guid = GetAttributeValue(found, "data-guid"),
                    BlockTypeGuid = GetAttributeValue(found, "data-block-guid"),
                    Type = found["type"]?.ToString(),
                    Name = found["name"]?.ToString() ?? GetAttributeValue(found, "data-block-type"),
                    ContentJSON = found,
                    StyleJSON = filteredStyles
                };

                return Request.CreateResponse(HttpStatusCode.OK, detail);
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred retrieving the block.");
            }
        }

        [HttpPost]
        public HttpResponseMessage Update(BlockUpdateRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ComponentId))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "componentId is required.");
                if (string.IsNullOrWhiteSpace(request.ContentJSON))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "contentJSON is required.");

                var tab = TabController.Instance.GetTab(request.PageId, PortalSettings.PortalId, false);
                if (tab == null || tab.IsDeleted)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Page not found or has been deleted.");
                if (tab.PortalID != PortalSettings.PortalId)
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Page does not belong to the current portal.");

                string locale = PortalSettings.CultureCode;
                Pages current = Vanjaro.Core.Managers.PageManager.GetLatestVersion(
                    request.PageId, false, locale, true, true);
                if (current == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No Vanjaro content found for this page.");

                if (request.ExpectedVersion.HasValue && request.ExpectedVersion.Value != current.Version)
                    return Request.CreateErrorResponse(HttpStatusCode.Conflict,
                        $"Version conflict. Expected version {request.ExpectedVersion.Value} but current is {current.Version}.");

                JArray tree = ParseContentJson(current.ContentJSON);
                if (tree == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Page has no parseable content.");

                JObject oldComponent = FindComponentById(tree, request.ComponentId);
                if (oldComponent == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Component not found.");

                JObject newComponent;
                try
                {
                    newComponent = JObject.Parse(request.ContentJSON);
                }
                catch
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "contentJSON is not valid JSON.");
                }

                var oldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectComponentIds(oldComponent, oldIds);

                if (!ReplaceComponent(tree, request.ComponentId, newComponent))
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to replace component in tree.");

                string mergedStyleJson = MergeStyles(current.StyleJSON, oldIds, request.StyleJSON);

                var newPage = new Pages
                {
                    ID = 0,
                    PortalID = PortalSettings.PortalId,
                    TabID = request.PageId,
                    Version = current.Version + 1,
                    ContentJSON = tree.ToString(Newtonsoft.Json.Formatting.None),
                    StyleJSON = mergedStyleJson,
                    Content = current.Content,
                    Style = current.Style,
                    IsPublished = false,
                    Locale = locale,
                    StateID = current.StateID
                };

                PageFactory.Update(newPage, UserInfo.UserID);

                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    pageId = request.PageId,
                    version = newPage.Version,
                    componentId = request.ComponentId
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "An error occurred updating the block.");
            }
        }

        #region Tree Traversal Helpers

        private static void CollectBlocks(JArray components, List<BlockListItem> results)
        {
            foreach (JObject component in components.OfType<JObject>())
            {
                string type = component["type"]?.ToString();
                if (type != null && BlockTypes.Contains(type))
                {
                    results.Add(new BlockListItem
                    {
                        ComponentId = GetAttributeValue(component, "id"),
                        Guid = GetAttributeValue(component, "data-guid"),
                        BlockTypeGuid = GetAttributeValue(component, "data-block-guid"),
                        Type = type,
                        Name = component["name"]?.ToString() ?? GetAttributeValue(component, "data-block-type"),
                        ChildCount = CountChildren(component)
                    });
                }

                // Always recurse into children to find nested blocks
                JArray children = component["components"] as JArray;
                if (children != null)
                    CollectBlocks(children, results);
            }
        }

        private static JObject FindComponentById(JArray components, string targetId)
        {
            foreach (JObject component in components.OfType<JObject>())
            {
                string id = GetAttributeValue(component, "id");
                if (string.Equals(id, targetId, StringComparison.OrdinalIgnoreCase))
                    return component;

                JArray children = component["components"] as JArray;
                if (children != null)
                {
                    JObject found = FindComponentById(children, targetId);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        private static void CollectComponentIds(JObject component, HashSet<string> ids)
        {
            string id = GetAttributeValue(component, "id");
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);

            JArray children = component["components"] as JArray;
            if (children != null)
            {
                foreach (JObject child in children.OfType<JObject>())
                    CollectComponentIds(child, ids);
            }
        }

        private static bool ReplaceComponent(JArray components, string targetId, JObject replacement)
        {
            for (int i = 0; i < components.Count; i++)
            {
                JObject component = components[i] as JObject;
                if (component == null) continue;

                string id = GetAttributeValue(component, "id");
                if (string.Equals(id, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    components[i] = replacement;
                    return true;
                }

                JArray children = component["components"] as JArray;
                if (children != null && ReplaceComponent(children, targetId, replacement))
                    return true;
            }
            return false;
        }

        private static int CountChildren(JObject component)
        {
            JArray children = component["components"] as JArray;
            return children?.Count ?? 0;
        }

        private static string GetAttributeValue(JObject component, string key)
        {
            JObject attrs = component["attributes"] as JObject;
            return attrs?[key]?.ToString();
        }

        #endregion

        #region JSON Helpers

        private static JArray ParseContentJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            try
            {
                JToken token = JToken.Parse(json);
                return token as JArray;
            }
            catch
            {
                return null;
            }
        }

        private static JArray FilterStyles(string styleJson, HashSet<string> componentIds)
        {
            if (string.IsNullOrWhiteSpace(styleJson) || componentIds.Count == 0)
                return new JArray();

            JArray styles;
            try
            {
                styles = JArray.Parse(styleJson);
            }
            catch
            {
                return new JArray();
            }

            var filtered = new JArray();
            foreach (JObject style in styles.OfType<JObject>())
            {
                JArray selectors = style["selectors"] as JArray;
                if (selectors == null)
                    continue;

                bool matches = selectors.OfType<JObject>()
                    .Any(s => componentIds.Contains(s["name"]?.ToString() ?? ""));

                if (matches)
                    filtered.Add(style);
            }
            return filtered;
        }

        private static string MergeStyles(string existingStyleJson, HashSet<string> oldComponentIds, string newStyleJson)
        {
            JArray existing;
            try
            {
                existing = string.IsNullOrWhiteSpace(existingStyleJson)
                    ? new JArray()
                    : JArray.Parse(existingStyleJson);
            }
            catch
            {
                existing = new JArray();
            }

            // Remove styles that belonged to the old component subtree
            if (oldComponentIds.Count > 0)
            {
                var kept = new JArray();
                foreach (JObject style in existing.OfType<JObject>())
                {
                    JArray selectors = style["selectors"] as JArray;
                    if (selectors == null)
                    {
                        kept.Add(style);
                        continue;
                    }

                    bool belongsToOld = selectors.OfType<JObject>()
                        .Any(s => oldComponentIds.Contains(s["name"]?.ToString() ?? ""));

                    if (!belongsToOld)
                        kept.Add(style);
                }
                existing = kept;
            }

            // Append new styles if provided
            if (!string.IsNullOrWhiteSpace(newStyleJson))
            {
                try
                {
                    JArray newStyles = JArray.Parse(newStyleJson);
                    foreach (JToken style in newStyles)
                        existing.Add(style);
                }
                catch
                {
                    // Ignore unparseable style input — keep existing styles intact
                }
            }

            return existing.ToString(Newtonsoft.Json.Formatting.None);
        }

        #endregion
    }
}
