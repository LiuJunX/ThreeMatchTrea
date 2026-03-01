using UnityEditor;

public static class ForceReimportBombs
{
    [MenuItem("Match3/Force Reimport Bombs")]
    public static void Reimport()
    {
        var paths = new[]
        {
            "Assets/Resources/Art/Gems/Models/Bomb_Color.fbx",
            "Assets/Resources/Art/Gems/Models/Bomb_Horizontal.fbx",
            "Assets/Resources/Art/Gems/Models/Bomb_Vertical.fbx",
            "Assets/Resources/Art/Gems/Models/Bomb_Ufo.fbx",
            "Assets/Resources/Art/Gems/Models/Bomb_Square5x5.fbx"
        };

        foreach (var path in paths)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            UnityEngine.Debug.Log($"[ForceReimport] Reimported {path}");
        }

        AssetDatabase.Refresh();
        UnityEngine.Debug.Log("[ForceReimport] Done!");
    }
}
