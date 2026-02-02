using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FigmaSync.Editor.Generation;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Parsing;
using FigmaSync.Editor.Settings;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.Core
{
    /// <summary>
    /// Main orchestrator for the FigmaSync import process.
    /// </summary>
    public class FigmaSyncImporter
    {
        private readonly FigmaSyncSettings _settings;
        private FigmaApiClient _apiClient;

        public event Action<string, float> OnProgress;
        public event Action<string> OnStatusChanged;
        public event Action<string> OnError;
        public event Action OnComplete;

        public bool IsRunning { get; private set; }
        public string CurrentStatus { get; private set; }
        public float Progress { get; private set; }

        public FigmaSyncImporter(FigmaSyncSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Executes the full import process.
        /// </summary>
        public async Task ImportAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                OnError?.Invoke("Import already in progress");
                return;
            }

            IsRunning = true;
            Progress = 0f;

            try
            {
                // Validate settings
                var errors = _settings.Validate();
                if (errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        OnError?.Invoke(error);
                    }
                    return;
                }

                _apiClient = new FigmaApiClient(_settings.PersonalAccessToken);

                // Step 1: Fetch Figma document
                ReportProgress("Fetching Figma document...", 0.05f);
                var rawJson = await _apiClient.GetFileRawJsonAsync(_settings.FileKey, cancellationToken);

                if (_settings.ExportDebugJson)
                {
                    FigmaJsonExporter.ExportRawJson(rawJson, _settings.DebugJsonFolder, _settings.FileKey);
                }

                ReportProgress("Parsing Figma document...", 0.15f);
                var figmaResponse = JsonConvert.DeserializeObject<FigmaFileResponse>(rawJson);

                if (figmaResponse?.document == null)
                {
                    OnError?.Invoke("Failed to parse Figma document");
                    return;
                }

                // Step 2: Parse to intermediate format
                ReportProgress("Converting to intermediate format...", 0.25f);
                var parser = new FigmaParser(_settings);
                var syncDocument = parser.Parse(figmaResponse);

                if (_settings.ExportDebugJson)
                {
                    FigmaJsonExporter.ExportNodeTreeSummary(
                        figmaResponse.document,
                        _settings.DebugJsonFolder,
                        _settings.FileKey
                    );
                }

                // Step 3: Download assets
                ReportProgress("Downloading assets...", 0.35f);
                var assetDownloader = new AssetDownloader(_apiClient, _settings);
                await assetDownloader.DownloadAssetsAsync(syncDocument, cancellationToken,
                    (progress) => ReportProgress("Downloading assets...", 0.35f + progress * 0.25f));

                // Step 4: Generate Unity assets
                ReportProgress("Generating Unity prefabs...", 0.60f);
                var generator = new UnityGenerator(_settings);
                await generator.GenerateAsync(syncDocument, cancellationToken,
                    (progress) => ReportProgress("Generating Unity prefabs...", 0.60f + progress * 0.35f));

                // Complete
                ReportProgress("Import complete!", 1.0f);
                OnComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                ReportProgress("Import cancelled", Progress);
                OnStatusChanged?.Invoke("Import cancelled");
            }
            catch (FigmaApiException ex)
            {
                OnError?.Invoke($"Figma API error: {ex.Message}");
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Import failed: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>
        /// Fetches page information from the Figma file for selection UI.
        /// </summary>
        public async Task<List<PageInfo>> FetchPagesAsync(CancellationToken cancellationToken = default)
        {
            var pages = new List<PageInfo>();

            if (string.IsNullOrEmpty(_settings.PersonalAccessToken))
            {
                OnError?.Invoke("Personal Access Token is required");
                return pages;
            }

            if (string.IsNullOrEmpty(_settings.FileKey))
            {
                OnError?.Invoke("Valid Figma URL is required");
                return pages;
            }

            try
            {
                _apiClient = new FigmaApiClient(_settings.PersonalAccessToken);

                ReportProgress("Fetching page information...", 0.5f);
                var response = await _apiClient.GetFileAsync(_settings.FileKey, cancellationToken);

                if (response?.document?.children != null)
                {
                    foreach (var page in response.document.children)
                    {
                        if (page.type == "CANVAS")
                        {
                            var componentCount = CountComponents(page);
                            pages.Add(new PageInfo
                            {
                                Id = page.id,
                                Name = page.name,
                                ComponentCount = componentCount,
                                SuggestedLevel = _settings.GetAtomicLevelForPage(page.name)
                            });
                        }
                    }
                }

                ReportProgress("Pages loaded", 1.0f);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to fetch pages: {ex.Message}");
            }

            return pages;
        }

        /// <summary>
        /// Imports from a local JSON file instead of the API.
        /// </summary>
        public async Task ImportFromJsonAsync(string jsonFilePath, CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                OnError?.Invoke("Import already in progress");
                return;
            }

            IsRunning = true;
            Progress = 0f;

            try
            {
                ReportProgress("Loading JSON file...", 0.10f);
                var rawJson = FigmaJsonExporter.ImportRawJson(jsonFilePath);

                ReportProgress("Parsing Figma document...", 0.20f);
                var figmaResponse = JsonConvert.DeserializeObject<FigmaFileResponse>(rawJson);

                if (figmaResponse?.document == null)
                {
                    OnError?.Invoke("Failed to parse Figma document from JSON");
                    return;
                }

                // Parse to intermediate format
                ReportProgress("Converting to intermediate format...", 0.30f);
                var parser = new FigmaParser(_settings);
                var syncDocument = parser.Parse(figmaResponse);

                // For local import, we need to handle assets differently
                // Assets should already be available or we skip download
                ReportProgress("Processing assets...", 0.50f);

                // Generate Unity assets
                ReportProgress("Generating Unity prefabs...", 0.60f);
                var generator = new UnityGenerator(_settings);
                await generator.GenerateAsync(syncDocument, cancellationToken,
                    (progress) => ReportProgress("Generating Unity prefabs...", 0.60f + progress * 0.35f));

                ReportProgress("Import complete!", 1.0f);
                OnComplete?.Invoke();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Import failed: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private int CountComponents(FigmaNode node)
        {
            int count = 0;

            if (node.type == "COMPONENT" || node.type == "COMPONENT_SET")
            {
                count = 1;
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    count += CountComponents(child);
                }
            }

            return count;
        }

        private void ReportProgress(string status, float progress)
        {
            CurrentStatus = status;
            Progress = progress;
            OnProgress?.Invoke(status, progress);
            OnStatusChanged?.Invoke(status);
        }
    }

    /// <summary>
    /// Information about a Figma page.
    /// </summary>
    public class PageInfo
    {
        public string Id;
        public string Name;
        public int ComponentCount;
        public AtomicLevel SuggestedLevel;
    }
}
