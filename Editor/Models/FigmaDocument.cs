using System;
using System.Collections.Generic;
using UnityEngine;

namespace FigmaSync.Editor.Models
{
    /// <summary>
    /// Root response from Figma API GET /v1/files/:file_key
    /// </summary>
    [Serializable]
    public class FigmaFileResponse
    {
        public string name;
        public string lastModified;
        public string thumbnailUrl;
        public string version;
        public string role;
        public string editorType;
        public FigmaNode document;
        public Dictionary<string, FigmaComponent> components;
        public Dictionary<string, FigmaComponentSet> componentSets;
        public Dictionary<string, FigmaStyle> styles;
    }

    /// <summary>
    /// Response from Figma API GET /v1/images/:file_key
    /// </summary>
    [Serializable]
    public class FigmaImagesResponse
    {
        public string err;
        public Dictionary<string, string> images;
    }

    /// <summary>
    /// Core Figma node structure representing any element in the design.
    /// </summary>
    [Serializable]
    public class FigmaNode
    {
        public string id;
        public string name;
        public string type;
        public bool visible = true;
        public bool locked;

        // Geometry
        public FigmaRectangle absoluteBoundingBox;
        public FigmaRectangle absoluteRenderBounds;
        public float rotation;

        // Layout properties (critical for proper translation)
        public string layoutMode; // "NONE", "HORIZONTAL", "VERTICAL"
        public string layoutWrap; // "NO_WRAP", "WRAP"
        public string primaryAxisSizingMode; // "FIXED", "AUTO"
        public string counterAxisSizingMode; // "FIXED", "AUTO"
        public string primaryAxisAlignItems; // "MIN", "CENTER", "MAX", "SPACE_BETWEEN"
        public string counterAxisAlignItems; // "MIN", "CENTER", "MAX", "BASELINE"
        public string counterAxisAlignContent; // For wrapped layouts
        public float itemSpacing;
        public float counterAxisSpacing;
        public float paddingLeft;
        public float paddingRight;
        public float paddingTop;
        public float paddingBottom;

        // Individual sizing (THE KEY PROPERTIES)
        public string layoutSizingHorizontal; // "FIXED", "HUG", "FILL"
        public string layoutSizingVertical;   // "FIXED", "HUG", "FILL"
        public string layoutAlign; // "INHERIT", "STRETCH", "MIN", "CENTER", "MAX"
        public float layoutGrow; // For fill behavior

        // Positioning
        public string layoutPositioning; // "AUTO", "ABSOLUTE"
        public float? minWidth;
        public float? maxWidth;
        public float? minHeight;
        public float? maxHeight;

        // Children
        public List<FigmaNode> children;

        // Visual properties
        public FigmaColor backgroundColor;
        public List<FigmaFill> fills;
        public List<FigmaStroke> strokes;
        public float strokeWeight;
        public string strokeAlign; // "INSIDE", "OUTSIDE", "CENTER"
        public List<FigmaEffect> effects;
        public float opacity = 1f;
        public string blendMode;
        public bool isMask;
        public float cornerRadius;
        public float[] rectangleCornerRadii; // [topLeft, topRight, bottomRight, bottomLeft]
        public string cornerSmoothing;
        public bool clipsContent;

        // Text properties
        public string characters;
        public FigmaTypeStyle style;
        public List<int> characterStyleOverrides;
        public Dictionary<string, FigmaTypeStyle> styleOverrideTable;
        public string textAutoResize; // "NONE", "HEIGHT", "WIDTH_AND_HEIGHT", "TRUNCATE"

        // Component properties
        public string componentId;
        public Dictionary<string, FigmaComponentProperty> componentProperties;
        public bool isComponentSet;

        // Prototype properties
        public List<FigmaPrototypeAction> interactions;
        public FigmaOverflowDirection overflowDirection;

        // Constraints (for absolute positioning)
        public FigmaConstraints constraints;

        // Export settings
        public List<FigmaExportSetting> exportSettings;

        /// <summary>
        /// Check if this node has any prototype interactions (likely a button).
        /// </summary>
        public bool HasPrototypeAction => interactions != null && interactions.Count > 0;

        /// <summary>
        /// Check if this node has image fills.
        /// </summary>
        public bool HasImageFill
        {
            get
            {
                if (fills == null) return false;
                foreach (var fill in fills)
                {
                    if (fill.type == "IMAGE") return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Check if this node has scrolling overflow.
        /// </summary>
        public bool HasScrolling => overflowDirection != null &&
            (overflowDirection.horizontal || overflowDirection.vertical);
    }

    /// <summary>
    /// Rectangle bounds structure.
    /// </summary>
    [Serializable]
    public class FigmaRectangle
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    /// <summary>
    /// Color structure with RGBA values (0-1 range).
    /// </summary>
    [Serializable]
    public class FigmaColor
    {
        public float r;
        public float g;
        public float b;
        public float a = 1f;

        public Color ToUnityColor()
        {
            return new Color(r, g, b, a);
        }
    }

    /// <summary>
    /// Fill properties for shapes and frames.
    /// </summary>
    [Serializable]
    public class FigmaFill
    {
        public string type; // "SOLID", "GRADIENT_LINEAR", "GRADIENT_RADIAL", "GRADIENT_ANGULAR", "GRADIENT_DIAMOND", "IMAGE", "EMOJI"
        public bool visible = true;
        public float opacity = 1f;
        public FigmaColor color;
        public string blendMode;
        public List<FigmaColorStop> gradientStops;
        public List<FigmaVector> gradientHandlePositions; // For gradients
        public string scaleMode; // For images: "FILL", "FIT", "CROP", "TILE"
        public string imageRef; // Reference to image
        public string imageHash; // Hash for image lookup
        public FigmaImageFilters filters;
    }

    /// <summary>
    /// Gradient color stop.
    /// </summary>
    [Serializable]
    public class FigmaColorStop
    {
        public float position;
        public FigmaColor color;
    }

    /// <summary>
    /// Image filter settings.
    /// </summary>
    [Serializable]
    public class FigmaImageFilters
    {
        public float exposure;
        public float contrast;
        public float saturation;
        public float temperature;
        public float tint;
        public float highlights;
        public float shadows;
    }

    /// <summary>
    /// Stroke properties.
    /// </summary>
    [Serializable]
    public class FigmaStroke
    {
        public string type;
        public bool visible = true;
        public float opacity = 1f;
        public FigmaColor color;
        public string blendMode;
    }

    /// <summary>
    /// Effect properties (shadows, blurs, etc.).
    /// </summary>
    [Serializable]
    public class FigmaEffect
    {
        public string type; // "INNER_SHADOW", "DROP_SHADOW", "LAYER_BLUR", "BACKGROUND_BLUR"
        public bool visible = true;
        public FigmaColor color;
        public string blendMode;
        public FigmaVector offset;
        public float radius;
        public float spread;
        public bool showShadowBehindNode;
    }

    /// <summary>
    /// 2D vector.
    /// </summary>
    [Serializable]
    public class FigmaVector
    {
        public float x;
        public float y;

        public Vector2 ToUnityVector()
        {
            return new Vector2(x, y);
        }
    }

    /// <summary>
    /// Typography style properties.
    /// </summary>
    [Serializable]
    public class FigmaTypeStyle
    {
        public string fontFamily;
        public string fontPostScriptName;
        public float fontSize;
        public float fontWeight;
        public string textCase; // "ORIGINAL", "UPPER", "LOWER", "TITLE", "SMALL_CAPS", "SMALL_CAPS_FORCED"
        public string textDecoration; // "NONE", "STRIKETHROUGH", "UNDERLINE"
        public string textAlignHorizontal; // "LEFT", "RIGHT", "CENTER", "JUSTIFIED"
        public string textAlignVertical; // "TOP", "CENTER", "BOTTOM"
        public float letterSpacing;
        public float lineHeightPx;
        public float lineHeightPercent;
        public string lineHeightUnit; // "PIXELS", "FONT_SIZE_%", "INTRINSIC_%"
        public float paragraphSpacing;
        public float paragraphIndent;
        public List<FigmaFill> fills;
        public string hyperlink;
        public string opentypeFlags;
    }

    /// <summary>
    /// Component metadata.
    /// </summary>
    [Serializable]
    public class FigmaComponent
    {
        public string key;
        public string name;
        public string description;
        public string componentSetId;
        public List<FigmaDocumentationLink> documentationLinks;
    }

    /// <summary>
    /// Documentation link for components.
    /// </summary>
    [Serializable]
    public class FigmaDocumentationLink
    {
        public string uri;
    }

    /// <summary>
    /// Component set (variants) metadata.
    /// </summary>
    [Serializable]
    public class FigmaComponentSet
    {
        public string key;
        public string name;
        public string description;
    }

    /// <summary>
    /// Style metadata.
    /// </summary>
    [Serializable]
    public class FigmaStyle
    {
        public string key;
        public string name;
        public string styleType; // "FILL", "TEXT", "EFFECT", "GRID"
        public string description;
    }

    /// <summary>
    /// Component property for component instances.
    /// </summary>
    [Serializable]
    public class FigmaComponentProperty
    {
        public string type; // "BOOLEAN", "TEXT", "INSTANCE_SWAP", "VARIANT"
        public string value;
        public List<object> preferredValues; // Can be array of various types
    }

    /// <summary>
    /// Prototype action/interaction.
    /// </summary>
    [Serializable]
    public class FigmaPrototypeAction
    {
        public FigmaTrigger trigger;
        public List<FigmaAction> actions;
    }

    /// <summary>
    /// Prototype trigger.
    /// </summary>
    [Serializable]
    public class FigmaTrigger
    {
        public string type; // "ON_CLICK", "ON_HOVER", "ON_PRESS", "ON_DRAG", etc.
    }

    /// <summary>
    /// Prototype action.
    /// </summary>
    [Serializable]
    public class FigmaAction
    {
        public string type; // "NAVIGATE", "BACK", "CLOSE", "URL", etc.
        public string destinationId;
        public string url;
        public FigmaTransition transition;
    }

    /// <summary>
    /// Prototype transition settings.
    /// </summary>
    [Serializable]
    public class FigmaTransition
    {
        public string type;
        public float duration;
        public string easing;
    }

    /// <summary>
    /// Overflow/scrolling direction settings.
    /// </summary>
    [Serializable]
    public class FigmaOverflowDirection
    {
        public bool horizontal;
        public bool vertical;
    }

    /// <summary>
    /// Layout constraints for absolute positioning.
    /// </summary>
    [Serializable]
    public class FigmaConstraints
    {
        public string vertical;   // "TOP", "BOTTOM", "CENTER", "TOP_BOTTOM", "SCALE"
        public string horizontal; // "LEFT", "RIGHT", "CENTER", "LEFT_RIGHT", "SCALE"
    }

    /// <summary>
    /// Export settings for a node.
    /// </summary>
    [Serializable]
    public class FigmaExportSetting
    {
        public string suffix;
        public string format; // "JPG", "PNG", "SVG", "PDF"
        public FigmaConstraint constraint;
    }

    /// <summary>
    /// Export constraint.
    /// </summary>
    [Serializable]
    public class FigmaConstraint
    {
        public string type; // "SCALE", "WIDTH", "HEIGHT"
        public float value;
    }
}
