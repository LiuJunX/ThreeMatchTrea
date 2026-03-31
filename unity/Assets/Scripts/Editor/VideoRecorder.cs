using System.IO;
using UnityEditor;
using UnityEngine;

namespace Match3.Unity.Editor
{
    /// <summary>
    /// Records Game view as PNG sequence, then converts to MP4 via ffmpeg.
    /// Usage: Match3 > Record > Start (records frames), Match3 > Record > Stop (saves video).
    /// Captures at screen resolution. Output: Screenshots/recording.mp4
    /// </summary>
    public static class VideoRecorder
    {
        private const string FrameDir = "Screenshots/frames";
        private const string OutputVideo = "Screenshots/recording.mp4";
        private static bool _isRecording;
        private static int _frameCount;

        [MenuItem("Match3/Record/Start Recording")]
        public static void StartRecording()
        {
            if (_isRecording)
            {
                Debug.LogWarning("[VideoRecorder] Already recording!");
                return;
            }

            // Clean previous frames
            if (Directory.Exists(FrameDir))
                Directory.Delete(FrameDir, true);
            Directory.CreateDirectory(FrameDir);

            _frameCount = 0;
            _isRecording = true;

            EditorApplication.update += CaptureFrame;
            Debug.Log("[VideoRecorder] Recording started. Play the game, then use Match3 > Record > Stop.");
        }

        [MenuItem("Match3/Record/Stop Recording")]
        public static void StopRecording()
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[VideoRecorder] Not recording!");
                return;
            }

            EditorApplication.update -= CaptureFrame;
            _isRecording = false;

            Debug.Log($"[VideoRecorder] Recording stopped. {_frameCount} frames captured.");
            Debug.Log("[VideoRecorder] Converting to MP4...");

            ConvertToVideo();
        }

        private static void CaptureFrame()
        {
            if (!Application.isPlaying || !_isRecording) return;

            var path = Path.Combine(FrameDir, $"{_frameCount:D5}.png");
            ScreenCapture.CaptureScreenshot(path);
            _frameCount++;
        }

        private static void ConvertToVideo()
        {
            if (_frameCount == 0)
            {
                Debug.LogWarning("[VideoRecorder] No frames captured!");
                return;
            }

            // Delete previous video
            if (File.Exists(OutputVideo))
                File.Delete(OutputVideo);

            var framesPath = Path.GetFullPath(FrameDir).Replace('\\', '/');
            var outputPath = Path.GetFullPath(OutputVideo).Replace('\\', '/');

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -framerate 60 -i \"{framesPath}/%05d.png\" -c:v libx264 -pix_fmt yuv420p -crf 23 -vf scale=480:-2 \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                var process = System.Diagnostics.Process.Start(startInfo);
                process.WaitForExit(30000); // 30 sec timeout

                if (process.ExitCode == 0)
                {
                    var fileInfo = new FileInfo(outputPath);
                    Debug.Log($"[VideoRecorder] Video saved: {outputPath} ({fileInfo.Length / 1024}KB, {_frameCount} frames)");

                    // Cleanup frames
                    Directory.Delete(framesPath, true);
                }
                else
                {
                    var error = process.StandardError.ReadToEnd();
                    Debug.LogError($"[VideoRecorder] ffmpeg failed: {error}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VideoRecorder] ffmpeg not found or failed: {e.Message}");
                Debug.Log($"[VideoRecorder] Frames saved at: {framesPath} ({_frameCount} files)");
            }
        }
    }
}
