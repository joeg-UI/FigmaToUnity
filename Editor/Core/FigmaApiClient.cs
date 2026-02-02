using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FigmaSync.Editor.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace FigmaSync.Editor.Core
{
    /// <summary>
    /// HTTP client for communicating with the Figma API.
    /// </summary>
    public class FigmaApiClient
    {
        private const string BaseUrl = "https://api.figma.com/v1";
        private readonly string _accessToken;

        public FigmaApiClient(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
                throw new ArgumentException("Access token is required", nameof(accessToken));

            _accessToken = accessToken;
        }

        /// <summary>
        /// Fetches the complete Figma document.
        /// </summary>
        public async Task<FigmaFileResponse> GetFileAsync(string fileKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("File key is required", nameof(fileKey));

            var url = $"{BaseUrl}/files/{fileKey}?geometry=paths";
            var json = await SendRequestAsync(url, cancellationToken);

            return JsonUtility.FromJson<FigmaFileResponse>(json);
        }

        /// <summary>
        /// Fetches specific nodes from a Figma document.
        /// </summary>
        public async Task<FigmaFileResponse> GetFileNodesAsync(string fileKey, IEnumerable<string> nodeIds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("File key is required", nameof(fileKey));

            var ids = string.Join(",", nodeIds);
            var url = $"{BaseUrl}/files/{fileKey}/nodes?ids={Uri.EscapeDataString(ids)}&geometry=paths";
            var json = await SendRequestAsync(url, cancellationToken);

            return JsonUtility.FromJson<FigmaFileResponse>(json);
        }

        /// <summary>
        /// Fetches image URLs for specified nodes.
        /// </summary>
        public async Task<Dictionary<string, string>> GetImageUrlsAsync(
            string fileKey,
            IEnumerable<string> nodeIds,
            string format = "png",
            int scale = 2,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("File key is required", nameof(fileKey));

            var ids = string.Join(",", nodeIds);
            var url = $"{BaseUrl}/images/{fileKey}?ids={Uri.EscapeDataString(ids)}&format={format}&scale={scale}";
            var json = await SendRequestAsync(url, cancellationToken);

            var response = JsonUtility.FromJson<FigmaImagesResponse>(json);

            if (!string.IsNullOrEmpty(response.err))
            {
                throw new FigmaApiException($"Figma API error: {response.err}");
            }

            return response.images ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Downloads an image from a URL.
        /// </summary>
        public async Task<byte[]> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(imageUrl))
                throw new ArgumentException("Image URL is required", nameof(imageUrl));

            using (var request = UnityWebRequest.Get(imageUrl))
            {
                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new FigmaApiException($"Failed to download image: {request.error}");
                }

                return request.downloadHandler.data;
            }
        }

        /// <summary>
        /// Fetches image fill URLs for a file.
        /// </summary>
        public async Task<Dictionary<string, string>> GetImageFillsAsync(string fileKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("File key is required", nameof(fileKey));

            var url = $"{BaseUrl}/files/{fileKey}/images";
            var json = await SendRequestAsync(url, cancellationToken);

            var response = JsonUtility.FromJson<ImageFillsResponse>(json);

            if (!string.IsNullOrEmpty(response.error))
            {
                throw new FigmaApiException($"Figma API error: {response.error}");
            }

            return response.meta?.images ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets the file's component list.
        /// </summary>
        public async Task<FigmaComponentsResponse> GetComponentsAsync(string fileKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("File key is required", nameof(fileKey));

            var url = $"{BaseUrl}/files/{fileKey}/components";
            var json = await SendRequestAsync(url, cancellationToken);

            return JsonUtility.FromJson<FigmaComponentsResponse>(json);
        }

        /// <summary>
        /// Gets the raw JSON response for a file (useful for debugging).
        /// </summary>
        public async Task<string> GetFileRawJsonAsync(string fileKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fileKey))
                throw new ArgumentException("File key is required", nameof(fileKey));

            var url = $"{BaseUrl}/files/{fileKey}?geometry=paths";
            return await SendRequestAsync(url, cancellationToken);
        }

        private async Task<string> SendRequestAsync(string url, CancellationToken cancellationToken)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("X-Figma-Token", _accessToken);

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(50, cancellationToken);
                }

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new FigmaApiException($"Connection error: {request.error}");
                }

                if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    var statusCode = request.responseCode;
                    var errorBody = request.downloadHandler?.text ?? "No response body";

                    switch (statusCode)
                    {
                        case 401:
                            throw new FigmaApiException("Invalid or expired access token");
                        case 403:
                            throw new FigmaApiException("Access denied. Check file permissions and token scope.");
                        case 404:
                            throw new FigmaApiException("File not found. Check the file URL.");
                        case 429:
                            throw new FigmaApiException("Rate limited. Please wait before retrying.");
                        default:
                            throw new FigmaApiException($"API error ({statusCode}): {errorBody}");
                    }
                }

                return request.downloadHandler.text;
            }
        }

        [Serializable]
        private class ImageFillsResponse
        {
            public string error;
            public ImageFillsMeta meta;
        }

        [Serializable]
        private class ImageFillsMeta
        {
            public Dictionary<string, string> images;
        }
    }

    /// <summary>
    /// Response from components endpoint.
    /// </summary>
    [Serializable]
    public class FigmaComponentsResponse
    {
        public string error;
        public ComponentsMeta meta;
    }

    /// <summary>
    /// Components metadata.
    /// </summary>
    [Serializable]
    public class ComponentsMeta
    {
        public List<FigmaComponentInfo> components;
    }

    /// <summary>
    /// Individual component info.
    /// </summary>
    [Serializable]
    public class FigmaComponentInfo
    {
        public string key;
        public string file_key;
        public string node_id;
        public string name;
        public string description;
        public string containing_frame_name;
        public string containing_page_name;
    }

    /// <summary>
    /// Exception thrown for Figma API errors.
    /// </summary>
    public class FigmaApiException : Exception
    {
        public FigmaApiException(string message) : base(message) { }
        public FigmaApiException(string message, Exception inner) : base(message, inner) { }
    }
}
