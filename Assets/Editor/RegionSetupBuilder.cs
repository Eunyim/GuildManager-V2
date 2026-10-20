using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// [메뉴] Tools > 던전 지역 설정 연결
/// 로비 씬 QuestGenerator의 던전 지역(regions)을 기본값으로 채웁니다. (지역 이름은 임시)
///  - 초원 (명성 Lv.1, D 80% / C 20%): 초원 늑대, 슬라임, 도적단
///  - 숲 (명성 Lv.2, C 70% / B 30%): 슬라임, 도적단, 고블린
///  - 지하 묘지 (명성 Lv.2, C 70% / B 30%): 슬라임, 스켈레톤, 초원 늑대
///  - 오크 부락 (명성 Lv.3, B 60% / A 40%): 고블린, 오크, 초원 늑대
///  - 드래곤 둥지 (명성 Lv.4, S): 해츨링, 베이비 드래곤
/// 몬스터 종족(races)도 채웁니다. 종족마다 보스가 하나 있고, 보스방에는 그 지역에 사는 종족의 보스 중 하나가 무작위로 나옵니다.
///  - 슬라임: 킹 슬라임 / 초원 늑대: 알파 / 스켈레톤: 스켈레톤 네크로맨서 / 고블린: 샤먼킹(고블린 무리와 함께) / 오크: 오크 백부장 / 드래곤: 드래곤
///  - 도적단은 보스가 없습니다.
/// 종족의 재료(재료 수급 의뢰용)는 그 종족 대표 몬스터의 드랍 표를 그대로 씁니다 ('Tools > 종족별 드랍 아이템 연결'을 먼저 실행).
/// 슬라임은 재료 수급 의뢰를 내지 않습니다.
/// 고블린·스켈레톤·오크·초원 늑대·도적단은 무리 대표 프리팹이라 뽑히면 무리로 나옵니다.
/// 실행할 때마다 지역 목록을 이 값으로 다시 씁니다 (인스펙터에서 고친 값은 덮어씀).
/// </summary>
public class RegionSetupBuilder : Editor
{
    const string FOLDER      = "Assets/Prefabs/Characters";
    const string LOBBY_SCENE = "Assets/Scenes/LobbyScene.unity";

    [MenuItem("Tools/던전 지역 설정 연결")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[RegionSetupBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }

        var regions = new List<DungeonRegion>
        {
            Region("초원",        1, RankType.D, RankType.C, 0.2f, "Enemy_PrairieWolf", "Enemy_Slime", "Enemy_Bandit"),
            Region("숲",          2, RankType.C, RankType.B, 0.3f, "Enemy_Slime", "Enemy_Bandit", "Enemy_Goblin"),
            Region("지하 묘지",   2, RankType.C, RankType.B, 0.3f, "Enemy_Slime", "Enemy_Skeleton", "Enemy_PrairieWolf"),
            Region("오크 부락",   3, RankType.B, RankType.A, 0.4f, "Enemy_Goblin", "Enemy_Orc", "Enemy_PrairieWolf"),
            Region("드래곤 둥지", 4, RankType.S, RankType.S, 0f, "Enemy_DragonHatchling", "Enemy_BabyDragon"),
        };

        var races = new List<MonsterRace>
        {
            Race("슬라임",    "Enemy_KingSlime",          false, "Enemy_Slime"),
            Race("도적단",    null,                       false, "Enemy_Bandit"), // 보스 없음 (재료 수급 의뢰용)
            Race("초원 늑대", "Enemy_AlphaWolf",          false, "Enemy_PrairieWolf"),
            Race("스켈레톤",  "Enemy_SkeletonNecromancer", false, "Enemy_Skeleton"),
            Race("고블린",    "Enemy_GoblinShamanKing",   true,  "Enemy_Goblin"),
            Race("오크",      "Enemy_OrcCenturion",       false, "Enemy_Orc"),
            Race("드래곤",    "Enemy_Dragon",             false, "Enemy_DragonHatchling", "Enemy_BabyDragon"),
        };

        Scene lobby = SceneManager.GetSceneByPath(LOBBY_SCENE);
        bool openedHere = !lobby.isLoaded;
        if (openedHere) lobby = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Additive);

        int connected = 0;
        foreach (GameObject go in lobby.GetRootGameObjects())
        {
            foreach (QuestGenerator generator in go.GetComponentsInChildren<QuestGenerator>(true))
            {
                generator.regions = regions;
                generator.races = races;
                EditorUtility.SetDirty(generator);
                connected++;
            }
        }

        if (connected == 0) Debug.LogWarning("[RegionSetupBuilder] 로비 씬에서 QuestGenerator를 찾지 못했습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            Debug.Log($"[RegionSetupBuilder] QuestGenerator {connected}개에 던전 지역 {regions.Count}곳, 몬스터 종족 {races.Count}개 연결");
        }
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
        Debug.Log("=== 던전 지역 설정 연결 완료! ===");
    }

    static MonsterRace Race(string name, string boss, bool bossLeadsPack, params string[] members)
    {
        var race = new MonsterRace { name = name, bossLeadsPack = bossLeadsPack, members = new List<GameObject>(), materials = new List<DropItem>() };
        if (!string.IsNullOrEmpty(boss))
        {
            race.boss = AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{boss}.prefab");
            if (race.boss == null) Debug.LogWarning($"[RegionSetupBuilder] 보스 {boss} 프리팹 없음 ({name}). 먼저 'Tools > 보스 프리팹 만들기'를 실행하세요.");
        }
        foreach (string file in members)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{file}.prefab");
            if (prefab == null) Debug.LogWarning($"[RegionSetupBuilder] {file} 프리팹 없음 ({name})");
            else race.members.Add(prefab);
        }
        // 재료: 첫 소속 몬스터의 드랍 표 (슬라임 제외)
        if (name != "슬라임" && race.members.Count > 0) race.materials = DropsOf(race.members[0]);
        return race;
    }

    static List<DropItem> DropsOf(GameObject prefab)
    {
        foreach (MonoBehaviour mb in prefab.GetComponents<MonoBehaviour>())
        {
            var field = mb != null ? mb.GetType().GetField("dropTable") : null;
            if (field != null && field.GetValue(mb) is List<DropItem> list) return list.FindAll(d => d.item != null);
        }
        return new List<DropItem>();
    }

    static DungeonRegion Region(string name, int requiredLevel, RankType min, RankType max, float maxRankChance, params string[] prefabs)
    {
        var region = new DungeonRegion
        {
            name = name, requiredReputationLevel = requiredLevel, minRank = min, maxRank = max, maxRankChance = maxRankChance,
            enemies = new List<GameObject>(),
        };
        foreach (string file in prefabs)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{file}.prefab");
            if (prefab == null) Debug.LogWarning($"[RegionSetupBuilder] {file} 프리팹 없음 ({name})");
            else region.enemies.Add(prefab);
        }
        return region;
    }
}
