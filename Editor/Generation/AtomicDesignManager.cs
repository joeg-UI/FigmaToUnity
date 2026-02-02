using System.Collections.Generic;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.Generation
{
    /// <summary>
    /// Manages atomic design hierarchy for prefab generation.
    /// </summary>
    public class AtomicDesignManager
    {
        private readonly FigmaSyncSettings _settings;
        private readonly Dictionary<string, GameObject> _prefabRegistry = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, string> _componentToPrefabPath = new Dictionary<string, string>();

        public AtomicDesignManager(FigmaSyncSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Gets the build order for atomic levels.
        /// </summary>
        public static AtomicLevel[] GetBuildOrder()
        {
            return new[]
            {
                AtomicLevel.Atom,
                AtomicLevel.Molecule,
                AtomicLevel.Organism,
                AtomicLevel.Screen
            };
        }

        /// <summary>
        /// Gets the folder path for a given atomic level.
        /// </summary>
        public string GetFolderForLevel(AtomicLevel level)
        {
            var basePath = _settings.PrefabsFolder;

            switch (level)
            {
                case AtomicLevel.Atom:
                    return $"{basePath}/Atoms";
                case AtomicLevel.Molecule:
                    return $"{basePath}/Molecules";
                case AtomicLevel.Organism:
                    return $"{basePath}/Organisms";
                case AtomicLevel.Screen:
                    return $"{basePath}/Screens";
                default:
                    return basePath;
            }
        }

        /// <summary>
        /// Registers a created prefab.
        /// </summary>
        public void RegisterPrefab(string componentId, GameObject prefab, string prefabPath)
        {
            _prefabRegistry[componentId] = prefab;
            _componentToPrefabPath[componentId] = prefabPath;
        }

        /// <summary>
        /// Gets a registered prefab by component ID.
        /// </summary>
        public GameObject GetPrefab(string componentId)
        {
            return _prefabRegistry.TryGetValue(componentId, out var prefab) ? prefab : null;
        }

        /// <summary>
        /// Gets the prefab path for a component ID.
        /// </summary>
        public string GetPrefabPath(string componentId)
        {
            return _componentToPrefabPath.TryGetValue(componentId, out var path) ? path : null;
        }

        /// <summary>
        /// Checks if a prefab exists for a component.
        /// </summary>
        public bool HasPrefab(string componentId)
        {
            return _prefabRegistry.ContainsKey(componentId);
        }

        /// <summary>
        /// Replaces component instances with prefab references in a hierarchy.
        /// </summary>
        public void ReplaceInstancesWithPrefabs(GameObject root, SyncNode rootNode)
        {
            ReplaceInstancesRecursive(root.transform, rootNode);
        }

        private void ReplaceInstancesRecursive(Transform parent, SyncNode parentNode)
        {
            var childCount = parent.childCount;

            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                var childNode = FindMatchingNode(parentNode, child.name);

                if (childNode != null && childNode.IsComponentInstance && HasPrefab(childNode.ComponentId))
                {
                    // Replace with prefab instance
                    var prefab = GetPrefab(childNode.ComponentId);
                    var prefabPath = GetPrefabPath(childNode.ComponentId);

                    if (prefab != null && !string.IsNullOrEmpty(prefabPath))
                    {
                        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                        if (prefabAsset != null)
                        {
                            // Create prefab instance
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent);

                            // Copy transform
                            instance.transform.SetSiblingIndex(i);
                            CopyRectTransform(child.GetComponent<RectTransform>(), instance.GetComponent<RectTransform>());

                            // Destroy original
                            Object.DestroyImmediate(child.gameObject);

                            continue;
                        }
                    }
                }

                // Recurse into children
                if (childNode != null)
                {
                    ReplaceInstancesRecursive(child, childNode);
                }
            }
        }

        private SyncNode FindMatchingNode(SyncNode parent, string childName)
        {
            if (parent == null) return null;

            foreach (var child in parent.Children)
            {
                if (child.CleanName == childName || child.Name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private void CopyRectTransform(RectTransform source, RectTransform dest)
        {
            if (source == null || dest == null) return;

            dest.anchorMin = source.anchorMin;
            dest.anchorMax = source.anchorMax;
            dest.anchoredPosition = source.anchoredPosition;
            dest.sizeDelta = source.sizeDelta;
            dest.pivot = source.pivot;
        }

        /// <summary>
        /// Determines the atomic level for a node based on its structure.
        /// </summary>
        public static AtomicLevel DetermineAtomicLevel(SyncNode node)
        {
            // If explicitly set, use that
            if (node.AtomicLevel != AtomicLevel.Screen && node.AtomicLevel != AtomicLevel.Skip)
            {
                return node.AtomicLevel;
            }

            // Analyze structure to determine level
            var depth = GetMaxDepth(node);
            var componentCount = CountComponents(node);

            // Simple heuristics
            if (depth <= 2 && componentCount == 0)
            {
                return AtomicLevel.Atom;
            }
            else if (depth <= 4 && componentCount <= 3)
            {
                return AtomicLevel.Molecule;
            }
            else if (depth <= 6)
            {
                return AtomicLevel.Organism;
            }

            return AtomicLevel.Screen;
        }

        private static int GetMaxDepth(SyncNode node, int currentDepth = 0)
        {
            if (node.Children.Count == 0)
            {
                return currentDepth;
            }

            int maxChildDepth = currentDepth;
            foreach (var child in node.Children)
            {
                var childDepth = GetMaxDepth(child, currentDepth + 1);
                if (childDepth > maxChildDepth)
                {
                    maxChildDepth = childDepth;
                }
            }

            return maxChildDepth;
        }

        private static int CountComponents(SyncNode node)
        {
            int count = 0;

            foreach (var child in node.Children)
            {
                if (child.IsComponentInstance)
                {
                    count++;
                }
                count += CountComponents(child);
            }

            return count;
        }

        /// <summary>
        /// Gets the display name for an atomic level.
        /// </summary>
        public static string GetLevelDisplayName(AtomicLevel level)
        {
            switch (level)
            {
                case AtomicLevel.Atom: return "Atom";
                case AtomicLevel.Molecule: return "Molecule";
                case AtomicLevel.Organism: return "Organism";
                case AtomicLevel.Screen: return "Screen";
                case AtomicLevel.Skip: return "Skip";
                default: return level.ToString();
            }
        }
    }
}
