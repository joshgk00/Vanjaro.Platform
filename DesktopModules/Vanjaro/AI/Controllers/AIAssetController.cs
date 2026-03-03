using DotNetNuke.Services.FileSystem;
using DotNetNuke.Web.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Vanjaro.AI.Models;

namespace Vanjaro.AI.Controllers
{
    [AllowAnonymous]
    [RequireAdmin]
    public class AIAssetController : DnnApiController
    {
        private static readonly HttpClient DownloadClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private const long MaxDownloadBytes = 50 * 1024 * 1024; // 50 MB

        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".svg", ".webp", ".ico",
            ".pdf", ".doc", ".docx"
        };

        private static readonly string[] ExcludedFolderPrefixes =
        {
            "vThemes/", "Users/", "Templates/", "Containers/", "Skins/"
        };

        [HttpGet]
        public HttpResponseMessage ListFolders()
        {
            try
            {
                int portalId = PortalSettings.PortalId;
                var folders = FolderManager.Instance.GetFolders(portalId);

                var items = new List<AssetFolderItem>();
                foreach (var folder in folders)
                {
                    if (IsExcludedFolder(folder.FolderPath))
                        continue;

                    items.Add(new AssetFolderItem
                    {
                        FolderId = folder.FolderID,
                        FolderPath = folder.FolderPath,
                        DisplayName = folder.DisplayName
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, items);
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to list folders: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage CreateFolder(AssetCreateFolderRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.FolderPath))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "folderPath is required.");

                int portalId = PortalSettings.PortalId;
                string folderPath = NormalizeFolderPath(request.FolderPath);

                var existing = FolderManager.Instance.GetFolder(portalId, folderPath);
                if (existing != null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new AssetFolderItem
                    {
                        FolderId = existing.FolderID,
                        FolderPath = existing.FolderPath,
                        DisplayName = existing.DisplayName
                    });
                }

                var created = FolderManager.Instance.AddFolder(portalId, folderPath);
                return Request.CreateResponse(HttpStatusCode.Created, new AssetFolderItem
                {
                    FolderId = created.FolderID,
                    FolderPath = created.FolderPath,
                    DisplayName = created.DisplayName
                });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to create folder: " + ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage ListFiles(int folderId)
        {
            try
            {
                int portalId = PortalSettings.PortalId;
                var folder = FolderManager.Instance.GetFolder(folderId);

                if (folder == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Folder not found.");

                if (folder.PortalID != portalId)
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Folder does not belong to this portal.");

                var files = FolderManager.Instance.GetFiles(folder);
                var items = files.Select(f => AssetFileItem.FromDnn(f)).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, items);
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to list files: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage Upload(AssetUploadRequest request)
        {
            try
            {
                if (request == null)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Request body is required.");

                if (string.IsNullOrWhiteSpace(request.FileName))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "fileName is required.");

                bool hasBase64 = !string.IsNullOrWhiteSpace(request.Base64Content);
                bool hasUrl = !string.IsNullOrWhiteSpace(request.SourceUrl);

                if (!hasBase64 && !hasUrl)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "One of base64Content or sourceUrl is required.");

                string ext = Path.GetExtension(request.FileName);
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, $"File extension '{ext}' is not allowed. Allowed: {string.Join(", ", AllowedExtensions)}");

                int portalId = PortalSettings.PortalId;

                var targetFolder = ResolveOrCreateFolder(portalId, request.FolderPath);

                Stream fileStream;
                string contentError;
                if (hasBase64)
                    fileStream = DecodeBase64Content(request.Base64Content, out contentError);
                else
                    fileStream = DownloadFromUrl(request.SourceUrl, out contentError);

                if (fileStream == null)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, contentError);

                using (fileStream)
                {
                    var addedFile = FileManager.Instance.AddFile(targetFolder, request.FileName, fileStream, request.Overwrite);
                    return Request.CreateResponse(HttpStatusCode.Created, AssetFileItem.FromDnn(addedFile));
                }
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to upload file: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage Delete(AssetDeleteRequest request)
        {
            try
            {
                if (request == null || request.FileId <= 0)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "fileId is required.");

                int portalId = PortalSettings.PortalId;
                var file = FileManager.Instance.GetFile(request.FileId);

                if (file == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "File not found.");

                if (file.PortalId != portalId)
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "File does not belong to this portal.");

                FileManager.Instance.DeleteFile(file);

                return Request.CreateResponse(HttpStatusCode.OK, new { fileId = request.FileId, deleted = true });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to delete file: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage DeleteFolder(AssetDeleteFolderRequest request)
        {
            try
            {
                if (request == null || request.FolderId <= 0)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "folderId is required.");

                int portalId = PortalSettings.PortalId;
                var folder = FolderManager.Instance.GetFolder(request.FolderId);

                if (folder == null)
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Folder not found.");

                if (folder.PortalID != portalId)
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden, "Folder does not belong to this portal.");

                if (string.IsNullOrEmpty(folder.FolderPath))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Cannot delete the root folder.");

                FolderManager.Instance.DeleteFolder(folder);

                return Request.CreateResponse(HttpStatusCode.OK, new { folderId = request.FolderId, deleted = true });
            }
            catch (Exception ex)
            {
                Vanjaro.Core.Managers.ExceptionManager.LogException(ex);
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Failed to delete folder: " + ex.Message);
            }
        }

        #region Private Helpers

        private static bool IsExcludedFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return false;

            foreach (var prefix in ExcludedFolderPrefixes)
            {
                if (folderPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return folderPath.Contains(".versions");
        }

        private static string NormalizeFolderPath(string path)
        {
            path = path.Replace("\\", "/").Trim('/');
            if (!path.EndsWith("/"))
                path += "/";
            return path;
        }

        private static IFolderInfo ResolveOrCreateFolder(int portalId, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return FolderManager.Instance.GetFolder(portalId, "");

            string normalized = NormalizeFolderPath(folderPath);
            return FolderManager.Instance.GetFolder(portalId, normalized)
                ?? FolderManager.Instance.AddFolder(portalId, normalized);
        }

        private static MemoryStream DecodeBase64Content(string base64, out string error)
        {
            error = null;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                error = "Invalid base64Content.";
                return null;
            }

            if (bytes.Length > MaxDownloadBytes)
            {
                error = $"File exceeds maximum size of {MaxDownloadBytes / (1024 * 1024)}MB.";
                return null;
            }

            return new MemoryStream(bytes);
        }

        private static MemoryStream DownloadFromUrl(string url, out string error)
        {
            error = null;
            try
            {
                var response = DownloadClient.GetAsync(url).Result;
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > MaxDownloadBytes)
                {
                    error = $"Remote file exceeds maximum size of {MaxDownloadBytes / (1024 * 1024)}MB.";
                    return null;
                }

                var bytes = response.Content.ReadAsByteArrayAsync().Result;
                if (bytes.Length > MaxDownloadBytes)
                {
                    error = $"Remote file exceeds maximum size of {MaxDownloadBytes / (1024 * 1024)}MB.";
                    return null;
                }

                return new MemoryStream(bytes);
            }
            catch (HttpRequestException httpEx)
            {
                error = "Failed to download from sourceUrl: " + httpEx.Message;
                return null;
            }
            catch (AggregateException aggEx)
            {
                error = "Failed to download from sourceUrl: " + (aggEx.InnerException?.Message ?? aggEx.Message);
                return null;
            }
        }

        #endregion
    }
}
