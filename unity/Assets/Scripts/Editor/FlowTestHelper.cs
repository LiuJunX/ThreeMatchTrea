using Match3.Unity.Bridge;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Match3.Unity.Editor
{
    /// <summary>
    /// Editor menu items for testing game flow via MCP.
    /// Only works in Play mode.
    /// </summary>
    public static class FlowTestHelper
    {
        [MenuItem("Match3/Test/Click Play")]
        public static void ClickPlay() => ClickButton("PlayButton");

        [MenuItem("Match3/Test/Click Level 1")]
        public static void ClickLevel1() => ClickButton("Level_level_001");

        [MenuItem("Match3/Test/Click Level 2")]
        public static void ClickLevel2() => ClickButton("Level_level_002");

        [MenuItem("Match3/Test/Click Level 3")]
        public static void ClickLevel3() => ClickButton("Level_level_003");

        [MenuItem("Match3/Test/Click Level 7")]
        public static void ClickLevel7() => ClickButton("Level_level_007");

        [MenuItem("Match3/Test/Click Level 9")]
        public static void ClickLevel9() => ClickButton("Level_level_009");

        [MenuItem("Match3/Test/Click Level 10")]
        public static void ClickLevel10() => ClickButton("Level_level_010");

        [MenuItem("Match3/Test/Click Level 15")]
        public static void ClickLevel15() => ClickButton("Level_level_015");

        [MenuItem("Match3/Test/Click Level 19")]
        public static void ClickLevel19() => ClickButton("Level_level_019");

        [MenuItem("Match3/Test/Click Level 20")]
        public static void ClickLevel20() => ClickButton("Level_level_020");

        [MenuItem("Match3/Test/Click Level 21")]
        public static void ClickLevel21() => ClickButton("Level_level_021");

        [MenuItem("Match3/Test/Click Restart")]
        public static void ClickRestart() => ClickButton("RestartButton");

        [MenuItem("Match3/Test/Click Next Level")]
        public static void ClickNextLevel() => ClickButton("NextLevelButton");

        [MenuItem("Match3/Test/Click Level Select")]
        public static void ClickLevelSelect() => ClickButton("LevelSelectButton");

        [MenuItem("Match3/Test/Click Back")]
        public static void ClickBack() => ClickButton("BackButton");

        [MenuItem("Match3/Test/Click Quit")]
        public static void ClickQuit() => ClickButton("QuitButton");

        [MenuItem("Match3/Test/Force Win")]
        public static void ForceWin()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[FlowTest] Not in Play mode"); return; }
            var bridge = Object.FindFirstObjectByType<Match3Bridge>();
            if (bridge == null) { Debug.LogWarning("[FlowTest] No Match3Bridge found"); return; }
            bridge.NotifyGameEnded(true, 1250);
            Debug.Log("[FlowTest] Forced Win (score=1250)");
        }

        [MenuItem("Match3/Test/Force Lose")]
        public static void ForceLose()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[FlowTest] Not in Play mode"); return; }
            var bridge = Object.FindFirstObjectByType<Match3Bridge>();
            if (bridge == null) { Debug.LogWarning("[FlowTest] No Match3Bridge found"); return; }
            bridge.NotifyGameEnded(false, 300);
            Debug.Log("[FlowTest] Forced Lose (score=300)");
        }

        private static void ClickButton(string name)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FlowTest] Not in Play mode");
                return;
            }

            // Search all buttons including inactive objects
            var buttons = Resources.FindObjectsOfTypeAll<Button>();
            foreach (var btn in buttons)
            {
                if (btn.gameObject.name == name && btn.gameObject.activeInHierarchy)
                {
                    btn.onClick.Invoke();
                    Debug.Log($"[FlowTest] Clicked '{name}'");
                    return;
                }
            }

            Debug.LogWarning($"[FlowTest] Button '{name}' not found or not active");
        }
    }
}
