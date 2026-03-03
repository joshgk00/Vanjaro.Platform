using DotNetNuke.Services.FileSystem;
using Newtonsoft.Json;
using System;

namespace Vanjaro.AI.Models
{
    public class AssetUploadRequest
    {
        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("folderPath")]
        public string FolderPath { get; set; }

        [JsonProperty("base64Content")]
        public string Base64Content { get; set; }

        [JsonProperty("sourceUrl")]
        public string SourceUrl { get; set; }

        [JsonProperty("overwrite")]
        public bool Overwrite { get; set; } = true;
    }

    public class AssetCreateFolderRequest
    {
        [JsonProperty("folderPath")]
        public string FolderPath { get; set; }
    }

    public class AssetDeleteRequest
    {
        [JsonProperty("fileId")]
        public int FileId { get; set; }
    }

    public class AssetDeleteFolderRequest
    {
        [JsonProperty("folderId")]
        public int FolderId { get; set; }
    }

    public class AssetFolderItem
    {
        [JsonProperty("folderId")]
        public int FolderId { get; set; }

        [JsonProperty("folderPath")]
        public string FolderPath { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }
    }

    public class AssetFileItem
    {
        [JsonProperty("fileId")]
        public int FileId { get; set; }

        [JsonProperty("fileName")]
        public string FileName { get; set; }

        [JsonProperty("folderPath")]
        public string FolderPath { get; set; }

        [JsonProperty("relativePath")]
        public string RelativePath { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("extension")]
        public string Extension { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("width", NullValueHandling = NullValueHandling.Ignore)]
        public int? Width { get; set; }

        [JsonProperty("height", NullValueHandling = NullValueHandling.Ignore)]
        public int? Height { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; }

        public static AssetFileItem FromDnn(IFileInfo file)
        {
            if (file == null)
                return null;

            return new AssetFileItem
            {
                FileId = file.FileId,
                FileName = file.FileName,
                FolderPath = file.Folder,
                RelativePath = file.RelativePath,
                Url = FileManager.Instance.GetUrl(file),
                Extension = file.Extension,
                Size = file.Size,
                Width = file.Width > 0 ? file.Width : (int?)null,
                Height = file.Height > 0 ? file.Height : (int?)null,
                ContentType = file.ContentType,
                LastModified = file.LastModifiedOnDate
            };
        }
    }
}
