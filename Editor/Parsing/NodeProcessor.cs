using System.Text.RegularExpressions;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;

namespace FigmaSync.Editor.Parsing
{
    /// <summary>
    /// Processes individual Figma nodes into SyncNodes.
    /// </summary>
    public class NodeProcessor
    {
        private readonly FigmaSyncSettings _settings;

        public NodeProcessor(FigmaSyncSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Processes a FigmaNode into a SyncNode.
        /// </summary>
        public SyncNode Process(FigmaNode figmaNode, AtomicLevel pageLevel)
        {
            var syncNode = new SyncNode
            {
                Id = figmaNode.id,
                Name = figmaNode.name,
                CleanName = CleanName(figmaNode.name),
                FigmaType = figmaNode.type,
                Visible = figmaNode.visible,
                AtomicLevel = DetermineAtomicLevel(figmaNode, pageLevel)
            };

            // Process geometry
            ProcessGeometry(figmaNode, syncNode);

            // Process layout properties
            ProcessLayout(figmaNode, syncNode);

            // Process visual properties
            ProcessVisuals(figmaNode, syncNode);

            // Process text properties
            if (figmaNode.type == "TEXT")
            {
                ProcessText(figmaNode, syncNode);
            }

            // Process component properties
            ProcessComponent(figmaNode, syncNode);

            // Process prototype properties
            ProcessPrototype(figmaNode, syncNode);

            // Process scrolling
            ProcessScrolling(figmaNode, syncNode);

            return syncNode;
        }

        private void ProcessGeometry(FigmaNode figmaNode, SyncNode syncNode)
        {
            if (figmaNode.absoluteBoundingBox != null)
            {
                syncNode.X = figmaNode.absoluteBoundingBox.x;
                syncNode.Y = figmaNode.absoluteBoundingBox.y;
                syncNode.Width = figmaNode.absoluteBoundingBox.width;
                syncNode.Height = figmaNode.absoluteBoundingBox.height;
            }

            syncNode.Rotation = figmaNode.rotation;

            // Min/max constraints
            syncNode.MinWidth = figmaNode.minWidth;
            syncNode.MaxWidth = figmaNode.maxWidth;
            syncNode.MinHeight = figmaNode.minHeight;
            syncNode.MaxHeight = figmaNode.maxHeight;
        }

        private void ProcessLayout(FigmaNode figmaNode, SyncNode syncNode)
        {
            // Layout mode (when this node IS a container)
            syncNode.LayoutMode = ParseLayoutMode(figmaNode.layoutMode);
            syncNode.LayoutWrap = ParseLayoutWrap(figmaNode.layoutWrap);

            // Container alignment
            syncNode.PrimaryAxisAlign = ParseAxisAlignment(figmaNode.primaryAxisAlignItems);
            syncNode.CounterAxisAlign = ParseAxisAlignment(figmaNode.counterAxisAlignItems);

            // Spacing and padding
            syncNode.ItemSpacing = figmaNode.itemSpacing;
            syncNode.CounterAxisSpacing = figmaNode.counterAxisSpacing;
            syncNode.PaddingLeft = (int)figmaNode.paddingLeft;
            syncNode.PaddingRight = (int)figmaNode.paddingRight;
            syncNode.PaddingTop = (int)figmaNode.paddingTop;
            syncNode.PaddingBottom = (int)figmaNode.paddingBottom;

            // CRITICAL: Individual sizing behavior
            syncNode.SizingHorizontal = ParseSizingMode(figmaNode.layoutSizingHorizontal);
            syncNode.SizingVertical = ParseSizingMode(figmaNode.layoutSizingVertical);
            syncNode.LayoutGrow = figmaNode.layoutGrow;

            // Positioning mode
            syncNode.Positioning = figmaNode.layoutPositioning == "ABSOLUTE"
                ? PositioningMode.Absolute
                : PositioningMode.Auto;

            // Constraints for absolute positioning
            if (figmaNode.constraints != null)
            {
                syncNode.HorizontalConstraint = ParseConstraint(figmaNode.constraints.horizontal);
                syncNode.VerticalConstraint = ParseConstraint(figmaNode.constraints.vertical);
            }
        }

        private void ProcessVisuals(FigmaNode figmaNode, SyncNode syncNode)
        {
            // Background color
            if (figmaNode.backgroundColor != null)
            {
                syncNode.BackgroundColor = new SyncColor(
                    figmaNode.backgroundColor.r,
                    figmaNode.backgroundColor.g,
                    figmaNode.backgroundColor.b,
                    figmaNode.backgroundColor.a
                );
            }

            // Fills
            if (figmaNode.fills != null)
            {
                foreach (var fill in figmaNode.fills)
                {
                    var syncFill = ProcessFill(fill);
                    if (syncFill != null)
                    {
                        syncNode.Fills.Add(syncFill);

                        // Track image hash
                        if (!string.IsNullOrEmpty(fill.imageHash))
                        {
                            syncNode.ImageHash = fill.imageHash;
                        }
                    }
                }
            }

            // Strokes
            if (figmaNode.strokes != null)
            {
                foreach (var stroke in figmaNode.strokes)
                {
                    var syncStroke = ProcessStroke(stroke, figmaNode.strokeWeight, figmaNode.strokeAlign);
                    if (syncStroke != null)
                    {
                        syncNode.Strokes.Add(syncStroke);
                    }
                }
            }

            syncNode.StrokeWeight = figmaNode.strokeWeight;
            syncNode.Opacity = figmaNode.opacity;

            // Corner radius
            syncNode.CornerRadius = figmaNode.cornerRadius;
            syncNode.CornerRadii = figmaNode.rectangleCornerRadii;

            syncNode.ClipsContent = figmaNode.clipsContent;

            // Effects
            if (figmaNode.effects != null)
            {
                foreach (var effect in figmaNode.effects)
                {
                    var syncEffect = ProcessEffect(effect);
                    if (syncEffect != null)
                    {
                        syncNode.Effects.Add(syncEffect);
                    }
                }
            }
        }

        private SyncFill ProcessFill(FigmaFill fill)
        {
            if (!fill.visible) return null;

            var syncFill = new SyncFill
            {
                Visible = fill.visible,
                Opacity = fill.opacity
            };

            switch (fill.type)
            {
                case "SOLID":
                    syncFill.Type = FillType.Solid;
                    if (fill.color != null)
                    {
                        syncFill.Color = new SyncColor(fill.color.r, fill.color.g, fill.color.b, fill.color.a);
                    }
                    break;

                case "GRADIENT_LINEAR":
                    syncFill.Type = FillType.GradientLinear;
                    ProcessGradientStops(fill, syncFill);
                    break;

                case "GRADIENT_RADIAL":
                    syncFill.Type = FillType.GradientRadial;
                    ProcessGradientStops(fill, syncFill);
                    break;

                case "GRADIENT_ANGULAR":
                    syncFill.Type = FillType.GradientAngular;
                    ProcessGradientStops(fill, syncFill);
                    break;

                case "GRADIENT_DIAMOND":
                    syncFill.Type = FillType.GradientDiamond;
                    ProcessGradientStops(fill, syncFill);
                    break;

                case "IMAGE":
                    syncFill.Type = FillType.Image;
                    syncFill.ImageHash = fill.imageHash;
                    syncFill.ImageScaleMode = fill.scaleMode;
                    break;

                default:
                    return null;
            }

            return syncFill;
        }

        private void ProcessGradientStops(FigmaFill fill, SyncFill syncFill)
        {
            if (fill.gradientStops == null) return;

            syncFill.GradientStops = new System.Collections.Generic.List<SyncGradientStop>();
            foreach (var stop in fill.gradientStops)
            {
                syncFill.GradientStops.Add(new SyncGradientStop
                {
                    Position = stop.position,
                    Color = new SyncColor(stop.color.r, stop.color.g, stop.color.b, stop.color.a)
                });
            }
        }

        private SyncStroke ProcessStroke(FigmaStroke stroke, float weight, string align)
        {
            if (!stroke.visible) return null;

            return new SyncStroke
            {
                Visible = stroke.visible,
                Opacity = stroke.opacity,
                Weight = weight,
                Align = align,
                Color = stroke.color != null
                    ? new SyncColor(stroke.color.r, stroke.color.g, stroke.color.b, stroke.color.a)
                    : null
            };
        }

        private SyncEffect ProcessEffect(FigmaEffect effect)
        {
            if (!effect.visible) return null;

            var syncEffect = new SyncEffect
            {
                Visible = effect.visible,
                Radius = effect.radius,
                Spread = effect.spread
            };

            if (effect.color != null)
            {
                syncEffect.Color = new SyncColor(effect.color.r, effect.color.g, effect.color.b, effect.color.a);
            }

            if (effect.offset != null)
            {
                syncEffect.OffsetX = effect.offset.x;
                syncEffect.OffsetY = effect.offset.y;
            }

            switch (effect.type)
            {
                case "DROP_SHADOW":
                    syncEffect.Type = EffectType.DropShadow;
                    break;
                case "INNER_SHADOW":
                    syncEffect.Type = EffectType.InnerShadow;
                    break;
                case "LAYER_BLUR":
                    syncEffect.Type = EffectType.LayerBlur;
                    break;
                case "BACKGROUND_BLUR":
                    syncEffect.Type = EffectType.BackgroundBlur;
                    break;
                default:
                    return null;
            }

            return syncEffect;
        }

        private void ProcessText(FigmaNode figmaNode, SyncNode syncNode)
        {
            syncNode.Text = figmaNode.characters;

            if (figmaNode.style != null)
            {
                var style = figmaNode.style;
                syncNode.TextStyle = new SyncTextStyle
                {
                    FontFamily = style.fontFamily,
                    FontPostScriptName = style.fontPostScriptName,
                    FontSize = style.fontSize,
                    FontWeight = style.fontWeight,
                    HorizontalAlign = ParseTextAlignH(style.textAlignHorizontal),
                    VerticalAlign = ParseTextAlignV(style.textAlignVertical),
                    LetterSpacing = style.letterSpacing,
                    LineHeight = style.lineHeightPx,
                    TextCase = ParseTextCase(style.textCase),
                    TextDecoration = ParseTextDecoration(style.textDecoration),
                    TextAutoResize = figmaNode.textAutoResize
                };

                // Text color from fills
                if (style.fills != null && style.fills.Count > 0)
                {
                    var firstFill = style.fills[0];
                    if (firstFill.color != null)
                    {
                        syncNode.TextStyle.Color = new SyncColor(
                            firstFill.color.r,
                            firstFill.color.g,
                            firstFill.color.b,
                            firstFill.color.a
                        );
                    }
                }
            }
        }

        private void ProcessComponent(FigmaNode figmaNode, SyncNode syncNode)
        {
            syncNode.IsComponent = figmaNode.type == "COMPONENT" || figmaNode.type == "COMPONENT_SET";
            syncNode.IsComponentInstance = figmaNode.type == "INSTANCE";
            syncNode.ComponentId = figmaNode.componentId;

            if (figmaNode.componentProperties != null)
            {
                foreach (var kvp in figmaNode.componentProperties)
                {
                    syncNode.ComponentProperties[kvp.Key] = kvp.Value.value;
                }
            }
        }

        private void ProcessPrototype(FigmaNode figmaNode, SyncNode syncNode)
        {
            syncNode.HasPrototypeAction = figmaNode.HasPrototypeAction;

            if (figmaNode.interactions != null && figmaNode.interactions.Count > 0)
            {
                var firstAction = figmaNode.interactions[0];
                if (firstAction.actions != null && firstAction.actions.Count > 0)
                {
                    syncNode.PrototypeDestination = firstAction.actions[0].destinationId;
                }
            }
        }

        private void ProcessScrolling(FigmaNode figmaNode, SyncNode syncNode)
        {
            if (figmaNode.overflowDirection != null)
            {
                syncNode.HasHorizontalScrolling = figmaNode.overflowDirection.horizontal;
                syncNode.HasVerticalScrolling = figmaNode.overflowDirection.vertical;
            }
        }

        private AtomicLevel DetermineAtomicLevel(FigmaNode figmaNode, AtomicLevel pageLevel)
        {
            // Components inherit the page level by default
            if (figmaNode.type == "COMPONENT" || figmaNode.type == "COMPONENT_SET")
            {
                return pageLevel;
            }

            // Instances inherit from their component
            // (This will be resolved later when we have the full document)
            return pageLevel;
        }

        private string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";

            // Remove special characters, replace spaces and slashes with underscores
            var clean = Regex.Replace(name, @"[^\w\-]", "_");
            clean = Regex.Replace(clean, @"_+", "_").Trim('_');

            if (string.IsNullOrEmpty(clean)) return "Unnamed";

            return clean;
        }

        #region Enum Parsing Helpers

        private LayoutMode ParseLayoutMode(string value)
        {
            switch (value)
            {
                case "HORIZONTAL": return LayoutMode.Horizontal;
                case "VERTICAL": return LayoutMode.Vertical;
                default: return LayoutMode.None;
            }
        }

        private LayoutWrap ParseLayoutWrap(string value)
        {
            return value == "WRAP" ? LayoutWrap.Wrap : LayoutWrap.NoWrap;
        }

        private SizingMode ParseSizingMode(string value)
        {
            switch (value)
            {
                case "HUG": return SizingMode.Hug;
                case "FILL": return SizingMode.Fill;
                default: return SizingMode.Fixed;
            }
        }

        private AxisAlignment ParseAxisAlignment(string value)
        {
            switch (value)
            {
                case "CENTER": return AxisAlignment.Center;
                case "MAX": return AxisAlignment.End;
                case "SPACE_BETWEEN": return AxisAlignment.SpaceBetween;
                case "BASELINE": return AxisAlignment.Baseline;
                default: return AxisAlignment.Start;
            }
        }

        private ConstraintMode ParseConstraint(string value)
        {
            switch (value)
            {
                case "RIGHT": return ConstraintMode.Right;
                case "CENTER": return ConstraintMode.Center;
                case "LEFT_RIGHT": return ConstraintMode.LeftRight;
                case "SCALE": return ConstraintMode.Scale;
                case "BOTTOM": return ConstraintMode.Bottom;
                case "TOP_BOTTOM": return ConstraintMode.TopBottom;
                default: return ConstraintMode.Left;
            }
        }

        private TextAlignmentH ParseTextAlignH(string value)
        {
            switch (value)
            {
                case "CENTER": return TextAlignmentH.Center;
                case "RIGHT": return TextAlignmentH.Right;
                case "JUSTIFIED": return TextAlignmentH.Justified;
                default: return TextAlignmentH.Left;
            }
        }

        private TextAlignmentV ParseTextAlignV(string value)
        {
            switch (value)
            {
                case "CENTER": return TextAlignmentV.Center;
                case "BOTTOM": return TextAlignmentV.Bottom;
                default: return TextAlignmentV.Top;
            }
        }

        private TextCase ParseTextCase(string value)
        {
            switch (value)
            {
                case "UPPER": return TextCase.Upper;
                case "LOWER": return TextCase.Lower;
                case "TITLE": return TextCase.Title;
                default: return TextCase.Original;
            }
        }

        private TextDecoration ParseTextDecoration(string value)
        {
            switch (value)
            {
                case "UNDERLINE": return TextDecoration.Underline;
                case "STRIKETHROUGH": return TextDecoration.Strikethrough;
                default: return TextDecoration.None;
            }
        }

        #endregion
    }
}
