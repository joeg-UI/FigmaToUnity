using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigmaSync.Editor.Settings
{
    /// <summary>
    /// Configuration settings for FigmaSync import tool.
    /// </summary>
    [CreateAssetMenu(fileName = "FigmaSyncSettings", menuName = "FigmaSync/Settings")]
    public class FigmaSyncSettings : ScriptableObject
    {
        private const string TokenEditorPrefsKey = "FigmaSync_PersonalAccessToken";
        private const string AIKeyEditorPrefsKey = "FigmaSync_AIApiKey";

        [Header("Figma Connection")]
        [Tooltip("The Figma file URL to import from")]
        public string FigmaFileUrl = "";

        [Header("Import Options")]
        [Tooltip("Only import selected pages instead of all pages")]
        public bool OnlySelectedPages = true;

        [Tooltip("List of pages with their selection and atomic level settings")]
        public List<PageSelection> Pages = new List<PageSelection>();

        [Header("Type Detection")]
        [Tooltip("Enable AI-assisted type detection for ambiguous elements")]
        public bool EnableAIDetection = false;

        [Tooltip("AI provider to use for type detection")]
        public AIProvider AIProviderType = AIProvider.OpenAI;

        [Header("Atomic Design Page Names")]
        [Tooltip("Name of the page containing Atom components")]
        public string AtomsPageName = "Atoms";

        [Tooltip("Name of the page containing Molecule components")]
        public string MoleculesPageName = "Molecules";

        [Tooltip("Name of the page containing Organism components")]
        public string OrganismsPageName = "Organisms";

        [Header("Output Folders")]
        [Tooltip("Folder where generated prefabs will be saved")]
        public string PrefabsFolder = "Assets/UI/Prefabs";

        [Tooltip("Folder where downloaded assets (images) will be saved")]
        public string AssetsFolder = "Assets/UI/Assets";

        [Tooltip("Folder where fonts will be saved")]
        public string FontsFolder = "Assets/UI/Fonts";

        [Header("Asset Options")]
        [Tooltip("Import images at their original Figma size (1x)")]
        public bool ImportAtOriginalSize = true;

        [Tooltip("Image scale for exported assets when not using original size (1 = 1x, 2 = 2x, etc.)")]
        [Range(1, 4)]
        public int ImageScale = 2;

        [Tooltip("Pixels per unit for imported sprites (100 = 1 Figma pixel = 1 Unity unit)")]
        public float PixelsPerUnit = 100f;

        [Tooltip("Export format for images")]
        public ImageFormat ImageExportFormat = ImageFormat.PNG;

        [Tooltip("Auto-detect and configure 9-slice sprites for scaleable elements")]
        public bool AutoDetectSliceable = true;

        [Tooltip("Keywords that indicate an asset should be sliceable (9-slice)")]
        public List<string> SliceableKeywords = new List<string>
        {
            "button", "btn", "panel", "card", "background", "bg",
            "container", "box", "frame", "border", "input", "field"
        };

        [Header("Debug Options")]
        [Tooltip("Export raw Figma JSON for debugging")]
        public bool ExportDebugJson = false;

        [Tooltip("Folder for debug JSON exports")]
        public string DebugJsonFolder = "Assets/FigmaSync/Debug";

        /// <summary>
        /// Gets the Figma file key from the URL.
        /// </summary>
        public string FileKey
        {
            get
            {
                if (string.IsNullOrEmpty(FigmaFileUrl))
                    return null;

                // URL format: https://www.figma.com/file/{fileKey}/...
                // or https://www.figma.com/design/{fileKey}/...
                var uri = new Uri(FigmaFileUrl);
                var segments = uri.AbsolutePath.Split('/');

                for (int i = 0; i < segments.Length - 1; i++)
                {
                    if (segments[i] == "file" || segments[i] == "design")
                    {
                        return segments[i + 1];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets or sets the Personal Access Token (stored in EditorPrefs for security).
        /// </summary>
        public string PersonalAccessToken
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetString(TokenEditorPrefsKey, "");
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetString(TokenEditorPrefsKey, value);
#endif
            }
        }

        /// <summary>
        /// Gets or sets the AI API Key (stored in EditorPrefs for security).
        /// </summary>
        public string AIApiKey
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetString(AIKeyEditorPrefsKey, "");
#else
                return "";
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetString(AIKeyEditorPrefsKey, value);
#endif
            }
        }

        /// <summary>
        /// Validates the settings and returns any errors.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(FigmaFileUrl))
                errors.Add("Figma File URL is required");

            if (string.IsNullOrEmpty(FileKey))
                errors.Add("Invalid Figma File URL format");

            if (string.IsNullOrEmpty(PersonalAccessToken))
                errors.Add("Personal Access Token is required");

            if (EnableAIDetection && string.IsNullOrEmpty(AIApiKey))
                errors.Add("AI API Key is required when AI Detection is enabled");

            if (string.IsNullOrEmpty(PrefabsFolder))
                errors.Add("Prefabs folder path is required");

            if (string.IsNullOrEmpty(AssetsFolder))
                errors.Add("Assets folder path is required");

            return errors;
        }

        /// <summary>
        /// Gets the atomic level for a page by name.
        /// </summary>
        public AtomicLevel GetAtomicLevelForPage(string pageName)
        {
            if (pageName.Equals(AtomsPageName, StringComparison.OrdinalIgnoreCase))
                return AtomicLevel.Atom;
            if (pageName.Equals(MoleculesPageName, StringComparison.OrdinalIgnoreCase))
                return AtomicLevel.Molecule;
            if (pageName.Equals(OrganismsPageName, StringComparison.OrdinalIgnoreCase))
                return AtomicLevel.Organism;

            return AtomicLevel.Screen;
        }
    }

    /// <summary>
    /// Represents a page selection with its atomic level designation.
    /// </summary>
    [Serializable]
    public class PageSelection
    {
        public string PageId;
        public string PageName;
        public bool IsSelected = true;
        public AtomicLevel Level = AtomicLevel.Screen;
        public int ComponentCount;
    }

    /// <summary>
    /// Atomic design hierarchy levels.
    /// </summary>
    public enum AtomicLevel
    {
        Skip = 0,
        Atom = 1,
        Molecule = 2,
        Organism = 3,
        Screen = 4
    }

    /// <summary>
    /// AI provider options for type detection.
    /// </summary>
    public enum AIProvider
    {
        OpenAI,
        Anthropic
    }

    /// <summary>
    /// Image export format options.
    /// </summary>
    public enum ImageFormat
    {
        PNG,
        JPG,
        SVG
    }
}
