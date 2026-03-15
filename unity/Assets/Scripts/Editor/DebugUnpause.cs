using Match3.Unity.Controllers;
using UnityEditor;
using UnityEngine;

namespace Match3.Unity.Editor
{
    /// <summary>
    /// Auto-unpause: prevents Unity "Error Pause" from freezing Play mode.
    /// MCP WebSocket errors at startup can trigger Error Pause, halting the game loop.
    /// This script listens for pause state changes and automatically unpauses.
    /// </summary>
    [InitializeOnLoad]
    public static class DebugTools
    {
        private static bool _autoUnpause;

        static DebugTools()
        {
            EditorApplication.update += AutoUnpauseUpdate;
        }

        private static void AutoUnpauseUpdate()
        {
            if (!_autoUnpause) return;
            if (EditorApplication.isPlaying && EditorApplication.isPaused)
            {
                EditorApplication.isPaused = false;
            }
        }

        [MenuItem("Match3/Debug/Force Unpause")]
        public static void ForceUnpause()
        {
            // Enable auto-unpause to prevent future Error Pause from freezing
            _autoUnpause = true;

            // Disable "Error Pause" via ConsoleWindow reflection
            TryDisableErrorPause();

            // Clear console to prevent re-triggering Error Pause
            ClearConsole();

            Debug.Log($"[DebugUnpause] isPaused={EditorApplication.isPaused} isPlaying={EditorApplication.isPlaying}");
            EditorApplication.isPaused = false;
            Debug.Log($"[DebugUnpause] After: isPaused={EditorApplication.isPaused} autoUnpause=ON");
        }

        [MenuItem("Match3/Debug/Check Pause State")]
        public static void CheckPauseState()
        {
            Debug.Log($"[DebugPause] isPaused={EditorApplication.isPaused} isPlaying={EditorApplication.isPlaying} isCompiling={EditorApplication.isCompiling} autoUnpause={_autoUnpause}");
        }

        [MenuItem("Match3/Debug/Toggle AutoPlay")]
        public static void ToggleAutoPlay()
        {
            if (!EditorApplication.isPlaying) return;
            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null || controller.Bridge == null) return;
            var bridge = controller.Bridge;
            bridge.IsAutoPlaying = !bridge.IsAutoPlaying;
            bridge.GameSpeed = 2f;
            Debug.Log($"[Debug] AutoPlay={bridge.IsAutoPlaying} Speed={bridge.GameSpeed}");
        }

        [MenuItem("Match3/Debug/Restart Game")]
        public static void RestartGame()
        {
            if (!EditorApplication.isPlaying) return;
            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null) return;
            controller.RestartGame();
            controller.Bridge.IsAutoPlaying = true;
            controller.Bridge.GameSpeed = 2f;
            Debug.Log($"[Debug] Restarted, AutoPlay=True Speed=2");
        }

        private static void TryDisableErrorPause()
        {
            try
            {
                var consoleType = System.Type.GetType("UnityEditor.ConsoleWindow,UnityEditor");
                if (consoleType == null) return;

                var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;

                var method = consoleType.GetMethod("SetConsoleErrorPause", flags);
                if (method != null)
                {
                    method.Invoke(null, new object[] { false });
                    return;
                }

                var field = consoleType.GetField("s_ConsoleErrorPause", flags);
                if (field != null)
                {
                    field.SetValue(null, false);
                }
            }
            catch { }
        }

        private static void ClearConsole()
        {
            try
            {
                var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntries == null) return;
                var clear = logEntries.GetMethod("Clear",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                clear?.Invoke(null, null);
            }
            catch { }
        }
    }
}
