using UnityEngine;

namespace FigmaSync.Runtime.Components
{
    /// <summary>
    /// Marker component for Figma-imported toggle/checkbox elements.
    /// </summary>
    [AddComponentMenu("FigmaSync/Figma Toggle")]
    public class FigmaToggle : MonoBehaviour
    {
        [Tooltip("The Figma node ID this toggle was created from")]
        public string NodeId;

        [Tooltip("Custom data for this toggle")]
        public string CustomData;
    }
}
