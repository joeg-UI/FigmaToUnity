using System;
using System.Collections.Generic;
using System.Threading;
using FigmaSync.Editor.Core;
using FigmaSync.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.UI
{
    /// <summary>
    /// Main editor window for FigmaSync with enhanced UI.
    /// </summary>
    public class FigmaSyncWindow : EditorWindow
    {
        private FigmaSyncSettings _settings;
        private FigmaSyncImporter _importer;
        private ProgressDisplay _progressDisplay;

        private CancellationTokenSource _cancellationSource;
        private Vector2 _scrollPosition;
        private Vector2 _pagesScrollPosition;

        // Tab system
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Connection", "Pages", "Build", "Settings" };

        // Page data
        private List<PageInfo> _pages = new List<PageInfo>();
        private bool _pagesLoaded = false;

        // Token inputs
        private string _tokenInput = "";
        private string _aiKeyInput = "";

        // Build options
        private bool _buildAtoms = true;
        private bool _buildMolecules = true;
        private bool _buildOrganisms = true;
        private bool _buildScreens = true;
        private bool _importStyleGuide = true;
        private bool _importAssets = true;

        [MenuItem("Tools/FigmaSync %#f")]
        public static void ShowWindow()
        {
            var window = GetWindow<FigmaSyncWindow>();
            window.titleContent = new GUIContent("FigmaSync", EditorGUIUtility.IconContent("d_Prefab Icon").image);
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        // Add toolbar button
        [InitializeOnLoadMethod]
        private static void InitializeToolbar()
        {
            EditorApplication.delayCall += () =>
            {
                SceneView.duringSceneGui -= OnSceneGUI;
                SceneView.duringSceneGui += OnSceneGUI;
            };
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Handles.BeginGUI();

            var rect = new Rect(sceneView.position.width - 110, 10, 100, 25);
            if (GUI.Button(rect, "FigmaSync"))
            {
                ShowWindow();
            }

            Handles.EndGUI();
        }

        private void OnEnable()
        {
            LoadOrCreateSettings();
            _progressDisplay = new ProgressDisplay();

            _tokenInput = string.IsNullOrEmpty(_settings.PersonalAccessToken) ? "" : "••••••••••••••••";
            _aiKeyInput = string.IsNullOrEmpty(_settings.AIApiKey) ? "" : "••••••••••••••••";
        }

        private void OnDisable()
        {
            CancelImport();
        }

        private void LoadOrCreateSettings()
        {
            var guids = AssetDatabase.FindAssets("t:FigmaSyncSettings");

            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _settings = AssetDatabase.LoadAssetAtPath<FigmaSyncSettings>(path);
            }

            if (_settings == null)
            {
                _settings = CreateInstance<FigmaSyncSettings>();

                if (!AssetDatabase.IsValidFolder("Assets/FigmaSync"))
                {
                    AssetDatabase.CreateFolder("Assets", "FigmaSync");
                }

                AssetDatabase.CreateAsset(_settings, "Assets/FigmaSync/FigmaSyncSettings.asset");
                AssetDatabase.SaveAssets();
            }
        }

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("Settings not found. Please re-open the window.", MessageType.Error);
                return;
            }

            DrawHeader();

            EditorGUILayout.Space(5);

            // Tab bar
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(30));

            EditorGUILayout.Space(10);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            switch (_selectedTab)
            {
                case 0:
                    DrawConnectionTab();
                    break;
                case 1:
                    DrawPagesTab();
                    break;
                case 2:
                    DrawBuildTab();
                    break;
                case 3:
                    DrawSettingsTab();
                    break;
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Progress and action buttons always visible
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };

            GUILayout.Label("  FigmaSync", headerStyle);

            GUILayout.FlexibleSpace();

            // Status indicator
            if (_pagesLoaded)
            {
                var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.green }
                };
                GUILayout.Label("● Connected", statusStyle);
            }
            else
            {
                var statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Color.gray }
                };
                GUILayout.Label("○ Not Connected", statusStyle);
            }

            GUILayout.Space(10);

            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Settings"), EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                Selection.activeObject = _settings;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawConnectionTab()
        {
            EditorGUILayout.LabelField("Figma Connection", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawBox(() =>
            {
                // Figma URL
                EditorGUILayout.LabelField("Figma File URL", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                _settings.FigmaFileUrl = EditorGUILayout.TextField(_settings.FigmaFileUrl);
                if (GUILayout.Button("Paste", GUILayout.Width(50)))
                {
                    _settings.FigmaFileUrl = EditorGUIUtility.systemCopyBuffer;
                    EditorUtility.SetDirty(_settings);
                }
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(_settings.FileKey))
                {
                    EditorGUILayout.LabelField($"File Key: {_settings.FileKey}", EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(10);

                // Token
                EditorGUILayout.LabelField("Personal Access Token", EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                var newToken = EditorGUILayout.PasswordField(_tokenInput);
                if (newToken != _tokenInput && newToken != "••••••••••••••••")
                {
                    _tokenInput = newToken;
                    _settings.PersonalAccessToken = newToken;
                }
                if (GUILayout.Button("?", GUILayout.Width(25)))
                {
                    Application.OpenURL("https://www.figma.com/developers/api#access-tokens");
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(10);

                // Connect button
                EditorGUI.BeginDisabledGroup(_progressDisplay.IsRunning || string.IsNullOrEmpty(_settings.FileKey) || string.IsNullOrEmpty(_settings.PersonalAccessToken));
                if (GUILayout.Button("Connect & Fetch Pages", GUILayout.Height(30)))
                {
                    FetchPages();
                }
                EditorGUI.EndDisabledGroup();
            });

            if (_pagesLoaded)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox($"Connected! Found {_pages.Count} pages. Go to the Pages tab to select which to import.", MessageType.Info);
            }
        }

        private void DrawPagesTab()
        {
            if (!_pagesLoaded)
            {
                EditorGUILayout.HelpBox("Connect to Figma first in the Connection tab.", MessageType.Warning);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Pages", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Select All", EditorStyles.miniButtonLeft, GUILayout.Width(70)))
            {
                foreach (var ps in _settings.Pages) ps.IsSelected = true;
                EditorUtility.SetDirty(_settings);
            }
            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(50)))
            {
                foreach (var ps in _settings.Pages) ps.IsSelected = false;
                EditorUtility.SetDirty(_settings);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Column headers
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("", GUILayout.Width(25));
            GUILayout.Label("Page Name", EditorStyles.miniLabel, GUILayout.MinWidth(120));
            GUILayout.Label("Components", EditorStyles.miniLabel, GUILayout.Width(70));
            GUILayout.Label("Type", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            // Page list
            _pagesScrollPosition = EditorGUILayout.BeginScrollView(_pagesScrollPosition, GUILayout.MaxHeight(300));

            for (int i = 0; i < _pages.Count; i++)
            {
                DrawPageRow(_pages[i]);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Legend
            DrawBox(() =>
            {
                EditorGUILayout.LabelField("Atomic Design Levels", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("• Atom - Basic building blocks (buttons, icons, inputs)", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Molecule - Simple combinations of atoms", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Organism - Complex UI sections", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Screen - Full page layouts", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Skip - Don't import this page", EditorStyles.miniLabel);
            });
        }

        private void DrawPageRow(PageInfo page)
        {
            var pageSelection = GetOrCreatePageSelection(page);

            var bgColor = pageSelection.IsSelected ? new Color(0.3f, 0.5f, 0.3f, 0.3f) : Color.clear;
            var rect = EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(rect, bgColor);

            // Checkbox
            var newSelected = EditorGUILayout.Toggle(pageSelection.IsSelected, GUILayout.Width(25));
            if (newSelected != pageSelection.IsSelected)
            {
                pageSelection.IsSelected = newSelected;
                EditorUtility.SetDirty(_settings);
            }

            // Page name
            EditorGUILayout.LabelField(page.Name, GUILayout.MinWidth(120));

            // Component count
            EditorGUILayout.LabelField(page.ComponentCount.ToString(), GUILayout.Width(70));

            // Atomic level dropdown
            var newLevel = (AtomicLevel)EditorGUILayout.EnumPopup(pageSelection.Level, GUILayout.Width(80));
            if (newLevel != pageSelection.Level)
            {
                pageSelection.Level = newLevel;
                EditorUtility.SetDirty(_settings);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBuildTab()
        {
            EditorGUILayout.LabelField("Build Order", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawBox(() =>
            {
                EditorGUILayout.LabelField("Build components in atomic design order:", EditorStyles.miniLabel);
                EditorGUILayout.Space(5);

                // Build order checkboxes with visual hierarchy
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                _buildAtoms = EditorGUILayout.ToggleLeft("1. Atoms (base components)", _buildAtoms);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40);
                EditorGUILayout.LabelField("↓", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                _buildMolecules = EditorGUILayout.ToggleLeft("2. Molecules (use Atoms)", _buildMolecules);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40);
                EditorGUILayout.LabelField("↓", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                _buildOrganisms = EditorGUILayout.ToggleLeft("3. Organisms (use Atoms + Molecules)", _buildOrganisms);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40);
                EditorGUILayout.LabelField("↓", GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                _buildScreens = EditorGUILayout.ToggleLeft("4. Screens (full layouts)", _buildScreens);
                EditorGUILayout.EndHorizontal();
            });

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Additional Options", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawBox(() =>
            {
                _importStyleGuide = EditorGUILayout.ToggleLeft("Import Style Guide (colors, typography)", _importStyleGuide);
                _importAssets = EditorGUILayout.ToggleLeft("Download & Import Assets (images, icons)", _importAssets);

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Image Scale", EditorStyles.miniLabel);
                _settings.ImageScale = EditorGUILayout.IntSlider(_settings.ImageScale, 1, 4);
            });

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Type Detection", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawBox(() =>
            {
                EditorGUILayout.LabelField("Automatically detect component types:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Button, Label, Toggle, Slider, Input Field", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Image, Icon, ScrollView, Card, etc.", EditorStyles.miniLabel);

                EditorGUILayout.Space(5);

                _settings.EnableAIDetection = EditorGUILayout.ToggleLeft("Enable AI-assisted detection (for ambiguous types)", _settings.EnableAIDetection);

                if (_settings.EnableAIDetection)
                {
                    EditorGUI.indentLevel++;
                    _settings.AIProviderType = (AIProvider)EditorGUILayout.EnumPopup("AI Provider", _settings.AIProviderType);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("API Key", GUILayout.Width(60));
                    var newAiKey = EditorGUILayout.PasswordField(_aiKeyInput);
                    if (newAiKey != _aiKeyInput && newAiKey != "••••••••••••••••")
                    {
                        _aiKeyInput = newAiKey;
                        _settings.AIApiKey = newAiKey;
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }
            });
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Output Folders", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawBox(() =>
            {
                EditorGUILayout.LabelField("Prefabs Folder", EditorStyles.miniLabel);
                _settings.PrefabsFolder = EditorGUILayout.TextField(_settings.PrefabsFolder);

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Assets Folder", EditorStyles.miniLabel);
                _settings.AssetsFolder = EditorGUILayout.TextField(_settings.AssetsFolder);

                EditorGUILayout.Space(5);

                EditorGUILayout.LabelField("Fonts Folder", EditorStyles.miniLabel);
                _settings.FontsFolder = EditorGUILayout.TextField(_settings.FontsFolder);
            });

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Atomic Design Page Names", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawBox(() =>
            {
                EditorGUILayout.LabelField("Pages with these names will auto-assign atomic levels:", EditorStyles.miniLabel);
                EditorGUILayout.Space(5);

                _settings.AtomsPageName = EditorGUILayout.TextField("Atoms Page", _settings.AtomsPageName);
                _settings.MoleculesPageName = EditorGUILayout.TextField("Molecules Page", _settings.MoleculesPageName);
                _settings.OrganismsPageName = EditorGUILayout.TextField("Organisms Page", _settings.OrganismsPageName);
            });

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            DrawBox(() =>
            {
                _settings.ExportDebugJson = EditorGUILayout.ToggleLeft("Export Debug JSON", _settings.ExportDebugJson);

                if (_settings.ExportDebugJson)
                {
                    EditorGUI.indentLevel++;
                    _settings.DebugJsonFolder = EditorGUILayout.TextField("Debug Folder", _settings.DebugJsonFolder);
                    EditorGUI.indentLevel--;
                }
            });

            EditorGUILayout.Space(20);

            // Reset button
            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings", "Reset all settings to defaults?", "Reset", "Cancel"))
                {
                    _settings.PrefabsFolder = "Assets/UI/Prefabs";
                    _settings.AssetsFolder = "Assets/UI/Assets";
                    _settings.FontsFolder = "Assets/UI/Fonts";
                    _settings.AtomsPageName = "Atoms";
                    _settings.MoleculesPageName = "Molecules";
                    _settings.OrganismsPageName = "Organisms";
                    EditorUtility.SetDirty(_settings);
                }
            }
        }

        private void DrawFooter()
        {
            // Progress bar
            if (_progressDisplay.IsRunning)
            {
                _progressDisplay.Draw();
            }

            EditorGUILayout.Space(5);

            // Main action button
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_progressDisplay.IsRunning)
            {
                if (GUILayout.Button("Cancel", GUILayout.Width(120), GUILayout.Height(35)))
                {
                    CancelImport();
                }
            }
            else
            {
                EditorGUI.BeginDisabledGroup(!CanSync());

                var buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 14
                };

                if (GUILayout.Button("Build Prefabs", buttonStyle, GUILayout.Width(150), GUILayout.Height(40)))
                {
                    StartImport();
                }

                EditorGUI.EndDisabledGroup();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Validation messages
            if (!CanSync() && !_progressDisplay.IsRunning)
            {
                var errors = _settings.Validate();
                if (errors.Count > 0)
                {
                    EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
                }
                else if (!_pagesLoaded)
                {
                    EditorGUILayout.HelpBox("Connect to Figma and fetch pages first.", MessageType.Info);
                }
            }

            EditorGUILayout.Space(5);
        }

        private void DrawBox(Action content)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            content();
            EditorGUILayout.EndVertical();
        }

        private PageSelection GetOrCreatePageSelection(PageInfo pageInfo)
        {
            foreach (var ps in _settings.Pages)
            {
                if (ps.PageId == pageInfo.Id)
                {
                    return ps;
                }
            }

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

        private bool CanSync()
        {
            return _settings.Validate().Count == 0 && _pagesLoaded && !_progressDisplay.IsRunning;
        }

        private async void FetchPages()
        {
            _progressDisplay.Start();

            try
            {
                _cancellationSource = new CancellationTokenSource();
                _importer = new FigmaSyncImporter(_settings);

                _importer.OnProgress += (status, progress) =>
                {
                    _progressDisplay.Update(status, progress);
                    Repaint();
                };

                _importer.OnError += (error) =>
                {
                    _progressDisplay.ShowError(error);
                    Repaint();
                };

                _pages = await _importer.FetchPagesAsync(_cancellationSource.Token);
                _pagesLoaded = _pages.Count > 0;
                _progressDisplay.Complete();

                // Auto-switch to pages tab
                if (_pagesLoaded)
                {
                    _selectedTab = 1;
                }
            }
            catch (Exception ex)
            {
                _progressDisplay.ShowError(ex.Message);
            }
            finally
            {
                Repaint();
            }
        }

        private async void StartImport()
        {
            _progressDisplay.Start();
            _progressDisplay.ClearError();

            try
            {
                _cancellationSource = new CancellationTokenSource();
                _importer = new FigmaSyncImporter(_settings);

                _importer.OnProgress += (status, progress) =>
                {
                    _progressDisplay.Update(status, progress);
                    Repaint();
                };

                _importer.OnError += (error) =>
                {
                    _progressDisplay.ShowError(error);
                    Repaint();
                };

                _importer.OnComplete += () =>
                {
                    _progressDisplay.Complete();
                    Repaint();
                    EditorUtility.DisplayDialog("FigmaSync", "Build completed successfully!", "OK");
                };

                await _importer.ImportAsync(_cancellationSource.Token);
            }
            catch (OperationCanceledException)
            {
                _progressDisplay.Update("Cancelled", _progressDisplay.Progress);
            }
            catch (Exception ex)
            {
                _progressDisplay.ShowError(ex.Message);
                Debug.LogException(ex);
            }
            finally
            {
                _progressDisplay.Stop();
                Repaint();
            }
        }

        private void CancelImport()
        {
            _cancellationSource?.Cancel();
            _progressDisplay.Stop();
        }
    }
}
