using System.IO;
using System.Linq;
using Match3.Core.Replay;
using Match3.Unity.Bridge;
using Match3.Unity.Controllers;
using UnityEditor;
using UnityEngine;

namespace Match3.Unity.Editor
{
    /// <summary>
    /// Debug menu items for recording and replaying game sessions.
    /// </summary>
    public static class ReplayTools
    {
        [MenuItem("Match3/Replay/Save Recording Now")]
        public static void SaveRecording()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Replay", "Must be in Play Mode.", "OK");
                return;
            }

            var bridge = Object.FindFirstObjectByType<Match3Bridge>();
            if (bridge == null || !bridge.IsInitialized || bridge.IsReplaying)
            {
                EditorUtility.DisplayDialog("Replay", "No active game to save.", "OK");
                return;
            }

            var path = bridge.SaveRecording();
            if (path != null)
            {
                Debug.Log($"[Recording] Saved: {path}");
                EditorUtility.RevealInFinder(path);
            }
        }

        [MenuItem("Match3/Replay/Replay Latest")]
        public static void ReplayLatest()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Replay", "Must be in Play Mode.", "OK");
                return;
            }

            var recording = LoadLatestRecording();
            if (recording == null)
            {
                var dir = Path.Combine(Application.persistentDataPath, "Recordings");
                EditorUtility.DisplayDialog("Replay",
                    $"No recordings found.\n\nPlay a game to completion first, or use 'Save Recording Now'.\n\nPath: {dir}",
                    "OK");
                return;
            }

            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Replay", "No GameController found.", "OK");
                return;
            }

            controller.StartReplay(recording);

            if (recording.Bookmarks.Count > 0)
            {
                var marks = string.Join(", ", recording.Bookmarks.Select(t => $"tick {t}"));
                Debug.Log($"[Replay] Bookmarks: {marks}  (total {recording.DurationTicks} ticks)");
            }
        }

        [MenuItem("Match3/Replay/Load Recording...")]
        public static void LoadRecording()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Replay", "Must be in Play Mode.", "OK");
                return;
            }

            var dir = Path.Combine(Application.persistentDataPath, "Recordings");
            if (!Directory.Exists(dir) || Directory.GetFiles(dir, "*.json").Length == 0)
            {
                EditorUtility.DisplayDialog("Replay",
                    $"No recordings found.\n\nPlay a game to completion first, or use 'Save Recording Now'.\n\nPath: {dir}",
                    "OK");
                return;
            }

            var path = EditorUtility.OpenFilePanel("Load Recording", dir, "json");
            if (string.IsNullOrEmpty(path)) return;

            var json = File.ReadAllText(path);
            var recording = GameRecordingSerializer.FromJson(json);
            if (recording == null)
            {
                EditorUtility.DisplayDialog("Replay", $"Failed to deserialize:\n{path}", "OK");
                return;
            }

            var controller = Object.FindFirstObjectByType<GameController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Replay", "No GameController found.", "OK");
                return;
            }

            controller.StartReplay(recording);

            if (recording.Bookmarks.Count > 0)
            {
                var marks = string.Join(", ", recording.Bookmarks.Select(t => $"tick {t}"));
                Debug.Log($"[Replay] Bookmarks: {marks}");
            }

            Debug.Log($"[Replay] Loaded: {Path.GetFileName(path)} ({recording.TotalMoves} moves, {recording.Commands.Count} commands)");
        }

        [MenuItem("Match3/Replay/Add Bookmark")]
        public static void AddBookmark()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Replay", "Must be in Play Mode.", "OK");
                return;
            }

            var bridge = Object.FindFirstObjectByType<Match3Bridge>();
            if (bridge == null || !bridge.IsInitialized || bridge.IsReplaying)
            {
                EditorUtility.DisplayDialog("Replay", "No active game to bookmark.", "OK");
                return;
            }

            bridge.AddBookmark();
        }

        [MenuItem("Match3/Replay/Open Recordings Folder")]
        public static void OpenRecordingsFolder()
        {
            var dir = Path.Combine(Application.persistentDataPath, "Recordings");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private static GameRecording LoadLatestRecording()
        {
            return Match3Bridge.LoadLatestRecording();
        }
    }
}
