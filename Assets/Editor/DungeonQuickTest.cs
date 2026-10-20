using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// [메뉴] Tools > 던전 빠른 테스트 > (랭크)
/// 로비에서 모험가를 모집하고 파견하는 과정을 건너뛰고 바로 던전 전투를 테스트합니다.
/// 어떤 씬이 열려 있든 로비에서 플레이를 시작한 뒤, 테스트 파티(전사/도적/궁수/사제)와
/// 해당 랭크의 의뢰를 만들어 곧바로 던전으로 들어갑니다. 실제 파견과 같은 경로(GameManager.StartQuest)를 씁니다.
/// 에디터 전용이며 빌드에는 포함되지 않습니다.
/// </summary>
[InitializeOnLoad]
public static class DungeonQuickTest
{
    const string LOBBY_SCENE_PATH = "Assets/Scenes/LobbyScene.unity";
    const string PENDING_RANK_KEY = "GuildManager.DungeonQuickTest.PendingRank"; // 플레이 진입(도메인 리로드) 후에도 유지
    const string TEST_PARTY_NAME  = "테스트 파티";

    // 테스트 파티 구성: 전위 2 (전사, 도적) + 후위 2 (궁수, 사제)
    static readonly JobType[] testJobs = { JobType.Warrior, JobType.Rogue, JobType.Archer, JobType.Healer };

    static DungeonQuickTest()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/던전 빠른 테스트/D 랭크 의뢰")] static void StartD() => Begin(RankType.D);
    [MenuItem("Tools/던전 빠른 테스트/C 랭크 의뢰")] static void StartC() => Begin(RankType.C);
    [MenuItem("Tools/던전 빠른 테스트/B 랭크 의뢰")] static void StartB() => Begin(RankType.B);
    [MenuItem("Tools/던전 빠른 테스트/A 랭크 의뢰")] static void StartA() => Begin(RankType.A);
    [MenuItem("Tools/던전 빠른 테스트/S 랭크 의뢰")] static void StartS() => Begin(RankType.S);

    static void Begin(RankType rank)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[DungeonQuickTest] 이미 플레이 중입니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LOBBY_SCENE_PATH) == null)
        {
            Debug.LogError($"[DungeonQuickTest] 로비 씬을 찾을 수 없습니다: {LOBBY_SCENE_PATH}");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        SessionState.SetInt(PENDING_RANK_KEY, (int)rank);

        // 지금 열려 있는 씬과 상관없이 로비에서 시작 (GameManager, 의뢰 생성기가 로비에 있음)
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(LOBBY_SCENE_PATH);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetInt(PENDING_RANK_KEY, -1) >= 0)
        {
            EditorApplication.update += StartDungeonWhenLobbyReady;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            // 다음에 그냥 Play를 누르면 원래대로 열려 있는 씬에서 시작하도록 되돌림
            EditorApplication.update -= StartDungeonWhenLobbyReady;
            SessionState.EraseInt(PENDING_RANK_KEY);
            EditorSceneManager.playModeStartScene = null;
        }
    }

    // 로비의 매니저들이 준비되면 테스트 파티와 의뢰를 만들어 던전으로 출발
    static void StartDungeonWhenLobbyReady()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= StartDungeonWhenLobbyReady;
            return;
        }
        if (GameManager.Instance == null || LobbyManager.Instance == null) return; // 아직 로딩 중

        EditorApplication.update -= StartDungeonWhenLobbyReady;

        RankType rank = (RankType)SessionState.GetInt(PENDING_RANK_KEY, 0);
        SessionState.EraseInt(PENDING_RANK_KEY); // 던전에서 로비로 돌아왔을 때 다시 시작되지 않도록

        GameManager gm = GameManager.Instance;

        QuestGenerator[] generators = Object.FindObjectsByType<QuestGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (generators.Length == 0)
        {
            Debug.LogError("[DungeonQuickTest] 로비 씬에서 QuestGenerator를 찾을 수 없습니다.");
            return;
        }

        QuestData quest = generators[0].Generate(rank);
        if (quest == null)
        {
            Debug.LogError($"[DungeonQuickTest] {rank} 랭크 의뢰를 만들 수 없습니다. QuestGenerator의 {rank} 등급 적 풀을 확인하세요.");
            return;
        }

        // 의뢰와 같은 랭크의 모험가로 파티 구성 후 1번 방에 배정
        Party party = new Party(TEST_PARTY_NAME);
        gm.partyList.Add(party);
        int partyIndex = gm.partyList.Count - 1;

        foreach (JobType job in testJobs)
        {
            Adventurer adventurer = AdventurerGenerator.Generate(rank, job);
            adventurer.assignedPartyIndex = partyIndex;
            gm.adventurers.Add(adventurer);
            party.members.Add(adventurer);
        }
        gm.AssignPartyToRoom(party, 0);

        Debug.Log($"[DungeonQuickTest] {rank} 랭크 테스트 시작: '{quest.questName}' (스테이지 {quest.stages.Count}개), 파티 {party.members.Count}명");
        gm.StartQuest(party, quest);
    }
}
