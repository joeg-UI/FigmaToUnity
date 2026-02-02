using UnityEngine;

namespace FigmaSync.Runtime.Components
{
    /// <summary>
    /// Marker component for Figma-imported label/text elements.
    /// </summary>
    [AddComponentMenu("FigmaSync/Figma Label")]
    public class FigmaLabel : MonoBehaviour
    {
        [Tooltip("The Figma node ID this label was created from")]
        public string NodeId;

        [Tooltip("Localization key for this text")]
        public string LocalizationKey;
    }
}
