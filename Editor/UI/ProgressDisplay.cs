using UnityEditor;
using UnityEngine;

namespace FigmaSync.Editor.UI
{
    /// <summary>
    /// Displays import progress in the editor window.
    /// </summary>
    public class ProgressDisplay
    {
        private string _currentStatus = "";
        private float _progress = 0f;
        private bool _isRunning = false;
        private string _errorMessage = null;
        private bool _showError = false;

        public bool IsRunning => _isRunning;
        public float Progress => _progress;
        public string Status => _currentStatus;

        /// <summary>
        /// Starts the progress display.
        /// </summary>
        public void Start()
        {
            _isRunning = true;
            _progress = 0f;
            _currentStatus = "Starting...";
            _errorMessage = null;
            _showError = false;
        }

        /// <summary>
        /// Updates the progress.
        /// </summary>
        public void Update(string status, float progress)
        {
            _currentStatus = status;
            _progress = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Shows an error.
        /// </summary>
        public void ShowError(string message)
        {
            _errorMessage = message;
            _showError = true;
        }

        /// <summary>
        /// Stops the progress display.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
        }

        /// <summary>
        /// Completes the progress display.
        /// </summary>
        public void Complete()
        {
            _isRunning = false;
            _progress = 1f;
            _currentStatus = "Complete!";
        }

        /// <summary>
        /// Draws the progress UI.
        /// </summary>
        public void Draw()
        {
            // Show error if present
            if (_showError && !string.IsNullOrEmpty(_errorMessage))
            {
                EditorGUILayout.HelpBox(_errorMessage, MessageType.Error);

                if (GUILayout.Button("Dismiss"))
                {
                    _showError = false;
                    _errorMessage = null;
                }

                EditorGUILayout.Space(10);
            }

            // Status text
            if (!string.IsNullOrEmpty(_currentStatus))
            {
                EditorGUILayout.LabelField(_currentStatus, EditorStyles.miniLabel);
            }

            // Progress bar
            var rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, _progress, $"{(_progress * 100):F0}%");

            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// Draws a minimal progress bar without status.
        /// </summary>
        public void DrawMinimal()
        {
            var rect = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(rect, _progress, _currentStatus);
        }

        /// <summary>
        /// Clears any error messages.
        /// </summary>
        public void ClearError()
        {
            _errorMessage = null;
            _showError = false;
        }
    }
}
