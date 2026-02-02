using System;
using System.Collections.Generic;
using FigmaSync.Editor.Settings;
using FigmaSync.Editor.TypeDetection;
using UnityEngine;

namespace FigmaSync.Editor.Models
{
    /// <summary>
    /// Intermediate node format that bridges Figma data and Unity generation.
    /// </summary>
    [Serializable]
    public class SyncNode
    {
        // Identity
        public string Id;
        public string Name;
        public string CleanName;
        public string FigmaType;
        public bool Visible = true;

        // Semantic type (determined by TypeDetector)
        public SemanticType Type = SemanticType.Container;

        // Atomic design level
        public AtomicLevel AtomicLevel = AtomicLevel.Screen;

        // Position and size (absolute)
        public float X;
        public float Y;
        public float Width;
        public float Height;
        public float Rotation;

        // Layout container properties (when this node IS a layout container)
        public LayoutMode LayoutMode = LayoutMode.None;
        public LayoutWrap LayoutWrap = LayoutWrap.NoWrap;
        public AxisAlignment PrimaryAxisAlign = AxisAlignment.Start;
        public AxisAlignment CounterAxisAlign = AxisAlignment.Start;
        public float ItemSpacing;
        public float CounterAxisSpacing;
        public int PaddingLeft;
        public int PaddingRight;
        public int PaddingTop;
        public int PaddingBottom;

        // Sizing behavior (how THIS node sizes within its parent)
        public SizingMode SizingHorizontal = SizingMode.Fixed;
        public SizingMode SizingVertical = SizingMode.Fixed;
        public float? MinWidth;
        public float? MaxWidth;
        public float? MinHeight;
        public float? MaxHeight;
        public float LayoutGrow;

        // Positioning mode
        public PositioningMode Positioning = PositioningMode.Auto;
        public ConstraintMode HorizontalConstraint = ConstraintMode.Left;
        public ConstraintMode VerticalConstraint = ConstraintMode.Top;

        // Visual properties
        public SyncColor BackgroundColor;
        public List<SyncFill> Fills = new List<SyncFill>();
        public List<SyncStroke> Strokes = new List<SyncStroke>();
        public float StrokeWeight;
        public float Opacity = 1f;
        public float CornerRadius;
        public float[] CornerRadii; // [topLeft, topRight, bottomRight, bottomLeft]
        public bool ClipsContent;
        public List<SyncEffect> Effects = new List<SyncEffect>();

        // Text properties
        public string Text;
        public SyncTextStyle TextStyle;

        // Component properties
        public string ComponentId;
        public bool IsComponent;
        public bool IsComponentInstance;
        public Dictionary<string, string> ComponentProperties = new Dictionary<string, string>();

        // Prototype properties
        public bool HasPrototypeAction;
        public string PrototypeDestination;

        // Scrolling
        public bool HasHorizontalScrolling;
        public bool HasVerticalScrolling;

        // Asset reference (for images)
        public string ImageHash;
        public string ImageAssetPath;

        // Children
        public List<SyncNode> Children = new List<SyncNode>();

        // Parent reference (not serialized to avoid circular reference)
        [NonSerialized]
        public SyncNode Parent;

        /// <summary>
        /// Check if this node has image fills.
        /// </summary>
        public bool HasImageFill
        {
            get
            {
                foreach (var fill in Fills)
                {
                    if (fill.Type == FillType.Image) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Check if this node has a text child.
        /// </summary>
        public bool HasTextChild
        {
            get
            {
                foreach (var child in Children)
                {
                    if (child.FigmaType == "TEXT") return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Check if this node has scrolling enabled.
        /// </summary>
        public bool HasScrolling => HasHorizontalScrolling || HasVerticalScrolling;

        /// <summary>
        /// Gets the first text content from this node or its children.
        /// </summary>
        public string GetTextContent()
        {
            if (!string.IsNullOrEmpty(Text))
                return Text;

            foreach (var child in Children)
            {
                var text = child.GetTextContent();
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }

        /// <summary>
        /// Gets all nodes at a specific atomic level.
        /// </summary>
        public void GetNodesByAtomicLevel(AtomicLevel level, List<SyncNode> results)
        {
            if (AtomicLevel == level)
            {
                results.Add(this);
            }

            foreach (var child in Children)
            {
                child.GetNodesByAtomicLevel(level, results);
            }
        }

        /// <summary>
        /// Gets all image nodes that need assets downloaded.
        /// </summary>
        public void GetImageNodes(List<SyncNode> results)
        {
            if (HasImageFill || FigmaType == "VECTOR" || FigmaType == "BOOLEAN_OPERATION")
            {
                results.Add(this);
            }

            foreach (var child in Children)
            {
                child.GetImageNodes(results);
            }
        }

        /// <summary>
        /// Finds a node by ID recursively.
        /// </summary>
        public SyncNode FindById(string id)
        {
            if (Id == id) return this;

            foreach (var child in Children)
            {
                var found = child.FindById(id);
                if (found != null) return found;
            }

            return null;
        }
    }

    /// <summary>
    /// Layout mode for containers.
    /// </summary>
    public enum LayoutMode
    {
        None,
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Layout wrap mode.
    /// </summary>
    public enum LayoutWrap
    {
        NoWrap,
        Wrap
    }

    /// <summary>
    /// Sizing mode for child elements.
    /// </summary>
    public enum SizingMode
    {
        Fixed,
        Hug,
        Fill
    }

    /// <summary>
    /// Axis alignment options.
    /// </summary>
    public enum AxisAlignment
    {
        Start,
        Center,
        End,
        SpaceBetween,
        Baseline
    }

    /// <summary>
    /// Positioning mode.
    /// </summary>
    public enum PositioningMode
    {
        Auto,
        Absolute
    }

    /// <summary>
    /// Constraint mode for absolute positioning.
    /// </summary>
    public enum ConstraintMode
    {
        Left,
        Right,
        Center,
        LeftRight,
        Scale,
        Top,
        Bottom,
        TopBottom
    }

    /// <summary>
    /// Color with alpha.
    /// </summary>
    [Serializable]
    public class SyncColor
    {
        public float R;
        public float G;
        public float B;
        public float A = 1f;

        public SyncColor() { }

        public SyncColor(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public Color ToUnityColor()
        {
            return new Color(R, G, B, A);
        }

        public static SyncColor FromUnityColor(Color color)
        {
            return new SyncColor(color.r, color.g, color.b, color.a);
        }
    }

    /// <summary>
    /// Fill type enumeration.
    /// </summary>
    public enum FillType
    {
        Solid,
        GradientLinear,
        GradientRadial,
        GradientAngular,
        GradientDiamond,
        Image
    }

    /// <summary>
    /// Fill properties.
    /// </summary>
    [Serializable]
    public class SyncFill
    {
        public FillType Type;
        public bool Visible = true;
        public float Opacity = 1f;
        public SyncColor Color;
        public List<SyncGradientStop> GradientStops;
        public string ImageHash;
        public string ImageScaleMode;
    }

    /// <summary>
    /// Gradient stop.
    /// </summary>
    [Serializable]
    public class SyncGradientStop
    {
        public float Position;
        public SyncColor Color;
    }

    /// <summary>
    /// Stroke properties.
    /// </summary>
    [Serializable]
    public class SyncStroke
    {
        public SyncColor Color;
        public float Weight;
        public bool Visible = true;
        public float Opacity = 1f;
        public string Align; // "INSIDE", "OUTSIDE", "CENTER"
    }

    /// <summary>
    /// Effect types.
    /// </summary>
    public enum EffectType
    {
        DropShadow,
        InnerShadow,
        LayerBlur,
        BackgroundBlur
    }

    /// <summary>
    /// Effect properties.
    /// </summary>
    [Serializable]
    public class SyncEffect
    {
        public EffectType Type;
        public bool Visible = true;
        public SyncColor Color;
        public float OffsetX;
        public float OffsetY;
        public float Radius;
        public float Spread;
    }

    /// <summary>
    /// Text style properties.
    /// </summary>
    [Serializable]
    public class SyncTextStyle
    {
        public string FontFamily;
        public string FontPostScriptName;
        public float FontSize;
        public float FontWeight;
        public TextAlignmentH HorizontalAlign = TextAlignmentH.Left;
        public TextAlignmentV VerticalAlign = TextAlignmentV.Top;
        public float LetterSpacing;
        public float LineHeight;
        public SyncColor Color;
        public TextCase TextCase = TextCase.Original;
        public TextDecoration TextDecoration = TextDecoration.None;
        public string TextAutoResize;
    }

    /// <summary>
    /// Horizontal text alignment.
    /// </summary>
    public enum TextAlignmentH
    {
        Left,
        Center,
        Right,
        Justified
    }

    /// <summary>
    /// Vertical text alignment.
    /// </summary>
    public enum TextAlignmentV
    {
        Top,
        Center,
        Bottom
    }

    /// <summary>
    /// Text case transformation.
    /// </summary>
    public enum TextCase
    {
        Original,
        Upper,
        Lower,
        Title
    }

    /// <summary>
    /// Text decoration.
    /// </summary>
    public enum TextDecoration
    {
        None,
        Underline,
        Strikethrough
    }
}
