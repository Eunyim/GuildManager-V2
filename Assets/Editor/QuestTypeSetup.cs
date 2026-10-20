using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// [메뉴] Tools > 의뢰 종류 세팅
/// 던전 씬 BattleManager에 구출 의뢰의 갇힌 인물 그림(0x72 드워프 여성, 임시)을 연결합니다. 다시 실행해도 안전합니다.
/// </summary>
public class QuestTypeSetup : Editor
{
    const string DUNGEON_SCENE  = "Assets/Scenes/DungeonScene.unity";
    const string CAPTIVE_SPRITE = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames/dwarf_f_idle_anim_f0.png";

    [MenuItem("Tools/의뢰 종류 세팅")]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogError("[QuestTypeSetup] 플레이 모드에서는 실행할 수 없습니다."); return; }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CAPTIVE_SPRITE);
        if (sprite == null) { Debug.LogError($"[QuestTypeSetup] {CAPTIVE_SPRITE} 없음"); return; }

        Scene scene = SceneManager.GetSceneByPath(DUNGEON_SCENE);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(DUNGEON_SCENE, OpenSceneMode.Additive);

        int connected = 0;
        foreach (GameObject go in scene.GetRootGameObjects())
            foreach (BattleManager bm in go.GetComponentsInChildren<BattleManager>(true))
            {
                bm.captiveSprite = sprite;
                EditorUtility.SetDirty(bm);
                connected++;
            }

        if (connected == 0) Debug.LogWarning("[QuestTypeSetup] 던전 씬에서 BattleManager를 찾지 못했습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"=== 의뢰 종류 세팅 완료! (BattleManager {connected}개에 갇힌 인물 그림 연결) ===");
    }
}
