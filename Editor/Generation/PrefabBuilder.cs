using System.Collections.Generic;
using System.IO;
using FigmaSync.Editor.Layout;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;
using FigmaSync.Editor.TypeDetection;
using FigmaSync.Runtime.Components;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaSync.Editor.Generation
{
    /// <summary>
    /// Builds Unity prefabs from SyncNodes.
    /// </summary>
    public class PrefabBuilder
    {
        private readonly FigmaSyncSettings _settings;
        private readonly LayoutTranslator _layoutTranslator;
        private readonly AssetDownloader _assetDownloader;

        public PrefabBuilder(FigmaSyncSettings settings, AssetDownloader assetDownloader)
        {
            _settings = settings;
            _layoutTranslator = new LayoutTranslator();
            _assetDownloader = assetDownloader;
        }

        /// <summary>
        /// Builds a GameObject hierarchy from a SyncNode.
        /// </summary>
        public GameObject Build(SyncNode node, SyncNode parent = null)
        {
            var go = new GameObject(node.CleanName);

            // Add RectTransform
            var rectTransform = go.AddComponent<RectTransform>();

            // Set initial size
            rectTransform.sizeDelta = new Vector2(node.Width, node.Height);

            // Build visual representation
            BuildVisuals(go, node);

            // Apply layout
            _layoutTranslator.ApplyLayout(go, node, parent);

            // Build children
            var childObjects = new List<GameObject>();
            foreach (var childNode in node.Children)
            {
                if (!childNode.Visible) continue;

                var childGo = Build(childNode, node);
                childGo.transform.SetParent(go.transform, false);
                childObjects.Add(childGo);

                // Set initial position for non-layout children
                if (node.LayoutMode == LayoutMode.None)
                {
                    _layoutTranslator.SetInitialPosition(
                        childGo.GetComponent<RectTransform>(),
                        childNode,
                        node
                    );
                }
            }

            // Handle SPACE_BETWEEN
            if (node.PrimaryAxisAlign == AxisAlignment.SpaceBetween)
            {
                _layoutTranslator.FinalizeSpaceBetween(go, node, childObjects);
            }

            // Add semantic component markers
            AddSemanticComponents(go, node);

            return go;
        }

        /// <summary>
        /// Saves a GameObject as a prefab.
        /// </summary>
        public string SaveAsPrefab(GameObject go, SyncNode node, AtomicDesignManager atomicManager)
        {
            var folder = atomicManager.GetFolderForLevel(node.AtomicLevel);
            EnsureDirectoryExists(folder);

            var prefabPath = $"{folder}/{node.CleanName}.prefab";

            // Ensure unique path
            var counter = 1;
            while (File.Exists(prefabPath))
            {
                prefabPath = $"{folder}/{node.CleanName}_{counter}.prefab";
                counter++;
            }

            // Save prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);

            // Register with atomic manager
            if (node.IsComponent)
            {
                atomicManager.RegisterPrefab(node.Id, prefab, prefabPath);
            }

            return prefabPath;
        }

        private void BuildVisuals(GameObject go, SyncNode node)
        {
            // Handle based on type
            switch (node.Type)
            {
                case SemanticType.Label:
                    BuildLabel(go, node);
                    break;

                case SemanticType.Image:
                case SemanticType.Icon:
                case SemanticType.Avatar:
                    BuildImage(go, node);
                    break;

                case SemanticType.Button:
                    BuildButton(go, node);
                    break;

                case SemanticType.InputField:
                    BuildInputField(go, node);
                    break;

                case SemanticType.Toggle:
                    BuildToggle(go, node);
                    break;

                case SemanticType.Slider:
                    BuildSlider(go, node);
                    break;

                case SemanticType.ScrollView:
                    BuildScrollView(go, node);
                    break;

                default:
                    BuildContainer(go, node);
                    break;
            }
        }

        private void BuildContainer(GameObject go, SyncNode node)
        {
            // Add Image component for background if needed
            if (node.Fills.Count > 0 || node.Strokes.Count > 0)
            {
                var image = go.AddComponent<Image>();
                ApplyFills(image, node);
                ApplyStrokes(image, node);
            }

            // Apply clipping
            if (node.ClipsContent)
            {
                var mask = go.AddComponent<Mask>();
                mask.showMaskGraphic = true;

                // Ensure there's an Image component for the mask
                if (go.GetComponent<Image>() == null)
                {
                    var image = go.AddComponent<Image>();
                    image.color = Color.white;
                }
            }
        }

        private void BuildLabel(GameObject go, SyncNode node)
        {
            var tmp = go.AddComponent<TextMeshProUGUI>();

            // Set text
            tmp.text = node.Text ?? "";

            // Apply text style
            if (node.TextStyle != null)
            {
                var style = node.TextStyle;

                tmp.fontSize = style.FontSize;

                // Font weight
                if (style.FontWeight >= 700)
                {
                    tmp.fontStyle = FontStyles.Bold;
                }

                // Alignment
                tmp.alignment = GetTextAlignment(style.HorizontalAlign, style.VerticalAlign);

                // Color
                if (style.Color != null)
                {
                    tmp.color = style.Color.ToUnityColor();
                }

                // Letter spacing (TMP uses em units)
                tmp.characterSpacing = style.LetterSpacing;

                // Line height
                if (style.LineHeight > 0)
                {
                    tmp.lineSpacing = (style.LineHeight / style.FontSize - 1) * 100;
                }

                // Text case
                switch (style.TextCase)
                {
                    case TextCase.Upper:
                        tmp.fontStyle |= FontStyles.UpperCase;
                        break;
                    case TextCase.Lower:
                        tmp.fontStyle |= FontStyles.LowerCase;
                        break;
                }

                // Text decoration
                switch (style.TextDecoration)
                {
                    case TextDecoration.Underline:
                        tmp.fontStyle |= FontStyles.Underline;
                        break;
                    case TextDecoration.Strikethrough:
                        tmp.fontStyle |= FontStyles.Strikethrough;
                        break;
                }

                // Auto-size handling
                if (style.TextAutoResize == "WIDTH_AND_HEIGHT")
                {
                    tmp.enableAutoSizing = false;
                    // ContentSizeFitter will handle sizing
                }
            }

            // Apply opacity
            if (node.Opacity < 1f)
            {
                var color = tmp.color;
                color.a *= node.Opacity;
                tmp.color = color;
            }
        }

        private void BuildImage(GameObject go, SyncNode node)
        {
            var image = go.AddComponent<Image>();

            // Try to load sprite from downloaded assets
            if (!string.IsNullOrEmpty(node.ImageAssetPath))
            {
                var sprite = _assetDownloader?.LoadSprite(node.ImageAssetPath);
                if (sprite != null)
                {
                    image.sprite = sprite;
                }
            }

            // Apply fills if no image
            if (image.sprite == null)
            {
                ApplyFills(image, node);
            }

            // Apply corner radius
            if (node.CornerRadius > 0)
            {
                // Note: Unity Image doesn't support corner radius natively
                // Would need custom shader or sprite with rounded corners
            }

            // Preserve aspect ratio for images
            if (node.Type == SemanticType.Image || node.Type == SemanticType.Avatar)
            {
                image.preserveAspect = true;
            }
        }

        private void BuildButton(GameObject go, SyncNode node)
        {
            // Add background
            var image = go.AddComponent<Image>();
            ApplyFills(image, node);
            ApplyStrokes(image, node);

            // Add Button component
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            // Set up color transitions
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(
                image.color.r * 1.1f,
                image.color.g * 1.1f,
                image.color.b * 1.1f,
                image.color.a
            );
            colors.pressedColor = new Color(
                image.color.r * 0.9f,
                image.color.g * 0.9f,
                image.color.b * 0.9f,
                image.color.a
            );
            button.colors = colors;
        }

        private void BuildInputField(GameObject go, SyncNode node)
        {
            // Add background
            var image = go.AddComponent<Image>();
            ApplyFills(image, node);
            ApplyStrokes(image, node);

            // For TMP InputField, we need a text area child
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);

            var textAreaRT = textArea.AddComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(10, 5);
            textAreaRT.offsetMax = new Vector2(-10, -5);

            // Add mask for text area
            textArea.AddComponent<RectMask2D>();

            // Create text component
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);

            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "";

            if (node.TextStyle != null)
            {
                text.fontSize = node.TextStyle.FontSize;
                if (node.TextStyle.Color != null)
                {
                    text.color = node.TextStyle.Color.ToUnityColor();
                }
            }

            // Create placeholder
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);

            var placeholderRT = placeholderGo.AddComponent<RectTransform>();
            placeholderRT.anchorMin = Vector2.zero;
            placeholderRT.anchorMax = Vector2.one;
            placeholderRT.offsetMin = Vector2.zero;
            placeholderRT.offsetMax = Vector2.zero;

            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text = node.GetTextContent() ?? "Enter text...";
            placeholder.fontStyle = FontStyles.Italic;

            if (node.TextStyle != null)
            {
                placeholder.fontSize = node.TextStyle.FontSize;
                if (node.TextStyle.Color != null)
                {
                    var c = node.TextStyle.Color.ToUnityColor();
                    placeholder.color = new Color(c.r, c.g, c.b, c.a * 0.5f);
                }
            }

            // Add InputField
            var inputField = go.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRT;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
        }

        private void BuildToggle(GameObject go, SyncNode node)
        {
            // Add background
            var bgImage = go.AddComponent<Image>();
            ApplyFills(bgImage, node);

            // Create checkmark child
            var checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(go.transform, false);

            var checkRT = checkmark.AddComponent<RectTransform>();
            checkRT.anchorMin = new Vector2(0.1f, 0.1f);
            checkRT.anchorMax = new Vector2(0.9f, 0.9f);
            checkRT.offsetMin = Vector2.zero;
            checkRT.offsetMax = Vector2.zero;

            var checkImage = checkmark.AddComponent<Image>();
            checkImage.color = Color.white;

            // Add Toggle component
            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;
        }

        private void BuildSlider(GameObject go, SyncNode node)
        {
            // Background
            var bgImage = go.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f);

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);

            var fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1, 0.75f);
            fillAreaRT.offsetMin = new Vector2(5, 0);
            fillAreaRT.offsetMax = new Vector2(-5, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);

            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;

            var fillImage = fill.AddComponent<Image>();
            ApplyFills(fillImage, node);

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(go.transform, false);

            var handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10, 0);
            handleAreaRT.offsetMax = new Vector2(-10, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);

            var handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20, 0);

            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;

            // Add Slider component
            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
        }

        private void BuildScrollView(GameObject go, SyncNode node)
        {
            // Add Image for background
            var image = go.AddComponent<Image>();
            ApplyFills(image, node);

            // Add mask
            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            // Create viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(go.transform, false);

            var viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;

            viewport.AddComponent<Image>().color = Color.clear;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            // Create content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);

            var contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0, 1);
            contentRT.sizeDelta = new Vector2(0, node.Height);

            // Add content size fitter
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Add layout group for content
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Add ScrollRect
            var scrollRect = go.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRT;
            scrollRect.content = contentRT;
            scrollRect.horizontal = node.HasHorizontalScrolling;
            scrollRect.vertical = node.HasVerticalScrolling;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
        }

        private void ApplyFills(Image image, SyncNode node)
        {
            if (node.Fills.Count == 0)
            {
                image.color = Color.clear;
                return;
            }

            var fill = node.Fills[0];

            switch (fill.Type)
            {
                case FillType.Solid:
                    if (fill.Color != null)
                    {
                        var color = fill.Color.ToUnityColor();
                        color.a *= fill.Opacity * node.Opacity;
                        image.color = color;
                    }
                    break;

                case FillType.Image:
                    // Image should be loaded separately
                    image.color = Color.white;
                    break;

                case FillType.GradientLinear:
                case FillType.GradientRadial:
                    // Unity Image doesn't support gradients natively
                    // Use first color as fallback
                    if (fill.GradientStops != null && fill.GradientStops.Count > 0)
                    {
                        var color = fill.GradientStops[0].Color.ToUnityColor();
                        color.a *= fill.Opacity * node.Opacity;
                        image.color = color;
                    }
                    break;
            }
        }

        private void ApplyStrokes(Image image, SyncNode node)
        {
            // Unity Image doesn't have built-in stroke support
            // Would need Outline component or custom shader

            if (node.Strokes.Count > 0)
            {
                var outline = image.gameObject.AddComponent<Outline>();
                var stroke = node.Strokes[0];

                if (stroke.Color != null)
                {
                    outline.effectColor = stroke.Color.ToUnityColor();
                }

                outline.effectDistance = new Vector2(stroke.Weight, stroke.Weight);
            }
        }

        private void AddSemanticComponents(GameObject go, SyncNode node)
        {
            // Add marker components for runtime identification
            switch (node.Type)
            {
                case SemanticType.Button:
                    var btn = go.AddComponent<FigmaButton>();
                    btn.NodeId = node.Id;
                    btn.PrototypeDestination = node.PrototypeDestination;
                    break;

                case SemanticType.Label:
                    var lbl = go.AddComponent<FigmaLabel>();
                    lbl.NodeId = node.Id;
                    break;

                case SemanticType.Toggle:
                    var tog = go.AddComponent<FigmaToggle>();
                    tog.NodeId = node.Id;
                    break;

                case SemanticType.Image:
                case SemanticType.Icon:
                case SemanticType.Avatar:
                    var img = go.AddComponent<FigmaImage>();
                    img.NodeId = node.Id;
                    img.ImageHash = node.ImageHash;
                    break;
            }
        }

        private TextAlignmentOptions GetTextAlignment(TextAlignmentH horizontal, TextAlignmentV vertical)
        {
            int h = horizontal == TextAlignmentH.Left ? 0 :
                    horizontal == TextAlignmentH.Center ? 1 :
                    horizontal == TextAlignmentH.Right ? 2 : 3;

            int v = vertical == TextAlignmentV.Top ? 0 :
                    vertical == TextAlignmentV.Center ? 1 : 2;

            // Map to TMP alignment
            var alignments = new TextAlignmentOptions[3, 4]
            {
                { TextAlignmentOptions.TopLeft, TextAlignmentOptions.Top, TextAlignmentOptions.TopRight, TextAlignmentOptions.TopJustified },
                { TextAlignmentOptions.MidlineLeft, TextAlignmentOptions.Midline, TextAlignmentOptions.MidlineRight, TextAlignmentOptions.MidlineJustified },
                { TextAlignmentOptions.BottomLeft, TextAlignmentOptions.Bottom, TextAlignmentOptions.BottomRight, TextAlignmentOptions.BottomJustified }
            };

            return alignments[v, h];
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
