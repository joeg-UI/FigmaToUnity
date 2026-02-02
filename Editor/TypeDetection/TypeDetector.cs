using System.Threading;
using System.Threading.Tasks;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;

namespace FigmaSync.Editor.TypeDetection
{
    /// <summary>
    /// Main type detection orchestrator that combines rule-based and AI detection.
    /// </summary>
    public class TypeDetector
    {
        private readonly FigmaSyncSettings _settings;
        private readonly RuleBasedDetector _ruleBasedDetector;
        private readonly AITypeDetector _aiDetector;

        public TypeDetector(FigmaSyncSettings settings)
        {
            _settings = settings;
            _ruleBasedDetector = new RuleBasedDetector();
            _aiDetector = new AITypeDetector(settings);
        }

        /// <summary>
        /// Detects the semantic type of a node using rules first, then AI if needed.
        /// </summary>
        public SemanticType DetectType(SyncNode node)
        {
            var result = _ruleBasedDetector.Detect(node);
            return result.Type;
        }

        /// <summary>
        /// Detects type with detailed result including confidence.
        /// </summary>
        public TypeDetectionResult DetectTypeWithDetails(SyncNode node)
        {
            return _ruleBasedDetector.Detect(node);
        }

        /// <summary>
        /// Detects type with optional AI fallback for low-confidence results.
        /// </summary>
        public async Task<TypeDetectionResult> DetectTypeAsync(SyncNode node, CancellationToken cancellationToken = default)
        {
            // First try rule-based detection
            var ruleResult = _ruleBasedDetector.Detect(node);

            // If confidence is high enough, use rule-based result
            if (ruleResult.Confidence >= DetectionConfidence.Medium)
            {
                return ruleResult;
            }

            // If AI detection is enabled and rule confidence is low, try AI
            if (_settings.EnableAIDetection && ruleResult.Confidence < DetectionConfidence.Medium)
            {
                var aiResult = await _aiDetector.DetectAsync(node, null, cancellationToken);

                // Use AI result if it has better confidence
                if (aiResult.Confidence > ruleResult.Confidence)
                {
                    return aiResult;
                }
            }

            return ruleResult;
        }

        /// <summary>
        /// Batch detects types for multiple nodes.
        /// </summary>
        public async Task DetectTypesAsync(SyncNode rootNode, CancellationToken cancellationToken = default)
        {
            await DetectTypeRecursiveAsync(rootNode, cancellationToken);
        }

        private async Task DetectTypeRecursiveAsync(SyncNode node, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await DetectTypeAsync(node, cancellationToken);
            node.Type = result.Type;

            foreach (var child in node.Children)
            {
                await DetectTypeRecursiveAsync(child, cancellationToken);
            }
        }

        /// <summary>
        /// Checks if a node should be treated as an interactive element.
        /// </summary>
        public static bool IsInteractive(SemanticType type)
        {
            switch (type)
            {
                case SemanticType.Button:
                case SemanticType.InputField:
                case SemanticType.Toggle:
                case SemanticType.Slider:
                case SemanticType.Dropdown:
                case SemanticType.Tab:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks if a node should have a Unity Button component.
        /// </summary>
        public static bool ShouldHaveButton(SemanticType type)
        {
            return type == SemanticType.Button || type == SemanticType.Tab;
        }

        /// <summary>
        /// Checks if a node is a text element.
        /// </summary>
        public static bool IsTextElement(SemanticType type)
        {
            return type == SemanticType.Label;
        }

        /// <summary>
        /// Checks if a node is an image element.
        /// </summary>
        public static bool IsImageElement(SemanticType type)
        {
            return type == SemanticType.Image || type == SemanticType.Icon || type == SemanticType.Avatar;
        }

        /// <summary>
        /// Checks if a node is a container element.
        /// </summary>
        public static bool IsContainer(SemanticType type)
        {
            switch (type)
            {
                case SemanticType.Container:
                case SemanticType.Card:
                case SemanticType.Navigation:
                case SemanticType.Header:
                case SemanticType.Footer:
                case SemanticType.Modal:
                case SemanticType.ScrollView:
                case SemanticType.List:
                case SemanticType.TabControl:
                    return true;
                default:
                    return false;
            }
        }
    }
}
