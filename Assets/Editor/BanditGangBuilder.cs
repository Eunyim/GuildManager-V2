using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// [메뉴] Tools > 도적단 프리팹 만들기
/// 도적단을 전사·암살자·궁수 세 클래스로 나눕니다.
///  - Enemy_Bandit(도적단 암살자)의 스탯을 프리팹 값으로 맞추고,
///  - Enemy_Bandit을 본떠 Enemy_BanditWarrior(도적단 전사), Enemy_BanditArcher(도적단 궁수)를 만들고,
///  - 로비 씬 QuestGenerator의 도적단 항목(대표 프리팹 + 구성원 목록)에 연결합니다.
/// 이미 있으면 스탯·연결만 다시 맞추므로 다시 실행해도 안전합니다.
/// </summary>
public class BanditGangBuilder : Editor
{
    const string FOLDER         = "Assets/Prefabs/Characters";
    const string BANDIT_PREFAB  = FOLDER + "/Enemy_Bandit.prefab";
    const string WARRIOR_PREFAB = FOLDER + "/Enemy_BanditWarrior.prefab";
    const string ARCHER_PREFAB  = FOLDER + "/Enemy_BanditArcher.prefab";
    const string ALLY_ARCHER    = FOLDER + "/Prefab_Archer.prefab";
    const string LOBBY_SCENE    = "Assets/Scenes/LobbyScene.unity";

    // 구성원 스탯 (한 명은 예전 도적단 한 명(HP 120, 공격 30)보다 약하게, 셋을 합쳐 예전 도적단 2명 정도)
    struct Stats { public float hp, atk, range, speed, cooldown; public Color color; }
    static readonly Stats rogueStats   = new Stats { hp = 60f,  atk = 18f, range = 0.8f, speed = 3.5f, cooldown = 0.9f, color = new Color(0.75f, 0.02f, 0.02f) };
    static readonly Stats warriorStats = new Stats { hp = 110f, atk = 14f, range = 1.0f, speed = 2.2f, cooldown = 1.3f, color = new Color(0.45f, 0.12f, 0.08f) };
    static readonly Stats archerStats  = new Stats { hp = 55f,  atk = 14f, range = 5.0f, speed = 2.5f, cooldown = 1.5f, color = new Color(0.9f, 0.5f, 0.1f) };

    [MenuItem("Tools/도적단 프리팹 만들기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[BanditGangBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BANDIT_PREFAB) == null) { Debug.LogError($"[BanditGangBuilder] {BANDIT_PREFAB} 없음"); return; }

        // 1. 도적단 암살자 (기존 Enemy_Bandit)
        GameObject hpBar = null; List<DropItem> drops = null;
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BANDIT_PREFAB);
            EnemyBandit rogue = root.GetComponent<EnemyBandit>();
            if (rogue == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[BanditGangBuilder] Enemy_Bandit에 EnemyBandit이 없습니다."); return; }
            hpBar = rogue.hpBarPrefab;
            drops = rogue.dropTable != null ? new List<DropItem>(rogue.dropTable) : new List<DropItem>();
            Apply(root, rogue, rogueStats);
            PrefabUtility.SaveAsPrefabAsset(root, BANDIT_PREFAB);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 2. 도적단 전사
        EnemyBanditWarrior warrior = MakeMember<EnemyBanditWarrior>(WARRIOR_PREFAB, "Enemy_BanditWarrior", hpBar, warriorStats, out GameObject warriorRoot);
        if (warrior == null) return;
        warrior.dropTable = new List<DropItem>(drops);
        PrefabUtility.SaveAsPrefabAsset(warriorRoot, WARRIOR_PREFAB);
        PrefabUtility.UnloadPrefabContents(warriorRoot);

        // 3. 도적단 궁수 (아군 궁수의 화살 프리팹을 같이 씀)
        EnemyBanditArcher archer = MakeMember<EnemyBanditArcher>(ARCHER_PREFAB, "Enemy_BanditArcher", hpBar, archerStats, out GameObject archerRoot);
        if (archer == null) return;
        archer.dropTable = new List<DropItem>(drops);
        GameObject allyArcher = AssetDatabase.LoadAssetAtPath<GameObject>(ALLY_ARCHER);
        Ally_Archer allyArcherScript = allyArcher != null ? allyArcher.GetComponent<Ally_Archer>() : null;
        archer.arrowPrefab = allyArcherScript != null ? allyArcherScript.arrowPrefab : null;
        if (archer.arrowPrefab == null) Debug.LogWarning("[BanditGangBuilder] 화살 프리팹을 찾지 못했습니다. 도적단 궁수는 화살 없이 바로 맞힙니다.");
        PrefabUtility.SaveAsPrefabAsset(archerRoot, ARCHER_PREFAB);
        PrefabUtility.UnloadPrefabContents(archerRoot);
        AssetDatabase.SaveAssets();

        // 4. 로비 씬 QuestGenerator에 연결
        ConnectQuestGenerator();
        Debug.Log("=== 도적단 프리팹 만들기 완료! (도적단 전사·암살자·궁수, QuestGenerator 연결) ===");
    }

    // Enemy_Bandit을 복사해 스크립트만 T로 바꾼 구성원 프리팹을 열어 돌려줌 (저장은 부른 쪽에서)
    static T MakeMember<T>(string path, string name, GameObject hpBar, Stats stats, out GameObject root) where T : BattleUnit
    {
        root = null;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(BANDIT_PREFAB, path))
        {
            Debug.LogError($"[BanditGangBuilder] {path} 복사 실패");
            return null;
        }
        root = PrefabUtility.LoadPrefabContents(path);
        root.name = name;
        root.tag = "Enemy";

        BattleUnit old = root.GetComponent<BattleUnit>();
        if (old != null && !(old is T)) DestroyImmediate(old);
        T unit = root.GetComponent<T>();
        if (unit == null) unit = root.AddComponent<T>();
        if (hpBar != null) unit.hpBarPrefab = hpBar;
        Apply(root, unit, stats);
        return unit;
    }

    static void Apply(GameObject root, BattleUnit unit, Stats stats)
    {
        unit.maxHp = stats.hp;
        unit.attackPower = stats.atk;
        unit.attackRange = stats.range;
        unit.moveSpeed = stats.speed;
        unit.attackCooldown = stats.cooldown;
        unit.aggro = 10f;
        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        // 임시 그래픽: 기본 네모일 때만 색으로 구분 (그림은 'Tools > 기본 몬스터 임시 스프라이트 입히기'가 입힘)
        if (sr != null && (sr.sprite == null || sr.sprite.name == "Square")) sr.color = stats.color;
    }

    static void ConnectQuestGenerator()
    {
        Scene lobby = SceneManager.GetSceneByPath(LOBBY_SCENE);
        bool openedHere = !lobby.isLoaded;
        if (openedHere) lobby = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Additive);

        int connected = 0;
        foreach (GameObject go in lobby.GetRootGameObjects())
        {
            foreach (QuestGenerator generator in go.GetComponentsInChildren<QuestGenerator>(true))
            {
                generator.banditGangPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BANDIT_PREFAB);
                generator.banditGangMembers = new List<GameObject>
                {
                    AssetDatabase.LoadAssetAtPath<GameObject>(WARRIOR_PREFAB),
                    AssetDatabase.LoadAssetAtPath<GameObject>(BANDIT_PREFAB),
                    AssetDatabase.LoadAssetAtPath<GameObject>(ARCHER_PREFAB),
                };
                EditorUtility.SetDirty(generator);
                connected++;
            }
        }

        if (connected == 0) Debug.LogWarning("[BanditGangBuilder] 로비 씬에서 QuestGenerator를 찾지 못했습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            Debug.Log($"[BanditGangBuilder] QuestGenerator {connected}개에 도적단 연결");
        }
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
    }
}
