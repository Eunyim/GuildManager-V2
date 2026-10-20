using UnityEngine;
using System.Collections.Generic;

// 퀘스트 랜덤 생성기
// LobbyScene의 QuestBoardPopup 등에 컴포넌트로 추가 후 각 등급 풀에 적 프리팹을 연결합니다.
public class QuestGenerator : MonoBehaviour
{
    [Header("던전 지역 (의뢰는 지역 안에서 생기고, 랭크는 지역 범위를 넘지 않음)")]
    [Tooltip("'Tools > 던전 지역 설정 연결'이 기본값(초원·숲·지하 묘지·오크 부락·드래곤 둥지)을 넣음")]
    public List<DungeonRegion> regions = new List<DungeonRegion>();

    [Header("몬스터 종족 (종족마다 보스 하나. 보스방에는 지역에 사는 종족의 보스 중 하나가 무작위로)")]
    public List<MonsterRace> races = new List<MonsterRace>();

    [Header("(예전 방식) 등급별 적 프리팹 풀 — 지역이 하나도 없을 때만 사용")]
    // 고블린·스켈레톤·오크는 무리 대표 프리팹 (뽑히면 packs 설정대로 전사·궁수 등의 무리로 바뀜)
    public List<GameObject> tierD_Prefabs = new List<GameObject>(); // 슬라임, 고블린 무리, 초원 늑대 무리
    public List<GameObject> tierC_Prefabs = new List<GameObject>(); // 고블린 무리, 스켈레톤 무리
    public List<GameObject> tierB_Prefabs = new List<GameObject>(); // 스켈레톤 무리, 오크 무리, 도적단
    public List<GameObject> tierA_Prefabs = new List<GameObject>(); // 오크 무리
    public List<GameObject> tierS_Prefabs = new List<GameObject>(); // 최상위 적

    [Header("기본 보스 (지역에 보스가 없을 때. 지금은 킹 슬라임)")]
    [Tooltip("보스로 쓸 슬라임 프리팹. 비워 두면 적 풀에서 슬라임을 찾아 씀")]
    public GameObject bossPrefab;

    [Header("방 적 뽑기 (랭크별 횟수, 아래 랭크 섞기)")]
    public RoomSpawnSettings roomSpawn = new RoomSpawnSettings();

    [Header("도적단 (적 목록에서 뽑히면 전사·암살자·궁수 무리로 바뀜)")]
    [Tooltip("적 풀에 들어 있는 '도적단' 대표 프리팹 (Enemy_Bandit)")]
    public GameObject banditGangPrefab;
    [Tooltip("무리 구성원 프리팹 (도적단 전사·암살자·궁수). 한 명씩 무작위로 뽑음")]
    public List<GameObject> banditGangMembers = new List<GameObject>();
    public BanditGangSettings banditGang = new BanditGangSettings();

    [Header("무리로 나오는 몬스터 (적 목록에서 한 번 뽑히면 같은 몬스터가 여러 마리)")]
    public List<EnemyPack> packs = new List<EnemyPack>(); // 초원 늑대, 고블린, 스켈레톤, 오크

    [Header("맵 테마 풀 (스테이지에 랜덤 배정)")]
    [Tooltip("인스펙터에서 MapThemeData 에셋을 연결하세요. 비워두면 맵이 생성되지 않습니다.")]
    public List<MapThemeData> mapThemes = new List<MapThemeData>();

    [Header("의뢰 랭크가 올라가는 명성 기준 (퀘스트 보드 / 동시 파견 공용)")]
    public int lowTierReputation  = 100;  // 이 명성부터 B랭크 의뢰 등장 (그 전에는 D/C만)
    public int midTierReputation  = 500;  // 이 명성부터 A/S랭크 의뢰 등장
    public int highTierReputation = 1000; // 이 명성부터 고랭크 의뢰 비중이 커짐

    [Header("던전 구조 (랭크별 방 개수, 갈림길 빈도)")]
    public DungeonLayoutSettings dungeonLayout = new DungeonLayoutSettings();

    static readonly string[] Verbs     = { "토벌", "소탕", "섬멸", "격퇴", "정화" };
    static readonly string[] Locations = { "소굴", "영역", "출몰지", "군락", "집단" };

    // ─── 지역 ────────────────────────────────────────────────────────

    public DungeonRegion GetRegion(int index) =>
        regions != null && index >= 0 && index < regions.Count ? regions[index] : null;

    public int FindRegionIndex(string regionName) =>
        regions == null || string.IsNullOrEmpty(regionName) ? -1 : regions.FindIndex(r => r != null && r.name == regionName);

    // 이 랭크를 받는 지역 중 하나 (없으면 -1)
    public int FindRegionForRank(RankType rank)
    {
        if (regions == null) return -1;
        var candidates = new List<int>();
        for (int i = 0; i < regions.Count; i++)
            if (regions[i] != null && RegionRules.Contains(rank, regions[i].minRank, regions[i].maxRank)) candidates.Add(i);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
    }

    // 지역 의뢰의 랭크: 지역의 최고 랭크 확률로 최저/최고 중 하나 (예: 초원 D 80% / C 20%)
    public RankType DetermineRegionQuestRank(int regionIndex)
    {
        DungeonRegion region = GetRegion(regionIndex);
        return region != null ? RegionRules.RollRank(region.minRank, region.maxRank, region.maxRankChance, Random.value) : RankType.D;
    }

    // 명성 레벨로 이 지역에 갈 수 있는지
    public bool IsRegionUnlocked(int regionIndex, int reputationLevel)
    {
        DungeonRegion region = GetRegion(regionIndex);
        return region != null && RegionRules.IsUnlocked(region.requiredReputationLevel, reputationLevel);
    }

    [Header("의뢰 종류 (보스 토벌 / 재료 수급 / 구출)")]
    public QuestTypeSettings questTypes = new QuestTypeSettings();

    [Header("의뢰비·추천 레벨")]
    public QuestRewardSettings questRewards = new QuestRewardSettings();

    static readonly string[] CaptiveRoles = { "행상인", "약초꾼", "견습 기사", "순례자", "광부", "사냥꾼", "음유시인" };
    static readonly string[] CaptiveNames = { "로이", "미아", "벤", "엘라", "토마", "리나", "하스", "델", "니코", "세라" };

    // 지역 안에서 일반 의뢰 1개 생성. forceType이 없으면 종류도 비율대로 뽑음
    public QuestData GenerateInRegion(int regionIndex, QuestType? forceType = null) =>
        Generate(regionIndex, DetermineRegionQuestRank(regionIndex), forceType);

    // 랭크만 정해서 생성 (빠른 테스트·봇용). 그 랭크를 받는 지역에서 만들고, 그런 지역이 없으면 예전 등급별 풀
    public QuestData Generate(RankType rank) => Generate(FindRegionForRank(rank), rank);

    // 퀘스트 1개 생성. 내부적으로 ScriptableObject.CreateInstance 사용 (사용 후 반드시 Destroy 필요)
    public QuestData Generate(int regionIndex, RankType rank, QuestType? forceType = null)
    {
        DungeonRegion region = GetRegion(regionIndex);
        List<GameObject> pool = GetPool(region, rank);
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[QuestGenerator] {(region != null ? region.name : rank + " 등급")} 적 목록이 비어있습니다. 인스펙터에서 프리팹을 연결해 주세요.");
            return null;
        }

        // 의뢰 종류: 지역에 보스·재료가 없으면 그 종류는 빼고 뽑음
        List<MonsterRace> bossRaces = RacesInRegion(region, r => r.boss != null);
        List<MonsterRace> materialRaces = RacesInRegion(region, r => r.materials != null && r.materials.Exists(m => m.item != null));
        QuestType type = forceType ?? (region != null
            ? QuestTypeRules.PickType(questTypes, Random.value, materialRaces.Count > 0, true)
            : QuestType.BossHunt);
        if (type == QuestType.Gather && materialRaces.Count == 0) type = QuestType.BossHunt;

        GameObject mainEnemy = pool[Random.Range(0, pool.Count)];
        string enemyName     = FamilyName(mainEnemy);
        string verb          = Verbs[Random.Range(0, Verbs.Length)];
        string where         = region != null ? region.name : Locations[Random.Range(0, Locations.Length)];

        QuestData quest           = ScriptableObject.CreateInstance<QuestData>();
        quest.rank                = rank.ToString();
        quest.region              = region != null ? region.name : "";
        quest.questType           = type;
        quest.rewardGold          = GetReward(rank);
        quest.recommendedLevel    = GetRecLevel(rank);
        quest.enemyPrefab         = mainEnemy;

        MonsterRace forcedBoss = null;
        GameObject favored = null; // 재료 수급: 그 재료를 주는 종족 (재료 몬스터가 모자란 던전에 보충할 때 씀)
        switch (type)
        {
            case QuestType.BossHunt:
                // 지역 종족 보스 중 하나를 정해 두고 보스방에 그 보스가 나오게 함
                forcedBoss = bossRaces.Count > 0 ? bossRaces[Random.Range(0, bossRaces.Count)] : null;
                string bossName = forcedBoss != null ? ToKorean(forcedBoss.boss.name) : enemyName;
                quest.targetBoss  = forcedBoss != null ? forcedBoss.boss : null;
                quest.questName   = $"[{rank}] {where}의 {bossName} 토벌";
                quest.description = $"{where}의 {bossName}이(가) 무리를 이끌고 있습니다. 던전 깊은 곳의 {bossName}을(를) 쓰러뜨려 주세요.";
                break;

            case QuestType.Gather:
            {
                MonsterRace race = materialRaces[Random.Range(0, materialRaces.Count)];
                List<DropItem> mats = race.materials.FindAll(m => m.item != null);
                DropItem mat = mats[Random.Range(0, mats.Count)];
                favored = race.members.Find(m => m != null && region.enemies.Contains(m));
                quest.gatherItem  = mat.item;
                quest.gatherFee   = QuestTypeRules.GatherFee(questTypes, GetAverageReward(rank));
                break; // 필요 개수·보상·이름은 던전을 만든 뒤 (나오는 재료 몬스터 수로 정함)
            }

            case QuestType.Rescue:
                quest.captiveName = $"{CaptiveRoles[Random.Range(0, CaptiveRoles.Length)]} {CaptiveNames[Random.Range(0, CaptiveNames.Length)]}";
                quest.questName   = $"[{rank}] {where}의 {quest.captiveName} 구출";
                quest.description = $"{where}에서 {quest.captiveName}이(가) 몬스터에게 붙잡혔습니다. 갇혀 있는 방을 찾아 구해 주세요. " +
                                    "구한 뒤 돌아와도 성공이고, 더 나아가 보스까지 쓰러뜨리면 보상이 늘어납니다.";
                break;
        }

        if (type == QuestType.Rescue) BuildRescueStages(quest, pool, rank, region);
        else if (type == QuestType.Gather) BuildGatherStages(quest, pool, rank, region, favored, where);
        else quest.stages = BuildStages(pool, rank, region, out quest.layout, forcedBoss);
        return quest;
    }

    // 재료 수급 의뢰: 던전을 만든 뒤 실제로 나오는 재료 몬스터 수로 필요 개수를 정함
    //  - 재료 몬스터의 드랍률 합이 모자라면(필요 개수 1개도 기대하기 어려우면) 던전을 다시 뽑고, 끝내 모자라면 전투 방에 재료 종족을 보충
    void BuildGatherStages(QuestData quest, List<GameObject> pool, RankType rank, DungeonRegion region, GameObject favored, string where)
    {
        const int maxTries = 20;
        float sumRates = 0f;
        for (int attempt = 0; attempt < maxTries; attempt++)
        {
            DestroyStages(quest);
            quest.stages = BuildStages(pool, rank, region, out quest.layout, null, favored, questTypes.gatherRaceBias);
            DropTableLookup.CountSources(quest.stages, quest.layout, quest.gatherItem, out sumRates);
            if (sumRates >= questTypes.gatherMinSourceRate) break;
        }
        if (sumRates < questTypes.gatherMinSourceRate && favored != null && quest.layout != null)
        {
            for (int i = 0; i < quest.stages.Count && sumRates < questTypes.gatherMinSourceRate; i++)
            {
                if (i >= quest.layout.rooms.Count || quest.layout.rooms[i].type != DungeonRoomType.Normal || i == quest.layout.bossRoom) continue;
                AddEnemy(quest.stages[i].enemyPrefabs, favored, rank);
                DropTableLookup.CountSources(quest.stages, quest.layout, quest.gatherItem, out sumRates);
            }
        }

        int sources = DropTableLookup.CountSources(quest.stages, quest.layout, quest.gatherItem, out sumRates);
        MonsterDropData item = quest.gatherItem;
        quest.gatherCount = GatherRules.RequiredCount(sumRates);
        quest.gatherDropRateMultiplier = questTypes.gatherDropRateMultiplier;
        quest.gatherGuaranteeAtEnd     = questTypes.gatherGuaranteeAtEnd;
        quest.rewardGold  = quest.gatherFee + QuestTypeRules.GatherMaterialPayment(questTypes, item.price, quest.gatherCount);
        quest.questName   = $"[{rank}] {where}의 {item.itemName} 수급 ({quest.gatherCount}개)";
        quest.description = $"{where}에서 {item.itemName} {quest.gatherCount}개를 모아 와 주세요. 모은 재료는 의뢰를 마치면 넘겨주고, " +
                            $"의뢰비 {quest.gatherFee:N0} G와 재료 값(시세의 {Mathf.RoundToInt(questTypes.gatherMaterialPriceRatio * 100f)}%)을 받습니다.";
        Debug.Log($"📦 [재료 수급 의뢰] {item.itemName}: 재료 몬스터 {sources}마리(드랍률 합 {sumRates:F0}%) → 필요 {quest.gatherCount}개");
    }

    void DestroyStages(QuestData quest)
    {
        if (quest.stages == null) return;
        foreach (StageData s in quest.stages)
            if (s != null) { if (Application.isPlaying) Destroy(s); else DestroyImmediate(s); }
        quest.stages = null;
    }

    // 구출 의뢰: 던전을 만들고 갇힌 방(입구에서 2칸 이상, 보스방 제외)을 정함. 그런 방이 없는 구조면 구조를 다시 뽑음
    void BuildRescueStages(QuestData quest, List<GameObject> pool, RankType rank, DungeonRegion region)
    {
        const int maxTries = 30;
        var rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));
        for (int attempt = 0; attempt < maxTries; attempt++)
        {
            if (quest.stages != null)
                foreach (StageData s in quest.stages)
                    if (s != null) { if (Application.isPlaying) Destroy(s); else DestroyImmediate(s); } // 편집 모드(검증 도구)에서도 정리
            quest.stages = BuildStages(pool, rank, region, out quest.layout);
            quest.captiveRoom = QuestTypeRules.PickCaptiveRoom(quest.layout, questTypes.rescueMinDistance, rng);
            if (quest.captiveRoom >= 0) break;
        }
        if (quest.captiveRoom < 0)
        {
            quest.captiveRoom = QuestTypeRules.FarthestNonBossRoom(quest.layout);
            Debug.LogWarning($"[QuestGenerator] 입구에서 {questTypes.rescueMinDistance}칸 이상 떨어진 방이 있는 구조를 {maxTries}번 안에 못 만들었습니다. 가장 먼 방에 둡니다.");
        }
        quest.questType = QuestType.Rescue;
        quest.rescueDeepBonus = QuestTypeRules.RescueDeepBonus(questTypes, quest.rewardGold);
    }

    // 지역 적 목록에 몬스터가 들어 있는 종족 중 조건에 맞는 것
    List<MonsterRace> RacesInRegion(DungeonRegion region, System.Predicate<MonsterRace> match)
    {
        var list = new List<MonsterRace>();
        if (region == null || region.enemies == null || races == null) return list;
        foreach (MonsterRace race in races)
            if (race != null && race.members != null && race.members.Exists(m => m != null && region.enemies.Contains(m)) && match(race))
                list.Add(race);
        return list;
    }

    // 의뢰 이름에 쓸 몬스터 이름 (도적단 대표 프리팹은 암살자라 무리 이름으로)
    string FamilyName(GameObject prefab) => prefab == banditGangPrefab ? "도적단" : ToKorean(prefab.name);

    int MinRooms(RankType rank)
    {
        int[] table = dungeonLayout != null ? dungeonLayout.minRoomsPerRank : null;
        if (table == null || table.Length == 0) return 4;
        return Mathf.Max(1, table[Mathf.Clamp((int)rank, 0, table.Length - 1)]);
    }

    // ─── 던전 구조와 방별 스테이지 생성 ──────────────────────────────

    // 방들의 연결 구조를 무작위로 만들고, 방마다 스테이지(적 구성)를 하나씩 만듦 (stages[i] = layout.rooms[i])
    // favored: 이 몬스터가 favorChance 확률로 뽑힘 (재료 수급 의뢰의 재료 종족). 없으면 지역 몬스터 중 고르게
    // 지역 바닥 테마: Resources/DungeonThemes 중 regionName이 지역 이름과 같은 것. 없으면 예전처럼 mapThemes 중 아무거나
    static MapThemeData[] regionThemes;

    MapThemeData ThemeFor(DungeonRegion region)
    {
        if (regionThemes == null) regionThemes = Resources.LoadAll<MapThemeData>("DungeonThemes");
        if (region != null)
            foreach (MapThemeData t in regionThemes)
                if (t != null && t.regionName == region.name) return t;
        return mapThemes != null && mapThemes.Count > 0 ? mapThemes[Random.Range(0, mapThemes.Count)] : null;
    }

    List<StageData> BuildStages(List<GameObject> pool, RankType rank, DungeonRegion region, out DungeonLayout layout,
                                MonsterRace forcedBossRace = null, GameObject favored = null, float favorChance = 0f)
    {
        layout = DungeonLayoutRules.Generate(dungeonLayout, rank, new System.Random(Random.Range(int.MinValue, int.MaxValue)));

        var stages = new List<StageData>();
        for (int i = 0; i < layout.rooms.Count; i++)
        {
            StageData stage       = ScriptableObject.CreateInstance<StageData>();
            stage.stageName       = $"{i + 1}번 방";
            stage.enemyPrefabs    = new List<GameObject>();
            stage.mapTheme        = ThemeFor(region);

            // 방 종류에 따라 적 구성이 달라짐
            int count;
            switch (layout.rooms[i].type)
            {
                case DungeonRoomType.Rest:
                    count = 0; // 전투 없음
                    break;
                case DungeonRoomType.Treasure:
                    count = 1; // 전투는 없지만 상자가 미믹일 때 튀어나올 적
                    break;
                case DungeonRoomType.Wave:
                    stage.waveCount = Mathf.Max(2, dungeonLayout.waveCount);
                    count = GetEnemyCount(rank, isBossRoom: false) * stage.waveCount;
                    break;
                case DungeonRoomType.Boss:
                    // 지역에 사는 종족들의 보스 중 하나 (없으면 기본 보스 킹 슬라임). 보스가 목록의 첫 번째 = 보스 체력 바
                    // 무리를 이끄는 보스(샤먼킹)는 그 종족 무리와 함께 나옴. 랭크 배율은 소환할 때 적용
                    MonsterRace bossRace = forcedBossRace ?? PickBossRace(region); // 보스 토벌 의뢰는 정해 둔 보스
                    GameObject boss = bossRace != null ? bossRace.boss : GetBossPrefab();
                    if (boss != null)
                    {
                        stage.isBossStage = true;
                        stage.enemyPrefabs.Add(boss);
                        if (bossRace != null && bossRace.bossLeadsPack) AddBossEscort(stage.enemyPrefabs, bossRace, rank);
                        stages.Add(stage);
                        continue;
                    }
                    count = GetEnemyCount(rank, isBossRoom: true);
                    break;
                default:
                    count = GetEnemyCount(rank, isBossRoom: i == layout.bossRoom);
                    break;
            }
            int questRankPicks = RoomSpawnRules.GetQuestRankPicks(roomSpawn, rank, count);
            // j: 지금까지 쓴 뽑는 횟수. 무리 종족은 packPickCost번으로 침 (남은 횟수가 모자라도 마지막 한 번은 뽑음)
            for (int j = 0; j < count; )
            {
                GameObject pick = favored != null && Random.value < favorChance ? favored : pool[Random.Range(0, pool.Count)];
                RankType pickRank = RoomSpawnRules.GetPickRank(rank, j, questRankPicks);
                AddEnemy(stage.enemyPrefabs, pick, pickRank); // 아래 랭크 뽑기는 무리 크기가 작아짐 (lowerRankStats면 능력치 배율도)
                int rankDown = RoomSpawnRules.GetStatsRankDown(roomSpawn, rank, pickRank);
                while (stage.enemyStatsRankDown.Count < stage.enemyPrefabs.Count) stage.enemyStatsRankDown.Add(rankDown);
                j += RoomSpawnRules.GetPickCost(roomSpawn, IsPack(pick));
            }

            stages.Add(stage);
        }
        return stages;
    }

    bool IsPack(GameObject prefab) => prefab != null && packs != null && packs.Exists(p => p != null && p.prefab == prefab);

    // 적 하나를 방에 넣음. 도적단이면 랭크에 맞는 인원의 무리(구성원은 무작위)로, 무리 몬스터면 랭크별 마릿수로 바꿔 넣음
    void AddEnemy(List<GameObject> list, GameObject prefab, RankType rank)
    {
        EnemyPack pack = prefab != null && packs != null ? packs.Find(p => p != null && p.prefab == prefab) : null;
        if (pack != null)
        {
            int count = EnemyPackRules.GetSize(pack.sizePerRank, rank);
            List<GameObject> packMembers = pack.members != null ? pack.members.FindAll(m => m != null) : new List<GameObject>();
            if (pack.leader != null) { list.Add(pack.leader); count--; }
            for (int i = 0; i < count; i++)
                list.Add(packMembers.Count > 0 ? packMembers[Random.Range(0, packMembers.Count)] : prefab);
            return;
        }

        bool isGang = prefab != null && prefab == banditGangPrefab && banditGangMembers != null && banditGangMembers.Exists(m => m != null);
        if (!isGang) { list.Add(prefab); return; }

        List<GameObject> members = banditGangMembers.FindAll(m => m != null);
        int size = BanditGangRules.GetSize(banditGang, rank);
        for (int i = 0; i < size; i++) list.Add(members[Random.Range(0, members.Count)]);
    }

    // 지역 적 목록에 몬스터가 들어 있는 종족 중 보스가 있는 것 하나를 무작위로 (없으면 null)
    MonsterRace PickBossRace(DungeonRegion region)
    {
        if (region == null || region.enemies == null || races == null) return null;
        var candidates = races.FindAll(race => race != null && race.boss != null && race.members != null
                                               && race.members.Exists(m => m != null && region.enemies.Contains(m)));
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // 보스와 함께 나오는 무리: 그 종족 무리 설정에서 지휘자 자리는 보스가 대신하고, 나머지 마릿수만큼 구성원
    void AddBossEscort(List<GameObject> list, MonsterRace race, RankType rank)
    {
        EnemyPack pack = packs != null ? packs.Find(p => p != null && p.prefab != null && race.members.Contains(p.prefab)) : null;
        if (pack == null) return;
        List<GameObject> packMembers = pack.members != null ? pack.members.FindAll(m => m != null) : new List<GameObject>();
        if (packMembers.Count == 0) packMembers.Add(pack.prefab);
        int count = EnemyPackRules.GetSize(pack.sizePerRank, rank) - 1; // 1자리는 보스
        for (int i = 0; i < count; i++) list.Add(packMembers[Random.Range(0, packMembers.Count)]);
    }

    // 기본 보스 프리팹: 인스펙터 값이 없으면 등급 풀에서 이름에 slime이 들어간 슬라임 프리팹을 찾음
    GameObject GetBossPrefab()
    {
        if (bossPrefab != null) return bossPrefab;
        foreach (List<GameObject> tier in new[] { tierD_Prefabs, tierC_Prefabs, tierB_Prefabs, tierA_Prefabs, tierS_Prefabs })
        {
            if (tier == null) continue;
            foreach (GameObject prefab in tier)
                if (prefab != null && prefab.GetComponent<EnemySlime>() != null && prefab.name.ToLower().Contains("slime"))
                    return prefab;
        }
        return null;
    }

    int GetEnemyCount(RankType rank, bool isBossRoom) =>
        RoomSpawnRules.GetPickCount(roomSpawn, rank, isBossRoom, (min, max) => Random.Range(min, max + 1));

    // ─── 구출 퀘스트 생성 ────────────────────────────────────────────

    // 낙오한 모험가를 위한 긴급 구출 퀘스트를 생성합니다.
    public QuestData GenerateRescueQuest(Adventurer stranded)
    {
        // 낙오한 그 의뢰의 지역·랭크로 (기록이 없는 예전 저장 파일은 모험가 랭크를 받는 지역)
        ResolveOrigin(stranded, out int regionIndex, out RankType rank);
        DungeonRegion region = GetRegion(regionIndex);
        List<GameObject> pool = GetPool(region, rank);

        // 풀이 비어있으면 D등급 풀로 대체
        if (pool == null || pool.Count == 0) pool = tierD_Prefabs;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[QuestGenerator] 구출 퀘스트 생성 실패: 적 풀이 비어있습니다.");
            return null;
        }

        GameObject mainEnemy  = pool[Random.Range(0, pool.Count)];

        QuestData quest           = ScriptableObject.CreateInstance<QuestData>();
        quest.isRescueQuest       = true;
        quest.rescueTargetName    = stranded.name;
        quest.rank                = rank.ToString();
        quest.region              = region != null ? region.name : "";
        quest.captiveName         = stranded.name; // 낙오한 모험가가 던전의 어느 방에 갇혀 있음 (인물 구출과 같은 규칙)
        quest.questName           = $"[긴급 구출] {stranded.name} 구출 작전";
        string where              = region != null ? $"{region.name}에서" : "던전에";
        quest.description         = $"{stranded.name}이(가) {where} 낙오했습니다. {Mathf.Max(1, stranded.strandedWeeksRemaining)}주 안에 구출하지 못하면 돌아오지 못합니다.";
        quest.rewardGold          = QuestRewardRules.RescueReward(questRewards, GetReward(rank)); // 구출 보너스
        quest.recommendedLevel    = GetRecLevel(rank);
        quest.enemyPrefab         = mainEnemy;
        BuildRescueStages(quest, pool, rank, region);

        return quest;
    }

    // ─── 유해 수습 퀘스트 생성 ───────────────────────────────────────

    // 전사한 모험가의 유해를 수습하는 퀘스트를 생성합니다.
    public QuestData GenerateCorpseQuest(Adventurer deceased)
    {
        // 전사한 그 의뢰의 지역·랭크로 (예전: 전사자 랭크 기준이라 D랭크 의뢰에서 죽은 C랭크 모험가의 유해 수습이 C랭크가 됐음)
        ResolveOrigin(deceased, out int regionIndex, out RankType rank);
        DungeonRegion region = GetRegion(regionIndex);
        List<GameObject> pool = GetPool(region, rank);
        if (pool == null || pool.Count == 0) pool = tierD_Prefabs;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"[QuestGenerator] 유해 수습 퀘스트 생성 실패: 적 풀이 비어있습니다.");
            return null;
        }

        GameObject mainEnemy = pool[Random.Range(0, pool.Count)];

        QuestData quest           = ScriptableObject.CreateInstance<QuestData>();
        quest.isCorpseQuest       = true;
        quest.corpseTargetName    = deceased.name;
        quest.rank                = rank.ToString();
        quest.region              = region != null ? region.name : "";
        string location = string.IsNullOrEmpty(deceased.diedAtQuestName) ? "알 수 없는 던전" : deceased.diedAtQuestName;
        quest.questName           = $"[유해 수습] {deceased.name}의 유해 수습";
        quest.description         = $"{deceased.name}이(가) '{location}'에서 전사했습니다. 유해를 수습하면 영혼석(신전에서 부활 가능)이나 골드를 얻습니다.";
        quest.rewardGold          = QuestRewardRules.CorpseReward(questRewards, GetReward(rank));
        quest.recommendedLevel    = GetRecLevel(rank);
        quest.enemyPrefab         = mainEnemy;
        quest.stages              = BuildStages(pool, rank, region, out quest.layout);

        return quest;
    }

    // ─── 보조 테이블 ─────────────────────────────────────────────────

    // 마지막 의뢰의 지역·랭크. 기록이 없거나 그 지역이 사라졌으면 그 랭크를 받는 지역
    void ResolveOrigin(Adventurer adv, out int regionIndex, out RankType rank)
    {
        if (!EnemyScalingRules.TryParseRank(adv.lastQuestRank, out rank)) rank = adv.rank;
        regionIndex = FindRegionIndex(adv.lastQuestRegion);
        if (regionIndex < 0) regionIndex = FindRegionForRank(rank);
        DungeonRegion region = GetRegion(regionIndex);
        if (region != null) rank = RegionRules.ClampRank(rank, region.minRank, region.maxRank);
    }

    // 지역이 있으면 그 지역의 몬스터, 없으면 예전 등급별 풀
    List<GameObject> GetPool(DungeonRegion region, RankType rank)
    {
        if (region != null && region.enemies != null)
        {
            List<GameObject> enemies = region.enemies.FindAll(e => e != null);
            if (enemies.Count > 0) return enemies;
        }
        return GetPool(rank);
    }

    List<GameObject> GetPool(RankType rank)
    {
        switch (rank)
        {
            case RankType.D: return tierD_Prefabs;
            case RankType.C: return tierC_Prefabs;
            case RankType.B: return tierB_Prefabs;
            case RankType.A: return tierA_Prefabs;
            case RankType.S: return tierS_Prefabs;
            default:         return tierD_Prefabs;
        }
    }

    // 그 랭크 의뢰비의 평균 (재료 수급 의뢰비의 기준)
    int GetAverageReward(RankType rank) => QuestRewardRules.AverageReward(questRewards, rank);

    int GetReward(RankType rank) => Random.Range(QuestRewardRules.RewardMin(questRewards, rank), QuestRewardRules.RewardMax(questRewards, rank) + 1);

    int GetRecLevel(RankType rank) => QuestRewardRules.RecommendedLevel(questRewards, rank);

    static string ToKorean(string prefabName) => DisplayNames.EnemyToKorean(prefabName);

    // 길드 명성 기반 랜덤 퀘스트 등급 결정 (퀘스트 보드와 동시 파견 창이 함께 사용, GDD §5.3)
    // 명성이 기준을 넘을 때마다 고등급 의뢰가 섞여 나옴
    public RankType DetermineQuestRank(int reputation)
    {
        float roll = Random.value;

        if (reputation >= highTierReputation) // 고명성 길드
        {
            if (roll < 0.15f) return RankType.S;
            if (roll < 0.45f) return RankType.A;
            if (roll < 0.70f) return RankType.B;
            if (roll < 0.90f) return RankType.C;
            return RankType.D;
        }
        if (reputation >= midTierReputation)
        {
            if (roll < 0.05f) return RankType.S;
            if (roll < 0.25f) return RankType.A;
            if (roll < 0.55f) return RankType.B;
            if (roll < 0.80f) return RankType.C;
            return RankType.D;
        }
        if (reputation >= lowTierReputation)
        {
            if (roll < 0.30f) return RankType.B;
            if (roll < 0.65f) return RankType.C;
            return RankType.D;
        }

        // 초보 길드: D/C만 등장
        return (roll < 0.40f) ? RankType.C : RankType.D;
    }
}
