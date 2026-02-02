using System.Collections.Generic;
using FigmaSync.Editor.Models;
using UnityEngine;
using UnityEngine.UI;

namespace FigmaSync.Editor.Layout
{
    /// <summary>
    /// Handles SPACE_BETWEEN alignment which Unity doesn't natively support.
    /// Solution: Insert invisible spacer objects between children.
    /// </summary>
    public static class SpaceBetweenHandler
    {
        private const string SpacerPrefix = "_spacer";

        /// <summary>
        /// Checks if a container uses SPACE_BETWEEN alignment.
        /// </summary>
        public static bool UsesSpaceBetween(SyncNode containerNode)
        {
            return containerNode.PrimaryAxisAlign == AxisAlignment.SpaceBetween;
        }

        /// <summary>
        /// Creates spacer GameObjects for SPACE_BETWEEN behavior.
        /// Call this after all children have been created.
        /// </summary>
        public static void InsertSpacers(GameObject container, SyncNode containerNode, List<GameObject> children)
        {
            if (!UsesSpaceBetween(containerNode) || children.Count < 2)
                return;

            bool isHorizontal = containerNode.LayoutMode == LayoutMode.Horizontal;

            // Insert spacers between children (not before first or after last)
            for (int i = children.Count - 1; i > 0; i--)
            {
                var spacer = CreateSpacer(container, $"{SpacerPrefix}_{i}", isHorizontal);

                // Set sibling index to be between children[i-1] and children[i]
                var targetIndex = children[i].transform.GetSiblingIndex();
                spacer.transform.SetSiblingIndex(targetIndex);
            }
        }

        /// <summary>
        /// Creates a single spacer GameObject with flexible sizing.
        /// </summary>
        private static GameObject CreateSpacer(GameObject parent, string name, bool isHorizontal)
        {
            var spacer = new GameObject(name);
            spacer.transform.SetParent(parent.transform, false);

            // Add RectTransform
            var rectTransform = spacer.AddComponent<RectTransform>();
            rectTransform.sizeDelta = Vector2.zero;

            // Add LayoutElement with flexible sizing
            var layoutElement = spacer.AddComponent<LayoutElement>();

            if (isHorizontal)
            {
                layoutElement.flexibleWidth = 1;
                layoutElement.flexibleHeight = 0;
                layoutElement.preferredWidth = 0;
                layoutElement.minWidth = 0;
            }
            else
            {
                layoutElement.flexibleWidth = 0;
                layoutElement.flexibleHeight = 1;
                layoutElement.preferredHeight = 0;
                layoutElement.minHeight = 0;
            }

            return spacer;
        }

        /// <summary>
        /// Alternative approach: Use a custom layout group that supports SPACE_BETWEEN.
        /// This adds a SpaceBetweenLayout component instead of modifying HorizontalLayoutGroup.
        /// </summary>
        public static SpaceBetweenLayout AddSpaceBetweenLayout(GameObject container, SyncNode containerNode)
        {
            var layout = container.AddComponent<SpaceBetweenLayout>();
            layout.IsHorizontal = containerNode.LayoutMode == LayoutMode.Horizontal;
            layout.padding = new RectOffset(
                containerNode.PaddingLeft,
                containerNode.PaddingRight,
                containerNode.PaddingTop,
                containerNode.PaddingBottom
            );
            return layout;
        }

        /// <summary>
        /// Cleans up spacer objects from a container.
        /// </summary>
        public static void RemoveSpacers(GameObject container)
        {
            var toRemove = new List<Transform>();

            foreach (Transform child in container.transform)
            {
                if (child.name.StartsWith(SpacerPrefix))
                {
                    toRemove.Add(child);
                }
            }

            foreach (var t in toRemove)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }
    }

    /// <summary>
    /// Custom layout group that implements SPACE_BETWEEN behavior.
    /// This is an alternative to using spacer GameObjects.
    /// </summary>
    [ExecuteAlways]
    public class SpaceBetweenLayout : LayoutGroup
    {
        public bool IsHorizontal = true;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();

            float totalMin = padding.horizontal;
            float totalPreferred = padding.horizontal;

            foreach (var child in rectChildren)
            {
                totalMin += LayoutUtility.GetMinWidth(child);
                totalPreferred += LayoutUtility.GetPreferredWidth(child);
            }

            SetLayoutInputForAxis(totalMin, totalPreferred, -1, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            float totalMin = padding.vertical;
            float totalPreferred = padding.vertical;

            foreach (var child in rectChildren)
            {
                totalMin += LayoutUtility.GetMinHeight(child);
                totalPreferred += LayoutUtility.GetPreferredHeight(child);
            }

            SetLayoutInputForAxis(totalMin, totalPreferred, -1, 1);
        }

        public override void SetLayoutHorizontal()
        {
            if (IsHorizontal)
            {
                SetChildrenAlongAxisSpaceBetween(0);
            }
            else
            {
                SetChildrenAlongAxisStretch(0);
            }
        }

        public override void SetLayoutVertical()
        {
            if (!IsHorizontal)
            {
                SetChildrenAlongAxisSpaceBetween(1);
            }
            else
            {
                SetChildrenAlongAxisStretch(1);
            }
        }

        private void SetChildrenAlongAxisSpaceBetween(int axis)
        {
            float size = rectTransform.rect.size[axis];
            float innerSize = size - (axis == 0 ? padding.horizontal : padding.vertical);
            float startOffset = axis == 0 ? padding.left : padding.top;

            // Calculate total children size
            float totalChildSize = 0;
            foreach (var child in rectChildren)
            {
                totalChildSize += axis == 0
                    ? LayoutUtility.GetPreferredWidth(child)
                    : LayoutUtility.GetPreferredHeight(child);
            }

            // Calculate spacing
            float spacing = 0;
            if (rectChildren.Count > 1)
            {
                spacing = (innerSize - totalChildSize) / (rectChildren.Count - 1);
                spacing = Mathf.Max(0, spacing);
            }

            // Position children
            float pos = startOffset;
            foreach (var child in rectChildren)
            {
                float childSize = axis == 0
                    ? LayoutUtility.GetPreferredWidth(child)
                    : LayoutUtility.GetPreferredHeight(child);

                SetChildAlongAxis(child, axis, pos, childSize);
                pos += childSize + spacing;
            }
        }

        private void SetChildrenAlongAxisStretch(int axis)
        {
            float size = rectTransform.rect.size[axis];
            float innerSize = size - (axis == 0 ? padding.horizontal : padding.vertical);
            float startOffset = axis == 0 ? padding.left : padding.top;

            foreach (var child in rectChildren)
            {
                SetChildAlongAxis(child, axis, startOffset, innerSize);
            }
        }
    }
}
