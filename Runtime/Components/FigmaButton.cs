using UnityEngine;

namespace FigmaSync.Runtime.Components
{
    /// <summary>
    /// Marker component for Figma-imported button elements.
    /// </summary>
    [AddComponentMenu("FigmaSync/Figma Button")]
    public class FigmaButton : MonoBehaviour
    {
        [Tooltip("The Figma node ID this button was created from")]
        public string NodeId;

        [Tooltip("The prototype destination node ID (if any)")]
        public string PrototypeDestination;

        [Tooltip("Custom data for this button")]
        public string CustomData;
    }
}
