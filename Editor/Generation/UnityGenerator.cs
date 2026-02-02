using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.Generation
{
    /// <summary>
    /// Main Unity asset generation orchestrator.
    /// </summary>
    public class UnityGenerator
    {
        private readonly FigmaSyncSettings _settings;
        private PrefabBuilder _prefabBuilder;
        private AtomicDesignManager _atomicManager;

        public UnityGenerator(FigmaSyncSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Generates all Unity assets from the SyncDocument.
        /// </summary>
        public Task GenerateAsync(SyncDocument document, CancellationToken cancellationToken, Action<float> onProgress = null)
        {
            _atomicManager = new AtomicDesignManager(_settings);

            // Ensure output directories exist
            EnsureDirectoriesExist();

            // Get AssetDownloader (should be passed in or created)
            // For now, we'll create prefabs without downloaded assets
            _prefabBuilder = new PrefabBuilder(_settings, null);

            var buildOrder = AtomicDesignManager.GetBuildOrder();
            var totalLevels = buildOrder.Length;
            var processedLevels = 0;

            foreach (var level in buildOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (level == AtomicLevel.Skip) continue;

                var nodes = document.GetNodesByAtomicLevel(level);

                if (nodes.Count == 0)
                {
                    processedLevels++;
                    continue;
                }

                Debug.Log($"[FigmaSync] Generating {AtomicDesignManager.GetLevelDisplayName(level)} prefabs ({nodes.Count} items)");

                var processedNodes = 0;
                foreach (var node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (node.AtomicLevel == AtomicLevel.Skip) continue;

                    // Build the GameObject hierarchy
                    var go = _prefabBuilder.Build(node);

                    // Replace component instances with prefab references
                    if (level != AtomicLevel.Atom)
                    {
                        _atomicManager.ReplaceInstancesWithPrefabs(go, node);
                    }

                    // Save as prefab
                    var prefabPath = _prefabBuilder.SaveAsPrefab(go, node, _atomicManager);

                    // Cleanup temporary GameObject
                    UnityEngine.Object.DestroyImmediate(go);

                    // Update component definition
                    if (document.Components.TryGetValue(node.Id, out var componentDef))
                    {
                        componentDef.PrefabPath = prefabPath;
                    }

                    processedNodes++;

                    // Report progress
                    float levelProgress = (float)processedNodes / nodes.Count;
                    float overallProgress = (processedLevels + levelProgress) / totalLevels;
                    onProgress?.Invoke(overallProgress);
                }

                processedLevels++;
            }

            // Final asset database refresh
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FigmaSync] Generation complete");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates prefabs with downloaded assets.
        /// </summary>
        public Task GenerateWithAssetsAsync(
            SyncDocument document,
            AssetDownloader assetDownloader,
            CancellationToken cancellationToken,
            Action<float> onProgress = null)
        {
            _atomicManager = new AtomicDesignManager(_settings);
            _prefabBuilder = new PrefabBuilder(_settings, assetDownloader);

            EnsureDirectoriesExist();

            var buildOrder = AtomicDesignManager.GetBuildOrder();
            var totalLevels = buildOrder.Length;
            var processedLevels = 0;

            foreach (var level in buildOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (level == AtomicLevel.Skip) continue;

                var nodes = document.GetNodesByAtomicLevel(level);

                if (nodes.Count == 0)
                {
                    processedLevels++;
                    continue;
                }

                Debug.Log($"[FigmaSync] Generating {AtomicDesignManager.GetLevelDisplayName(level)} prefabs ({nodes.Count} items)");

                var processedNodes = 0;
                foreach (var node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (node.AtomicLevel == AtomicLevel.Skip) continue;

                    // Build the GameObject hierarchy
                    var go = _prefabBuilder.Build(node);

                    // Replace component instances with prefab references
                    if (level != AtomicLevel.Atom)
                    {
                        _atomicManager.ReplaceInstancesWithPrefabs(go, node);
                    }

                    // Save as prefab
                    var prefabPath = _prefabBuilder.SaveAsPrefab(go, node, _atomicManager);

                    // Cleanup temporary GameObject
                    UnityEngine.Object.DestroyImmediate(go);

                    // Update component definition
                    if (document.Components.TryGetValue(node.Id, out var componentDef))
                    {
                        componentDef.PrefabPath = prefabPath;
                    }

                    processedNodes++;

                    // Report progress
                    float levelProgress = (float)processedNodes / nodes.Count;
                    float overallProgress = (processedLevels + levelProgress) / totalLevels;
                    onProgress?.Invoke(overallProgress);
                }

                processedLevels++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FigmaSync] Generation complete");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Generates a single prefab for preview purposes.
        /// </summary>
        public GameObject GeneratePreview(SyncNode node)
        {
            if (_prefabBuilder == null)
            {
                _prefabBuilder = new PrefabBuilder(_settings, null);
            }

            return _prefabBuilder.Build(node);
        }

        /// <summary>
        /// Cleans up generated assets.
        /// </summary>
        public void CleanupGeneratedAssets()
        {
            if (Directory.Exists(_settings.PrefabsFolder))
            {
                Directory.Delete(_settings.PrefabsFolder, true);
            }

            if (Directory.Exists(_settings.AssetsFolder))
            {
                Directory.Delete(_settings.AssetsFolder, true);
            }

            AssetDatabase.Refresh();
        }

        private void EnsureDirectoriesExist()
        {
            var directories = new[]
            {
                _settings.PrefabsFolder,
                $"{_settings.PrefabsFolder}/Atoms",
                $"{_settings.PrefabsFolder}/Molecules",
                $"{_settings.PrefabsFolder}/Organisms",
                $"{_settings.PrefabsFolder}/Screens",
                _settings.AssetsFolder,
                $"{_settings.AssetsFolder}/Images",
                $"{_settings.AssetsFolder}/Icons",
                _settings.FontsFolder
            };

            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
