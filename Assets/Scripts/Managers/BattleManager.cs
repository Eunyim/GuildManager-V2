using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance; // 싱글톤

    [Header("던전 구성")]
    public List<StageData> dungeonStages; // 스테이지 데이터 리스트
    public int currentStageIndex = 0;
    
    [Header("스폰 위치")]
    public Transform playerSpawnPoint; // 아군 시작 위치 (기본)
    public Transform enemySpawnPoint;  // 적군 시작 위치 (오른쪽)

    [Header("상태 정보")]
    public bool isBattleActive = false;
    public int allyCount = 0;  // 현재 생존 아군 수
    public int startAllyCount = 0; // 던전에 출발한 아군 수 (외향적 등 아군 비율 특성의 기준)
    public int enemyCount = 0; // 현재 생존 적군 수

    [Header("UI 패널 연결")]
    public GameObject victoryPanel; // 승리 패널
    public GameObject defeatPanel;  // 패배 패널
    public GameObject fleePanel;    // 도주 패널 (도주 시 결과 표시)

    [Header("아군 프리팹 연결 (직업별)")]
    public GameObject warriorPrefab; 
    public GameObject archerPrefab;  
    public GameObject magePrefab;    
    public GameObject roguePrefab;
    public GameObject healerPrefab;

    [Header("던전 전리품")]
    public List<MonsterDropData> earnedItems = new List<MonsterDropData>();
    public List<EquipmentItem> earnedEquipment = new List<EquipmentItem>(); // 이번 던전에서 얻은 장비 (전멸하면 잃음)

    [Header("던전 오브젝트")]
    public GameObject chestPrefab; // 보물상자 프리팹

    [Header("아군 스폰 위치 설정")]
    public Transform[] frontSpawnPoints; // 전위 스폰 좌표들 (앞줄)
    public Transform[] backSpawnPoints;  // 후위 스폰 좌표들 (뒷줄)

    [Header("타격감")]
    public HitFeedbackSettings hitFeedback = new HitFeedbackSettings();

    [Header("전투 이동 (성향별: 원거리 거리 유지, 지원 도망, 우회, 수호)")]
    public CombatMovementSettings combatMovement = new CombatMovementSettings();

    [Header("스폰 산개")]
    [Tooltip("전위 줄에 선 아군끼리의 위아래 간격. 줄의 중심은 전위 스폰 좌표들의 평균")]
    public float frontLineSpacing = 2.4f;
    [Tooltip("후위 줄에 선 아군끼리의 위아래 간격. 후위는 마름모 바닥의 왼쪽 끝이라 폭이 좁음")]
    public float backLineSpacing = 2.0f;
    [Tooltip("아군 자리마다 무작위로 흩는 폭")]
    public float allySpawnJitter = 0.4f;
    [Tooltip("적 사이의 간격 (아이소메트릭 마름모 대형, 사각 맵은 가로 한 줄)")]
    public float enemySpawnSpacing = 2.2f;
    [Tooltip("적 자리마다 무작위로 흩는 폭")]
    public float enemySpawnJitter = 0.4f;
    [Tooltip("아이소메트릭 적 대형: 한 세로 열에 서는 적 수")]
    public int enemyRowsPerColumn = 4;
    [Tooltip("아이소메트릭 적 대형: 위아래 간격")]
    public float enemyRowSpacing = 1.5f;
    [Tooltip("아이소메트릭 적 대형: 열 사이 가로 간격")]
    public float enemyColumnSpacing = 1.6f;

    [Header("던전 진행")]
    private bool isRoomCleared = false; // 방 클리어 여부

    // (에러 방지용)
    public GameObject damageTextPrefab; 

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 1. 아군 소환 (직업별 프리팹 사용)
        SetupAllyParty();

        // 2. 퀘스트에 스테이지가 있으면 인스펙터 값보다 우선 사용
        if (GameManager.Instance != null &&
            GameManager.Instance.currentQuest != null &&
            GameManager.Instance.currentQuest.stages != null &&
            GameManager.Instance.currentQuest.stages.Count > 0)
        {
            dungeonStages = GameManager.Instance.currentQuest.stages;
        }

        // 3. 던전 구조 준비 후 입구 방부터 시작
        if (dungeonStages != null && dungeonStages.Count > 0)
        {
            SetupLayout();
            InitGatherTracking();
            StartCoroutine(EnterRoomCoroutine(layout.startRoom));
        }
        else
        {
            Debug.LogError("🚨 던전 스테이지 데이터(StageData)가 비어있습니다!");
        }
    }

    // --- 1. 아군 소환 로직 ---
    void SetupAllyParty()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentDispatchParty == null) 
        {
            Debug.LogWarning("게임 매니저나 파티 정보가 없습니다. 테스트 모드일 수 있습니다.");
            return;
        }

        List<Adventurer> members = GameManager.Instance.currentDispatchParty.members;

        List<Transform> frontUnits = new List<Transform>(), backUnits = new List<Transform>();
        allyCount = 0;

        // 케미 보너스: 모험가마다 출발할 때의 동료들과의 관계로 따로 정해짐
        Party dispatchParty = GameManager.Instance.currentDispatchParty;
        Debug.Log($"💞 [케미] {GameManager.Instance.GetChemistryText(dispatchParty)}");

        foreach (Adventurer member in members)
        {
            GameObject prefabToSpawn = null;
            bool isFrontLine = false;

            switch (member.job)
            {
                case JobType.Warrior: prefabToSpawn = warriorPrefab; isFrontLine = true; break;
                case JobType.Rogue:   prefabToSpawn = roguePrefab; isFrontLine = true; break;
                case JobType.Archer:  prefabToSpawn = archerPrefab; isFrontLine = false; break;
                case JobType.Mage:    prefabToSpawn = magePrefab; isFrontLine = false; break;
                case JobType.Healer:  prefabToSpawn = healerPrefab; isFrontLine = false; break;
                default:              prefabToSpawn = warriorPrefab; isFrontLine = true; break;
            }

            if (prefabToSpawn == null) continue;

            GameObject newUnit = Instantiate(prefabToSpawn);
            newUnit.name = $"Unit_{member.name}";
            newUnit.tag = "Player";

            (isFrontLine ? frontUnits : backUnits).Add(newUnit.transform);

            AdventurerSprites.ApplyToUnit(newUnit, member); // 이 모험가의 겉모습으로 조립한 도트 그림 + 애니메이션

            BattleUnit unitScript = newUnit.GetComponent<BattleUnit>();
            if (unitScript != null)
            {
                unitScript.Initialize(member);
                float chemistryMultiplier = GameManager.Instance.GetChemistryStatMultiplier(member, dispatchParty);
                if (chemistryMultiplier > 1f) unitScript.ApplyStatBonus(chemistryMultiplier); // 개인 케미 보너스 (HP·공격력)
            }

            allyCount++;
        }
        startAllyCount = allyCount;
        PlaceAllyLines(frontUnits, backUnits);
        Debug.Log($"⚔️ 아군 {allyCount}명 소환 완료!");
    }

    // --- 2. 던전 구조와 방 진입 ---
    // 던전은 동서남북으로 이어진 방들의 묶음. currentStageIndex가 지금 있는 방 번호이고 dungeonStages[방 번호]가 그 방의 적 구성.
    // 방을 정리하면 모닥불에서 갈 방향을 고르고, 보스방을 정리하면 던전 클리어.

    private DungeonLayout layout;
    private bool[] roomVisited; // 들어가 본 방
    private bool[] roomCleared; // 적을 정리하고 상자를 연 방 (다시 들어가도 전투 없음)

    public DungeonLayout Layout => layout;
    public bool IsRoomVisited(int room) => roomVisited != null && room >= 0 && room < roomVisited.Length && roomVisited[room];

    // 지도에 방 종류를 보여 줄 수 있는가: 가 본 방, 그리고 육감을 가진 파티원이 살아 있으면 지금 방과 이어진 방
    public bool IsRoomTypeKnown(int room)
    {
        if (room == currentStageIndex || IsRoomVisited(room)) return true;
        if (layout == null || currentStageIndex < 0 || currentStageIndex >= layout.rooms.Count) return false;
        if (System.Array.IndexOf(layout.rooms[currentStageIndex].neighbors, room) < 0) return false;
        return PartyHasTraitEffect(TraitEffectType.RevealNextRoom);
    }

    // 살아 있는 파티원 중 이 효과를 가진 특성이 있는가
    bool PartyHasTraitEffect(TraitEffectType effect)
    {
        foreach (BattleUnit u in BattleUnit.Active)
        {
            if (u == null || u.isDead || u.SourceData == null || u.SourceData.traits == null) continue;
            foreach (TraitData t in u.SourceData.traits)
                if (t != null && t.HasEffect(effect)) return true;
        }
        return false;
    }

    // 살아 있는 파티원의 성격 목록 (덜렁거리는·운 좋은 효과 계산용)
    List<TraitType> AlivePersonalities()
    {
        var list = new List<TraitType>();
        foreach (BattleUnit u in BattleUnit.Active)
            if (u != null && !u.isDead && u.SourceData != null && u.CompareTag("Player")) list.Add(u.SourceData.trait);
        return list;
    }

    // 덜렁거리는 성격만큼 줄어든 골드
    int ApplyGoldGain(int amount)
    {
        if (GameManager.Instance == null || amount <= 0) return amount;
        float m = PersonalityRules.StackedMultiplier(AlivePersonalities(), TraitType.Clumsy, GameManager.Instance.personality.clumsyGoldMultiplier);
        return Mathf.RoundToInt(amount * m);
    }
    public bool IsRoomCleared(int room) => roomCleared != null && room >= 0 && room < roomCleared.Length && roomCleared[room];

    public int ClearedRoomCount
    {
        get
        {
            int count = 0;
            if (roomCleared != null) foreach (bool cleared in roomCleared) if (cleared) count++;
            return count;
        }
    }

    void SetupLayout()
    {
        QuestData quest = GameManager.Instance != null ? GameManager.Instance.currentQuest : null;
        bool usesQuestStages = quest != null && quest.stages == dungeonStages;
        bool hasLayout = usesQuestStages && quest.layout != null && quest.layout.IsValid && quest.layout.rooms.Count == dungeonStages.Count;

        // 구조 정보가 없는 의뢰(직접 만든 에셋, 인스펙터의 스테이지)는 방이 한 줄로 이어진 것으로 봄
        layout = hasLayout ? quest.layout : DungeonLayoutRules.Linear(dungeonStages.Count);
        roomVisited = new bool[layout.rooms.Count];
        roomCleared = new bool[layout.rooms.Count];

        int bossDistance = DungeonLayoutRules.GetDistances(layout, layout.startRoom)[layout.bossRoom];
        Debug.Log($"🗺️ 던전 구조: 방 {layout.rooms.Count}개, 보스방은 입구에서 {bossDistance}칸 떨어진 방 {layout.bossRoom + 1}{(hasLayout ? "" : " (구조 정보 없음: 한 줄로 이어진 방)")}");
    }

    IEnumerator EnterRoomCoroutine(int room)
    {
        currentStageIndex = room;
        roomVisited[room] = true;
        EnsureMinimap();

        // 맵 생성 (테마가 연결된 경우). 같은 방에 되돌아오면 같은 모양이 나오도록 방마다 정해진 값으로 생성
        StageData currentStage = dungeonStages[room];
        if (MapGenerator.Instance != null && currentStage.mapTheme != null)
        {
            Random.State savedState = Random.state;
            Random.InitState(layout.rooms[room].seed);
            MapGenerator.Instance.GenerateMap(currentStage.mapTheme);
            Random.state = savedState;
        }

        if (roomCleared[room])
        {
            // 이미 정리한 방: 전투 없이 지나가며 다음 갈 곳을 고름
            Debug.Log($"🚶 정리한 방으로 되돌아옴 (방 {room + 1})");
            yield return new WaitForSeconds(revisitDelay);
            ResetAllyPositions();
            if (campfirePanel != null) EnterCampfire(0f);
            else MoveToDefaultExit();
            yield break;
        }

        DungeonRoomType type = layout.rooms[room].type;
        Debug.Log($"⚔️ 방 {room + 1} 진입 ({DungeonLayoutRules.GetRoomTypeName(type)})");
        lastRoomGold = 0;

        yield return new WaitForSeconds(2.0f);

        ResetAllyPositions();
        if (room == CaptiveRoom && !captiveRescued && type != DungeonRoomType.Rest) SpawnCaptive(); // 방을 정리하면 구함

        switch (type)
        {
            case DungeonRoomType.Rest:
                // 휴식 공간: 전투 없이 바로 쉬며 크게 회복 (구출 대상이 여기 있으면 바로 구함)
                roomCleared[room] = true;
                if (room == CaptiveRoom && !captiveRescued) { SpawnCaptive(); RescueCaptive(); }
                if (FinishIfRescuedAfterBoss()) break;
                if (campfirePanel != null) EnterCampfire(roomSettings.restHealRatio);
                else MoveToDefaultExit();
                break;

            case DungeonRoomType.Treasure:
                // 보물 방: 전투 없이 상자만 (미믹이면 싸움이 벌어짐)
                enemyCount = 0;
                wavesLeft = 0;
                SpawnTreasureChest();
                break;

            default:
                // 기본 방 / 보스 방 / 웨이브 방
                waveEnemies = currentStage.enemyPrefabs;
                waveStage = currentStage;
                int waves = type == DungeonRoomType.Wave ? Mathf.Max(1, currentStage.waveCount) : 1;
                waveSize = Mathf.CeilToInt((float)waveEnemies.Count / waves);
                waveStart = 0;
                wavesLeft = waves;
                enemyCount = 0;
                SpawnNextWave();
                isBattleActive = true;
                // 방 전투 시작 알림 (웨이브 방은 첫 웨이브에서만). 예: 도적의 시작 은신
                foreach (BattleUnit unit in new List<BattleUnit>(BattleUnit.Active))
                    if (unit != null && !unit.isDead) unit.OnRoomBattleStart();
                FleeIfOnlyHealersLeft();
                break;
        }
    }

    // ─── 방 종류별 수치 ─────────────────────────────────────────────
    [Header("방 종류별 수치")]
    public DungeonRoomSettings roomSettings = new DungeonRoomSettings();

    public DungeonRoomType GetRoomType(int room) =>
        layout != null && room >= 0 && room < layout.rooms.Count ? layout.rooms[room].type : DungeonRoomType.Normal;

    private List<GameObject> waveEnemies; // 이 방에 나올 적 전체 (웨이브 방은 여러 번에 나눠 등장)
    private StageData waveStage;          // waveEnemies가 속한 방 (적별 랭크 낮춤 정보)
    private int waveSize;                 // 한 번에 나오는 수
    private int waveStart;                // 다음에 나올 적의 시작 위치
    private int wavesLeft;                // 아직 안 나온 웨이브 수
    private int lastRoomGold;             // 방금 방에서 얻은 골드 (보물 상자 / 웨이브 보상). 모닥불 화면에 표시

    void SpawnNextWave()
    {
        if (waveEnemies == null || wavesLeft <= 0) return;
        wavesLeft--;

        int count = wavesLeft == 0 ? waveEnemies.Count - waveStart : Mathf.Min(waveSize, waveEnemies.Count - waveStart);
        SpawnEnemies(waveEnemies.GetRange(waveStart, Mathf.Max(0, count)), waveStart);
        waveStart += count;
    }

    IEnumerator NextWaveAfterDelay()
    {
        Debug.Log($"🌊 [웨이브] 다음 무리가 몰려옵니다! (남은 웨이브 {wavesLeft})");
        yield return new WaitForSeconds(roomSettings.waveInterval);
        if (!gameEnded) SpawnNextWave();
    }

    // 던전 안에서 주운 골드 (보물 상자, 웨이브 보상). 전멸하면 미믹 보너스와 함께 잃음
    void GrantRoomGold(int amount, string reason)
    {
        if (amount <= 0 || GameManager.Instance == null) return;
        amount = ApplyGoldGain(amount); // 덜렁거리는 성격
        GameManager.Instance.gold += amount;
        GameManager.Instance.lastEarnedGold += amount;
        mimicBonusTotal += amount;
        lastRoomGold += amount;
        Debug.Log($"💰 [{reason}] +{amount}G");
    }

    RankType GetQuestRank()
    {
        QuestData quest = GameManager.Instance != null ? GameManager.Instance.currentQuest : null;
        return quest != null && EnemyScalingRules.TryParseRank(quest.rank, out RankType rank) ? rank : RankType.D;
    }

    // 고를 화면이 없을 때(모닥불 화면 미설치) 쓰는 기본 이동: 안 가 본 방 우선
    void MoveToDefaultExit()
    {
        int fallback = -1;
        for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
        {
            int next = layout.GetNeighbor(currentStageIndex, d);
            if (next < 0) continue;
            if (!roomVisited[next]) { StartCoroutine(EnterRoomCoroutine(next)); return; }
            if (fallback < 0) fallback = next;
        }
        if (fallback >= 0) StartCoroutine(EnterRoomCoroutine(fallback));
    }

    // firstIndex: enemies[0]이 이 방 적 목록(currentStage.enemyPrefabs)에서 몇 번째인지 (랭크 낮춤 정보를 찾을 때 씀)
    void SpawnEnemies(List<GameObject> enemies, int firstIndex)
    {
        if (enemies == null) return;

        enemyCount += enemies.Count; // 앞 웨이브의 적(분열체 등)이 남아 있을 수 있어 더함

        bool isometric = MapGenerator.Instance != null && MapGenerator.Instance.IsIsometric;

        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject prefab = enemies[i];
            if (prefab == null) continue;

            // 아이소메트릭 맵은 오른쪽으로 갈수록 좁아지므로, 한 줄 대신 마름모꼴 대형으로 세움
            Vector3 pos = enemySpawnPoint.position
                + (isometric ? GetIsoFormationOffset(i) : new Vector3(i * enemySpawnSpacing, 0, 0));
            pos = ScatterOnFloor(pos, enemySpawnJitter, enemySpawnPoint.position);
            GameObject newEnemy = Instantiate(prefab, pos, Quaternion.identity);
            newEnemy.tag = "Enemy";
            if (gatherSourcePrefabs.Contains(prefab)) gatherSourceIds.Add(newEnemy.GetInstanceID()); // 재료 수급: 계산에 들어간 재료 몬스터
            EnemySlime.LinkPrefab(newEnemy, prefab);
            ApplyQuestRankScaling(newEnemy, waveStage != null ? waveStage.GetStatsRankDown(firstIndex + i) : 0);
            // 왼쪽(아군 쪽)을 보도록 좌우만 뒤집음. 크기는 프리팹에 설정된 값 그대로
            Vector3 enemyScale = newEnemy.transform.localScale;
            enemyScale.x = -Mathf.Abs(enemyScale.x);
            newEnemy.transform.localScale = enemyScale;

            if (IsBossStage && i == 0) MakeBoss(newEnemy, prefab); // 보스방 목록의 첫 번째가 보스 (나머지는 보스를 따라온 무리)
        }
    }

    // ─── 보스 ─────────────────────────────────────────────

    [Header("보스")]
    public string kingSlimeName = "킹 슬라임";
    [Tooltip("킹 슬라임의 키가 모험가(전사 프리팹) 키의 몇 배인지")]
    public float kingSlimeSizeVsAdventurer = 2.5f;
    [Tooltip("모든 보스의 체력에 곱하는 배율 (프리팹 체력 × 랭크 배율 × 이 값)")]
    public float bossHpMultiplier = 1.5f;

    [Header("데모 엔딩 (드래곤 처치)")]
    public DemoEndingSettings demoEnding = new DemoEndingSettings();
    bool endingBossSpawned; // 이번 던전의 보스가 엔딩 보스(어른 드래곤)인가

    bool EndingBossSlain => endingBossSpawned && bossDefeated;

    // 의뢰 결과를 기록하고, 직접 지휘로 드래곤을 쓰러뜨렸으면 로비에서 엔딩을 띄우도록 예약
    void RecordResultAndEnding(bool success, List<Adventurer> members)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.NoteQuestResult(success);
        if (GuildRecordRules.ShouldTriggerEnding(demoEnding, gm.record, success, EndingBossSlain))
            gm.ReserveDemoEnding(members);
    }

    bool IsBossStage => dungeonStages != null && currentStageIndex >= 0 && currentStageIndex < dungeonStages.Count
                        && dungeonStages[currentStageIndex] != null && dungeonStages[currentStageIndex].isBossStage;

    // 보스방의 적을 보스로 바꾸고 화면 위에 이름과 체력 바를 띄움
    //  - 슬라임 → 킹 슬라임 (키를 모험가의 N배로 키우고, 죽으면 일반 슬라임으로 분열)
    //  - 그 밖의 보스 (예: 드래곤 둥지의 드래곤) → 크기·능력치는 프리팹 그대로, 보스 체력 바만
    void MakeBoss(GameObject enemy, GameObject prefab)
    {
        BattleUnit scaledUnit = enemy.GetComponent<BattleUnit>();
        if (scaledUnit != null)
        {
            scaledUnit.MultiplyHpScaling(bossHpMultiplier);
            scaledUnit.isBoss = true;
        }
        endingBossSpawned = GuildRecordRules.IsEndingBoss(demoEnding, prefab != null ? prefab.name : enemy.name);

        EnemySlime slime = enemy.GetComponent<EnemySlime>();
        if (slime == null)
        {
            BattleUnit bossUnit = enemy.GetComponent<BattleUnit>();
            if (bossUnit == null) return;
            string bossName = DisplayNames.EnemyToKorean(prefab != null ? prefab.name : enemy.name);
            ShowBossBar(bossUnit, bossName);
            Debug.Log($"👑 보스 등장: {bossName}");
            return;
        }

        slime.isKing = true;
        enemy.name = "Enemy_KingSlime";
        MonsterAnimator kingLook = enemy.GetComponent<MonsterAnimator>();
        if (kingLook != null) kingLook.UseBossSet(); // 왕관 쓴 킹 슬라임 그림

        // 키를 모험가의 N배로: (모험가 스프라이트 키 × 크기) × N ÷ (슬라임 키)
        // 도트 애니메이션 몬스터는 칸 여백을 뺀 실제 몸 키를 씀
        float factor = kingSlimeSizeVsAdventurer;
        float adventurerHeight = GetSpriteHeight(warriorPrefab);
        float slimeHeight = kingLook != null && kingLook.VisualHeight > 0f ? kingLook.VisualHeight : GetSpriteHeight(prefab);
        if (adventurerHeight > 0f && slimeHeight > 0f) factor = adventurerHeight * kingSlimeSizeVsAdventurer / slimeHeight;
        enemy.transform.localScale = new Vector3(enemy.transform.localScale.x * factor, enemy.transform.localScale.y * factor, enemy.transform.localScale.z);

        BattleUnit unit = enemy.GetComponent<BattleUnit>();
        ShowBossBar(unit, kingSlimeName);
        Debug.Log($"👑 보스 등장: {kingSlimeName} (크기 x{factor:F2})");
    }

    // 프리팹의 화면상 키 (스프라이트 높이 × 세로 크기)
    static float GetSpriteHeight(GameObject prefab)
    {
        if (prefab == null) return 0f;
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return 0f;
        return sr.sprite.bounds.size.y * Mathf.Abs(sr.transform.lossyScale.y);
    }

    void ShowBossBar(BattleUnit boss, string bossName)
    {
        if (boss == null) return;
        Canvas canvas = campfirePanel != null ? campfirePanel.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = campfireStatusText != null ? campfireStatusText.font : null;
        if (font == null)
        {
            TMP_Text anyText = canvas.GetComponentInChildren<TMP_Text>(true);
            if (anyText != null) font = anyText.font;
        }

        GameObject go = new GameObject("BossHealthBar", typeof(RectTransform));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsFirstSibling(); // 결과창·모닥불 창이 위에 그려지도록
        go.AddComponent<BossHealthBar>().Setup(boss, bossName, font);
    }

    // 방금 소환한 적에게 현재 의뢰 랭크의 능력치 배율을 적용 (미믹처럼 다른 곳에서 소환한 적도 이걸 호출)
    // statsRankDown: 의뢰 랭크보다 몇 단계 낮은 배율을 쓸지 (아래 랭크로 뽑힌 적, D 밑으로는 내려가지 않음)
    public void ApplyQuestRankScaling(GameObject enemy, int statsRankDown = 0)
    {
        if (enemy == null || GameManager.Instance == null || GameManager.Instance.currentQuest == null) return;

        BattleUnit unit = enemy.GetComponent<BattleUnit>();
        if (unit == null) return;

        if (!EnemyScalingRules.TryParseRank(GameManager.Instance.currentQuest.rank, out RankType rank)) return;
        if (statsRankDown > 0) rank = (RankType)Mathf.Max(0, (int)rank - statsRankDown);

        EnemyScalingSettings settings = GameManager.Instance.enemyScaling;
        unit.SetRankScaling(
            EnemyScalingRules.GetHpMultiplier(settings, rank),
            EnemyScalingRules.GetAttackMultiplier(settings, rank));
    }

    [Header("아이소메트릭 적 대형")]
    [Tooltip("(더 이상 쓰지 않음, 스폰 산개의 enemySpawnSpacing 사용)")]
    public float isoEnemySpacing = 1.2f;

    // 아이소메트릭 칸 방향(오른쪽 위 / 오른쪽 아래)을 따라 적을 마름모꼴로 채워 나가는 순서

    // 아이소메트릭 적 대형: 세로 줄(열)로 채움. 한 열에 enemyRowsPerColumn명을 위아래로 고르게, 열은 오른쪽(적 쪽)으로 늘어남
    // 마름모 바닥은 가운데가 넓고 오른쪽 끝으로 갈수록 좁아지므로, 예전처럼 오른쪽으로 갈수록 위아래로 벌어지는 대형보다 바닥에 잘 맞음
    // 바닥 밖으로 나간 자리는 ScatterOnFloor가 가장 가까운 바닥으로 옮김
    Vector3 GetIsoFormationOffset(int index)
    {
        int rows = Mathf.Max(1, enemyRowsPerColumn);
        int column = index / rows, row = index % rows;
        float y = (row - (rows - 1) * 0.5f) * enemyRowSpacing;
        float x = column * enemyColumnSpacing + (row % 2) * enemyColumnSpacing * 0.5f; // 줄마다 반 칸 어긋나게
        return new Vector3(x, y, 0f);
    }

    // --- 3. 유닛 사망 처리 ---
    [Tooltip("사제만 남으면 자동 도주. 예전 사제 AI(다친 아군이 있으면 적을 안 때림)의 교착을 막던 임시 처리. 지금 사제는 늘 적을 때리므로 기본 꺼짐")]
    public bool fleeIfOnlyHealersLeft = false;

    // (임시) 사제만 남으면 자동 도주
    void FleeIfOnlyHealersLeft()
    {
        if (!fleeIfOnlyHealersLeft) return;
        if (!isBattleActive || gameEnded) return;
        int alive = 0;
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Player"))
        {
            BattleUnit unit = obj.GetComponent<BattleUnit>();
            if (unit == null || unit.isDead) continue;
            if (!(unit is Ally_Healer)) return; // 사제가 아닌 아군이 있으면 계속 싸움
            alive++;
        }
        if (alive == 0) return;
        Debug.Log($"🏃 [자동 도주] 사제만 {alive}명 남아 싸움을 끝낼 수 없어 도주합니다.");
        OnClickFleeButton();
    }

    public void OnUnitDead(bool isPlayerSide)
    {
        if (isPlayerSide)
        {
            // 아군 수는 전투 중이 아닐 때(스테이지 사이)에도 정확히 셈
            allyCount--;
            if (allyCount <= 0 && !gameEnded)
            {
                isBattleActive = false;
                GameOver(false);
            }
            else FleeIfOnlyHealersLeft();
            return;
        }

        if (!isBattleActive) return;

        // 적 사망 처리
        {
            enemyCount--;

            // 미믹 처치 보너스: 추가 골드 지급
            if (mimicBonusPending && enemyCount <= 0)
            {
                int bonusGold = ApplyGoldGain(Random.Range(50, 151)); // 덜렁거리는 성격
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.gold += bonusGold;
                    GameManager.Instance.lastEarnedGold += bonusGold; // 결과창의 획득 골드에 포함
                    mimicBonusTotal += bonusGold;
                    Debug.Log($"💎 [미믹 보너스] 미믹을 처치해 추가 {bonusGold}G 획득!");
                }
                mimicBonusPending = false;
            }

            if (enemyCount <= 0 && !isRoomCleared)
            {
                if (wavesLeft > 0)
                {
                    // 웨이브 방: 다음 무리가 남아 있으면 상자 대신 다음 웨이브
                    StartCoroutine(NextWaveAfterDelay());
                }
                else
                {
                    isRoomCleared = true;
                    isBattleActive = false;
                    SpawnTreasureChest();
                }
            }
        }
    }

    void SpawnTreasureChest()
    {
        Vector3 chestSpawnPos = new Vector3(4.0f, 0f, 0f); 
        GameObject chest = Instantiate(chestPrefab, chestSpawnPos, Quaternion.identity);
        lastChestPos = chest.transform.position;

        // 보스방 상자는 미믹이 될 수 없음 (Start 전에 설정해야 미믹 판정에 반영됨)
        bool isBossRoom = layout != null && currentStageIndex == layout.bossRoom;
        TreasureChest treasure = chest.GetComponent<TreasureChest>();
        if (treasure != null && isBossRoom) treasure.canBeMimic = false;

        OrderToLoot(chest.transform);
    }

    Vector3 lastChestPos; // 마지막 상자 위치 (장비 이름을 띄울 곳)

    void RollEquipmentReward(bool bossRoom)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || gm.currentDispatchParty == null) return;
        float chance = bossRoom ? gm.equipment.bossDropChance : gm.equipment.chestDropChance;
        if (Random.value >= chance) return;
        EquipmentItem item = gm.RollEquipmentDrop(GetQuestRank(), gm.currentDispatchParty.members);
        earnedEquipment.Add(item);
        Vector3 pos = lastChestPos;
        SpawnFloatingText(pos + Vector3.up * 0.9f, EquipmentRules.Name(gm.equipment, item), new Color(1f, 0.85f, 0.4f));
        Debug.Log($"🗡️ [장비 발견] {EquipmentRules.Name(gm.equipment, item)}");
    }

    public void AddItemToLootBag(MonsterDropData item)
    {
        if (item != null)
        {
            earnedItems.Add(item);
            Debug.Log($"💎 전리품 획득: {item.itemName}");
        }
    }

    // --- 4. 게임 종료 및 UI ---
    private bool battleWon = false; // 로비 복귀 시 보상 지급 여부 판단용
    private bool gameEnded = false; // 승리/패배 판정이 이미 났는가

    public void GameOver(bool isWin)
    {
        if (gameEnded) return;
        gameEnded = true;

        battleWon = isWin;
        if (!isWin) partyWiped = true; // 전멸: 어떤 의뢰든 실패
        if (isWin) { if (victoryPanel != null) victoryPanel.SetActive(true); }
        else       { if (defeatPanel != null) defeatPanel.SetActive(true); }
        if (isWin) DeliverGatheredItems();

        if (GameManager.Instance != null)
        {
            // 전멸하면 들고 나올 사람이 없으므로 전리품과 미믹 보너스 골드를 모두 잃음
            // (도주는 EscapeToLobby에서 따로 처리하며, 그때는 전리품을 가지고 나옴)
            if (!isWin)
            {
                if (earnedItems.Count > 0)
                    Debug.Log($"💀 [전멸] 전리품 {earnedItems.Count}개를 던전에 두고 왔습니다.");
                earnedItems.Clear();
                earnedEquipment.Clear();

                GameManager.Instance.gold -= mimicBonusTotal;
                GameManager.Instance.lastEarnedGold -= mimicBonusTotal;
                mimicBonusTotal = 0;
            }

            // 결과창 제목(의뢰 성공/실패)은 보스방 클리어가 아니라 의뢰 성공 여부로 (재료가 모자라면 보스를 잡아도 실패)
            GameManager.Instance.AddLootToGuild(earnedItems, isWin && QuestSucceeded(), earnedEquipment);
        }
    }

    // --- 5. 로비로 돌아가기 ---
    public void OnClick_ReturnToLobby()
    {
        if (GameManager.Instance != null)
        {
            // 1. 상태 동기화
            SyncAdventurerStatus();

            // 2. 파티 및 퀘스트 정산
            if (GameManager.Instance.currentDispatchParty != null)
            {
                GameManager.Instance.currentDispatchParty.state = PartyState.Idle;

                // 보상, 의뢰 완수 처리, 경험치는 의뢰에 성공했을 때만 (실패하면 전리품만 가지고 귀환)
                //  - 보스 토벌·유해 수습: 보스방 클리어 / 재료 수급: 재료를 다 모아 돌아옴 / 구출: 구하고 돌아옴
                bool succeeded = QuestSucceeded();
                RecordResultAndEnding(succeeded, GameManager.Instance.currentDispatchParty.members);
                if (succeeded)
                {
                    PayQuestSuccess();

                    // 유해 수습 퀘스트 승리 시 → 유해 수습 보상 지급
                    if (GameManager.Instance.currentQuest != null && GameManager.Instance.currentQuest.isCorpseQuest)
                        GameManager.Instance.CompleteCorpseQuest(GameManager.Instance.currentQuest);

                    // 생환 파티원 경험치 지급
                    string rank = GameManager.Instance.currentQuest != null
                        ? GameManager.Instance.currentQuest.rank : "D";
                    GameManager.Instance.GainExpAfterBattle(
                        GameManager.Instance.currentDispatchParty.members, rank);
                }

                // 부상자도 파티에 그대로 남음 (요양 기간만 매겨짐)

                GameManager.Instance.currentDispatchParty = null;
                GameManager.Instance.CleanupCurrentQuest();
            }
        }
        SceneManager.LoadScene("LobbyScene");
    }

    private void CheckRoomClear()
    {
        if (isRoomCleared) return;
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0)
        {
            isRoomCleared = true;
            SpawnTreasureChest();
        }
    }

    private void OrderToLoot(Transform chestTransform)
    {
        GameObject[] allies = GameObject.FindGameObjectsWithTag("Player");
        if (allies.Length > 0)
        {
            BattleUnit looter = allies[0].GetComponent<BattleUnit>();
            if (looter != null) looter.CommandLoot(chestTransform);
        }
    }

    [Header("데미지 텍스트")]
    [Tooltip("비워 두면 기본 TMP 폰트를 사용 (숫자만 표시하므로 한글 폰트가 아니어도 됨)")]
    public TMP_FontAsset damageTextFont;
    public Color damageTextColor = Color.white;
    public Color critTextColor   = new Color(1f, 0.85f, 0.2f);
    public Color healTextColor   = new Color(0.4f, 1f, 0.5f);
    public Color blockTextColor  = new Color(0.6f, 0.8f, 1f);
    public float damageTextSize      = 4f;    // 월드 공간 글자 크기
    public float damageTextLifetime  = 0.8f;  // 떠 있는 시간(초)
    public float damageTextRiseSpeed = 1.2f;  // 초당 떠오르는 높이
    public Vector2 damageTextOffset  = new Vector2(0f, 0.6f); // 유닛 위치 기준 시작 위치
    public float damageTextJitter    = 0.25f; // 여러 개가 겹치지 않도록 좌우로 흩는 폭
    public int damageTextSortingOrder = 1000; // 유닛/맵보다 항상 위에 그려지도록
    public float damageTextPopScale = 1.5f;   // 처음 나올 때 크기 배율 (곧 1로 줄어듦)
    public float damageTextPopTime  = 0.12f;  // 원래 크기로 줄어드는 시간(초)
    public float critTextScale      = 1.4f;   // 치명타 숫자 크기 배율
    [Tooltip("글자 뒤 그림자 위치 (월드 유닛, 0이면 그림자 없음). 도트 폰트라 외곽선 대신 그림자를 씀")]
    public Vector2 damageTextShadowOffset = new Vector2(0.06f, -0.06f);
    public Color damageTextShadowColor = new Color(0.08f, 0.05f, 0.03f, 1f);

    public void ShowDamageText(Vector3 pos, float damage, bool crit)
    {
        if (damage <= 0f) return;
        SpawnFloatingText(pos, Mathf.Max(1, Mathf.RoundToInt(damage)) + (crit ? "!" : ""), crit ? critTextColor : damageTextColor, crit ? critTextScale : 1f);
    }

    public void ShowHealText(Vector3 pos, float amount)
    {
        if (amount <= 0f) return;
        SpawnFloatingText(pos, "+" + Mathf.Max(1, Mathf.RoundToInt(amount)), healTextColor);
    }

    // 공격을 막아 피해가 없을 때 (데미지 텍스트 폰트가 한글을 지원하지 않을 수 있어 영문으로 표시)
    public void ShowBlockText(Vector3 pos)
    {
        SpawnFloatingText(pos, "BLOCK", blockTextColor);
    }

    // 피해 면역(도적 은신 등)으로 피해를 받지 않았을 때
    public void ShowImmuneText(Vector3 pos)
    {
        SpawnFloatingText(pos, "IMMUNE", blockTextColor);
    }

    // 회피(MISS), 게으른 성격(Zzz), 다혈질 돌진(!) 같은 짧은 상태 표시 (폰트 때문에 영문·기호만)
    public void ShowStatusText(Vector3 pos, string message)
    {
        SpawnFloatingText(pos, message, blockTextColor);
    }

    void SpawnFloatingText(Vector3 pos, string message, Color color, float scale = 1f)
    {
        Vector3 spawnPos = pos + (Vector3)damageTextOffset
            + new Vector3(Random.Range(-damageTextJitter, damageTextJitter), 0f, 0f);

        // 프리팹이 연결돼 있으면 그 모양을 쓰고, 없으면 글자 오브젝트를 바로 만듦
        GameObject go = damageTextPrefab != null
            ? Instantiate(damageTextPrefab, spawnPos, Quaternion.identity)
            : new GameObject("DamageText");
        go.transform.position = spawnPos;

        TMP_Text label = go.GetComponentInChildren<TMP_Text>();
        if (label == null)
        {
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = damageTextSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.sortingOrder = damageTextSortingOrder;
            if (damageTextFont != null) tmp.font = damageTextFont;
            label = tmp;
        }

        FloatingText floating = go.GetComponent<FloatingText>();
        if (floating == null) floating = go.AddComponent<FloatingText>();
        // 그림자: 같은 글자를 어둡게 한 칸 뒤에
        TMP_Text shadow = null;
        if (damageTextShadowOffset != Vector2.zero && label is TextMeshPro worldLabel)
        {
            GameObject sg = new GameObject("Shadow");
            sg.transform.SetParent(go.transform, false);
            sg.transform.localPosition = (Vector3)damageTextShadowOffset;
            TextMeshPro st = sg.AddComponent<TextMeshPro>();
            st.font = worldLabel.font;
            st.fontSize = worldLabel.fontSize;
            st.fontStyle = worldLabel.fontStyle;
            st.alignment = worldLabel.alignment;
            st.textWrappingMode = worldLabel.textWrappingMode;
            st.sortingOrder = worldLabel.sortingOrder - 1;
            st.rectTransform.sizeDelta = worldLabel.rectTransform.sizeDelta;
            st.text = message;
            st.color = damageTextShadowColor;
            shadow = st;
        }

        floating.Play(label, message, color, damageTextLifetime, damageTextRiseSpeed, scale, damageTextPopScale, damageTextPopTime, shadow);
    }

    public void OnClickFleeButton()
    {
        if (!isBattleActive) return;

        isBattleActive = false;
        gameEnded = true; // 도주 뒤 날아가던 투사체에 누가 쓰러져도 전멸 판정이 다시 나지 않도록

        // 아군 유닛 비활성화 (전투 중지)
        GameObject[] allies = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject obj in allies)
        {
            BattleUnit u = obj.GetComponent<BattleUnit>();
            if (u != null) u.enabled = false;
        }

        // 적 유닛도 멈추기
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject obj in enemies)
        {
            BattleUnit u = obj.GetComponent<BattleUnit>();
            if (u != null) u.enabled = false;
        }

        StartCoroutine(EscapeToLobby());
    }

    private IEnumerator EscapeToLobby()
    {
        // 도주 결과 산출. 재료를 다 모았거나 구출을 마친 뒤의 도주는 의뢰 성공 (도주에 따른 낙오·사기 하락은 그대로)
        if (GameManager.Instance != null && GameManager.Instance.currentDispatchParty != null)
        {
            bool success = QuestSucceeded();
            if (success) DeliverGatheredItems();
            List<Adventurer> members = new List<Adventurer>(GameManager.Instance.currentDispatchParty.members);

            ApplyFleeConsequences();
            GameManager.Instance.AddLootToGuild(earnedItems, false, earnedEquipment);
            RecordResultAndEnding(success, members);

            if (success)
            {
                PayQuestSuccess();
                string rank = GameManager.Instance.currentQuest != null ? GameManager.Instance.currentQuest.rank : "D";
                GameManager.Instance.GainExpAfterBattle(members.FindAll(m => !m.isDead && !m.isStranded), rank);
                fleeResultText = $"<color=#7CFC7C>의뢰 성공!</color>\n" + fleeResultText;
            }
        }

        // 도주 패널 표시 (있으면)
        if (fleePanel != null)
        {
            fleePanel.SetActive(true);

            // 도주 결과 텍스트 갱신
            TMPro.TextMeshProUGUI fleePanelText = fleePanel.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (fleePanelText != null)
                fleePanelText.text = "도주 성공!\n\n" + fleeResultText;

            // 패널에 로비 복귀 버튼이 있으면 거기서 씬 전환 (없으면 3초 후 자동 이동)
            if (fleePanel.GetComponentInChildren<UnityEngine.UI.Button>() != null)
                yield break; // 버튼 클릭으로 복귀
        }

        yield return new WaitForSeconds(2.0f);
        SceneManager.LoadScene("LobbyScene");
    }

    // 도주 결과 요약 (ApplyFleeConsequences가 파티 정리 전에 만들어 둠)
    private string fleeResultText = "";

    // 도주 결과 요약 텍스트 생성. 전사자/낙오자는 곧 파티에서 빠지므로 정리하기 전의 명단을 받음
    private string BuildFleeResultText(List<Adventurer> members)
    {
        string result = "";
        foreach (Adventurer member in members)
        {
            if (member.isDead)
                result += $"<color=red>{member.name} - 전사</color>\n";
            else if (member.isStranded)
                result += $"<color=orange>{member.name} - 낙오 (구출 기한: {member.strandedWeeksRemaining}주)</color>\n";
            else if (member.morale < 0f)
                result += $"<color=yellow>{member.name} - 사기 하락 (현재 -{MoraleRules.Percent(member.morale)}%)</color>\n";
            else
                result += $"{member.name} - 무사 귀환\n";
        }
        return result;
    }

    // 같은 파티에 살아 있는 의형제가 있는가
    bool HasLivingSwornSibling(Adventurer member, GameObject[] allyObjects)
    {
        foreach (GameObject obj in allyObjects)
        {
            BattleUnit u = obj.GetComponent<BattleUnit>();
            if (u == null || u.isDead || u.SourceData == null || u.SourceData == member) continue;
            if (GameManager.Instance.GetRelation(member, u.SourceData) == RelationType.SwornSibling) return true;
        }
        return false;
    }

    // 도주 시 낙오 + 사기 하락 처리
    private void ApplyFleeConsequences()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentDispatchParty == null) return;

        GameObject[] allyObjects = GameObject.FindGameObjectsWithTag("Player");
        List<Adventurer> strandedList  = new List<Adventurer>();
        List<Adventurer> returnedList  = new List<Adventurer>();
        List<Adventurer> deadList      = new List<Adventurer>();

        foreach (Adventurer member in GameManager.Instance.currentDispatchParty.members)
        {
            string unitName = $"Unit_{member.name}";
            BattleUnit unitScript = null;
            foreach (GameObject obj in allyObjects)
            {
                if (obj.name == unitName) { unitScript = obj.GetComponent<BattleUnit>(); break; }
            }

            // 전사 판정
            bool isDead    = (unitScript == null || unitScript.isDead);
            // 낙오 판정: 남은 체력이 적을수록(경상 < 중상 < 빈사) 낙오 확률이 높음
            float hpRatio = (!isDead && unitScript.maxHp > 0f) ? unitScript.currentHp / unitScript.maxHp : 0f;
            float strandRoll = Random.value;
            if (!isDead && HasLivingSwornSibling(member, allyObjects))
                strandRoll = Mathf.Min(1f, strandRoll / Mathf.Max(0.01f, GameManager.Instance.relationship.swornStrandMultiplier)); // 의형제는 서로 낙오시키지 않음
            bool isStranded = !isDead && FleeRules.RollStranded(GameManager.Instance.flee, hpRatio, strandRoll);

            if (isDead)
            {
                member.isDead     = true;
                member.currentHp  = 0;
                GameManager.Instance.NoteFallen(member);
                deadList.Add(member);
            }
            else if (isStranded)
            {
                // 부상자 → 낙오
                member.currentHp              = unitScript.GetReturnHp(member); // 출발 시 최대 체력 대비 비율
                member.isStranded             = true;
                member.strandedWeeksRemaining = 4; // 4주 안에 구출하지 못하면 사망
                member.assignedPartyIndex     = -1;
                // 구출 기한이 지나면 전사자가 되므로 낙오한 의뢰 이름을 미리 기록 (유해 수습 의뢰에 사용)
                member.diedAtQuestName        = GameManager.Instance.currentQuest != null
                    ? GameManager.Instance.currentQuest.questName : "알 수 없는 던전";
                strandedList.Add(member);
                Debug.Log($"😨 [낙오] {member.name}이(가) 부상 상태로 던전에 남겨졌습니다. (구출 기한: 4주)");
                GameManager.Instance.TryGiveAffliction(member, AfflictionCause.Stranded);
            }
            else
            {
                // 복귀 (다쳤다면 승리 귀환 때와 같은 부상 처리)
                member.currentHp = unitScript.GetReturnHp(member); // 출발 시 최대 체력 대비 비율로 부상 판정
                ApplyInjuryIfHurt(member);
                returnedList.Add(member);
            }
        }

        // 사기 하락: 복귀 파티원 전체 능력치 10~20% 랜덤 하락
        if (returnedList.Count > 0)
        {
            float penalty = Random.Range(0.10f, 0.20f);
            foreach (Adventurer adv in returnedList)
            {
                adv.morale = MoraleRules.Clamp(GameManager.Instance.moraleSettings, adv.morale - penalty * GameManager.Instance.GetMoraleSwing(adv)); // 지금 사기에서 뺌 (오른 사기가 먼저 깎임, 나르시시스트는 크게)
                Debug.Log($"😞 [사기 하락] {adv.name} - 능력치 {penalty * 100f:F0}% 감소 (다음 파견 적용)");
            }
        }

        // 결과창 문구는 전사자/낙오자를 파티에서 빼기 전에 만들어 둠
        fleeResultText = BuildFleeResultText(GameManager.Instance.currentDispatchParty.members);

        // 파티 케미: 도주는 완수가 아님. 사망자가 있으면 남은 파티원 우울함 판정 (사망자를 빼기 전에)
        GameManager.Instance.ResolvePartyChemistry(GameManager.Instance.currentDispatchParty, false, deadList);

        // 전사자: 파티 & 모험가 명단에서 제거, 유해 수습 목록에 보존
        string questName = GameManager.Instance.currentQuest != null
            ? GameManager.Instance.currentQuest.questName : "알 수 없는 던전";
        foreach (Adventurer dead in deadList)
        {
            dead.diedAtQuestName = questName;
            GameManager.Instance.currentDispatchParty.members.Remove(dead);
            GameManager.Instance.adventurers.Remove(dead);
            if (!GameManager.Instance.deadAdventurers.Contains(dead))
                GameManager.Instance.deadAdventurers.Add(dead);
            Debug.Log($"💀 [전사] {dead.name}이(가) '{questName}'에서 사망했습니다. (유해 수습 가능)");
        }

        // 낙오자: 파티에서만 제거 (adventurers 명단은 유지 → 나중에 구출 가능)
        foreach (Adventurer stranded in strandedList)
        {
            GameManager.Instance.currentDispatchParty.members.Remove(stranded);
        }

        // 도주로 함께 돌아온 인원은 다쳤더라도 파티에 그대로 남음 (파티 유지. 요양 기간만 매겨짐)

        GameManager.Instance.currentDispatchParty.state = PartyState.Idle;
        GameManager.Instance.currentDispatchParty = null;
        GameManager.Instance.CleanupCurrentQuest();
    }

    public void OnChestOpened()
    {
        CancelInvoke("CheckRoomClear");
        isRoomCleared = false;

        roomCleared[currentStageIndex] = true;
        if (currentStageIndex == CaptiveRoom && !captiveRescued) RescueCaptive();

        // 방 종류에 따른 골드 보상
        DungeonRoomType type = layout.rooms[currentStageIndex].type;
        if (type == DungeonRoomType.Treasure)
            GrantRoomGold(DungeonRoomRules.RollTreasureGold(roomSettings, GetQuestRank(), Random.value), "보물 상자");
        else if (type == DungeonRoomType.Wave)
            GrantRoomGold(DungeonRoomRules.RollWaveGold(roomSettings, GetQuestRank(), Random.value), "웨이브 보상");

        // 장비: 상자에서 낮은 확률, 보스를 쓰러뜨리면 더 높은 확률 (등급 = 의뢰 랭크)
        RollEquipmentReward(currentStageIndex == layout.bossRoom);

        // 보스방을 정리하면 던전 클리어 (구출 의뢰인데 아직 못 구했으면 계속 찾아야 함)
        if (currentStageIndex == layout.bossRoom)
        {
            bossDefeated = true;
            if (CaptiveRoom >= 0 && !captiveRescued)
            {
                Debug.Log($"👑 보스를 쓰러뜨렸지만 {CurrentQuest.captiveName}을(를) 아직 찾지 못했습니다. 던전을 더 살펴봐야 합니다.");
            }
            else
            {
                Debug.Log("🎉 던전 클리어!");
                GameOver(true);
                return;
            }
        }

        // 보스를 먼저 쓰러뜨리고 이 방에서 구출을 마쳤으면 던전 클리어
        if (FinishIfRescuedAfterBoss()) return;

        // 그 밖의 방은 모닥불에서 쉬고 갈 방향을 고름
        if (campfirePanel != null) EnterCampfire(campfireHealRatio);
        else MoveToDefaultExit();
    }

    // ─── 모닥불 휴식 (방과 방 사이) ──────────────────────────────────
    // 방을 정리하고 상자를 열면 모닥불에서 체력을 조금 회복하고, 어느 방향으로 갈지 또는 돌아갈지 고름.
    // 이미 정리한 방에 되돌아왔을 때도 같은 화면에서 방향을 고르지만 회복은 없음 (방마다 처음 정리했을 때 한 번만).
    // 여기서 돌아가면 낙오 판정 없이 전리품을 들고 귀환함 (의뢰 보상과 경험치는 없음)

    [Header("모닥불 휴식 (방과 방 사이)")]
    public GameObject campfirePanel;               // 휴식 화면 (Tools > 던전 모닥불 세팅으로 생성)
    public TextMeshProUGUI campfireStatusText;     // 파티 상태 표시
    public GameObject campfireObject;              // 맵 위의 모닥불 (쉬는 동안만 보임)
    [Range(0f, 1f)]
    public float campfireHealRatio = 0.3f;         // 휴식 한 번에 회복하는 양 (최대 체력 대비)
    public float campfireGatherRadius = 1.1f;      // 모닥불에서 아군이 둘러앉는 거리
    public float revisitDelay = 0.6f;              // 정리한 방에 되돌아왔을 때 방향 선택 화면이 뜨기까지의 시간(초)

    private bool isResting = false;
    private DungeonMinimap minimap; // 우상단에 항상 떠 있는 미니맵 (첫 방에 들어갈 때 만들어짐)

    // 화면 캔버스에 미니맵을 붙임 (모닥불 창이 있는 캔버스를 우선 사용)
    void EnsureMinimap()
    {
        if (minimap != null) return;
        Canvas canvas = campfirePanel != null ? campfirePanel.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = campfireStatusText != null ? campfireStatusText.font : null;
        if (font == null)
        {
            TMP_Text anyText = canvas.GetComponentInChildren<TMP_Text>(true);
            if (anyText != null) font = anyText.font;
        }

        GameObject go = new GameObject("Minimap", typeof(RectTransform));
        go.layer = canvas.gameObject.layer;
        go.transform.SetParent(canvas.transform, false);
        minimap = go.AddComponent<DungeonMinimap>();
        minimap.Setup(this, font);
    }

    private DungeonMapPanel mapPanel;              // 모닥불 화면 안의 지도와 방향 버튼 (처음 열 때 만들어짐)

    // 지금 방향을 고르는 중인가, 그리고 이번에 회복이 있었는가 (자동 플레이/검증 도구용)
    public bool IsResting => isResting;
    public bool LastRestHealed { get; private set; }

    [Header("모닥불 둘러앉기")]
    [Tooltip("모닥불에서 앉는 자리까지의 세로 거리 비율 (가로 campfireGatherRadius × 이 값, 아이소메트릭이라 납작한 타원)")]
    public float seatEllipseY = 0.55f;
    [Tooltip("앉을 때 통나무에서 모닥불 쪽으로 이만큼 나와 앉음 (엉덩이가 통나무에 걸치게, 월드 단위)")]
    public float seatForward = 0.14f;
    [Tooltip("앉을 때 발이 통나무 기준점보다 이만큼 아래 (월드 단위)")]
    public float seatDrop = 0.04f;
    [Tooltip("모험가들이 자리에 앉는 모습을 보여 준 뒤 휴식 창을 띄우기까지 최대 시간(초)")]
    public float campfirePanelDelay = 2.2f;
    [Tooltip("모닥불과 둘레 자리가 화면 가로 이 비율 안에 들어오는 곳을 고름 (오른쪽은 휴식 창 자리, 0~1)")]
    [Range(0f, 1f)] public float campfireMaxViewportX = 0.46f;

    private Coroutine campfirePanelRoutine;
    private bool campfireWindowPlaced;

    // healRatio: 이번에 회복하는 양 (최대 체력 대비). 0이면 회복 없이 방향만 고름
    void EnterCampfire(float healRatio)
    {
        bool heal = healRatio > 0f;
        isResting = true;
        LastRestHealed = heal;

        List<BattleUnit> survivors = new List<BattleUnit>();
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Player"))
        {
            BattleUnit unit = obj.GetComponent<BattleUnit>();
            if (unit != null && !unit.isDead) survivors.Add(unit);
        }

        // 모닥불 자리와 둘레의 앉을 자리 (파티 가까이, 바닥 위, 사이에 구멍 없이)
        Vector3 fallback = campfireObject != null ? campfireObject.transform.position : Vector3.zero;
        Vector3 center = ChooseCampfireSpot(survivors, fallback, out List<Vector3> seats);

        CampfireVisual visual = campfireObject != null ? campfireObject.GetComponent<CampfireVisual>() : null;
        if (campfireObject != null)
        {
            campfireObject.transform.position = center;
            campfireObject.SetActive(true);
        }
        if (visual != null) visual.Light(center, seats, FloorSortingOrder());

        for (int i = 0; i < survivors.Count; i++)
        {
            BattleUnit unit = survivors[i];

            // 회복 (실제로 찬 만큼만 표시)
            if (heal)
            {
                float before = unit.currentHp;
                unit.currentHp = Mathf.Min(unit.currentHp + unit.maxHp * healRatio, unit.maxHp);
                ShowHealText(unit.transform.position, unit.currentHp - before);
                unit.NotifyHealed(); // 겁쟁이 성격은 모닥불에서 회복해도 다시 싸움
            }

            // 자리에 앉음: 통나무에서 모닥불 쪽으로 조금 나와 엉덩이를 걸침 (그림의 발 위치 보정), 모닥불을 바라봄
            Vector3 seat = seats[i];
            Vector3 toFire = center - seat;
            toFire.z = 0f;
            Vector3 feet = seat + toFire.normalized * seatForward + new Vector3(0f, -seatDrop, 0f);
            SpriteRenderer body = unit.GetComponent<SpriteRenderer>();
            float feetOffset = body != null ? body.bounds.min.y - unit.transform.position.y : 0f;
            unit.CommandRest(feet - new Vector3(0f, feetOffset, 0f), center);
            if (visual != null) visual.SortRenderer(body, feet.y); // 위쪽 자리는 통나무 앞, 아래쪽 자리(등을 보임)는 통나무 뒤
        }

        if (campfireStatusText != null) campfireStatusText.text = BuildCampfireStatusText(survivors, healRatio);

        // 지도와 방향 버튼
        if (mapPanel == null)
        {
            mapPanel = campfirePanel.GetComponent<DungeonMapPanel>();
            if (mapPanel == null) mapPanel = campfirePanel.AddComponent<DungeonMapPanel>();
        }

        // 둘러앉는 모습을 잠깐 보여 준 뒤 휴식 창 (창은 화면 오른쪽). 이미 정리한 방에 되돌아왔을 때는 짧게
        if (campfirePanelRoutine != null) StopCoroutine(campfirePanelRoutine);
        campfirePanelRoutine = StartCoroutine(ShowCampfirePanel(survivors, heal ? campfirePanelDelay : Mathf.Min(campfirePanelDelay, 0.8f)));

        if (heal)
            Debug.Log($"🔥 [모닥불] 방 {currentStageIndex + 1} 정리 완료 (정리한 방 {ClearedRoomCount}개). 생존자 {survivors.Count}명이 체력을 {healRatio * 100f:F0}% 회복했습니다.");
    }

    IEnumerator ShowCampfirePanel(List<BattleUnit> survivors, float maxDelay)
    {
        float t = 0f;
        while (t < maxDelay && isResting)
        {
            bool allSeated = true;
            foreach (BattleUnit u in survivors) if (u != null && !u.isDead && !u.IsSeated) { allSeated = false; break; }
            if (allSeated && t > 0.6f) break;
            t += Time.deltaTime;
            yield return null;
        }
        campfirePanelRoutine = null;
        if (!isResting) yield break;

        PlaceCampfireWindow();
        campfirePanel.SetActive(true);
        campfirePanel.transform.SetAsLastSibling();
        mapPanel.Show(this, campfireStatusText);
    }

    // 휴식 창을 화면 오른쪽으로 옮기고 배경을 옅게 (모닥불이 보이도록). 처음 한 번만
    void PlaceCampfireWindow()
    {
        if (campfireWindowPlaced || campfirePanel == null) return;
        campfireWindowPlaced = true;
        Image dim = campfirePanel.GetComponent<Image>();
        if (dim != null) dim.color = new Color(0f, 0f, 0f, 0.12f);
        RectTransform window = campfirePanel.transform.Find("Window") as RectTransform;
        if (window == null) return;
        window.anchorMin = window.anchorMax = new Vector2(1f, 0.5f);
        window.pivot = new Vector2(1f, 0.5f);
        window.anchoredPosition = new Vector2(-40f, 0f);
    }

    int FloorSortingOrder()
    {
        MapGenerator map = MapGenerator.Instance;
        if (map == null || map.floorTilemap == null) return 0;
        UnityEngine.Tilemaps.TilemapRenderer r = map.floorTilemap.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
        return r != null ? r.sortingOrder : 0;
    }

    // 둘레 자리 (모닥불을 가운데 두고 빙 둘러, 화면 바로 아래쪽은 비워서 불이 가려지지 않게)
    List<Vector3> SeatRing(Vector3 center, int count)
    {
        var list = new List<Vector3>(count);
        float rx = campfireGatherRadius, ry = campfireGatherRadius * seatEllipseY;
        float step = 360f / Mathf.Max(1, count);
        float start = 270f + step * 0.5f; // 아래 정면(270도)을 피해 반 칸 돌림
        for (int i = 0; i < count; i++)
        {
            float a = (start + step * i) * Mathf.Deg2Rad;
            list.Add(center + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f));
        }
        return list;
    }

    // 파티 가운데에서 가까운 바닥 칸 중, 화덕과 둘레 자리가 모두 바닥이고 사이에 구멍이 없는 곳
    Vector3 ChooseCampfireSpot(List<BattleUnit> survivors, Vector3 fallback, out List<Vector3> seats)
    {
        int count = Mathf.Max(1, survivors.Count);
        Vector3 centroid = fallback;
        if (survivors.Count > 0)
        {
            centroid = Vector3.zero;
            foreach (BattleUnit u in survivors) centroid += u.transform.position;
            centroid /= survivors.Count;
        }

        MapGenerator map = MapGenerator.Instance;
        if (map == null || map.floorTilemap == null || map.floorTilemap.GetUsedTilesCount() == 0)
        {
            seats = SeatRing(centroid, count);
            return centroid;
        }

        var tm = map.floorTilemap;
        var candidates = new List<Vector3>();
        foreach (Vector3Int cell in tm.cellBounds.allPositionsWithin)
        {
            if (!tm.HasTile(cell)) continue;
            Vector3 c = tm.GetCellCenterWorld(cell);
            c.z = centroid.z;
            candidates.Add(c);
        }
        candidates.Sort((a, b) => (a - centroid).sqrMagnitude.CompareTo((b - centroid).sqrMagnitude));

        Camera cam = Camera.main;
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (Vector3 c in candidates)
            {
                if (!map.IsOnFloor(c + new Vector3(-0.65f, 0f, 0f)) || !map.IsOnFloor(c + new Vector3(0.65f, 0f, 0f))) continue; // 화덕 폭
                List<Vector3> ring = SeatRing(c, count);
                bool ok = true;
                foreach (Vector3 s in ring)
                {
                    if (!map.IsOnFloor(s) || !map.IsLineClear(c, s)) { ok = false; break; }
                    // 처음에는 둘레 자리까지 휴식 창(화면 오른쪽)에 가리지 않는 곳만
                    if (pass == 0 && cam != null && cam.WorldToViewportPoint(s + new Vector3(0.3f, 0f, 0f)).x > campfireMaxViewportX) { ok = false; break; }
                }
                if (!ok) continue;
                seats = ring;
                return c;
            }
        }

        // 마땅한 곳이 없으면 가장 가까운 바닥 칸에 두고, 바닥 밖 자리는 가까운 바닥으로
        Vector3 center = candidates.Count > 0 ? candidates[0] : centroid;
        seats = SeatRing(center, count);
        for (int i = 0; i < seats.Count; i++) seats[i] = map.NearestFloor(seats[i]);
        return center;
    }

    string BuildCampfireStatusText(List<BattleUnit> survivors, float healRatio)
    {
        DungeonRoomType type = layout.rooms[currentStageIndex].type;
        string text;
        if (healRatio <= 0f)
            text = "이미 정리한 방입니다. 어디로 갈지 고르세요.\n";
        else if (type == DungeonRoomType.Rest)
            text = $"휴식 공간입니다. 푹 쉬며 체력을 {healRatio * 100f:F0}% 회복했습니다.\n";
        else
            text = $"방을 정리했습니다. (정리한 방 {ClearedRoomCount}개)\n모닥불에서 쉬며 체력을 {healRatio * 100f:F0}% 회복했습니다.\n";

        if (healRatio > 0f && lastRoomGold > 0)
            text += $"<color=#FFD966>{(type == DungeonRoomType.Wave ? "웨이브를 막아낸 보상" : "보물 상자")} +{lastRoomGold:N0} G</color>\n";

        foreach (BattleUnit unit in survivors)
        {
            string who = unit.SourceData != null
                ? $"{unit.SourceData.name} ({DisplayNames.JobToKorean(unit.SourceData.job)})"
                : unit.name;
            float ratio = unit.maxHp > 0f ? unit.currentHp / unit.maxHp : 0f;
            string color = ratio <= 0.3f ? "#FF7366" : ratio <= 0.7f ? "#FFD966" : "#7CFC7C";
            text += $"\n<color={color}>{who}  HP {Mathf.CeilToInt(unit.currentHp)} / {Mathf.CeilToInt(unit.maxHp)}</color>";
        }

        // 쓰러진 동료
        if (GameManager.Instance != null && GameManager.Instance.currentDispatchParty != null)
        {
            foreach (Adventurer member in GameManager.Instance.currentDispatchParty.members)
            {
                if (survivors.Exists(u => u.SourceData == member)) continue;
                text += $"\n<color=#888888>{member.name} ({DisplayNames.JobToKorean(member.job)})  전사</color>";
            }
        }

        if (earnedItems.Count > 0) text += $"\n\n지금까지 얻은 전리품 {earnedItems.Count}개";
        return QuestProgressText() + text;
    }

    void LeaveCampfire()
    {
        isResting = false;
        if (campfirePanelRoutine != null) { StopCoroutine(campfirePanelRoutine); campfirePanelRoutine = null; }
        if (campfirePanel != null) campfirePanel.SetActive(false);
        if (campfireObject != null) campfireObject.SetActive(false);
    }

    // 모닥불의 방향 버튼: 그 방향으로 이어진 방으로 이동 (direction은 DungeonLayoutRules.North ~ West)
    public void OnClickMove(int direction)
    {
        if (!isResting) return;
        int next = layout.GetNeighbor(currentStageIndex, direction);
        if (next < 0) return;

        LeaveCampfire();
        StartCoroutine(EnterRoomCoroutine(next));
    }

    // 예전 [다음 방으로] 버튼: 방향을 고르지 않고 안 가 본 방 우선으로 이동
    public void OnClickCampfireContinue()
    {
        if (!isResting) return;
        LeaveCampfire();
        MoveToDefaultExit();
    }

    // 모닥불의 [귀환] 버튼: 낙오 판정 없이 전리품만 들고 돌아감 (의뢰는 완수하지 못한 것으로 처리)
    private bool returnedByCampfire = false;

    public void OnClickCampfireReturn()
    {
        if (!isResting) return;
        LeaveCampfire();

        gameEnded = true;
        returnedByCampfire = true; // 던전 완수로 침 (파티 케미)
        Debug.Log($"🏕️ [귀환] 방 {ClearedRoomCount}개를 정리하고 전리품 {earnedItems.Count}개를 챙겨 돌아갑니다.");

        // 재료를 다 모았으면 넘겨줌 (구출을 마쳤으면 의뢰 성공으로 정산은 OnClick_ReturnToLobby에서)
        if (QuestSucceeded()) DeliverGatheredItems();
        if (GameManager.Instance != null)
            GameManager.Instance.AddLootToGuild(earnedItems, false, earnedEquipment);

        OnClick_ReturnToLobby();
    }

    // ─── 의뢰 종류 (보스 토벌 / 재료 수급 / 구출) ──────────────────────

    [Header("의뢰 종류 — 구출")]
    [Tooltip("갇힌 인물 그림 ('Tools > 의뢰 종류 세팅'이 연결)")]
    public Sprite captiveSprite;
    public float captiveScale = 0.6f;

    private bool captiveRescued;   // 구출 대상을 구했는가
    private bool bossDefeated;     // 보스방을 정리했는가 (구출 뒤 더 나아간 보상)
    private bool partyWiped;       // 전멸했는가
    private bool gatherDelivered;  // 재료를 넘겼는가 (넘긴 뒤에는 전리품에서 빠짐)
    private GameObject captiveObject;

    QuestData CurrentQuest => GameManager.Instance != null ? GameManager.Instance.currentQuest : null;

    // 구출 의뢰(인물 구출 / 낙오 모험가 긴급 구출)의 갇힌 방. 구출 의뢰가 아니면 -1
    int CaptiveRoom
    {
        get
        {
            QuestData q = CurrentQuest;
            return q != null && q.questType == QuestType.Rescue && layout != null && q.captiveRoom >= 0 && q.captiveRoom < layout.rooms.Count
                ? q.captiveRoom : -1;
        }
    }

    // ─── 재료 수급: 드랍률 상향 · 다 모으면 중단 · 막판 보장 ─────────────────
    private readonly HashSet<GameObject> gatherSourcePrefabs = new HashSet<GameObject>(); // 의뢰 재료를 떨어뜨리는 적 프리팹
    private readonly HashSet<int> gatherSourceIds = new HashSet<int>();                   // 소환된 재료 몬스터 (의뢰 계산에 들어간 것만)
    private int gatherSourcesRemaining;                                                    // 아직 안 쓰러진 재료 몬스터 수 (안 가 본 방 포함)

    void InitGatherTracking()
    {
        gatherSourcePrefabs.Clear();
        gatherSourceIds.Clear();
        gatherSourcesRemaining = 0;
        QuestData q = CurrentQuest;
        if (q == null || q.questType != QuestType.Gather || q.gatherItem == null) return;

        gatherSourcesRemaining = DropTableLookup.CountSources(dungeonStages, layout, q.gatherItem, out float sumRates);
        for (int i = 0; i < dungeonStages.Count; i++)
        {
            if (dungeonStages[i] == null || layout.rooms[i].type == DungeonRoomType.Treasure) continue;
            foreach (GameObject prefab in dungeonStages[i].enemyPrefabs)
                if (DropTableLookup.GetRate(prefab, q.gatherItem) > 0f) gatherSourcePrefabs.Add(prefab);
        }
        Debug.Log($"📦 [재료 수급] {q.gatherItem.itemName} {q.gatherCount}개 필요 — 재료 몬스터 {gatherSourcesRemaining}마리(드랍률 합 {sumRates:F0}%)");
    }

    // 몬스터가 쓰러질 때 이 아이템이 떨어질 확률(%). 의뢰 재료가 아니면 원래 확률 그대로
    public float GetDropRate(MonsterDropData item, float baseRate, BattleUnit source)
    {
        // 운 좋은 성격: 희귀 드랍(기본 확률이 낮은 아이템) 확률 증가
        PersonalitySettings ps = GameManager.Instance != null ? GameManager.Instance.personality : null;
        if (ps != null && baseRate > 0f && baseRate <= ps.rareDropMaxRate)
            baseRate = Mathf.Min(100f, baseRate * PersonalityRules.StackedMultiplier(AlivePersonalities(), TraitType.Lucky, ps.luckyRareDropMultiplier));

        QuestData q = CurrentQuest;
        if (q == null || q.questType != QuestType.Gather || item == null || item != q.gatherItem || gatherDelivered) return baseRate;

        bool counted = source != null && gatherSourceIds.Contains(source.gameObject.GetInstanceID());
        int gathered = GatheredCount();
        float rate = GatherRules.EffectiveRate(baseRate, q.gatherDropRateMultiplier, gathered, q.gatherCount,
                                               counted, gatherSourcesRemaining, q.gatherGuaranteeAtEnd);
        if (counted && rate >= 100f && baseRate < 100f && gathered < q.gatherCount)
            Debug.Log($"📦 [재료 수급] 막판 보장: 남은 재료 몬스터 {gatherSourcesRemaining}마리 ≤ 남은 필요 {q.gatherCount - gathered}개 → {item.itemName} 확정 드랍");
        return rate;
    }

    // 몬스터의 드랍 판정이 끝난 뒤: 계산에 들어간 재료 몬스터였으면 남은 수에서 뺌
    public void OnDropSourceDied(BattleUnit source)
    {
        if (source == null) return;
        if (gatherSourceIds.Remove(source.gameObject.GetInstanceID()) && gatherSourcesRemaining > 0) gatherSourcesRemaining--;
    }

    // 이번 던전에서 모은 수급 재료 수
    int GatheredCount()
    {
        QuestData q = CurrentQuest;
        if (q == null || q.gatherItem == null) return 0;
        int n = 0;
        foreach (MonsterDropData item in earnedItems) if (item == q.gatherItem) n++;
        return n;
    }

    public bool QuestSucceeded()
    {
        QuestData q = CurrentQuest;
        if (q == null) return battleWon;
        if (partyWiped) return false;
        switch (q.questType)
        {
            case QuestType.Gather: return gatherDelivered || GatheredCount() >= q.gatherCount;
            case QuestType.Rescue: return CaptiveRoom < 0 ? battleWon : captiveRescued;
            default:               return battleWon;
        }
    }

    int QuestReward()
    {
        QuestData q = CurrentQuest;
        if (q == null) return 0;
        int reward = q.rewardGold;
        if (q.questType == QuestType.Rescue && captiveRescued && bossDefeated) reward += q.rescueDeepBonus; // 구한 뒤 보스까지
        return reward;
    }

    // 의뢰 성공 보상 (골드·명성, 낙오 모험가 긴급 구출이면 구출 처리)
    void PayQuestSuccess()
    {
        QuestData q = CurrentQuest;
        int reward = QuestReward();
        int gold = Mathf.RoundToInt(reward * GameManager.Instance.GetGoldGainMultiplier(GameManager.Instance.currentDispatchParty?.members)); // 덜렁거리는 성격
        GameManager.Instance.AddQuestRewards(gold, EconomyRules.SuccessReputation(GameManager.Instance.economy, reward), true);
        if (q != null && q.isRescueQuest) GameManager.Instance.CompleteRescueQuest(q);
        string extra = q != null && q.questType == QuestType.Rescue && bossDefeated ? $" (보스까지 쓰러뜨려 +{q.rescueDeepBonus:N0} G)" : "";
        Debug.Log($"📜 [의뢰 성공] {(q != null ? q.questName : "")} 보상 {reward:N0} G{extra}");
    }

    // 재료 수급: 필요한 개수만큼 전리품에서 빼서 의뢰인에게 넘김 (한 번만)
    void DeliverGatheredItems()
    {
        QuestData q = CurrentQuest;
        if (gatherDelivered || q == null || q.questType != QuestType.Gather || q.gatherItem == null) return;
        if (GatheredCount() < q.gatherCount) return;
        int removed = 0;
        for (int i = earnedItems.Count - 1; i >= 0 && removed < q.gatherCount; i--)
            if (earnedItems[i] == q.gatherItem) { earnedItems.RemoveAt(i); removed++; }
        gatherDelivered = true;
        Debug.Log($"📦 [재료 수급] {q.gatherItem.itemName} {removed}개를 의뢰인에게 넘겼습니다.");
    }

    // 갇힌 인물을 방 안쪽(적이 나오는 쪽)에 세움
    void SpawnCaptive()
    {
        if (captiveObject != null || CurrentQuest == null) return;
        captiveObject = new GameObject("Captive");
        Vector3 basePos = enemySpawnPoint != null ? enemySpawnPoint.position : new Vector3(5f, 0f, 0f);
        captiveObject.transform.position = basePos + new Vector3(1.5f, 0.3f, 0f);
        captiveObject.transform.localScale = new Vector3(-captiveScale, captiveScale, 1f); // 모험가 쪽을 봄
        SpriteRenderer sr = captiveObject.AddComponent<SpriteRenderer>();
        sr.sprite = captiveSprite;
        sr.sortingOrder = 1;
        Debug.Log($"🔗 [구출] 방 {currentStageIndex + 1}에 {CurrentQuest.captiveName}이(가) 갇혀 있습니다! 방을 정리하면 구할 수 있습니다.");
    }

    // 구출 의뢰: 보스를 먼저 쓰러뜨린 뒤 구출까지 마쳤으면 던전 클리어 (보스방 클리어 때는 구출 전이라 끝나지 않았으므로)
    bool FinishIfRescuedAfterBoss()
    {
        if (!bossDefeated || !captiveRescued || gameEnded) return false;
        Debug.Log("🎉 던전 클리어! (보스를 쓰러뜨린 뒤 구출까지 마침)");
        GameOver(true);
        return true;
    }

    void RescueCaptive()
    {
        if (captiveRescued || CurrentQuest == null) return;
        captiveRescued = true;
        Vector3 pos = captiveObject != null ? captiveObject.transform.position : Vector3.zero;
        SpawnFloatingText(pos + Vector3.up * 0.6f, "구출!", healTextColor);
        if (captiveObject != null) Destroy(captiveObject, 1.0f);
        Debug.Log($"🙌 [구출 완료] {CurrentQuest.captiveName}을(를) 구했습니다! 돌아가도 의뢰 성공, 더 나아가 보스를 쓰러뜨리면 +{CurrentQuest.rescueDeepBonus:N0} G");
    }

    // 모닥불 화면에 띄울 의뢰 진행 상황
    string QuestProgressText()
    {
        QuestData q = CurrentQuest;
        if (q == null) return "";
        switch (q.questType)
        {
            case QuestType.Gather:
            {
                if (q.gatherItem == null) return "";
                int have = GatheredCount();
                return have >= q.gatherCount
                    ? $"<color=#7CFC7C>[재료 수급] {q.gatherItem.itemName} {have}/{q.gatherCount} — 다 모았습니다! 돌아가면 의뢰 성공</color>\n"
                    : $"<color=#FFD966>[재료 수급] {q.gatherItem.itemName} {have}/{q.gatherCount}</color>\n";
            }
            case QuestType.Rescue:
                if (CaptiveRoom < 0) return "";
                if (!captiveRescued) return $"<color=#FFD966>[구출] {q.captiveName}을(를) 아직 찾지 못했습니다</color>\n";
                return bossDefeated
                    ? $"<color=#7CFC7C>[구출] {q.captiveName} 구출 완료, 보스까지 쓰러뜨렸습니다 (+{q.rescueDeepBonus:N0} G)</color>\n"
                    : $"<color=#7CFC7C>[구출] {q.captiveName} 구출 완료! 돌아가도 의뢰 성공 · 보스를 쓰러뜨리면 +{q.rescueDeepBonus:N0} G</color>\n";
            default:
                return q.targetBoss != null ? $"[보스 토벌] {DisplayNames.EnemyToKorean(q.targetBoss.name)}\n" : "";
        }
    }

    // 신중한 성격의 파티원이 살아 있으면 미믹을 알아챌 수 있음 (알아채면 true)
    public bool TryDetectMimic()
    {
        if (GameManager.Instance == null) return false;
        if (!AlivePersonalities().Contains(TraitType.Careful)) return false;
        if (Random.value >= GameManager.Instance.personality.carefulMimicDetectChance) return false;
        Debug.Log("🔎 [신중한] 보물상자가 미믹이라는 것을 알아챘습니다!");
        return true;
    }

    // 미믹 소환 시 호출: 적 카운트 증가 후 전투 재개 + 보너스 드롭 예약
    public void OnMimicSpawned()
    {
        enemyCount++;
        isBattleActive = true;
        isRoomCleared  = false;
        FleeIfOnlyHealersLeft();
        mimicBonusPending = true;
        Debug.Log("💀 [미믹] 전투 재개! 미믹을 처치하라!");
    }

    // 미믹 처치 보너스 드롭 플래그
    private bool mimicBonusPending = false;
    // 이번 던전에서 받은 미믹 보너스 골드 합계 (전멸하면 회수)
    private int mimicBonusTotal = 0;

    // 전투 중 적이 추가될 때 호출 (슬라임 분열 등)
    public void OnEnemyAdded()
    {
        enemyCount++;
    }

    private void ResetAllyPositions()
    {
        GameObject[] allyObjects = GameObject.FindGameObjectsWithTag("Player");
        List<BattleUnit> survivingAllies = new List<BattleUnit>();

        foreach (GameObject obj in allyObjects)
        {
            BattleUnit unit = obj.GetComponent<BattleUnit>();
            if (unit != null && !unit.isDead)
            {
                survivingAllies.Add(unit);
                unit.ResetState();
            }
        }

        survivingAllies.Sort((a, b) => b.maxHp.CompareTo(a.maxHp));

        // 체력이 높은 순으로 전위 자리 수만큼 앞줄, 나머지는 뒷줄
        int frontSlots = frontSpawnPoints != null && frontSpawnPoints.Length > 0 ? frontSpawnPoints.Length : 2;
        List<Transform> frontUnits = new List<Transform>(), backUnits = new List<Transform>();
        foreach (BattleUnit unit in survivingAllies)
            (frontUnits.Count < frontSlots ? frontUnits : backUnits).Add(unit.transform);
        PlaceAllyLines(frontUnits, backUnits);
    }

    // 전위/후위 줄마다 인원 수에 맞춰 위아래로 고르게 벌려 세우고 조금씩 흩음
    void PlaceAllyLines(List<Transform> frontUnits, List<Transform> backUnits)
    {
        Vector3 fallback = playerSpawnPoint != null ? playerSpawnPoint.position : new Vector3(-3f, 0f, 0f);
        Vector3 frontCenter = GetLineCenter(frontSpawnPoints, fallback);
        Vector3 backCenter  = GetLineCenter(backSpawnPoints, fallback);
        PlaceLine(frontUnits, frontCenter, frontLineSpacing);
        PlaceLine(backUnits, backCenter, backLineSpacing);
    }

    void PlaceLine(List<Transform> units, Vector3 center, float spacing)
    {
        for (int i = 0; i < units.Count; i++)
        {
            Vector3 pos = center + new Vector3(0f, (i - (units.Count - 1) * 0.5f) * spacing, 0f);
            units[i].position = ScatterOnFloor(pos, allySpawnJitter, center);
        }
    }

    static Vector3 GetLineCenter(Transform[] points, Vector3 fallback)
    {
        if (points == null) return fallback;
        Vector3 sum = Vector3.zero; int n = 0;
        foreach (Transform t in points) if (t != null) { sum += t.position; n++; }
        return n > 0 ? sum / n : fallback;
    }

    // 무작위로 흩은 자리가 바닥 밖이면 기준점 쪽으로 당겨 바닥 안에 세움
    Vector3 ScatterOnFloor(Vector3 pos, float jitter, Vector3 anchor)
    {
        pos += new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter), 0f);
        if (MapGenerator.Instance == null) return pos;
        for (int step = 0; step <= 10; step++)
        {
            Vector3 p = Vector3.Lerp(pos, anchor, step / 10f);
            if (MapGenerator.Instance.IsOnFloor(p)) return p;
        }
        return MapGenerator.Instance.NearestFloor(anchor); // 기준점마저 바닥 밖이면 가장 가까운 바닥으로 (방 모양에 따라 스폰 지점이 바닥 밖일 수 있음)
    }

    // 살아 돌아온 모험가가 다쳤다면 요양 기간을 매기고, 중상이면 트라우마 판정 (승리 귀환과 도주 복귀 공통)
    // 부상자는 파티에 그대로 남음
    private void ApplyInjuryIfHurt(Adventurer member)
    {
        if (member.currentHp >= member.hp) return;

        float hpPercent = (float)member.currentHp / member.hp;

        // 가벼운 상처(체력이 기준보다 많이 남음)는 부상으로 치지 않고, 돌아오면 바로 회복
        if (hpPercent > GameManager.Instance.injuryHpThreshold)
        {
            member.currentHp = member.hp;
            return;
        }

        // [중상 처리: 체력 30% 이하] 중상·빈사면 트라우마·결함 판정 (빈사가 확률이 더 높음)
        if (hpPercent <= 0.3f)
        {
            member.recoveryWeeks = GameManager.Instance.severeInjuryWeeks;

            bool nearDeath = hpPercent <= GameManager.Instance.moraleSettings.nearDeathRatio;
            if (GameManager.Instance.TryGiveAffliction(member, nearDeath ? AfflictionCause.NearDeath : AfflictionCause.Severe) == null)
                Debug.Log($"😌 [{(nearDeath ? "빈사" : "중상")}] {member.name}님이 크게 다쳤지만 트라우마·결함은 생기지 않았습니다.");
        }
        else
        {
            member.recoveryWeeks = GameManager.Instance.minorInjuryWeeks;
        }
    }

    private void SyncAdventurerStatus()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentDispatchParty == null) return;

        List<Adventurer> deadAdventurers = new List<Adventurer>();
        GameObject[] allies = GameObject.FindGameObjectsWithTag("Player");
        MoraleSettings moraleSettings = GameManager.Instance.moraleSettings;
        int nearDeathCount = 0, severeCount = 0; // 사기 상승 판정용 (출발 시 최대 체력 대비)

        foreach (Adventurer member in GameManager.Instance.currentDispatchParty.members)
        {
            string unitName = $"Unit_{member.name}";
            BattleUnit unitScript = null;
            foreach(GameObject obj in allies) { if(obj.name == unitName) { unitScript = obj.GetComponent<BattleUnit>(); break; } }

            if (unitScript != null)
            {
                member.currentHp = unitScript.GetReturnHp(member); // 출발 시 최대 체력(사기·케미 적용) 대비 남은 비율로 부상 판정
                member.isDead = unitScript.isDead; 
            }
            else { member.currentHp = 0; member.isDead = true; }

            if (member.isDead || member.currentHp <= 0) { deadAdventurers.Add(member); GameManager.Instance.NoteFallen(member); }
            else
            {
                float ratio = member.hp > 0 ? (float)member.currentHp / member.hp : 1f;
                if (ratio <= moraleSettings.nearDeathRatio) nearDeathCount++;
                else if (ratio <= moraleSettings.severeRatio) severeCount++;
                ApplyInjuryIfHurt(member);
            }
        }

        // 사기 상승: 끝까지 돌파했고 사망·빈사가 없으면 중상 인원에 따라 (모닥불 귀환·도주·전멸은 해당 없음)
        if (battleWon)
        {
            GameManager.Instance.OnDungeonCleared(GameManager.Instance.currentDispatchParty.members.FindAll(m => !deadAdventurers.Contains(m))); // 혼자 살아남아 돌파하면 용사의 기운

            float gain = MoraleRules.VictoryGain(moraleSettings, deadAdventurers.Count, nearDeathCount, severeCount);
            if (gain > 0f)
            {
                foreach (Adventurer member in GameManager.Instance.currentDispatchParty.members)
                    if (!deadAdventurers.Contains(member)) member.morale = MoraleRules.Clamp(moraleSettings, member.morale + gain * GameManager.Instance.GetMoraleSwing(member)); // 나르시시스트는 크게
                Debug.Log($"🔥 [사기 상승] 던전 돌파! 중상 {severeCount}명 → 파티 사기 +{gain * 100f:F0}%");
            }
        }

        // 파티 케미: 승리·모닥불 귀환이면 완수 +1, 사망자가 있으면 남은 파티원 우울함 판정 (사망자를 빼기 전에)
        GameManager.Instance.ResolvePartyChemistry(GameManager.Instance.currentDispatchParty, battleWon || returnedByCampfire, deadAdventurers);

        string questNameForDead = GameManager.Instance.currentQuest != null
            ? GameManager.Instance.currentQuest.questName : "알 수 없는 던전";
        foreach (Adventurer deadGuy in deadAdventurers)
        {
            deadGuy.diedAtQuestName = questNameForDead;
            GameManager.Instance.currentDispatchParty.members.Remove(deadGuy);
            if (GameManager.Instance.adventurers.Contains(deadGuy))
                GameManager.Instance.adventurers.Remove(deadGuy);
            if (!GameManager.Instance.deadAdventurers.Contains(deadGuy))
                GameManager.Instance.deadAdventurers.Add(deadGuy);
            Debug.Log($"💀 [전사] {deadGuy.name}이(가) '{questNameForDead}'에서 사망했습니다. (유해 수습 가능)");
        }
    }
}