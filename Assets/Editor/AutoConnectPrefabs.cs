using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// GuildManager/Auto Connect Prefabs 메뉴를 실행하면
/// 프로젝트의 모든 게임 씬을 순회하며
/// QuestGenerator 등급별 적 풀과 GameManager unitPrefabs를 자동으로 연결합니다.
/// </summary>
public static class AutoConnectPrefabs
{
    // 탐색할 씬 경로 (TextMesh Pro 샘플 씬 제외)
    static readonly string[] TargetScenes =
    {
        "Assets/Scenes/LobbyScene.unity",
        "Assets/Scenes/DungeonScene.unity",
        "Assets/Scenes/TitleScene.unity",
        "Assets/Scenes/GuildCreationScene.unity",
    };

    [MenuItem("GuildManager/Auto Connect Prefabs")]
    static void AutoConnect()
    {
        // 현재 열린 씬에 저장되지 않은 변경사항이 있으면 저장할지 물어봄
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[Auto Connect] 취소됨.");
            return;
        }

        // ── 프리팹 로드 ──────────────────────────────────────────────
        GameObject slime    = Load("Assets/Prefabs/Characters/Enemy_Slime.prefab");
        GameObject goblin   = Load("Assets/Prefabs/Characters/Enemy_Goblin.prefab");
        GameObject skeleton = Load("Assets/Prefabs/Characters/Enemy_Skeleton.prefab");
        GameObject orc      = Load("Assets/Prefabs/Characters/Enemy_Orc.prefab");
        GameObject bandit   = Load("Assets/Prefabs/Characters/Enemy_Bandit.prefab");
        GameObject dragon   = Load("Assets/Prefabs/Characters/Enemy_Dragon.prefab");

        GameObject warrior = Load("Assets/Prefabs/Characters/Prefab_Warrior.prefab");
        GameObject archer  = Load("Assets/Prefabs/Characters/Prefab_Archer.prefab");
        GameObject mage    = Load("Assets/Prefabs/Characters/Prefab_Mage.prefab");
        GameObject rogue   = Load("Assets/Prefabs/Characters/Prefab_Rogue.prefab");
        GameObject healer  = Load("Assets/Prefabs/Characters/Prefab_Healer.prefab");

        int totalGen = 0;
        int totalMgr = 0;

        // ── 씬 순회 ────────────────────────────────────────────────
        foreach (string scenePath in TargetScenes)
        {
            if (!System.IO.File.Exists(scenePath))
            {
                Debug.Log($"[Auto Connect] 씬 없음, 건너뜀: {scenePath}");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            bool sceneModified = false;

            // QuestGenerator 연결
            foreach (QuestGenerator gen in Object.FindObjectsOfType<QuestGenerator>())
            {
                Undo.RecordObject(gen, "Auto Connect QuestGenerator Prefabs");
                gen.tierD_Prefabs = FilterNull(new List<GameObject> { slime, goblin });
                gen.tierC_Prefabs = FilterNull(new List<GameObject> { goblin, skeleton });
                gen.tierB_Prefabs = FilterNull(new List<GameObject> { skeleton, orc, bandit });
                gen.tierA_Prefabs = FilterNull(new List<GameObject> { orc });
                gen.tierS_Prefabs = FilterNull(new List<GameObject> { dragon });
                EditorUtility.SetDirty(gen);
                sceneModified = true;
                totalGen++;
                Debug.Log($"✅ [Auto Connect] QuestGenerator 연결 완료 → {scenePath}");
            }

            // GameManager 연결
            foreach (GameManager gm in Object.FindObjectsOfType<GameManager>())
            {
                Undo.RecordObject(gm, "Auto Connect GameManager UnitPrefabs");
                gm.unitPrefabs = new GameObject[] { warrior, archer, mage, rogue, healer };
                EditorUtility.SetDirty(gm);
                sceneModified = true;
                totalMgr++;
                Debug.Log($"✅ [Auto Connect] GameManager 연결 완료 → {scenePath}");
            }

            if (sceneModified)
                EditorSceneManager.SaveScene(scene);
        }

        string result = $"QuestGenerator {totalGen}개, GameManager {totalMgr}개 연결 완료!";
        Debug.Log($"✅ [Auto Connect] {result}");
        EditorUtility.DisplayDialog("프리팹 자동 연결 완료", result, "확인");
    }

    static GameObject Load(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            Debug.LogWarning($"⚠️ [Auto Connect] 프리팹 없음: {path}");
        return prefab;
    }

    static List<GameObject> FilterNull(List<GameObject> list)
    {
        list.RemoveAll(g => g == null);
        return list;
    }
}
