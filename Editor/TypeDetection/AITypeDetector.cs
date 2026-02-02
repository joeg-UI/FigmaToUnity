using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FigmaSync.Editor.Models;
using FigmaSync.Editor.Settings;
using UnityEngine;
using UnityEngine.Networking;

namespace FigmaSync.Editor.TypeDetection
{
    /// <summary>
    /// AI-assisted type detection using LLM APIs (OpenAI or Anthropic).
    /// </summary>
    public class AITypeDetector
    {
        private readonly FigmaSyncSettings _settings;

        public AITypeDetector(FigmaSyncSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Detects semantic type using AI analysis.
        /// </summary>
        public async Task<TypeDetectionResult> DetectAsync(SyncNode node, string screenshotBase64 = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_settings.AIApiKey))
            {
                return new TypeDetectionResult(SemanticType.Container, DetectionConfidence.VeryLow, "AI API key not configured");
            }

            try
            {
                var prompt = BuildPrompt(node);
                string response;

                if (_settings.AIProviderType == AIProvider.Anthropic)
                {
                    response = await CallAnthropicAsync(prompt, screenshotBase64, cancellationToken);
                }
                else
                {
                    response = await CallOpenAIAsync(prompt, screenshotBase64, cancellationToken);
                }

                return ParseResponse(response);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FigmaSync] AI detection failed: {ex.Message}");
                return new TypeDetectionResult(SemanticType.Container, DetectionConfidence.VeryLow, $"AI error: {ex.Message}");
            }
        }

        private string BuildPrompt(SyncNode node)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Analyze this UI element and determine its semantic type.");
            sb.AppendLine();
            sb.AppendLine($"Name: {node.Name}");
            sb.AppendLine($"Figma Type: {node.FigmaType}");
            sb.AppendLine($"Size: {node.Width}x{node.Height}");
            sb.AppendLine($"Has children: {node.Children.Count > 0} (count: {node.Children.Count})");
            sb.AppendLine($"Has text child: {node.HasTextChild}");
            sb.AppendLine($"Has image fill: {node.HasImageFill}");
            sb.AppendLine($"Has scrolling: {node.HasScrolling}");
            sb.AppendLine($"Has prototype action: {node.HasPrototypeAction}");
            sb.AppendLine($"Has background: {node.Fills.Count > 0}");
            sb.AppendLine($"Has stroke: {node.Strokes.Count > 0}");
            sb.AppendLine($"Corner radius: {node.CornerRadius}");

            if (node.Children.Count > 0 && node.Children.Count <= 5)
            {
                sb.AppendLine();
                sb.AppendLine("Children:");
                foreach (var child in node.Children)
                {
                    sb.AppendLine($"  - {child.Name} ({child.FigmaType})");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Return ONLY one of these types (no other text):");
            sb.AppendLine("Button, Label, InputField, Toggle, Slider, Dropdown, Image, Icon, ScrollView, List, Card, Navigation, Header, Footer, Modal, Tooltip, ProgressBar, TabControl, Tab, Badge, Avatar, Divider, Spacer, Container");

            return sb.ToString();
        }

        private async Task<string> CallOpenAIAsync(string prompt, string screenshotBase64, CancellationToken cancellationToken)
        {
            var url = "https://api.openai.com/v1/chat/completions";

            var requestBody = new OpenAIRequest
            {
                model = "gpt-4o-mini",
                max_tokens = 50,
                messages = new[]
                {
                    new OpenAIMessage
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            // If we have a screenshot, we could use vision model
            // For now, just use text-based analysis

            var json = JsonUtility.ToJson(requestBody);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {_settings.AIApiKey}");

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"OpenAI API error: {request.error}");
                }

                var response = JsonUtility.FromJson<OpenAIResponse>(request.downloadHandler.text);
                return response.choices?[0]?.message?.content ?? "";
            }
        }

        private async Task<string> CallAnthropicAsync(string prompt, string screenshotBase64, CancellationToken cancellationToken)
        {
            var url = "https://api.anthropic.com/v1/messages";

            var requestBody = new AnthropicRequest
            {
                model = "claude-3-haiku-20240307",
                max_tokens = 50,
                messages = new[]
                {
                    new AnthropicMessage
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var json = JsonUtility.ToJson(requestBody);

            using (var request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-api-key", _settings.AIApiKey);
                request.SetRequestHeader("anthropic-version", "2023-06-01");

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception($"Anthropic API error: {request.error}");
                }

                var response = JsonUtility.FromJson<AnthropicResponse>(request.downloadHandler.text);
                return response.content?[0]?.text ?? "";
            }
        }

        private TypeDetectionResult ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return new TypeDetectionResult(SemanticType.Container, DetectionConfidence.VeryLow, "Empty AI response");
            }

            var cleanResponse = response.Trim().ToLower();

            // Try to parse the semantic type
            if (TryParseSemanticType(cleanResponse, out var type))
            {
                return new TypeDetectionResult(type, DetectionConfidence.Medium, "AI classification");
            }

            return new TypeDetectionResult(SemanticType.Container, DetectionConfidence.Low, $"Could not parse AI response: {response}");
        }

        private bool TryParseSemanticType(string value, out SemanticType type)
        {
            type = SemanticType.Container;

            var mapping = new (string key, SemanticType value)[]
            {
                ("button", SemanticType.Button),
                ("label", SemanticType.Label),
                ("inputfield", SemanticType.InputField),
                ("input", SemanticType.InputField),
                ("toggle", SemanticType.Toggle),
                ("slider", SemanticType.Slider),
                ("dropdown", SemanticType.Dropdown),
                ("image", SemanticType.Image),
                ("icon", SemanticType.Icon),
                ("scrollview", SemanticType.ScrollView),
                ("scroll", SemanticType.ScrollView),
                ("list", SemanticType.List),
                ("card", SemanticType.Card),
                ("navigation", SemanticType.Navigation),
                ("nav", SemanticType.Navigation),
                ("header", SemanticType.Header),
                ("footer", SemanticType.Footer),
                ("modal", SemanticType.Modal),
                ("tooltip", SemanticType.Tooltip),
                ("progressbar", SemanticType.ProgressBar),
                ("progress", SemanticType.ProgressBar),
                ("tabcontrol", SemanticType.TabControl),
                ("tab", SemanticType.Tab),
                ("badge", SemanticType.Badge),
                ("avatar", SemanticType.Avatar),
                ("divider", SemanticType.Divider),
                ("spacer", SemanticType.Spacer),
                ("container", SemanticType.Container)
            };

            foreach (var (key, semanticType) in mapping)
            {
                if (value.Contains(key))
                {
                    type = semanticType;
                    return true;
                }
            }

            return false;
        }

        #region API Request/Response Models

        [Serializable]
        private class OpenAIRequest
        {
            public string model;
            public int max_tokens;
            public OpenAIMessage[] messages;
        }

        [Serializable]
        private class OpenAIMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class OpenAIResponse
        {
            public OpenAIChoice[] choices;
        }

        [Serializable]
        private class OpenAIChoice
        {
            public OpenAIMessage message;
        }

        [Serializable]
        private class AnthropicRequest
        {
            public string model;
            public int max_tokens;
            public AnthropicMessage[] messages;
        }

        [Serializable]
        private class AnthropicMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class AnthropicResponse
        {
            public AnthropicContent[] content;
        }

        [Serializable]
        private class AnthropicContent
        {
            public string type;
            public string text;
        }

        #endregion
    }
}
