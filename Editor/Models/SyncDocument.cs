using System;
using System.Collections.Generic;
using FigmaSync.Editor.Settings;

namespace FigmaSync.Editor.Models
{
    /// <summary>
    /// Intermediate document format representing the entire Figma file.
    /// </summary>
    [Serializable]
    public class SyncDocument
    {
        /// <summary>
        /// Document name from Figma.
        /// </summary>
        public string Name;

        /// <summary>
        /// Figma file key.
        /// </summary>
        public string FileKey;

        /// <summary>
        /// Last modified timestamp from Figma.
        /// </summary>
        public string LastModified;

        /// <summary>
        /// Version from Figma.
        /// </summary>
        public string Version;

        /// <summary>
        /// All pages in the document.
        /// </summary>
        public List<SyncPage> Pages = new List<SyncPage>();

        /// <summary>
        /// Component definitions (id -> component info).
        /// </summary>
        public Dictionary<string, SyncComponentDef> Components = new Dictionary<string, SyncComponentDef>();

        /// <summary>
        /// Style definitions (id -> style info).
        /// </summary>
        public Dictionary<string, SyncStyleDef> Styles = new Dictionary<string, SyncStyleDef>();

        /// <summary>
        /// Image hashes to download (hash -> node IDs using it).
        /// </summary>
        public Dictionary<string, List<string>> ImageHashes = new Dictionary<string, List<string>>();

        /// <summary>
        /// Gets all nodes at a specific atomic level across all pages.
        /// </summary>
        public List<SyncNode> GetNodesByAtomicLevel(AtomicLevel level)
        {
            var results = new List<SyncNode>();

            foreach (var page in Pages)
            {
                if (!page.IsSelected) continue;

                foreach (var node in page.Children)
                {
                    node.GetNodesByAtomicLevel(level, results);
                }
            }

            return results;
        }

        /// <summary>
        /// Gets all selected pages.
        /// </summary>
        public List<SyncPage> GetSelectedPages()
        {
            var results = new List<SyncPage>();

            foreach (var page in Pages)
            {
                if (page.IsSelected)
                {
                    results.Add(page);
                }
            }

            return results;
        }

        /// <summary>
        /// Gets all nodes that require image assets.
        /// </summary>
        public List<SyncNode> GetAllImageNodes()
        {
            var results = new List<SyncNode>();

            foreach (var page in Pages)
            {
                if (!page.IsSelected) continue;

                foreach (var node in page.Children)
                {
                    node.GetImageNodes(results);
                }
            }

            return results;
        }

        /// <summary>
        /// Gets all unique image hashes that need to be downloaded.
        /// </summary>
        public List<string> GetUniqueImageHashes()
        {
            return new List<string>(ImageHashes.Keys);
        }

        /// <summary>
        /// Finds a node by ID across all pages.
        /// </summary>
        public SyncNode FindNodeById(string id)
        {
            foreach (var page in Pages)
            {
                foreach (var node in page.Children)
                {
                    var found = node.FindById(id);
                    if (found != null) return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all component instances that reference a given component ID.
        /// </summary>
        public List<SyncNode> GetInstancesOfComponent(string componentId)
        {
            var results = new List<SyncNode>();
            FindComponentInstances(componentId, results);
            return results;
        }

        private void FindComponentInstances(string componentId, List<SyncNode> results)
        {
            foreach (var page in Pages)
            {
                foreach (var node in page.Children)
                {
                    FindComponentInstancesRecursive(node, componentId, results);
                }
            }
        }

        private void FindComponentInstancesRecursive(SyncNode node, string componentId, List<SyncNode> results)
        {
            if (node.IsComponentInstance && node.ComponentId == componentId)
            {
                results.Add(node);
            }

            foreach (var child in node.Children)
            {
                FindComponentInstancesRecursive(child, componentId, results);
            }
        }
    }

    /// <summary>
    /// Represents a page (canvas) in the Figma document.
    /// </summary>
    [Serializable]
    public class SyncPage
    {
        /// <summary>
        /// Figma node ID for this page.
        /// </summary>
        public string Id;

        /// <summary>
        /// Page name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Whether this page is selected for import.
        /// </summary>
        public bool IsSelected = true;

        /// <summary>
        /// Atomic design level for this page.
        /// </summary>
        public AtomicLevel AtomicLevel = AtomicLevel.Screen;

        /// <summary>
        /// Background color of the page.
        /// </summary>
        public SyncColor BackgroundColor;

        /// <summary>
        /// Top-level children of this page (usually frames).
        /// </summary>
        public List<SyncNode> Children = new List<SyncNode>();

        /// <summary>
        /// Gets the count of component definitions on this page.
        /// </summary>
        public int ComponentCount
        {
            get
            {
                int count = 0;
                foreach (var child in Children)
                {
                    count += CountComponents(child);
                }
                return count;
            }
        }

        private int CountComponents(SyncNode node)
        {
            int count = node.IsComponent ? 1 : 0;

            foreach (var child in node.Children)
            {
                count += CountComponents(child);
            }

            return count;
        }
    }

    /// <summary>
    /// Component definition information.
    /// </summary>
    [Serializable]
    public class SyncComponentDef
    {
        /// <summary>
        /// Component key (unique identifier).
        /// </summary>
        public string Key;

        /// <summary>
        /// Component name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Component description.
        /// </summary>
        public string Description;

        /// <summary>
        /// ID of the component set (for variants).
        /// </summary>
        public string ComponentSetId;

        /// <summary>
        /// Node ID in the document.
        /// </summary>
        public string NodeId;

        /// <summary>
        /// Page containing this component.
        /// </summary>
        public string PageName;

        /// <summary>
        /// Atomic design level.
        /// </summary>
        public AtomicLevel AtomicLevel = AtomicLevel.Atom;

        /// <summary>
        /// Generated prefab path (set after generation).
        /// </summary>
        public string PrefabPath;
    }

    /// <summary>
    /// Style definition information.
    /// </summary>
    [Serializable]
    public class SyncStyleDef
    {
        /// <summary>
        /// Style key.
        /// </summary>
        public string Key;

        /// <summary>
        /// Style name.
        /// </summary>
        public string Name;

        /// <summary>
        /// Style type ("FILL", "TEXT", "EFFECT", "GRID").
        /// </summary>
        public string StyleType;

        /// <summary>
        /// Style description.
        /// </summary>
        public string Description;
    }
}
