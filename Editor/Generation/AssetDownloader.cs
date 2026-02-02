using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FigmaSync.Editor.Core;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.Generation
{
    /// <summary>
    /// Downloads and imports image assets from Figma.
    /// </summary>
    public class AssetDownloader
    {
        private readonly FigmaApiClient _apiClient;
        private readonly FigmaSyncSettings _settings;
        private Dictionary<string, string> _downloadedAssets = new Dictionary<string, string>();

        public AssetDownloader(FigmaApiClient apiClient, FigmaSyncSettings settings)
        {
            _apiClient = apiClient;
            _settings = settings;
        }

        /// <summary>
        /// Downloads all required assets for the document.
        /// </summary>
        public async Task DownloadAssetsAsync(SyncDocument document, CancellationToken cancellationToken, Action<float> onProgress = null)
        {
            EnsureDirectoryExists(_settings.AssetsFolder);
            EnsureDirectoryExists(Path.Combine(_settings.AssetsFolder, "Images"));
            EnsureDirectoryExists(Path.Combine(_settings.AssetsFolder, "Icons"));

            // Collect all nodes that need image export
            var imageNodes = document.GetAllImageNodes();
            var nodeIdsToExport = new List<string>();

            foreach (var node in imageNodes)
            {
                // For image fills, we use the image hash
                // For vectors/icons, we export the node as an image
                if (node.FigmaType == "VECTOR" || node.FigmaType == "BOOLEAN_OPERATION" ||
                    node.FigmaType == "STAR" || node.FigmaType == "POLYGON" ||
                    node.FigmaType == "ELLIPSE" || node.FigmaType == "LINE")
                {
                    nodeIdsToExport.Add(node.Id);
                }
            }

            // Get image fill URLs
            var imageFillUrls = new Dictionary<string, string>();
            if (document.ImageHashes.Count > 0)
            {
                try
                {
                    imageFillUrls = await _apiClient.GetImageFillsAsync(_settings.FileKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FigmaSync] Failed to get image fills: {ex.Message}");
                }
            }

            // Get node export URLs
            var nodeExportUrls = new Dictionary<string, string>();
            if (nodeIdsToExport.Count > 0)
            {
                try
                {
                    var format = _settings.ImageExportFormat.ToString().ToLower();
                    nodeExportUrls = await _apiClient.GetImageUrlsAsync(
                        _settings.FileKey,
                        nodeIdsToExport,
                        format,
                        _settings.ImageScale,
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FigmaSync] Failed to get node export URLs: {ex.Message}");
                }
            }

            // Download all images
            int totalImages = imageFillUrls.Count + nodeExportUrls.Count;
            int downloadedCount = 0;

            // Download image fills
            foreach (var kvp in imageFillUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var imageHash = kvp.Key;
                var url = kvp.Value;

                if (string.IsNullOrEmpty(url)) continue;

                try
                {
                    var assetPath = await DownloadImageAsync(url, imageHash, "Images", cancellationToken);
                    _downloadedAssets[imageHash] = assetPath;

                    // Update nodes that use this hash
                    if (document.ImageHashes.TryGetValue(imageHash, out var nodeIds))
                    {
                        foreach (var nodeId in nodeIds)
                        {
                            var node = document.FindNodeById(nodeId);
                            if (node != null)
                            {
                                node.ImageAssetPath = assetPath;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FigmaSync] Failed to download image {imageHash}: {ex.Message}");
                }

                downloadedCount++;
                onProgress?.Invoke((float)downloadedCount / totalImages);
            }

            // Download node exports (vectors/icons)
            foreach (var kvp in nodeExportUrls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var nodeId = kvp.Key;
                var url = kvp.Value;

                if (string.IsNullOrEmpty(url)) continue;

                var node = document.FindNodeById(nodeId);
                if (node == null) continue;

                try
                {
                    var assetPath = await DownloadImageAsync(url, node.CleanName, "Icons", cancellationToken);
                    _downloadedAssets[$"node:{nodeId}"] = assetPath;
                    node.ImageAssetPath = assetPath;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FigmaSync] Failed to download node image {nodeId}: {ex.Message}");
                }

                downloadedCount++;
                onProgress?.Invoke((float)downloadedCount / totalImages);
            }

            // Refresh asset database
            AssetDatabase.Refresh();

            Debug.Log($"[FigmaSync] Downloaded {downloadedCount} assets");
        }

        /// <summary>
        /// Downloads a single image and saves it to disk.
        /// </summary>
        private async Task<string> DownloadImageAsync(string url, string name, string subfolder, CancellationToken cancellationToken)
        {
            var bytes = await _apiClient.DownloadImageAsync(url, cancellationToken);

            var cleanName = CleanAssetName(name);
            var extension = GetExtensionFromFormat(_settings.ImageExportFormat);
            var relativePath = Path.Combine(_settings.AssetsFolder, subfolder, $"{cleanName}{extension}");
            var fullPath = Path.GetFullPath(relativePath);

            // Ensure unique filename
            var counter = 1;
            while (File.Exists(fullPath))
            {
                relativePath = Path.Combine(_settings.AssetsFolder, subfolder, $"{cleanName}_{counter}{extension}");
                fullPath = Path.GetFullPath(relativePath);
                counter++;
            }

            File.WriteAllBytes(fullPath, bytes);

            // Configure import settings
            ConfigureTextureImporter(relativePath);

            return relativePath;
        }

        /// <summary>
        /// Configures texture import settings for UI sprites.
        /// </summary>
        private void ConfigureTextureImporter(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;

                // For icons, set up 9-slice if appropriate
                var filename = Path.GetFileNameWithoutExtension(assetPath).ToLower();
                if (filename.Contains("button") || filename.Contains("panel") || filename.Contains("card"))
                {
                    // These might benefit from 9-slice
                    // But we can't automatically determine the border
                }

                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Gets the downloaded asset path for a given identifier.
        /// </summary>
        public string GetAssetPath(string identifier)
        {
            return _downloadedAssets.TryGetValue(identifier, out var path) ? path : null;
        }

        /// <summary>
        /// Loads a sprite from the downloaded assets.
        /// </summary>
        public Sprite LoadSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        /// <summary>
        /// Cleans a name to be a valid asset filename.
        /// </summary>
        private string CleanAssetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Asset";

            // Remove or replace invalid characters
            var clean = Regex.Replace(name, @"[^\w\-]", "_");
            clean = Regex.Replace(clean, @"_+", "_").Trim('_');

            if (string.IsNullOrEmpty(clean)) return "Asset";

            // Limit length
            if (clean.Length > 64)
            {
                clean = clean.Substring(0, 64);
            }

            return clean;
        }

        private string GetExtensionFromFormat(ImageFormat format)
        {
            switch (format)
            {
                case ImageFormat.JPG: return ".jpg";
                case ImageFormat.SVG: return ".svg";
                default: return ".png";
            }
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
