using System.Collections.Generic;
using FigmaSync.Editor.Models;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaSync.Editor.Layout
{
    /// <summary>
    /// Translates Figma auto-layout to Unity layout components.
    /// </summary>
    public class LayoutTranslator
    {
        /// <summary>
        /// Applies layout properties to a GameObject based on the SyncNode.
        /// </summary>
        public void ApplyLayout(GameObject go, SyncNode node, SyncNode parent)
        {
            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                rectTransform = go.AddComponent<RectTransform>();
            }

            // Step 1: If this node IS a layout container, add layout group
            if (node.LayoutMode != LayoutMode.None)
            {
                ApplyLayoutGroup(go, node);
            }

            // Step 2: Configure THIS node's sizing within its parent
            SizingHandler.ApplySizing(go, node, parent);

            // Step 3: Handle absolute positioning
            if (node.Positioning == PositioningMode.Absolute)
            {
                ApplyAbsolutePositioning(rectTransform, node, parent);
            }
        }

        /// <summary>
        /// Adds and configures a layout group component.
        /// </summary>
        private void ApplyLayoutGroup(GameObject go, SyncNode node)
        {
            // Check if SPACE_BETWEEN - use custom layout or spacers
            bool useSpaceBetween = SpaceBetweenHandler.UsesSpaceBetween(node);

            HorizontalOrVerticalLayoutGroup layoutGroup;

            if (node.LayoutMode == LayoutMode.Horizontal)
            {
                layoutGroup = go.GetComponent<HorizontalLayoutGroup>();
                if (layoutGroup == null)
                {
                    layoutGroup = go.AddComponent<HorizontalLayoutGroup>();
                }
            }
            else
            {
                layoutGroup = go.GetComponent<VerticalLayoutGroup>();
                if (layoutGroup == null)
                {
                    layoutGroup = go.AddComponent<VerticalLayoutGroup>();
                }
            }

            // Configure layout group
            layoutGroup.spacing = useSpaceBetween ? 0 : node.ItemSpacing;
            layoutGroup.padding = new RectOffset(
                node.PaddingLeft,
                node.PaddingRight,
                node.PaddingTop,
                node.PaddingBottom
            );

            // Set child control and expansion
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Set alignment (if not SPACE_BETWEEN)
            if (!useSpaceBetween)
            {
                layoutGroup.childAlignment = MapAlignment(node.PrimaryAxisAlign, node.CounterAxisAlign, node.LayoutMode);
            }
            else
            {
                // For SPACE_BETWEEN, align to start on primary axis
                // (spacers will handle the distribution)
                layoutGroup.childAlignment = MapAlignment(AxisAlignment.Start, node.CounterAxisAlign, node.LayoutMode);
            }

            // Configure child control based on children's sizing modes
            SizingHandler.ConfigureLayoutGroupChildControl(layoutGroup, node);
        }

        /// <summary>
        /// Maps Figma alignment to Unity TextAnchor.
        /// </summary>
        private TextAnchor MapAlignment(AxisAlignment primaryAxis, AxisAlignment counterAxis, LayoutMode layoutMode)
        {
            // For horizontal layout: primary = horizontal, counter = vertical
            // For vertical layout: primary = vertical, counter = horizontal

            int horizontal, vertical;

            if (layoutMode == LayoutMode.Horizontal)
            {
                horizontal = MapAxisToInt(primaryAxis);
                vertical = MapAxisToInt(counterAxis);
            }
            else
            {
                horizontal = MapAxisToInt(counterAxis);
                vertical = MapAxisToInt(primaryAxis);
            }

            // Map to TextAnchor (0=start, 1=center, 2=end)
            return (TextAnchor)(vertical * 3 + horizontal);
        }

        private int MapAxisToInt(AxisAlignment alignment)
        {
            switch (alignment)
            {
                case AxisAlignment.Start: return 0;
                case AxisAlignment.Center: return 1;
                case AxisAlignment.End: return 2;
                case AxisAlignment.SpaceBetween: return 0; // Handled separately
                case AxisAlignment.Baseline: return 0; // Treat as start
                default: return 0;
            }
        }

        /// <summary>
        /// Applies absolute positioning based on constraints.
        /// </summary>
        private void ApplyAbsolutePositioning(RectTransform rectTransform, SyncNode node, SyncNode parent)
        {
            if (parent == null) return;

            // Calculate position relative to parent
            float relativeX = node.X - parent.X - parent.PaddingLeft;
            float relativeY = node.Y - parent.Y - parent.PaddingTop;

            // Apply horizontal constraints
            ApplyHorizontalConstraint(rectTransform, node, parent, relativeX);

            // Apply vertical constraints
            ApplyVerticalConstraint(rectTransform, node, parent, relativeY);
        }

        private void ApplyHorizontalConstraint(RectTransform rectTransform, SyncNode node, SyncNode parent, float relativeX)
        {
            float parentWidth = parent.Width - parent.PaddingLeft - parent.PaddingRight;
            float rightEdge = relativeX + node.Width;
            float distanceFromRight = parentWidth - rightEdge;

            switch (node.HorizontalConstraint)
            {
                case ConstraintMode.Left:
                    rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(0, rectTransform.anchorMax.y);
                    rectTransform.anchoredPosition = new Vector2(relativeX + node.Width / 2, rectTransform.anchoredPosition.y);
                    rectTransform.sizeDelta = new Vector2(node.Width, rectTransform.sizeDelta.y);
                    break;

                case ConstraintMode.Right:
                    rectTransform.anchorMin = new Vector2(1, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
                    rectTransform.anchoredPosition = new Vector2(-distanceFromRight - node.Width / 2, rectTransform.anchoredPosition.y);
                    rectTransform.sizeDelta = new Vector2(node.Width, rectTransform.sizeDelta.y);
                    break;

                case ConstraintMode.Center:
                    rectTransform.anchorMin = new Vector2(0.5f, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(0.5f, rectTransform.anchorMax.y);
                    float centerX = relativeX + node.Width / 2 - parentWidth / 2;
                    rectTransform.anchoredPosition = new Vector2(centerX, rectTransform.anchoredPosition.y);
                    rectTransform.sizeDelta = new Vector2(node.Width, rectTransform.sizeDelta.y);
                    break;

                case ConstraintMode.LeftRight:
                    rectTransform.anchorMin = new Vector2(0, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(1, rectTransform.anchorMax.y);
                    rectTransform.offsetMin = new Vector2(relativeX, rectTransform.offsetMin.y);
                    rectTransform.offsetMax = new Vector2(-distanceFromRight, rectTransform.offsetMax.y);
                    break;

                case ConstraintMode.Scale:
                    float leftRatio = relativeX / parentWidth;
                    float rightRatio = rightEdge / parentWidth;
                    rectTransform.anchorMin = new Vector2(leftRatio, rectTransform.anchorMin.y);
                    rectTransform.anchorMax = new Vector2(rightRatio, rectTransform.anchorMax.y);
                    rectTransform.offsetMin = new Vector2(0, rectTransform.offsetMin.y);
                    rectTransform.offsetMax = new Vector2(0, rectTransform.offsetMax.y);
                    break;
            }
        }

        private void ApplyVerticalConstraint(RectTransform rectTransform, SyncNode node, SyncNode parent, float relativeY)
        {
            float parentHeight = parent.Height - parent.PaddingTop - parent.PaddingBottom;
            float bottomEdge = relativeY + node.Height;
            float distanceFromBottom = parentHeight - bottomEdge;

            // Note: Unity Y is inverted (positive up), Figma Y is positive down
            switch (node.VerticalConstraint)
            {
                case ConstraintMode.Top:
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 1);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
                    rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -relativeY - node.Height / 2);
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, node.Height);
                    break;

                case ConstraintMode.Bottom:
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 0);
                    rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, distanceFromBottom + node.Height / 2);
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, node.Height);
                    break;

                case ConstraintMode.Center:
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0.5f);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 0.5f);
                    float centerY = parentHeight / 2 - relativeY - node.Height / 2;
                    rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, centerY);
                    rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, node.Height);
                    break;

                case ConstraintMode.TopBottom:
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, 0);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, 1);
                    rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, distanceFromBottom);
                    rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, -relativeY);
                    break;

                case ConstraintMode.Scale:
                    float topRatio = 1 - (relativeY / parentHeight);
                    float bottomRatio = 1 - (bottomEdge / parentHeight);
                    rectTransform.anchorMin = new Vector2(rectTransform.anchorMin.x, bottomRatio);
                    rectTransform.anchorMax = new Vector2(rectTransform.anchorMax.x, topRatio);
                    rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, 0);
                    rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, 0);
                    break;
            }
        }

        /// <summary>
        /// Sets up initial RectTransform position for a non-layout child.
        /// </summary>
        public void SetInitialPosition(RectTransform rectTransform, SyncNode node, SyncNode parent)
        {
            if (parent == null)
            {
                // Root node
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = new Vector2(node.Width, node.Height);
                rectTransform.anchoredPosition = Vector2.zero;
                return;
            }

            // Calculate position relative to parent's top-left
            float relativeX = node.X - parent.X;
            float relativeY = node.Y - parent.Y;

            // Default to top-left anchoring
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.sizeDelta = new Vector2(node.Width, node.Height);
            rectTransform.anchoredPosition = new Vector2(relativeX, -relativeY);
        }

        /// <summary>
        /// Handles SPACE_BETWEEN by inserting spacers after children are created.
        /// </summary>
        public void FinalizeSpaceBetween(GameObject container, SyncNode node, List<GameObject> childObjects)
        {
            if (SpaceBetweenHandler.UsesSpaceBetween(node))
            {
                SpaceBetweenHandler.InsertSpacers(container, node, childObjects);
            }
        }
    }
}
