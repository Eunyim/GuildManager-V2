using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [메뉴] Tools > 데모 엔딩 연결
/// TitleScene·LobbyScene의 GameManager에 엔딩 화면용 드래곤 그림(Art/Monsters/Sets/dragon.asset)을 연결하고 저장합니다.
/// 다시 실행해도 안전합니다.
/// </summary>
public static class DemoEndingSetup
{
    static readonly string[] Scenes = { "Assets/Scenes/TitleScene.unity", "Assets/Scenes/LobbyScene.unity" };
    const string DragonSetPath = "Assets/Art/Monsters/Sets/dragon.asset";

    [MenuItem("Tools/데모 엔딩 연결")]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogError("[DemoEndingSetup] 플레이를 끄고 실행하세요."); return; }
        MonsterSpriteSet dragon = AssetDatabase.LoadAssetAtPath<MonsterSpriteSet>(DragonSetPath);
        if (dragon == null) { Debug.LogError($"[DemoEndingSetup] {DragonSetPath}가 없습니다."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string back = EditorSceneManager.GetActiveScene().path;
        foreach (string path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            GameManager gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm == null) { Debug.LogWarning($"[DemoEndingSetup] {path}에 GameManager가 없습니다."); continue; }
            Undo.RecordObject(gm, "데모 엔딩 연결");
            gm.endingDragonSet = dragon;
            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[DemoEndingSetup] {path} 연결·저장");
        }
        if (!string.IsNullOrEmpty(back)) EditorSceneManager.OpenScene(back);
        Debug.Log("=== 데모 엔딩 연결 완료 ===");
    }
}
