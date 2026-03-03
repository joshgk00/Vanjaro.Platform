using DotNetNuke.Web.Api;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using Vanjaro.AI.Models;
using Vanjaro.Core.Entities.Interface;
using Vanjaro.Core.Entities.Theme;
using static Vanjaro.Core.Factories;

namespace Vanjaro.AI.Controllers
{
    [AllowAnonymous]
    [RequireAdmin]
    public class AIDesignController : DnnApiController
    {
        private static readonly Regex HexColorRegex = new Regex(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        [HttpGet]
        public HttpResponseMessage GetSettings()
        {
            try
            {
                int portalId = PortalSettings.PortalId;
                var theme = Vanjaro.Core.Managers.ThemeManager.GetCurrent(portalId);
                string themeName = theme.Name;

                var result = CollectAllControls(portalId, themeName);

                var response = new DesignSettingsResponse
                {
                    ThemeName = themeName,
                    Controls = result.Controls.ToArray(),
                    AvailableFonts = result.AvailableFonts.Count > 0 ? result.AvailableFonts.ToArray() : null
                };

                return Request.CreateResponse(HttpStatusCode.OK, response);
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to read design settings: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage UpdateSettings(DesignUpdateRequest request)
        {
            try
            {
                if (request?.Controls == null || request.Controls.Length == 0)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Controls array is required and must not be empty.");

                int portalId = PortalSettings.PortalId;
                var theme = Vanjaro.Core.Managers.ThemeManager.GetCurrent(portalId);
                string themeName = theme.Name;

                var collected = CollectAllControls(portalId, themeName);

                var resolvedUpdates = new List<ResolvedUpdate>();
                foreach (var item in request.Controls)
                {
                    if (string.IsNullOrWhiteSpace(item.Value))
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Each control must have a value.");

                    var resolved = ResolveControl(item, collected.Index);
                    if (resolved == null)
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, $"Control not found for guid='{item.Guid}', lessVariable='{item.LessVariable}'.");

                    string validatedValue = ValidateValue(resolved, item.Value, collected.AvailableFonts);
                    if (validatedValue == null)
                        return Request.CreateErrorResponse(HttpStatusCode.BadRequest, GetValidationErrorMessage(resolved, item.Value));

                    resolvedUpdates.Add(new ResolvedUpdate { Control = resolved, Value = validatedValue });
                }

                var updatesByCategory = new Dictionary<string, List<ThemeEditorValue>>();
                var snapshots = new Dictionary<string, string>();

                foreach (var update in resolvedUpdates)
                {
                    string catGuid = update.Control.CategoryGuid;

                    if (!updatesByCategory.ContainsKey(catGuid))
                    {
                        updatesByCategory[catGuid] = new List<ThemeEditorValue>();
                        string existingPath = FindValueJsonPath(portalId, themeName, catGuid);
                        if (existingPath != null)
                            snapshots[catGuid] = File.ReadAllText(existingPath);
                    }

                    updatesByCategory[catGuid].Add(new ThemeEditorValue
                    {
                        Guid = update.Control.Guid,
                        Value = update.Value
                    });
                }

                try
                {
                    foreach (var kvp in updatesByCategory)
                    {
                        string catGuid = kvp.Key;
                        var newValues = kvp.Value;

                        var existing = ReadThemeValues(portalId, themeName, catGuid) ?? new List<ThemeEditorValue>();

                        foreach (var nv in newValues)
                        {
                            var match = existing.FirstOrDefault(e => e.Guid == nv.Guid);
                            if (match != null)
                                match.Value = nv.Value;
                            else
                                existing.Add(nv);
                        }

                        Vanjaro.Core.Managers.ThemeManager.Save(catGuid, existing);
                    }

                    Vanjaro.Core.Managers.ThemeManager.ProcessScss(portalId, false);
                }
                catch (Exception scssEx)
                {
                    foreach (var kvp in snapshots)
                    {
                        string jsonPath = EnsureValueJsonPath(portalId, themeName, kvp.Key);
                        File.WriteAllText(jsonPath, kvp.Value);
                    }
                    CacheFactory.Clear(CacheFactory.Keys.ThemeManager);

                    Vanjaro.Core.Managers.ExceptionManager.LogException(scssEx);
                    return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "SCSS compilation failed; changes rolled back: " + scssEx.Message);
                }

                return GetSettings();
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to update design settings: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage ResetSettings()
        {
            try
            {
                int portalId = PortalSettings.PortalId;
                var theme = Vanjaro.Core.Managers.ThemeManager.GetCurrent(portalId);
                string themeName = theme.Name;

                var categories = Vanjaro.Core.Managers.ThemeManager.GetCategories(false);
                bool anyReset = false;

                foreach (IThemeEditor category in categories)
                {
                    string existingPath = FindValueJsonPath(portalId, themeName, category.Guid);
                    if (existingPath != null)
                    {
                        File.WriteAllText(existingPath, "[]");
                        anyReset = true;
                    }
                }

                if (anyReset)
                {
                    CacheFactory.Clear(CacheFactory.Keys.ThemeManager);
                    Vanjaro.Core.Managers.ThemeManager.ProcessScss(portalId, false);
                }

                return GetSettings();
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to reset design settings: " + ex.Message);
            }
        }

        #region Private Helpers

        private class ControlCollection
        {
            public List<DesignControl> Controls { get; set; }
            public Dictionary<string, DesignControl> Index { get; set; }
            public List<DesignFontOption> AvailableFonts { get; set; }
        }

        private class ResolvedUpdate
        {
            public DesignControl Control { get; set; }
            public string Value { get; set; }
        }

        private ControlCollection CollectAllControls(int portalId, string themeName)
        {
            var categories = Vanjaro.Core.Managers.ThemeManager.GetCategories(false);
            var controls = new List<DesignControl>();
            var index = new Dictionary<string, DesignControl>(StringComparer.OrdinalIgnoreCase);
            var fontSet = new Dictionary<string, DesignFontOption>(StringComparer.OrdinalIgnoreCase);

            foreach (IThemeEditor category in categories)
            {
                var wrapper = Vanjaro.Core.Managers.ThemeManager.GetThemeEditors(portalId, category.Guid, false);
                if (wrapper?.ThemeEditors == null)
                    continue;

                // Collect available fonts from the wrapper during the main loop
                if (wrapper.Fonts != null)
                {
                    foreach (ThemeFont font in wrapper.Fonts)
                    {
                        if (!string.IsNullOrEmpty(font.Name) && !fontSet.ContainsKey(font.Name))
                            fontSet[font.Name] = new DesignFontOption { Name = font.Name, Value = font.Family };
                    }
                }

                var currentValues = ReadThemeValues(portalId, themeName, category.Guid);

                foreach (ThemeEditor editor in wrapper.ThemeEditors)
                {
                    if (editor.Controls == null)
                        continue;

                    foreach (dynamic control in editor.Controls)
                    {
                        string controlType = null;
                        try { controlType = (string)control.Type; } catch { continue; }

                        if (string.IsNullOrEmpty(controlType))
                            continue;

                        string controlGuid = (string)control.Guid;
                        string lessVariable = (string)control.LessVariable;
                        string defaultValue = (string)control.DefaultValue;

                        var savedValue = currentValues?.FirstOrDefault(v => v.Guid == controlGuid);

                        var entry = new DesignControl
                        {
                            Guid = controlGuid,
                            Title = (string)control.Title,
                            Type = controlType,
                            LessVariable = lessVariable,
                            CurrentValue = savedValue?.Value ?? defaultValue,
                            DefaultValue = defaultValue,
                            DefaultIsVariable = !string.IsNullOrEmpty(defaultValue) && defaultValue.StartsWith("$"),
                            Category = editor.Category,
                            CategoryGuid = category.Guid
                        };

                        if (controlType == "Slider")
                        {
                            try { entry.RangeMin = (float)control.RangeMin; } catch { }
                            try { entry.RangeMax = (float)control.RangeMax; } catch { }
                            try { entry.Increment = (float)control.Increment; } catch { }
                        }

                        try
                        {
                            string suffix = (string)control.Suffix;
                            if (!string.IsNullOrEmpty(suffix))
                                entry.Suffix = suffix;
                        }
                        catch { }

                        if (controlType == "Dropdown")
                        {
                            try
                            {
                                var opts = new List<DesignDropdownOption>();
                                foreach (JToken opt in (JArray)control.Options)
                                {
                                    JProperty prop = (JProperty)((JObject)opt).First;
                                    opts.Add(new DesignDropdownOption { Value = prop.Name, Label = prop.Value.ToString() });
                                }
                                if (opts.Count > 0)
                                    entry.Options = opts.ToArray();
                            }
                            catch { }
                        }

                        controls.Add(entry);

                        if (!string.IsNullOrEmpty(controlGuid))
                            index["guid:" + controlGuid] = entry;

                        if (!string.IsNullOrEmpty(lessVariable))
                            index["var:" + lessVariable] = entry;
                    }
                }
            }

            return new ControlCollection
            {
                Controls = controls,
                Index = index,
                AvailableFonts = fontSet.Values.ToList()
            };
        }

        private string ValidateValue(DesignControl control, string value, List<DesignFontOption> availableFonts)
        {
            switch (control.Type)
            {
                case "Color Picker":
                    return HexColorRegex.IsMatch(value) ? value : null;

                case "Slider":
                    float floatVal;
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatVal))
                        return null;
                    if (control.RangeMin.HasValue && floatVal < control.RangeMin.Value)
                        return null;
                    if (control.RangeMax.HasValue && floatVal > control.RangeMax.Value)
                        return null;
                    return value;

                case "Dropdown":
                    if (control.Options == null || !control.Options.Any(o => o.Value == value))
                        return null;
                    return value;

                case "Fonts":
                    if (availableFonts == null)
                        return null;
                    var matchingFont = availableFonts.FirstOrDefault(f =>
                        string.Equals(f.Value, value, StringComparison.OrdinalIgnoreCase));
                    return matchingFont?.Value;

                default:
                    return value;
            }
        }

        private string GetValidationErrorMessage(DesignControl control, string value)
        {
            switch (control.Type)
            {
                case "Color Picker":
                    return $"Invalid hex color: {value}. Must be format #RRGGBB.";
                case "Slider":
                    float floatVal;
                    if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out floatVal))
                        return $"Invalid slider value: {value}. Must be a number.";
                    return $"Slider value {value} out of range [{control.RangeMin}, {control.RangeMax}] for '{control.Title}'.";
                case "Dropdown":
                    return $"Invalid dropdown value: {value}. Must be one of the available options for '{control.Title}'.";
                case "Fonts":
                    return $"Invalid font: {value}. Must be one of the available fonts.";
                default:
                    return $"Invalid value: {value} for control '{control.Title}'.";
            }
        }

        private string FindValueJsonPath(int portalId, string themeName, string categoryGuid)
        {
            string filePath = HttpContext.Current.Server.MapPath(
                "~/Portals/" + portalId + "/vThemes/" + themeName + "/editor/" + categoryGuid + "/theme.json");

            return File.Exists(filePath) ? filePath : null;
        }

        private string EnsureValueJsonPath(int portalId, string themeName, string categoryGuid)
        {
            string folderPath = HttpContext.Current.Server.MapPath(
                "~/Portals/" + portalId + "/vThemes/" + themeName + "/editor/" + categoryGuid);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, "theme.json");
            if (!File.Exists(filePath))
                File.Create(filePath).Dispose();

            return filePath;
        }

        private List<ThemeEditorValue> ReadThemeValues(int portalId, string themeName, string categoryGuid)
        {
            string jsonPath = FindValueJsonPath(portalId, themeName, categoryGuid);
            if (jsonPath == null)
                return new List<ThemeEditorValue>();

            try
            {
                string content = File.ReadAllText(jsonPath);
                if (string.IsNullOrWhiteSpace(content))
                    return new List<ThemeEditorValue>();

                return JsonConvert.DeserializeObject<List<ThemeEditorValue>>(content)
                    ?? new List<ThemeEditorValue>();
            }
            catch
            {
                return new List<ThemeEditorValue>();
            }
        }

        private DesignControl ResolveControl(DesignUpdateItem item, Dictionary<string, DesignControl> index)
        {
            if (!string.IsNullOrEmpty(item.Guid))
            {
                DesignControl byGuid;
                if (index.TryGetValue("guid:" + item.Guid, out byGuid))
                    return byGuid;
            }

            if (!string.IsNullOrEmpty(item.LessVariable))
            {
                DesignControl byVar;
                if (index.TryGetValue("var:" + item.LessVariable, out byVar))
                    return byVar;
            }

            return null;
        }

        #endregion
    }
}
