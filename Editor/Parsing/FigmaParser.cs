using System.Collections.Generic;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;
using FigmaSync.Editor.TypeDetection;
using UnityEngine;

namespace FigmaSync.Editor.Parsing
{
    /// <summary>
    /// Parses Figma API response into the intermediate SyncDocument format.
    /// </summary>
    public class FigmaParser
    {
        private readonly FigmaSyncSettings _settings;
        private readonly NodeProcessor _nodeProcessor;
        private readonly TypeDetector _typeDetector;
        private SyncDocument _document;

        public FigmaParser(FigmaSyncSettings settings)
        {
            _settings = settings;
            _nodeProcessor = new NodeProcessor(settings);
            _typeDetector = new TypeDetector(settings);
        }

        /// <summary>
        /// Parses a FigmaFileResponse into a SyncDocument.
        /// </summary>
        public SyncDocument Parse(FigmaFileResponse response)
        {
            _document = new SyncDocument
            {
                Name = response.name,
                FileKey = _settings.FileKey,
                LastModified = response.lastModified,
                Version = response.version
            };

            // Parse components metadata
            if (response.components != null)
            {
                foreach (var kvp in response.components)
                {
                    _document.Components[kvp.Key] = new SyncComponentDef
                    {
                        Key = kvp.Value.key,
                        Name = kvp.Value.name,
                        Description = kvp.Value.description,
                        ComponentSetId = kvp.Value.componentSetId,
                        NodeId = kvp.Key
                    };
                }
            }

            // Parse styles metadata
            if (response.styles != null)
            {
                foreach (var kvp in response.styles)
                {
                    _document.Styles[kvp.Key] = new SyncStyleDef
                    {
                        Key = kvp.Value.key,
                        Name = kvp.Value.name,
                        StyleType = kvp.Value.styleType,
                        Description = kvp.Value.description
                    };
                }
            }

            // Parse document structure
            if (response.document?.children != null)
            {
                foreach (var pageNode in response.document.children)
                {
                    if (pageNode.type == "CANVAS")
                    {
                        var page = ParsePage(pageNode);
                        _document.Pages.Add(page);
                    }
                }
            }

            // Second pass: Detect semantic types and resolve component references
            foreach (var page in _document.Pages)
            {
                foreach (var node in page.Children)
                {
                    ProcessNodeTypes(node);
                }
            }

            // Collect image hashes
            CollectImageHashes();

            return _document;
        }

        private SyncPage ParsePage(FigmaNode pageNode)
        {
            var pageName = pageNode.name;
            var atomicLevel = GetPageAtomicLevel(pageName);
            var isSelected = IsPageSelected(pageNode.id, pageName);

            var page = new SyncPage
            {
                Id = pageNode.id,
                Name = pageName,
                IsSelected = isSelected,
                AtomicLevel = atomicLevel
            };

            if (pageNode.backgroundColor != null)
            {
                page.BackgroundColor = new SyncColor(
                    pageNode.backgroundColor.r,
                    pageNode.backgroundColor.g,
                    pageNode.backgroundColor.b,
                    pageNode.backgroundColor.a
                );
            }

            // Parse children
            if (pageNode.children != null)
            {
                foreach (var childNode in pageNode.children)
                {
                    var syncNode = ParseNode(childNode, atomicLevel, null);
                    if (syncNode != null)
                    {
                        page.Children.Add(syncNode);
                    }
                }
            }

            return page;
        }

        private SyncNode ParseNode(FigmaNode figmaNode, AtomicLevel pageLevel, SyncNode parent)
        {
            // Skip invisible nodes unless they are components
            if (!figmaNode.visible && figmaNode.type != "COMPONENT" && figmaNode.type != "COMPONENT_SET")
            {
                return null;
            }

            var syncNode = _nodeProcessor.Process(figmaNode, pageLevel);
            syncNode.Parent = parent;

            // Parse children recursively
            if (figmaNode.children != null)
            {
                foreach (var childNode in figmaNode.children)
                {
                    var childSyncNode = ParseNode(childNode, pageLevel, syncNode);
                    if (childSyncNode != null)
                    {
                        syncNode.Children.Add(childSyncNode);
                    }
                }
            }

            // Update component definition with page info
            if (syncNode.IsComponent && _document.Components.TryGetValue(syncNode.Id, out var componentDef))
            {
                componentDef.AtomicLevel = pageLevel;
                componentDef.PageName = parent != null ? FindPageName(parent) : "";
            }

            return syncNode;
        }

        private void ProcessNodeTypes(SyncNode node)
        {
            // Detect semantic type
            node.Type = _typeDetector.DetectType(node);

            // Process children
            foreach (var child in node.Children)
            {
                ProcessNodeTypes(child);
            }
        }

        private void CollectImageHashes()
        {
            foreach (var page in _document.Pages)
            {
                if (!page.IsSelected) continue;

                foreach (var node in page.Children)
                {
                    CollectImageHashesRecursive(node);
                }
            }
        }

        private void CollectImageHashesRecursive(SyncNode node)
        {
            // Check for image fills
            foreach (var fill in node.Fills)
            {
                if (fill.Type == FillType.Image && !string.IsNullOrEmpty(fill.ImageHash))
                {
                    AddImageHash(fill.ImageHash, node.Id);
                }
            }

            // For vector nodes, we might want to export them as images
            if (node.FigmaType == "VECTOR" || node.FigmaType == "BOOLEAN_OPERATION" ||
                node.FigmaType == "STAR" || node.FigmaType == "POLYGON" ||
                node.FigmaType == "ELLIPSE" || node.FigmaType == "LINE")
            {
                // These nodes should be exported as images
                // The ID will be used to request image export from Figma
                AddImageHash($"node:{node.Id}", node.Id);
            }

            foreach (var child in node.Children)
            {
                CollectImageHashesRecursive(child);
            }
        }

        private void AddImageHash(string hash, string nodeId)
        {
            if (!_document.ImageHashes.TryGetValue(hash, out var nodeIds))
            {
                nodeIds = new List<string>();
                _document.ImageHashes[hash] = nodeIds;
            }

            if (!nodeIds.Contains(nodeId))
            {
                nodeIds.Add(nodeId);
            }
        }

        private AtomicLevel GetPageAtomicLevel(string pageName)
        {
            // Check settings first
            foreach (var pageSelection in _settings.Pages)
            {
                if (pageSelection.PageName == pageName)
                {
                    return pageSelection.Level;
                }
            }

            // Fall back to name-based detection
            return _settings.GetAtomicLevelForPage(pageName);
        }

        private bool IsPageSelected(string pageId, string pageName)
        {
            if (!_settings.OnlySelectedPages)
            {
                return true;
            }

            foreach (var pageSelection in _settings.Pages)
            {
                if (pageSelection.PageId == pageId || pageSelection.PageName == pageName)
                {
                    return pageSelection.IsSelected;
                }
            }

            // Default to selected if not in the list
            return true;
        }

        private string FindPageName(SyncNode node)
        {
            // Walk up the parent chain to find the page
            while (node.Parent != null)
            {
                node = node.Parent;
            }

            return node.Name;
        }
    }
}
