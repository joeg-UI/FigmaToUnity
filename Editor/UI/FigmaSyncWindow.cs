using System;
using System.Threading;
using FigmaSync.Editor.Core;
using FigmaSync.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.UI
{
    /// <summary>
    /// Main editor window for FigmaSync.
    /// </summary>
    public class FigmaSyncWindow : EditorWindow
    {
        private FigmaSyncSettings _settings;
        private FigmaSyncImporter _importer;
        private PageSelector _pageSelector;
        private ProgressDisplay _progressDisplay;

        private CancellationTokenSource _cancellationSource;
        private Vector2 _scrollPosition;

        private bool _showConnectionSection = true;
        private bool _showPagesSection = true;
        private bool _showOptionsSection = true;
        private bool _showOutputSection = true;

        private string _tokenInput = "";
        private string _aiKeyInput = "";

        [MenuItem("Tools/FigmaSync %#f")]
        public static void ShowWindow()
        {
            var window = GetWindow<FigmaSyncWindow>();
            window.titleContent = new GUIContent("FigmaSync", EditorGUIUtility.IconContent("d_Prefab Icon").image);
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnEnable()
        {
            LoadOrCreateSettings();

            _pageSelector = new PageSelector(_settings);
            _progressDisplay = new ProgressDisplay();

            // Load masked token display
            _tokenInput = string.IsNullOrEmpty(_settings.PersonalAccessToken) ? "" : "••••••••••••••••";
            _aiKeyInput = string.IsNullOrEmpty(_settings.AIApiKey) ? "" : "••••••••••••••••";
        }

        private void OnDisable()
        {
            CancelImport();
        }

        private void LoadOrCreateSettings()
        {
            // Try to find existing settings
            var guids = AssetDatabase.FindAssets("t:FigmaSyncSettings");

            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _settings = AssetDatabase.LoadAssetAtPath<FigmaSyncSettings>(path);
            }

            if (_settings == null)
            {
                // Create new settings
                _settings = CreateInstance<FigmaSyncSettings>();

                // Ensure directory exists
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

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();

            EditorGUILayout.Space(10);

            DrawConnectionSection();
            DrawPagesSection();
            DrawOptionsSection();
            DrawOutputSection();

            EditorGUILayout.Space(20);

            DrawActionButtons();

            EditorGUILayout.Space(10);

            _progressDisplay.Draw();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            var headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("FigmaSync", headerStyle, GUILayout.Height(30));

            GUILayout.FlexibleSpace();

            // Settings button
            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Settings"), GUILayout.Width(30), GUILayout.Height(30)))
            {
                Selection.activeObject = _settings;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Import Figma designs with proper auto-layout", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawConnectionSection()
        {
            _showConnectionSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showConnectionSection, "Figma Connection");

            if (_showConnectionSection)
            {
                EditorGUI.indentLevel++;

                // Figma URL
                EditorGUILayout.BeginHorizontal();
                _settings.FigmaFileUrl = EditorGUILayout.TextField("Figma File URL", _settings.FigmaFileUrl);
                if (GUILayout.Button("Paste", GUILayout.Width(50)))
                {
                    _settings.FigmaFileUrl = EditorGUIUtility.systemCopyBuffer;
                    EditorUtility.SetDirty(_settings);
                }
                EditorGUILayout.EndHorizontal();

                // Show file key
                if (!string.IsNullOrEmpty(_settings.FileKey))
                {
                    EditorGUILayout.LabelField("File Key", _settings.FileKey, EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(5);

                // Token
                EditorGUILayout.BeginHorizontal();
                var newToken = EditorGUILayout.PasswordField("Personal Access Token", _tokenInput);
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

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPagesSection()
        {
            EditorGUILayout.Space(10);
            _showPagesSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showPagesSection, "Pages");

            if (_showPagesSection)
            {
                EditorGUI.indentLevel++;

                // Fetch pages button
                EditorGUI.BeginDisabledGroup(_progressDisplay.IsRunning);
                if (GUILayout.Button("Fetch Pages"))
                {
                    FetchPages();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space(5);

                _pageSelector.Draw();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawOptionsSection()
        {
            EditorGUILayout.Space(10);
            _showOptionsSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showOptionsSection, "Options");

            if (_showOptionsSection)
            {
                EditorGUI.indentLevel++;

                // Import options
                _settings.OnlySelectedPages = EditorGUILayout.Toggle("Only Selected Pages", _settings.OnlySelectedPages);
                _settings.ImageScale = EditorGUILayout.IntSlider("Image Scale", _settings.ImageScale, 1, 4);
                _settings.ImageExportFormat = (ImageFormat)EditorGUILayout.EnumPopup("Image Format", _settings.ImageExportFormat);

                EditorGUILayout.Space(5);

                // Type detection
                EditorGUILayout.LabelField("Type Detection", EditorStyles.boldLabel);
                _settings.EnableAIDetection = EditorGUILayout.Toggle("Enable AI Detection", _settings.EnableAIDetection);

                if (_settings.EnableAIDetection)
                {
                    EditorGUI.indentLevel++;
                    _settings.AIProviderType = (AIProvider)EditorGUILayout.EnumPopup("AI Provider", _settings.AIProviderType);

                    EditorGUILayout.BeginHorizontal();
                    var newAiKey = EditorGUILayout.PasswordField("API Key", _aiKeyInput);
                    if (newAiKey != _aiKeyInput && newAiKey != "••••••••••••••••")
                    {
                        _aiKeyInput = newAiKey;
                        _settings.AIApiKey = newAiKey;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                // Debug options
                _settings.ExportDebugJson = EditorGUILayout.Toggle("Export Debug JSON", _settings.ExportDebugJson);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.Space(10);
            _showOutputSection = EditorGUILayout.BeginFoldoutHeaderGroup(_showOutputSection, "Output Folders");

            if (_showOutputSection)
            {
                EditorGUI.indentLevel++;

                _settings.PrefabsFolder = EditorGUILayout.TextField("Prefabs", _settings.PrefabsFolder);
                _settings.AssetsFolder = EditorGUILayout.TextField("Assets", _settings.AssetsFolder);
                _settings.FontsFolder = EditorGUILayout.TextField("Fonts", _settings.FontsFolder);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (_progressDisplay.IsRunning)
            {
                // Cancel button
                var cancelStyle = new GUIStyle(GUI.skin.button)
                {
                    normal = { textColor = Color.red }
                };

                if (GUILayout.Button("Cancel", cancelStyle, GUILayout.Width(100), GUILayout.Height(35)))
                {
                    CancelImport();
                }
            }
            else
            {
                // Sync button
                var syncStyle = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 14
                };

                EditorGUI.BeginDisabledGroup(!CanSync());

                if (GUILayout.Button("Sync Document", syncStyle, GUILayout.Width(150), GUILayout.Height(35)))
                {
                    StartImport();
                }

                EditorGUI.EndDisabledGroup();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            // Validation messages
            if (!CanSync())
            {
                var errors = _settings.Validate();
                if (errors.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
                }
            }
        }

        private bool CanSync()
        {
            return _settings.Validate().Count == 0 && !_progressDisplay.IsRunning;
        }

        private async void FetchPages()
        {
            _pageSelector.SetLoading(true);
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

                var pages = await _importer.FetchPagesAsync(_cancellationSource.Token);
                _pageSelector.SetPages(pages);
                _progressDisplay.Complete();
            }
            catch (Exception ex)
            {
                _progressDisplay.ShowError(ex.Message);
            }
            finally
            {
                _pageSelector.SetLoading(false);
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
                    EditorUtility.DisplayDialog("FigmaSync", "Import completed successfully!", "OK");
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
