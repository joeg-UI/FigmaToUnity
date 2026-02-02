using UnityEngine;

namespace FigmaSync.Runtime.Components
{
    /// <summary>
    /// Marker component for Figma-imported image elements.
    /// </summary>
    [AddComponentMenu("FigmaSync/Figma Image")]
    public class FigmaImage : MonoBehaviour
    {
        [Tooltip("The Figma node ID this image was created from")]
        public string NodeId;

        [Tooltip("The Figma image hash for this element")]
        public string ImageHash;

        [Tooltip("URL to dynamically load this image from")]
        public string RemoteUrl;
    }
}
