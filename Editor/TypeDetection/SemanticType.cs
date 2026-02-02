namespace FigmaSync.Editor.TypeDetection
{
    /// <summary>
    /// Semantic UI element types that can be detected from Figma nodes.
    /// </summary>
    public enum SemanticType
    {
        /// <summary>
        /// Generic container with no specific semantic meaning.
        /// </summary>
        Container,

        /// <summary>
        /// A clickable button element.
        /// </summary>
        Button,

        /// <summary>
        /// A text label or heading.
        /// </summary>
        Label,

        /// <summary>
        /// A text input field.
        /// </summary>
        InputField,

        /// <summary>
        /// A toggle/checkbox/switch control.
        /// </summary>
        Toggle,

        /// <summary>
        /// A slider control.
        /// </summary>
        Slider,

        /// <summary>
        /// A dropdown/select control.
        /// </summary>
        Dropdown,

        /// <summary>
        /// An image element.
        /// </summary>
        Image,

        /// <summary>
        /// An icon (small image with specific naming).
        /// </summary>
        Icon,

        /// <summary>
        /// A scrollable container.
        /// </summary>
        ScrollView,

        /// <summary>
        /// A list or grid of items.
        /// </summary>
        List,

        /// <summary>
        /// A card or panel component.
        /// </summary>
        Card,

        /// <summary>
        /// A navigation bar or menu.
        /// </summary>
        Navigation,

        /// <summary>
        /// A header section.
        /// </summary>
        Header,

        /// <summary>
        /// A footer section.
        /// </summary>
        Footer,

        /// <summary>
        /// A modal or dialog.
        /// </summary>
        Modal,

        /// <summary>
        /// A tooltip or popover.
        /// </summary>
        Tooltip,

        /// <summary>
        /// A progress bar or indicator.
        /// </summary>
        ProgressBar,

        /// <summary>
        /// A tab control.
        /// </summary>
        TabControl,

        /// <summary>
        /// An individual tab.
        /// </summary>
        Tab,

        /// <summary>
        /// A badge or notification indicator.
        /// </summary>
        Badge,

        /// <summary>
        /// An avatar or profile image.
        /// </summary>
        Avatar,

        /// <summary>
        /// A divider or separator.
        /// </summary>
        Divider,

        /// <summary>
        /// A spacer element.
        /// </summary>
        Spacer
    }

    /// <summary>
    /// Confidence level for type detection.
    /// </summary>
    public enum DetectionConfidence
    {
        /// <summary>
        /// Very low confidence, basically a guess.
        /// </summary>
        VeryLow,

        /// <summary>
        /// Low confidence, some indicators present.
        /// </summary>
        Low,

        /// <summary>
        /// Medium confidence, multiple indicators match.
        /// </summary>
        Medium,

        /// <summary>
        /// High confidence, strong indicators present.
        /// </summary>
        High,

        /// <summary>
        /// Very high confidence, explicit naming or type.
        /// </summary>
        VeryHigh
    }

    /// <summary>
    /// Result of type detection including confidence.
    /// </summary>
    public class TypeDetectionResult
    {
        public SemanticType Type { get; set; }
        public DetectionConfidence Confidence { get; set; }
        public string Reason { get; set; }

        public TypeDetectionResult(SemanticType type, DetectionConfidence confidence, string reason = null)
        {
            Type = type;
            Confidence = confidence;
            Reason = reason;
        }
    }
}
