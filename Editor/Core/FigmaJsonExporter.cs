using System;
using System.IO;
using System.Threading.Tasks;
using FigmaSync.Editor.Models;
using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.Core
{
    /// <summary>
    /// Handles exporting and importing Figma JSON data for debugging and offline use.
    /// </summary>
    public static class FigmaJsonExporter
    {
        /// <summary>
        /// Exports raw Figma API JSON to a file.
        /// </summary>
        public static void ExportRawJson(string json, string folderPath, string fileName)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("JSON content is required", nameof(json));

            EnsureDirectoryExists(folderPath);

            var filePath = Path.Combine(folderPath, $"{fileName}.json");
            File.WriteAllText(filePath, json);

            Debug.Log($"[FigmaSync] Exported raw JSON to: {filePath}");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Exports a FigmaFileResponse to a formatted JSON file.
        /// </summary>
        public static void ExportFileResponse(FigmaFileResponse response, string folderPath, string fileName)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            EnsureDirectoryExists(folderPath);

            var json = JsonUtility.ToJson(response, true);
            var filePath = Path.Combine(folderPath, $"{fileName}_parsed.json");
            File.WriteAllText(filePath, json);

            Debug.Log($"[FigmaSync] Exported parsed document to: {filePath}");
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Imports a FigmaFileResponse from a JSON file.
        /// </summary>
        public static FigmaFileResponse ImportFileResponse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON file not found: {filePath}");

            var json = File.ReadAllText(filePath);
            return JsonUtility.FromJson<FigmaFileResponse>(json);
        }

        /// <summary>
        /// Imports raw JSON string from a file.
        /// </summary>
        public static string ImportRawJson(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON file not found: {filePath}");

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Creates a timestamped backup of the current import.
        /// </summary>
        public static string CreateBackup(string json, string folderPath, string fileKey)
        {
            EnsureDirectoryExists(folderPath);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var fileName = $"{fileKey}_{timestamp}.json";
            var filePath = Path.Combine(folderPath, fileName);

            File.WriteAllText(filePath, json);
            Debug.Log($"[FigmaSync] Created backup: {filePath}");

            AssetDatabase.Refresh();
            return filePath;
        }

        /// <summary>
        /// Lists all backup files for a given file key.
        /// </summary>
        public static string[] GetBackups(string folderPath, string fileKey)
        {
            if (!Directory.Exists(folderPath))
                return Array.Empty<string>();

            return Directory.GetFiles(folderPath, $"{fileKey}_*.json");
        }

        /// <summary>
        /// Exports a node tree summary for debugging layout issues.
        /// </summary>
        public static void ExportNodeTreeSummary(FigmaNode node, string folderPath, string fileName, int maxDepth = 10)
        {
            EnsureDirectoryExists(folderPath);

            using (var writer = new StringWriter())
            {
                WriteNodeSummary(writer, node, 0, maxDepth);

                var filePath = Path.Combine(folderPath, $"{fileName}_tree.txt");
                File.WriteAllText(filePath, writer.ToString());

                Debug.Log($"[FigmaSync] Exported node tree to: {filePath}");
            }

            AssetDatabase.Refresh();
        }

        private static void WriteNodeSummary(StringWriter writer, FigmaNode node, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;

            var indent = new string(' ', depth * 2);
            var sizing = $"[H:{node.layoutSizingHorizontal ?? "?"}, V:{node.layoutSizingVertical ?? "?"}]";
            var layout = string.IsNullOrEmpty(node.layoutMode) ? "" : $" Layout:{node.layoutMode}";

            writer.WriteLine($"{indent}{node.type}: \"{node.name}\" {sizing}{layout}");

            if (node.absoluteBoundingBox != null)
            {
                var box = node.absoluteBoundingBox;
                writer.WriteLine($"{indent}  Size: {box.width}x{box.height}");
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    WriteNodeSummary(writer, child, depth + 1, maxDepth);
                }
            }
        }

        /// <summary>
        /// Exports layout properties for debugging.
        /// </summary>
        public static void ExportLayoutDebug(FigmaNode node, string folderPath, string fileName)
        {
            EnsureDirectoryExists(folderPath);

            using (var writer = new StringWriter())
            {
                WriteLayoutDebug(writer, node, 0);

                var filePath = Path.Combine(folderPath, $"{fileName}_layout.txt");
                File.WriteAllText(filePath, writer.ToString());

                Debug.Log($"[FigmaSync] Exported layout debug to: {filePath}");
            }

            AssetDatabase.Refresh();
        }

        private static void WriteLayoutDebug(StringWriter writer, FigmaNode node, int depth)
        {
            var indent = new string(' ', depth * 2);

            writer.WriteLine($"{indent}=== {node.name} ({node.type}) ===");
            writer.WriteLine($"{indent}  layoutMode: {node.layoutMode}");
            writer.WriteLine($"{indent}  layoutSizingHorizontal: {node.layoutSizingHorizontal}");
            writer.WriteLine($"{indent}  layoutSizingVertical: {node.layoutSizingVertical}");
            writer.WriteLine($"{indent}  layoutAlign: {node.layoutAlign}");
            writer.WriteLine($"{indent}  layoutGrow: {node.layoutGrow}");
            writer.WriteLine($"{indent}  primaryAxisSizingMode: {node.primaryAxisSizingMode}");
            writer.WriteLine($"{indent}  counterAxisSizingMode: {node.counterAxisSizingMode}");
            writer.WriteLine($"{indent}  primaryAxisAlignItems: {node.primaryAxisAlignItems}");
            writer.WriteLine($"{indent}  counterAxisAlignItems: {node.counterAxisAlignItems}");
            writer.WriteLine($"{indent}  itemSpacing: {node.itemSpacing}");
            writer.WriteLine($"{indent}  padding: L{node.paddingLeft} R{node.paddingRight} T{node.paddingTop} B{node.paddingBottom}");

            if (node.absoluteBoundingBox != null)
            {
                var box = node.absoluteBoundingBox;
                writer.WriteLine($"{indent}  bounds: {box.x},{box.y} {box.width}x{box.height}");
            }

            writer.WriteLine();

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    WriteLayoutDebug(writer, child, depth + 1);
                }
            }
        }

        private static void EnsureDirectoryExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }
    }
}
