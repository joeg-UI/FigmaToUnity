using System.Collections.Generic;
using FigmaSync.Editor.Core;
using FigmaSync.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.UI
{
    /// <summary>
    /// UI component for selecting Figma pages to import.
    /// </summary>
    public class PageSelector
    {
        private readonly FigmaSyncSettings _settings;
        private List<PageInfo> _pages = new List<PageInfo>();
        private Vector2 _scrollPosition;
        private bool _isLoading = false;

        private readonly string[] _atomicLevelOptions = { "Skip", "Atom", "Molecule", "Organism", "Screen" };

        public PageSelector(FigmaSyncSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Sets the pages list.
        /// </summary>
        public void SetPages(List<PageInfo> pages)
        {
            _pages = pages ?? new List<PageInfo>();
            _isLoading = false;

            // Sync with settings
            SyncWithSettings();
        }

        /// <summary>
        /// Sets loading state.
        /// </summary>
        public void SetLoading(bool loading)
        {
            _isLoading = loading;
        }

        /// <summary>
        /// Draws the page selector UI.
        /// </summary>
        public void Draw()
        {
            EditorGUILayout.LabelField("Pages to Import:", EditorStyles.boldLabel);

            if (_isLoading)
            {
                EditorGUILayout.HelpBox("Loading pages...", MessageType.Info);
                return;
            }

            if (_pages.Count == 0)
            {
                EditorGUILayout.HelpBox("No pages loaded. Enter a valid Figma URL and token, then click 'Fetch Pages'.", MessageType.Info);
                return;
            }

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("", GUILayout.Width(20));
            EditorGUILayout.LabelField("Page Name", EditorStyles.miniLabel, GUILayout.MinWidth(100));
            EditorGUILayout.LabelField("Components", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.LabelField("Level", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // Page list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MaxHeight(200));

            for (int i = 0; i < _pages.Count; i++)
            {
                DrawPageRow(i);
            }

            EditorGUILayout.EndScrollView();

            // Quick selection buttons
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft))
            {
                SelectAll(true);
            }

            if (GUILayout.Button("Deselect All", EditorStyles.miniButtonRight))
            {
                SelectAll(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPageRow(int index)
        {
            var page = _pages[index];
            var pageSelection = GetOrCreatePageSelection(page);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Checkbox
            var newSelected = EditorGUILayout.Toggle(pageSelection.IsSelected, GUILayout.Width(20));
            if (newSelected != pageSelection.IsSelected)
            {
                pageSelection.IsSelected = newSelected;
                EditorUtility.SetDirty(_settings);
            }

            // Page name
            EditorGUILayout.LabelField(page.Name, GUILayout.MinWidth(100));

            // Component count
            EditorGUILayout.LabelField($"{page.ComponentCount}", GUILayout.Width(80));

            // Atomic level dropdown
            var currentLevel = (int)pageSelection.Level;
            var newLevel = EditorGUILayout.Popup(currentLevel, _atomicLevelOptions, GUILayout.Width(80));
            if (newLevel != currentLevel)
            {
                pageSelection.Level = (AtomicLevel)newLevel;
                EditorUtility.SetDirty(_settings);
            }

            EditorGUILayout.EndHorizontal();
        }

        private PageSelection GetOrCreatePageSelection(PageInfo pageInfo)
        {
            // Find existing
            foreach (var ps in _settings.Pages)
            {
                if (ps.PageId == pageInfo.Id)
                {
                    return ps;
                }
            }

            // Create new
            var newSelection = new PageSelection
            {
                PageId = pageInfo.Id,
                PageName = pageInfo.Name,
                IsSelected = true,
                Level = pageInfo.SuggestedLevel,
                ComponentCount = pageInfo.ComponentCount
            };

            _settings.Pages.Add(newSelection);
            EditorUtility.SetDirty(_settings);

            return newSelection;
        }

        private void SyncWithSettings()
        {
            // Update existing selections with new page info
            foreach (var page in _pages)
            {
                var selection = GetOrCreatePageSelection(page);
                selection.PageName = page.Name;
                selection.ComponentCount = page.ComponentCount;

                // Update suggested level if not manually set
                if (selection.Level == AtomicLevel.Screen)
                {
                    selection.Level = page.SuggestedLevel;
                }
            }

            // Remove selections for pages that no longer exist
            _settings.Pages.RemoveAll(ps =>
            {
                foreach (var page in _pages)
                {
                    if (page.Id == ps.PageId) return false;
                }
                return true;
            });

            EditorUtility.SetDirty(_settings);
        }

        private void SelectAll(bool selected)
        {
            foreach (var page in _pages)
            {
                var selection = GetOrCreatePageSelection(page);
                selection.IsSelected = selected;
            }

            EditorUtility.SetDirty(_settings);
        }

        /// <summary>
        /// Gets the count of selected pages.
        /// </summary>
        public int GetSelectedPageCount()
        {
            int count = 0;
            foreach (var ps in _settings.Pages)
            {
                if (ps.IsSelected) count++;
            }
            return count;
        }

        /// <summary>
        /// Returns true if any pages are loaded.
        /// </summary>
        public bool HasPages => _pages.Count > 0;
    }
}
