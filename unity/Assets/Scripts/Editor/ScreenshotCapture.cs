using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.Unity.Editor
{
    /// <summary>
    /// Captures the Game view screenshot for AI-assisted debugging.
    /// Menu: Match3 > Capture Screenshot
    /// </summary>
    public static class ScreenshotCapture
    {
        private const string OutputPath = "Screenshots/capture.png";

        [MenuItem("Match3/Capture Screenshot")]
        public static void Capture()
        {
            var dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var fullPath = Path.GetFullPath(OutputPath);

            // Delete previous file so we can detect when the new one is written
            if (File.Exists(fullPath))
                File.Delete(fullPath);

            // CaptureScreenshot captures everything including UI Overlay canvases
            // It writes asynchronously on the next rendered frame
            ScreenCapture.CaptureScreenshot(fullPath);

            Debug.Log($"[Screenshot] Capturing to {fullPath} (async, next frame)");
        }
    }
}
