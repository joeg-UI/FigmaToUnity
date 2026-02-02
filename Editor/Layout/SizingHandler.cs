using FigmaSync.Editor.Models;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaSync.Editor.Layout
{
    /// <summary>
    /// Handles FIXED/HUG/FILL sizing translation from Figma to Unity.
    /// </summary>
    public static class SizingHandler
    {
        /// <summary>
        /// Applies sizing behavior to a GameObject based on the SyncNode's sizing modes.
        /// </summary>
        public static void ApplySizing(GameObject go, SyncNode node, SyncNode parent)
        {
            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null) return;

            // Determine if we're in a layout group
            bool inHorizontalLayout = parent?.LayoutMode == LayoutMode.Horizontal;
            bool inVerticalLayout = parent?.LayoutMode == LayoutMode.Vertical;
            bool inLayout = inHorizontalLayout || inVerticalLayout;

            // Get or add LayoutElement if we're in a layout
            LayoutElement layoutElement = null;
            if (inLayout)
            {
                layoutElement = go.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = go.AddComponent<LayoutElement>();
                }
            }

            // Apply horizontal sizing
            ApplyHorizontalSizing(rectTransform, layoutElement, node, inHorizontalLayout, inVerticalLayout);

            // Apply vertical sizing
            ApplyVerticalSizing(rectTransform, layoutElement, node, inHorizontalLayout, inVerticalLayout);

            // Add ContentSizeFitter for HUG behavior
            ApplyContentSizeFitter(go, node);
        }

        private static void ApplyHorizontalSizing(
            RectTransform rectTransform,
            LayoutElement layoutElement,
            SyncNode node,
            bool inHorizontalLayout,
            bool inVerticalLayout)
        {
            switch (node.SizingHorizontal)
            {
                case SizingMode.Fixed:
                    if (layoutElement != null)
                    {
                        layoutElement.preferredWidth = node.Width;
                        layoutElement.flexibleWidth = 0;
                        layoutElement.minWidth = node.MinWidth ?? -1;
                    }
                    else
                    {
                        // Set fixed size through anchors
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, node.Width);
                    }
                    break;

                case SizingMode.Hug:
                    if (layoutElement != null)
                    {
                        layoutElement.preferredWidth = -1;
                        layoutElement.flexibleWidth = 0;
                        layoutElement.minWidth = node.MinWidth ?? -1;
                    }
                    // ContentSizeFitter will be added separately
                    break;

                case SizingMode.Fill:
                    if (layoutElement != null)
                    {
                        // Use layoutGrow if specified, otherwise default to 1
                        layoutElement.flexibleWidth = node.LayoutGrow > 0 ? node.LayoutGrow : 1;
                        layoutElement.preferredWidth = -1;
                        layoutElement.minWidth = node.MinWidth ?? -1;

                        if (node.MaxWidth.HasValue)
                        {
                            // Unity doesn't have maxWidth on LayoutElement,
                            // but we can use preferredWidth as a soft max
                            layoutElement.preferredWidth = node.MaxWidth.Value;
                        }
                    }
                    else if (inVerticalLayout)
                    {
                        // In vertical layout, FILL horizontally means stretch to parent
                        rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
                        rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
                        rectTransform.offsetMin = new Vector2(0, rectTransform.offsetMin.y);
                        rectTransform.offsetMax = new Vector2(0, rectTransform.offsetMax.y);
                    }
                    break;
            }
        }

        private static void ApplyVerticalSizing(
            RectTransform rectTransform,
            LayoutElement layoutElement,
            SyncNode node,
            bool inHorizontalLayout,
            bool inVerticalLayout)
        {
            switch (node.SizingVertical)
            {
                case SizingMode.Fixed:
                    if (layoutElement != null)
                    {
                        layoutElement.preferredHeight = node.Height;
                        layoutElement.flexibleHeight = 0;
                        layoutElement.minHeight = node.MinHeight ?? -1;
                    }
                    else
                    {
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, node.Height);
                    }
                    break;

                case SizingMode.Hug:
                    if (layoutElement != null)
                    {
                        layoutElement.preferredHeight = -1;
                        layoutElement.flexibleHeight = 0;
                        layoutElement.minHeight = node.MinHeight ?? -1;
                    }
                    // ContentSizeFitter will be added separately
                    break;

                case SizingMode.Fill:
                    if (layoutElement != null)
                    {
                        layoutElement.flexibleHeight = node.LayoutGrow > 0 ? node.LayoutGrow : 1;
                        layoutElement.preferredHeight = -1;
                        layoutElement.minHeight = node.MinHeight ?? -1;

                        if (node.MaxHeight.HasValue)
                        {
                            layoutElement.preferredHeight = node.MaxHeight.Value;
                        }
                    }
                    else if (inHorizontalLayout)
                    {
                        // In horizontal layout, FILL vertically means stretch to parent
                        rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
                        rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
                        rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, 0);
                        rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, 0);
                    }
                    break;
            }
        }

        private static void ApplyContentSizeFitter(GameObject go, SyncNode node)
        {
            bool needsHorizontalHug = node.SizingHorizontal == SizingMode.Hug;
            bool needsVerticalHug = node.SizingVertical == SizingMode.Hug;

            if (!needsHorizontalHug && !needsVerticalHug) return;

            var fitter = go.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = go.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = needsHorizontalHug
                ? ContentSizeFitter.FitMode.PreferredSize
                : ContentSizeFitter.FitMode.Unconstrained;

            fitter.verticalFit = needsVerticalHug
                ? ContentSizeFitter.FitMode.PreferredSize
                : ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>
        /// Configures a layout group's child control settings based on children's sizing modes.
        /// </summary>
        public static void ConfigureLayoutGroupChildControl(HorizontalOrVerticalLayoutGroup layoutGroup, SyncNode containerNode)
        {
            // childControlWidth/Height should be true to respect LayoutElement settings
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;

            // childForceExpandWidth/Height should be false so FILL children expand
            // while FIXED children keep their size
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
        }
    }
}
