using System.Text.RegularExpressions;
using FigmaSync.Editor.Models;

namespace FigmaSync.Editor.TypeDetection
{
    /// <summary>
    /// Rule-based type detection using naming conventions and structural analysis.
    /// </summary>
    public class RuleBasedDetector
    {
        /// <summary>
        /// Detects semantic type using rules and heuristics.
        /// </summary>
        public TypeDetectionResult Detect(SyncNode node)
        {
            // Priority 1: Figma type gives us definitive answers
            var figmaTypeResult = DetectFromFigmaType(node);
            if (figmaTypeResult != null && figmaTypeResult.Confidence >= DetectionConfidence.High)
            {
                return figmaTypeResult;
            }

            // Priority 2: Prototype interactions indicate buttons
            if (node.HasPrototypeAction)
            {
                return new TypeDetectionResult(SemanticType.Button, DetectionConfidence.High, "Has prototype interaction");
            }

            // Priority 3: Name-based detection
            var nameResult = DetectFromName(node);
            if (nameResult != null && nameResult.Confidence >= DetectionConfidence.Medium)
            {
                return nameResult;
            }

            // Priority 4: Structure-based detection
            var structureResult = DetectFromStructure(node);
            if (structureResult != null)
            {
                return structureResult;
            }

            // Fallback to Figma type result or Container
            return figmaTypeResult ?? new TypeDetectionResult(SemanticType.Container, DetectionConfidence.Low);
        }

        private TypeDetectionResult DetectFromFigmaType(SyncNode node)
        {
            switch (node.FigmaType)
            {
                case "TEXT":
                    return new TypeDetectionResult(SemanticType.Label, DetectionConfidence.VeryHigh, "Figma TEXT node");

                case "VECTOR":
                case "STAR":
                case "POLYGON":
                case "ELLIPSE":
                case "LINE":
                case "BOOLEAN_OPERATION":
                    // These are typically icons or decorative elements
                    return new TypeDetectionResult(SemanticType.Icon, DetectionConfidence.Medium, "Figma vector node");

                case "RECTANGLE":
                    // Could be image, container, or divider based on other properties
                    if (node.HasImageFill)
                    {
                        return new TypeDetectionResult(SemanticType.Image, DetectionConfidence.High, "Rectangle with image fill");
                    }
                    if (node.Width > node.Height * 10 || node.Height > node.Width * 10)
                    {
                        return new TypeDetectionResult(SemanticType.Divider, DetectionConfidence.Medium, "Very thin rectangle");
                    }
                    break;

                case "COMPONENT":
                case "COMPONENT_SET":
                case "INSTANCE":
                    // Components need name-based detection
                    break;
            }

            return null;
        }

        private TypeDetectionResult DetectFromName(SyncNode node)
        {
            var name = node.Name?.ToLower() ?? "";

            // Button patterns
            if (MatchesPattern(name, "btn", "button", "cta", "action", "submit", "cancel", "confirm", "close"))
            {
                return new TypeDetectionResult(SemanticType.Button, DetectionConfidence.High, "Name contains button keyword");
            }

            // Input field patterns
            if (MatchesPattern(name, "input", "field", "textfield", "text-field", "text_field", "textarea", "search"))
            {
                return new TypeDetectionResult(SemanticType.InputField, DetectionConfidence.High, "Name contains input keyword");
            }

            // Toggle patterns
            if (MatchesPattern(name, "toggle", "switch", "checkbox", "check", "radio"))
            {
                return new TypeDetectionResult(SemanticType.Toggle, DetectionConfidence.High, "Name contains toggle keyword");
            }

            // Slider patterns
            if (MatchesPattern(name, "slider", "range", "scrubber"))
            {
                return new TypeDetectionResult(SemanticType.Slider, DetectionConfidence.High, "Name contains slider keyword");
            }

            // Dropdown patterns
            if (MatchesPattern(name, "dropdown", "select", "picker", "menu", "combobox"))
            {
                return new TypeDetectionResult(SemanticType.Dropdown, DetectionConfidence.High, "Name contains dropdown keyword");
            }

            // Image patterns
            if (MatchesPattern(name, "image", "img", "photo", "picture", "thumbnail", "cover"))
            {
                return new TypeDetectionResult(SemanticType.Image, DetectionConfidence.High, "Name contains image keyword");
            }

            // Icon patterns
            if (MatchesPattern(name, "icon", "icn", "glyph", "symbol"))
            {
                return new TypeDetectionResult(SemanticType.Icon, DetectionConfidence.High, "Name contains icon keyword");
            }

            // Scroll view patterns
            if (MatchesPattern(name, "scroll", "scrollview", "scrollable", "scroller"))
            {
                return new TypeDetectionResult(SemanticType.ScrollView, DetectionConfidence.High, "Name contains scroll keyword");
            }

            // List patterns
            if (MatchesPattern(name, "list", "grid", "collection", "items"))
            {
                return new TypeDetectionResult(SemanticType.List, DetectionConfidence.Medium, "Name contains list keyword");
            }

            // Card patterns
            if (MatchesPattern(name, "card", "tile", "panel", "cell"))
            {
                return new TypeDetectionResult(SemanticType.Card, DetectionConfidence.Medium, "Name contains card keyword");
            }

            // Navigation patterns
            if (MatchesPattern(name, "nav", "navbar", "navigation", "menubar", "sidebar", "bottombar", "tabbar"))
            {
                return new TypeDetectionResult(SemanticType.Navigation, DetectionConfidence.High, "Name contains navigation keyword");
            }

            // Header patterns
            if (MatchesPattern(name, "header", "topbar", "appbar", "titlebar"))
            {
                return new TypeDetectionResult(SemanticType.Header, DetectionConfidence.High, "Name contains header keyword");
            }

            // Footer patterns
            if (MatchesPattern(name, "footer", "bottombar"))
            {
                return new TypeDetectionResult(SemanticType.Footer, DetectionConfidence.High, "Name contains footer keyword");
            }

            // Modal patterns
            if (MatchesPattern(name, "modal", "dialog", "popup", "overlay", "sheet"))
            {
                return new TypeDetectionResult(SemanticType.Modal, DetectionConfidence.High, "Name contains modal keyword");
            }

            // Tooltip patterns
            if (MatchesPattern(name, "tooltip", "hint", "popover"))
            {
                return new TypeDetectionResult(SemanticType.Tooltip, DetectionConfidence.High, "Name contains tooltip keyword");
            }

            // Progress patterns
            if (MatchesPattern(name, "progress", "loading", "spinner", "loader"))
            {
                return new TypeDetectionResult(SemanticType.ProgressBar, DetectionConfidence.High, "Name contains progress keyword");
            }

            // Tab patterns
            if (MatchesPattern(name, "tab", "tabs", "tabcontrol", "tabview"))
            {
                return new TypeDetectionResult(SemanticType.TabControl, DetectionConfidence.Medium, "Name contains tab keyword");
            }

            // Badge patterns
            if (MatchesPattern(name, "badge", "tag", "chip", "pill", "notification"))
            {
                return new TypeDetectionResult(SemanticType.Badge, DetectionConfidence.Medium, "Name contains badge keyword");
            }

            // Avatar patterns
            if (MatchesPattern(name, "avatar", "profile", "user-image", "userimage"))
            {
                return new TypeDetectionResult(SemanticType.Avatar, DetectionConfidence.High, "Name contains avatar keyword");
            }

            // Divider patterns
            if (MatchesPattern(name, "divider", "separator", "line", "hr"))
            {
                return new TypeDetectionResult(SemanticType.Divider, DetectionConfidence.Medium, "Name contains divider keyword");
            }

            // Spacer patterns
            if (MatchesPattern(name, "spacer", "gap", "padding"))
            {
                return new TypeDetectionResult(SemanticType.Spacer, DetectionConfidence.Medium, "Name contains spacer keyword");
            }

            // Label patterns (lower priority as it's common)
            if (MatchesPattern(name, "label", "title", "text", "caption", "subtitle", "heading", "description"))
            {
                return new TypeDetectionResult(SemanticType.Label, DetectionConfidence.Low, "Name contains label keyword");
            }

            return null;
        }

        private TypeDetectionResult DetectFromStructure(SyncNode node)
        {
            // Image detection based on fills
            if (node.HasImageFill && node.Children.Count == 0)
            {
                return new TypeDetectionResult(SemanticType.Image, DetectionConfidence.High, "Has image fill and no children");
            }

            // Scroll view detection based on overflow
            if (node.HasScrolling)
            {
                return new TypeDetectionResult(SemanticType.ScrollView, DetectionConfidence.High, "Has scrolling enabled");
            }

            // Button detection: small interactive-looking frame with text
            if (node.Children.Count > 0 && node.Children.Count <= 3 && node.HasTextChild)
            {
                // Check if it looks like a button (has background, reasonable size)
                bool hasBackground = node.Fills.Count > 0 && node.Fills[0].Type == FillType.Solid;
                bool reasonableSize = node.Width < 400 && node.Height < 100;

                if (hasBackground && reasonableSize && node.CornerRadius > 0)
                {
                    return new TypeDetectionResult(SemanticType.Button, DetectionConfidence.Low, "Looks like a button");
                }
            }

            // Input field detection: frame with placeholder text
            if (node.Children.Count >= 1 && node.Children.Count <= 2)
            {
                bool hasTextChild = false;

                foreach (var child in node.Children)
                {
                    if (child.FigmaType == "TEXT") hasTextChild = true;
                }

                // Stroke typically indicates input field
                if (hasTextChild && node.Strokes.Count > 0)
                {
                    return new TypeDetectionResult(SemanticType.InputField, DetectionConfidence.Low, "Frame with text and stroke");
                }
            }

            // Divider detection: very thin rectangle
            if (node.FigmaType == "RECTANGLE" || node.FigmaType == "FRAME")
            {
                if (node.Width > 0 && node.Height > 0)
                {
                    float ratio = node.Width / node.Height;
                    if (ratio > 20 || ratio < 0.05f)
                    {
                        return new TypeDetectionResult(SemanticType.Divider, DetectionConfidence.Low, "Very thin element");
                    }
                }
            }

            return null;
        }

        private bool MatchesPattern(string name, params string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                // Match as whole word or with common separators
                var regex = new Regex($@"(^|[_\-\s/])({Regex.Escape(pattern)})($|[_\-\s/])", RegexOptions.IgnoreCase);
                if (regex.IsMatch(name))
                {
                    return true;
                }

                // Also check if name starts with or equals the pattern
                if (name.StartsWith(pattern) || name == pattern)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
