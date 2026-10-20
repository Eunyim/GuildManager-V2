using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Reflection;

// 임시 플레이 봇 (검증 후 삭제): 타이틀 씬에서 플레이를 시작한 뒤 실행.
// 새 게임부터 4주(월급날까지)를 실제 경로로 플레이하며 골드 흐름을 기록한다.
//   매주: (부활 가능하면 신전) -> 접수처 고용 -> 파티 편성 -> 퀘스트 보드에서 의뢰 선택 -> 던전 전투(실제 진행) -> 결과 확인(한 주 경과)
//   고용 기준: 월급날 지출을 내고도 여유가 남을 때만, 최대 6명까지
//   편성 기준: 건강한 모험가 우선, 4명이 안 되면 체력이 절반 이상 남은 부상자로 채움 (다친 체력으로 출전)
//   도주 기준: 전투 중 아군이 절반 이하로 줄면 도주
//   모닥불 기준: 절반 이상 쓰러졌거나 생존자 평균 체력이 절반 미만이면 귀환, 아니면 다음 방으로 (10/18: 1명만 쓰러져도 귀환하던 규칙에서 바꿈)
// 같은 설정으로 RUNS번 반복하고(회차마다 타이틀로 돌아가 새 길드로 시작) 회차별 결과와 평균을 남긴다.
// 모닥불에 앉기 직전(회복 전) 체력도 기록해 회복량이 실제로 얼마나 쓰였는지 본다.
public static class TempPlaythrough
{
    const int WEEKS = 4;
    const int RUNS = 10;
    // 시험할 설정 목록. 위에서부터 차례로 각각 RUNS번씩 돌림
    //   rank: 값을 넣으면 퀘스트 보드에 나온 것 대신 이 랭크의 일반 의뢰를 만들어 수행 (명성이 모자라 안 나오는 랭크를 시험할 때). null이면 보드에서 고름
    //   noHealer: 사제를 파티에 넣지 않음
    //   partyRank: 값을 넣으면 시작 모험가 대신 이 랭크의 전사/마법사/도적/사제 4명으로 시작하고 고용은 하지 않음 (접수처에 찾아오는 모험가와 같은 방식으로 생성)
    //   partyMinLevel: partyRank와 함께 쓰며, 이 레벨에 못 미치면 실제 레벨업 규칙으로 올림 (0이면 생성된 레벨 그대로)
    //   weeks / runs: 0이면 WEEKS / RUNS 사용
    //   campfireHeal: 값을 넣으면 플레이 중에만 모닥불 회복 비율을 이 값으로 바꿈 (파일은 바뀌지 않음, null이면 그대로)
    //   enemyAtkScale / enemyHpScale: 플레이 중에만 적 공격력/체력 배율표 전체에 곱하는 값 (파일은 바뀌지 않음, 0이면 그대로)
    //   warriorFirst: 전사가 2명이 될 때까지 방문자 중 전사를 먼저 고용하고(다른 직업 고용은 한 자리를 비워 둠),
    //                 편성 때 건강한 전사는 한 명만 넣어 나머지는 쉬게 함(교대). 다른 직업으로 4명을 못 채울 때만 둘째 전사를 넣음
    //   warriorVisitor: 전사가 2명 미만인데 방문자에 전사가 없으면 방문자 한 명을 같은 랭크의 전사로 바꿈 (접수처 직업 선택/전사 방문 보정을 흉내, 게임 코드는 그대로)
    //   injuryThreshold / minorWeeks: 플레이 중에만 부상 기준 체력 비율 / 경상 요양 주 수를 바꿈 (0이면 그대로)
    //   warriorAps: 플레이 중에만 전사 공격 속도(초당 횟수)를 이 값으로 바꿈 (0이면 그대로)
    //   knockback: 플레이 중에만 전사 넉백 거리를 이 값으로 바꿈 (0이면 넉백 없음, null이면 그대로)
    //   fixedParty: 지난주 파티원 중 살아 있고 낙오되지 않은 사람은 다쳤어도 그대로 다시 보냄 (케미 유지). 빈자리만 새로 채움
    //   noChemistry: 플레이 중에만 케미 보너스와 우울함 확률을 0으로 (비교용)
    //   gangSize: 플레이 중에만 도적단 무리 인원을 모든 랭크에서 이 값으로 (0이면 그대로)
    //   openingStealth: 플레이 중에만 도적의 방 전투 시작 은신 시간을 이 값으로 (null이면 그대로)
    //   noBacklineDefense: 플레이 중에만 후열 방어(수호·원거리가 우리 후열을 노리는 적 우선)를 끔
    //   noRoles: 플레이 중에만 전투 성향 이동을 끔 (모두 예전처럼 대상에게 곧장 달려감, 비교용)
    //   lowerRankStats: 플레이 중에만 아래 랭크로 뽑힌 적의 능력치 배율도 한 단계 아래로 (QuestGenerator.roomSpawn.lowerRankStats)
    //   packPickCost: 플레이 중에만 무리 종족 한 번 뽑기를 뽑는 횟수 N번으로 침 (0이면 그대로)
    //   picksMin / picksMax: 플레이 중에만 방마다 적을 뽑는 횟수표(D~S)를 이 값으로 (null이면 그대로)
    //   kingDamage: 플레이 중에만 킹 슬라임 착지 피해 기준값(랭크 배율 곱하기 전)을 이 값으로 (0이면 그대로)
    //   isoRoomSize: 플레이 중에만 던전 방 크기(MapGenerator.isoRoomSize)를 이 값으로 (0이면 그대로)
    struct Config { public string label; public RankType? rank; public bool noHealer; public RankType? partyRank; public int partyMinLevel, weeks, runs; public float enemyAtkScale, enemyHpScale; public float? campfireHeal;
                    public bool warriorFirst, warriorVisitor; public float injuryThreshold; public int minorWeeks; public float warriorAps; public float? knockback; public bool fixedParty, noChemistry, noRoles, noBacklineDefense; public int gangSize; public float? openingStealth; public int region; public bool bossFullMp; public string bossOverride; public QuestType? questType; public int roomLevel; public int partySize; public bool slimeOnly, bossOnly; public float kingHopInterval, kingDamage;
                    public bool lowerRankStats; public int packPickCost; public int[] picksMin, picksMax; public int isoRoomSize; }
    static readonly Config[] configs =
    {
        // 예: new Config { label = "모닥불 0% / C의뢰 / 시작 파티", rank = RankType.C, weeks = 1, campfireHeal = 0f },
        // 10/14 측정: new Config { label = "기준 (보드에서 선택)" }, new Config { label = "전사 우선 고용 + 교대", warriorFirst = true },
        //            new Config { label = "전사 우선 고용 + 교대 + 전사 방문 보장", warriorFirst = true, warriorVisitor = true },
        // 10/14 측정(휴식 50%): new Config { label = "C랭크 강제 / 기준", rank = RankType.C }, new Config { label = "C랭크 강제 / 전사 우선 고용 + 교대", rank = RankType.C, warriorFirst = true },
        // 10/14 측정(휴식 50%): "휴식50 D보드 / 기준", "… / 전사 우선 고용 + 교대"(warriorFirst), "… + 전사 방문 보장"(warriorVisitor)
        // 10/14: new Config { label = "전사 너프+넉백 / 스폰 산개 / 타격감 (D보드)" },
        // 10/15: new Config { label = "킹 슬라임 (일반 슬라임으로만 분열) (D보드)" },
        // 10/15 측정: "기준 (D보드)", "전사 공격 속도 초당 1회 (D보드)"(warriorAps = 1f), "넉백 끔 (D보드)"(knockback = 0f),
        //            "킹 슬라임 C의뢰 / 시작 파티"(rank C, weeks 1, runs 20), B/A/S도 같은 랭크 파티(partyRank)로 각 20회
        // 10/15: new Config { label = "파티 케미 확인 (D보드)", runs = 2 },
        // 10/15 측정: new Config { label = "파티 고정 + 케미 (D보드, 12주)", weeks = 12, fixedParty = true },
        // 10/15 측정: new Config { label = "파티 고정 + 케미 없음 (D보드, 12주)", weeks = 12, fixedParty = true, noChemistry = true },
        // 10/15 측정: "성향 이동 켬/끔 (D보드)", "성향 이동 켬/끔 / B의뢰 B랭크 파티"(rank B, partyRank B, weeks 1, runs 20, noRoles)
        // 10/16 측정: "사제 전열 뒤로 피함 / B의뢰 B랭크 파티", "사제 전열 뒤로 피함 (D보드)"
        // 10/16 측정: "도적단 3클래스 / B의뢰 B랭크 파티"(rank B, partyRank B, weeks 1, runs 20)
        // 10/16: new Config { label = "도적 은신 확인 (D보드)", weeks = 2, runs = 3 },
        // 10/16 측정: "도적단 3/4/5명 / B의뢰 B랭크 파티"(gangSize 3/4/5, rank B, partyRank B, weeks 1, runs 20)
        // 10/16 측정: "도적 시작 은신 3초 / 도적단 3명 / B의뢰 B랭크 파티"(gangSize 3, noBacklineDefense 없던 시점)
        // 10/16 측정: "1번: 후열 방어 + 시작 은신 3초"(B, gangSize 3), "2번: 시작 은신 1.5초, 후열 방어 없음"(openingStealth 1.5, noBacklineDefense)
        // 10/16 측정: "현재 상태 / D보드", "현재 상태 / C의뢰 시작 파티"(C, weeks 1, runs 20), A·S도 같은 랭크 파티로
        // 10/16: new Config { label = "사기·부상 판정 확인 (D보드)", weeks = 3, runs = 3 },
        // 10/16: new Config { label = "사기 상승 확인 (D보드)", weeks = 4, runs = 3 },
        // 10/16 측정: "새 사제 (이동 중 마나, 전체 힐) / D보드", "새 사제 / C의뢰 시작 파티"(C, weeks 1, runs 20), "새 사제 / B의뢰 B랭크 파티"(B, partyRank B, weeks 1, runs 20)
        // 10/16 측정: "초원 늑대 추가 / D보드" (출혈 스킬이 한 번도 안 나감)
        // 10/16 측정: "초원 늑대 첫 공격 출혈 / D보드"
        // 10/16 측정: "초원 늑대 무리(D 3마리) / D보드" (첫 공격 출혈)
        // 10/16 측정: "초원 늑대 무리, 스킬로만 출혈 / D보드"
        // 10/16: new Config { label = "지역 의뢰 흐름 확인 / 초원", weeks = 2, runs = 1 },
        // 10/16: new Config { label = "드래곤 둥지 확인 / S의뢰 S랭크 파티", rank = RankType.S, partyRank = RankType.S, weeks = 1, runs = 1 },
        // 10/16 측정: "보스 확인 / 초원 C랭크 파티"(region 0, partyRank C, weeks 1, runs 4), 숲·지하 묘지 B, 오크 부락 A, 드래곤 둥지 S(runs 2)
        // 10/16: "보스 스킬 확인(마나 가득) / 초원·숲·지하 묘지·오크 부락·드래곤 둥지"(partyRank S, bossFullMp)
        // 10/16: "네크로맨서 지정(마나 가득) / 지하 묘지"(bossOverride Enemy_SkeletonNecromancer), "오크 백부장 지정(마나 가득) / 오크 부락"(Enemy_OrcCenturion)
        // 10/16: new Config { label = "드랍 아이템 확인 / 초원", weeks = 2, runs = 1 },
        // 10/16: "의뢰 종류 확인 / 재료 수급·구출·보스 토벌 / 초원"(questType)
        // 10/17 측정: "방 Lv.1 (4명) / 초원"(weeks 4, runs 5), "방 Lv.3 (6명) / 초원"(roomLevel 3)
        // 10/17 측정: "D랭크 6명/4명 / 초원"(weeks 4, runs 5, partyRank D, partySize 6·4), "D랭크 6명/4명 / 숲"(region 1, weeks 1, runs 10)
        // 10/17 측정: "재료 수급 편향 / D랭크 6명 / 초원"(weeks 4, runs 3, partyRank D, partySize 6, roomLevel 3, questType Gather) — 편향 50%
        // 10/17: new Config { label = "킹 슬라임 점프(마나 가득) / 초원", weeks = 1, runs = 3, partyRank = RankType.C, bossFullMp = true, bossOverride = "Enemy_KingSlime" },
        // 10/17 측정: "재료 수급 편향 80% / D랭크 6명 / 초원"(weeks 4, runs 5, partyRank D, partySize 6, roomLevel 3, questType Gather)
        // 10/17 측정: "킹 슬라임 착지 범위 피해 / 초원 / C랭크 파티·D랭크 4명"(weeks 1, runs 6, partyRank C·D, bossOverride Enemy_KingSlime)
        // 10/17 측정: "D랭크 슬라임 의뢰만 (보스 토벌, 킹 슬라임) / D랭크 4명"(rank D, weeks 1, runs 12, partyRank D, questType BossHunt, slimeOnly, bossOverride Enemy_KingSlime)
        // 10/17 측정: "보스전만 (D랭크, 킹 슬라임) / D랭크 4명"(rank D, weeks 1, runs 20, partyRank D, questType BossHunt, bossOnly, bossOverride Enemy_KingSlime) — 쉬는 시간 1.4초·2초
        // 10/18 측정: "보스전만 / 은신 면역 / 쉬는 시간 1.5초·2.0초 / D랭크 4명"(위 설정 + kingHopInterval 1.5·2.0)
        // 10/18 측정: new Config { label = "오크 부락 / A랭크 파티", region = 3, weeks = 1, runs = 10, partyRank = RankType.A }, — 봇이 낮은 랭크를 골라 전부 B의뢰
        // 10/18 측정: "오크 부락 A랭크 의뢰 / A랭크 파티"(rank A, region 3, weeks 1, runs 10, partyRank A)
        // 10/18 측정: "오크 부락 A랭크 의뢰 / A랭크 6명"(rank A, region 3, weeks 1, runs 10, partyRank A, partySize 6, roomLevel 3)
        // 10/18 측정: "오크 부락 A랭크 의뢰 / S랭크 4·5·6명"(rank A, region 3, weeks 1, runs 10, partyRank S, partySize 4·5·6, roomLevel 3)
        // 10/18 측정: new Config { label = "[새 뽑기+방어력] 오크 부락 B랭크 의뢰 / A랭크 4명", rank = RankType.B, region = 3, weeks = 1, runs = 10, partyRank = RankType.A },
        // 10/18 측정: new Config { label = "[새 뽑기+방어력] 오크 부락 A랭크 의뢰 / A랭크 4명", rank = RankType.A, region = 3, weeks = 1, runs = 10, partyRank = RankType.A },
        // 10/18 측정: new Config { label = "[새 뽑기+방어력] 오크 부락 A랭크 의뢰 / A랭크 6명", rank = RankType.A, region = 3, weeks = 1, runs = 10, partyRank = RankType.A, partySize = 6, roomLevel = 3 },
        // 10/18 측정: "[새 뽑기+방어력] 오크 부락 A랭크 의뢰 / S랭크 6명"(rank A, region 3, weeks 1, runs 10, partyRank S, partySize 6, roomLevel 3)
        new Config { label = "[재측정] 재료 수급 / D랭크 6명 / 초원", weeks = 4, runs = 5, partyRank = RankType.D, partySize = 6, roomLevel = 3, questType = QuestType.Gather },
        new Config { label = "[재측정] 재료 수급 / D랭크 4명 / 초원", weeks = 4, runs = 5, partyRank = RankType.D, questType = QuestType.Gather },
        new Config { label = "[재측정] 오크 부락 B랭크 의뢰 / A랭크 4명", rank = RankType.B, region = 3, weeks = 1, runs = 10, partyRank = RankType.A },
        new Config { label = "[재측정] 오크 부락 A랭크 의뢰 / A랭크 4명", rank = RankType.A, region = 3, weeks = 1, runs = 10, partyRank = RankType.A },
        new Config { label = "[재측정] 오크 부락 A랭크 의뢰 / A랭크 6명", rank = RankType.A, region = 3, weeks = 1, runs = 10, partyRank = RankType.A, partySize = 6, roomLevel = 3 },
        new Config { label = "[재측정] 오크 부락 A랭크 의뢰 / S랭크 6명", rank = RankType.A, region = 3, weeks = 1, runs = 10, partyRank = RankType.S, partySize = 6, roomLevel = 3 },
        // 10/18 측정: "보스전만 킹 슬라임 / D랭크 4명"(rank D, weeks 1, runs 20, partyRank D, questType BossHunt, bossOnly, bossOverride Enemy_KingSlime), 점프 중 면역·무리=2회 상태
        //   kingDamage 50·60·70 (쉬는 시간 1.5초): 11승·0승·0승 / kingHopInterval 1.2·0.9·0.6 (피해 40): 10승·0승·0승 (기준 40·1.5초는 20승)
        // 10/18: 분열 슬라임 0.5초 기절 확인 (킹 슬라임 분열·미니 슬라임 94/94마리 기절)
        // 10/18 측정 (귀환 규칙: 절반 이상 쓰러지면 귀환으로 바꾼 뒤), 오크 부락 A의뢰 A랭크 4명/6명 각 10회:
        //   기준 2승/2승, 방법1 lowerRankStats 0승/0승, 방법2 picksMin {1,2,2,3,3}·picksMax {2,3,3,4,5} 1승/5승, 방법3 packPickCost 2 7승/10승
        // 10/18 측정: "D의뢰 초원 / D랭크 6명"(rank D, weeks 1, runs 20, partyRank D, partySize 6, roomLevel 3) isoRoomSize 18·22 둘 다 20승, 전사 평균 0.2명
        // 10/18 측정: "보스전만 킹 슬라임 / D랭크 4명" 쉬는 시간 1.5초 + 점프 중 면역 → 20승, 전사 0명
        // 예: new Config { label = "C랭크 의뢰 강제", rank = RankType.C },
        // 예: new Config { label = "D의뢰 / 시작 파티, 첫 주만", rank = RankType.D, weeks = 1 },
        // 예: new Config { label = "공격 x2.0 / C의뢰 / 시작 파티", rank = RankType.C, weeks = 1, enemyAtkScale = 2f },
        // new Config { label = "B랭크 의뢰 / B랭크 파티(생성된 레벨 그대로)", rank = RankType.B, partyRank = RankType.B, weeks = 1, runs = 20 },
        // new Config { label = "B랭크 의뢰 / C랭크 Lv4 파티", rank = RankType.B, partyRank = RankType.C, partyMinLevel = 4, weeks = 1, runs = 20 },
        // new Config { label = "B랭크 의뢰 / D랭크 Lv4 파티", rank = RankType.B, partyRank = RankType.D, partyMinLevel = 4, weeks = 1, runs = 20 },
        // 예: new Config { label = "B랭크", rank = RankType.B },
        //     new Config { label = "D랭크 사제 없음", rank = RankType.D, noHealer = true },
    };
    static int configIndex;
    static RankType? forceRank => configs[configIndex].rank;
    static bool noHealer => configs[configIndex].noHealer;
    static int Weeks => configs[configIndex].weeks > 0 ? configs[configIndex].weeks : WEEKS;
    static int Runs => configs[configIndex].runs > 0 ? configs[configIndex].runs : RUNS;

    // 시작 모험가를 지정한 랭크/레벨의 4인 파티로 교체
    static void ReplaceRoster(GameManager gm)
    {
        Config config = configs[configIndex];
        MethodInfo levelUp = typeof(GameManager).GetMethod("LevelUp", BindingFlags.NonPublic | BindingFlags.Instance);
        gm.adventurers.Clear();
        List<string> info = new List<string>();
        // 지정 인원 (기본 4명). 5명째 궁수, 6명째 전사
        JobType[] jobs = { JobType.Warrior, JobType.Mage, JobType.Rogue, JobType.Healer, JobType.Archer, JobType.Warrior };
        int size = config.partySize > 0 ? Mathf.Clamp(config.partySize, 1, jobs.Length) : 4;
        for (int j = 0; j < size; j++)
        {
            JobType job = jobs[j];
            Adventurer adv = AdventurerGenerator.Generate(config.partyRank.Value, job);
            while (adv.level < config.partyMinLevel) levelUp.Invoke(gm, new object[] { adv });
            adv.exp = 0;
            gm.adventurers.Add(adv);
            info.Add($"{DisplayNames.JobToKorean(adv.job)} {adv.rank} Lv{adv.level} HP{adv.hp} ATK{adv.atk} DEF{adv.def} 특성{adv.traits.Count}개");
        }
        gm.levelUpLog.Clear();
        Debug.Log($"[Play] 지정 파티: {string.Join(" / ", info)}");
    }
    static readonly List<string> allOutcomes = new List<string>(), week1Outcomes = new List<string>();
    const float DUNGEON_TIME_SCALE = 3f;
    const int ROSTER_TARGET = 6;
    const int HIRE_BUFFER = 100;
    static readonly int[] salaryBase = { 50, 100, 200, 350, 500 }; // GameManager.GetRankSalaryBase와 같은 값 (예상 월급 계산용)

    static int step, weeksPlayed;
    static double stepStart;
    static Party party;
    static int startAllies, goldBeforeQuest, weekStartGold, hireSpent, reviveSpent, lootValue, lootCount, shopEarned;
    static string weekDate, questInfo, outcome;
    static QuestData chosenQuest;
    static bool fled;
    static readonly List<string> ledger = new List<string>();
    static int totalReward, totalExtra, totalHire, totalSalary, totalRevive, totalLootValue, totalShop;

    // 반복 실행 기록
    static int runIndex;
    static readonly List<string> runOutcomes = new List<string>();
    static readonly List<string> runSummaries = new List<string>();
    static readonly List<int> runGold = new List<int>(), runRep = new List<int>(), runWins = new List<int>(), runDead = new List<int>(), runLeveled = new List<int>(), runWarriors = new List<int>();
    // 전사 유무별 결과: 이번 주 파티에 전사가 있었는지, 그 주의 결과
    static bool weekHadWarrior; static int weekWarriorsOnRoster;
    static int withWarriorWeeks, withWarriorWins, noWarriorWeeks, noWarriorWins, secondWarriorWeek, secondWarriorRuns, warriorHires;
    static readonly List<string> noWarriorOutcomes = new List<string>();
    static bool IsWarrior(Adventurer a) => a.job == JobType.Warrior && !a.isDead && !a.isStranded;

    // 모닥불 기록: 방 번호, 생존자 수, 회복 전 평균/최저 체력 비율, 회복 후 평균 체력 비율
    struct CampfireRecord { public int room, alive, startAllies; public float before, minBefore, after; }
    static readonly List<CampfireRecord> campfires = new List<CampfireRecord>();
    static readonly List<float> victoryHp = new List<float>(); // 마지막 방을 깬 순간의 평균 체력 비율
    static int snapAlive; static float snapAvg, snapMin;

    // 갈 방향 고르기: 지도 없이 탐험하는 플레이어처럼 안 가 본 방을 먼저 가고(여럿이면 무작위),
    // 지금 방에 안 가 본 길이 없으면 안 가 본 길이 남은 가장 가까운 방 쪽으로 되돌아감
    static int PickDirection(BattleManager bm)
    {
        DungeonLayout layout = bm.Layout;
        int current = bm.currentStageIndex;

        List<int> fresh = new List<int>();
        for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
        {
            int next = layout.GetNeighbor(current, d);
            if (next >= 0 && !bm.IsRoomVisited(next)) fresh.Add(d);
        }
        if (fresh.Count > 0) return fresh[Random.Range(0, fresh.Count)];

        // 가 본 방만 거쳐서, 안 가 본 이웃이 있는 방까지의 첫걸음을 찾음
        Dictionary<int, int> firstStep = new Dictionary<int, int>();
        Queue<int> queue = new Queue<int>();
        firstStep[current] = -1;
        queue.Enqueue(current);
        while (queue.Count > 0)
        {
            int room = queue.Dequeue();
            for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
            {
                int next = layout.GetNeighbor(room, d);
                if (next < 0) continue;
                int step = room == current ? d : firstStep[room];
                if (!bm.IsRoomVisited(next)) return step;
                if (firstStep.ContainsKey(next)) continue;
                firstStep[next] = step;
                queue.Enqueue(next);
            }
        }

        // 다 가 봤다면 아무 길이나 (보스방을 정리하면 던전이 끝나므로 보통 여기까지 오지 않음)
        for (int d = 0; d < DungeonLayoutRules.DirectionCount; d++)
            if (layout.GetNeighbor(current, d) >= 0) return d;
        return 0;
    }

    // 케미 기록: 출발할 때의 케미 레벨별 던전 수와 승리, 우울한 모험가가 명단에 있던 (사람 x 주)
    static int weekChemLevel, depressedWeeks;

    // 성향 관찰: (편, 성향)별 가장 가까운 상대와의 거리 표본, 우회 유닛의 공격 순간 기록, 직업별 사망
    static readonly Dictionary<string, float> roleDistSum = new Dictionary<string, float>();
    static readonly Dictionary<string, int> roleDistCount = new Dictionary<string, int>();
    static readonly Dictionary<BattleUnit, float> flankLastAttack = new Dictionary<BattleUnit, float>();
    static int flankAttacks, flankBehind, flankOnBackline; static int enemyFlankAttacks, enemyFlankOnBackline;
    static readonly Dictionary<string, int> deathsByJob = new Dictionary<string, int>();
    static float nextRoleSample;
    static readonly FieldInfo targetField = typeof(BattleUnit).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);

    static readonly Dictionary<string, int> enemySpawns = new Dictionary<string, int>();
    static readonly HashSet<int> seenEnemies = new HashSet<int>();

    static void ObserveRoles()
    {
        foreach (BattleUnit e in BattleUnit.Active)
        {
            if (e == null || !e.CompareTag("Enemy") || !seenEnemies.Add(e.GetInstanceID())) continue;
            string n = DisplayNames.EnemyToKorean(e.gameObject.name.Replace("(Clone)", ""));
            enemySpawns[n] = (enemySpawns.TryGetValue(n, out int k) ? k : 0) + 1;
        }
        float now = Time.time;
        bool sample = now >= nextRoleSample;
        if (sample) nextRoleSample = now + 0.25f;

        foreach (BattleUnit u in BattleUnit.Active)
        {
            if (u == null || u.isDead) continue;
            bool ally = u.CompareTag("Player");
            if (!ally && !u.CompareTag("Enemy")) continue;

            if (sample)
            {
                float near = Mathf.Infinity;
                foreach (BattleUnit o in BattleUnit.Active)
                {
                    if (o == null || o.isDead || o.CompareTag(u.tag) || !(o.CompareTag("Player") || o.CompareTag("Enemy"))) continue;
                    near = Mathf.Min(near, Vector2.Distance(u.transform.position, o.transform.position));
                }
                if (near < Mathf.Infinity)
                {
                    string key = $"{(ally ? "아군" : "적")} {u.Role}";
                    roleDistSum[key] = (roleDistSum.TryGetValue(key, out float sum) ? sum : 0f) + near;
                    roleDistCount[key] = (roleDistCount.TryGetValue(key, out int c) ? c : 0) + 1;
                }
            }

            // 우회 유닛의 공격 순간: 대상이 후열인지, 후열이 아니면 등 뒤였는지
            if (u.Role != CombatRole.Flanker) continue;
            float lastAttack = (float)lastAttackField.GetValue(u);
            bool attacked = flankLastAttack.TryGetValue(u, out float prev) && lastAttack > prev + 0.01f;
            flankLastAttack[u] = lastAttack;
            if (!attacked) continue;
            BattleUnit t = targetField.GetValue(u) as BattleUnit;
            if (t == null || t.isDead || t.CompareTag(u.tag)) continue;
            bool onBackline = CombatPositioning.IsBackline(t.Role);
            if (ally)
            {
                flankAttacks++;
                if (onBackline) flankOnBackline++;
                else
                {
                    BattleUnit tt = targetField.GetValue(t) as BattleUnit;
                    Vector2 facing = tt != null && !tt.isDead ? (Vector2)tt.transform.position : (Vector2)u.transform.position;
                    if (tt != null && !tt.isDead && CombatPositioning.IsBehind(u.transform.position, t.transform.position, CombatPositioning.BackDirection(t.transform.position, facing))) flankBehind++;
                }
            }
            else
            {
                enemyFlankAttacks++;
                if (onBackline) enemyFlankOnBackline++;
            }
        }
    }

    // 로그로 세는 것: 실제 전사 수(유해를 수습하면 전사자 목록에서 빠지므로), 우울함 발생 수
    static int deathsLogged, depressionLogged, stalemates, partyHeals, wolfBleeds, bleedKills;
    static void OnLog(string message, string stack, LogType type)
    {
        if (message.Contains("숨은 채 전투를 시작") && configs[configIndex].openingStealth.HasValue)
            foreach (Ally_Rogue r in Object.FindObjectsByType<Ally_Rogue>(FindObjectsSortMode.None))
                r.openingStealthDuration = configs[configIndex].openingStealth.Value; // 로그 직후 BeginStealth(openingStealthDuration)가 이 값을 읽음
        if (message.Contains("💀 [전사]"))
        {
            deathsLogged++;
            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                Adventurer dead = gm.deadAdventurers.Find(a => message.Contains($"[전사] {a.name}이(가)"));
                string job = dead != null ? DisplayNames.JobToKorean(dead.job) : "?";
                deathsByJob[job] = (deathsByJob.TryGetValue(job, out int n) ? n : 0) + 1;
            }
        }
        else if (message.Contains("😢 [우울함]")) depressionLogged++;
        else if (message.Contains("의 파티 힐!")) partyHeals++;
        else if (message.Contains("📜 [의뢰 성공]"))
        {
            var m = System.Text.RegularExpressions.Regex.Match(message, @"보상 ([0-9,]+) G");
            if (m.Success && int.TryParse(m.Groups[1].Value.Replace(",", ""), out int paid)) paidReward += paid;
        }
        else if (message.Contains("[초원 늑대] 출혈 공격")) wolfBleeds++;
        else if (message.Contains("🩸 [출혈]") && message.Contains("Unit_")) bleedKills++;
    }
    const double STALEMATE_SECONDS = 600; // 던전 하나가 이보다 오래 걸리면 교착으로 보고 도주 (사제만 남아 서로 회복만 하는 경우 등)
    static readonly int[] chemWeeks = new int[5], chemWins = new int[5];

    // 보스전 기록: 보스방에 들어간 던전 수와 결과, 보스방에서 쓰러진 아군, 킹 슬라임 처치 여부, 보스전 시간
    static bool bossMet, kingSeen, kingDown; static int bossEntryAlive; static float bossStartTime;
    static int bossFights, bossWins, bossDeaths, kingKills, dungeonsRun;
    static readonly List<string> bossFailOutcomes = new List<string>();
    static readonly List<int> bossSeconds = new List<int>();
    static readonly List<string> bossEntryInfo = new List<string>();

    // 넉백 기록: 적마다 밀려나기 시작한 위치와 그때의 마지막 공격 시각을 들고 있다가, 끝나면 이동 거리·바닥 밖 여부·밀리는 중 공격 여부를 셈
    struct KnockState { public Vector3 start; public float lastAttack; public float startTime; }
    static readonly Dictionary<BattleUnit, KnockState> knocking = new Dictionary<BattleUnit, KnockState>();
    static int kbCount, kbOffFloor, kbAttackedDuring, kbNextAttackSamples; static float kbDistSum, kbDistMin = 99f, kbDistMax, kbNextAttackSum;
    static readonly Dictionary<BattleUnit, float> kbEndTime = new Dictionary<BattleUnit, float>(); // 넉백이 끝난 시각 (다음 공격까지 걸린 시간 측정용)
    static readonly FieldInfo kbTimeField = typeof(BattleUnit).GetField("knockbackTimeLeft", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo lastAttackField = typeof(BattleUnit).GetField("lastAttackTime", BindingFlags.NonPublic | BindingFlags.Instance);

    // 던전 한 번에 방을 몇 개 정리했고 몇 번 이동했는지 (승리한 던전 기준)
    static readonly List<int> clearedOnWin = new List<int>(), movesOnWin = new List<int>(), totalRoomsOnWin = new List<int>();
    static int roomMoves;
    static readonly int[] roomTypeCounts = new int[5]; // 정리한 방의 종류별 횟수 (보스방 제외: 정리하면 바로 끝남)

    // 살아 있는 아군의 체력 비율 (평균, 최저)
    static void Snapshot(out int alive, out float avg, out float min)
    {
        float sum = 0f; alive = 0; min = 1f;
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Player"))
        {
            BattleUnit u = obj.GetComponent<BattleUnit>();
            if (u == null || u.isDead || u.maxHp <= 0f) continue;
            float ratio = u.currentHp / u.maxHp;
            alive++; sum += ratio; if (ratio < min) min = ratio;
        }
        avg = alive > 0 ? sum / alive : 0f;
        if (alive == 0) min = 0f;
    }

    // 방 적 뽑기 설정을 시험 설정대로 (처음 본 값을 기억해 두고, 설정이 비어 있으면 그 값으로 되돌림)
    static int[] origPicksMin, origPicksMax; static int origIsoRoomSize, origPackPickCost; static bool origLowerRankStats; static bool origSpawnSaved;
    static void ApplySpawnOverrides(RoomSpawnSettings rs, Config cfg)
    {
        if (!origSpawnSaved)
        {
            origSpawnSaved = true;
            origPicksMin = (int[])rs.minPicksPerRank.Clone(); origPicksMax = (int[])rs.maxPicksPerRank.Clone();
            origPackPickCost = rs.packPickCost; origLowerRankStats = rs.lowerRankStats;
        }
        rs.minPicksPerRank = (int[])(cfg.picksMin ?? origPicksMin).Clone();
        rs.maxPicksPerRank = (int[])(cfg.picksMax ?? origPicksMax).Clone();
        rs.lowerRankStats = cfg.lowerRankStats || origLowerRankStats;
        rs.packPickCost = cfg.packPickCost > 0 ? cfg.packPickCost : origPackPickCost;
    }
    // 던전 씬이 열리면 (첫 방 맵을 만들기 전) 방 크기를 시험 설정대로
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (MapGenerator.Instance == null) return;
        if (origIsoRoomSize == 0) origIsoRoomSize = MapGenerator.Instance.isoRoomSize;
        int size = configs[configIndex].isoRoomSize > 0 ? configs[configIndex].isoRoomSize : origIsoRoomSize;
        MapGenerator.Instance.isoRoomSize = size;
    }

    [MenuItem("Tools/TEMP 4주 플레이")]
    static void Run()
    {
        if (!Application.isPlaying) { Debug.LogError("[Play] 플레이 모드가 아님"); return; }
        Application.runInBackground = true;
        TutorialManager.Suppressed = true; // 튜토리얼 설명 창이 전투를 멈추지 않게
        configIndex = 0;
        ResetConfig();
        stepStart = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log("[Play] 시작");
    }

    static void ResetConfig()
    {
        runIndex = 0;
        runSummaries.Clear(); campfires.Clear(); victoryHp.Clear(); allOutcomes.Clear(); week1Outcomes.Clear();
        clearedOnWin.Clear(); movesOnWin.Clear(); totalRoomsOnWin.Clear();
        System.Array.Clear(roomTypeCounts, 0, roomTypeCounts.Length);
        runGold.Clear(); runRep.Clear(); runWins.Clear(); runDead.Clear(); runLeveled.Clear(); runWarriors.Clear();
        withWarriorWeeks = withWarriorWins = noWarriorWeeks = noWarriorWins = secondWarriorWeek = secondWarriorRuns = warriorHires = 0;
        noWarriorOutcomes.Clear();
        bossFights = bossWins = bossDeaths = kingKills = dungeonsRun = 0;
        System.Array.Clear(chemWeeks, 0, 5); System.Array.Clear(chemWins, 0, 5); depressedWeeks = 0;
        deathsLogged = depressionLogged = stalemates = partyHeals = wolfBleeds = bleedKills = 0;
        offFloorSamples = unitSamples = 0; offFloorUnits.Clear();
        roleDistSum.Clear(); roleDistCount.Clear(); flankLastAttack.Clear(); deathsByJob.Clear(); enemySpawns.Clear(); seenEnemies.Clear();
        flankAttacks = flankBehind = flankOnBackline = enemyFlankAttacks = enemyFlankOnBackline = 0;
        bossFailOutcomes.Clear(); bossSeconds.Clear(); bossEntryInfo.Clear();
        kbCount = kbOffFloor = kbAttackedDuring = kbNextAttackSamples = 0; kbDistSum = kbDistMax = kbNextAttackSum = 0f; kbDistMin = 99f;
        knocking.Clear(); kbEndTime.Clear();
        ResetRun();
    }

    // 전투 중 매 틱: 설정에 따른 전사 수치 덮어쓰기, 보스방 진입 기록, 넉백 관찰
    static void ObserveBattle(BattleManager bm)
    {
        Config config = configs[configIndex];
        bm.combatMovement.useRoles = !config.noRoles;
        bm.combatMovement.backlineDefense = !config.noBacklineDefense;
        ObserveRoles();
        foreach (Ally_Warrior w in Object.FindObjectsByType<Ally_Warrior>(FindObjectsSortMode.None))
        {
            if (config.warriorAps > 0f) { w.attacksPerSecond = config.warriorAps; w.attackCooldown = 1f / config.warriorAps; }
            if (config.knockback.HasValue) w.knockbackDistance = config.knockback.Value;
        }

        // 보스방
        if (!bossMet && bm.isBattleActive && bm.GetRoomType(bm.currentStageIndex) == DungeonRoomType.Boss)
        {
            bossMet = true; kingSeen = false; kingDown = false;
            bossEntryAlive = bm.allyCount;
            bossStartTime = Time.time;
            Snapshot(out int _, out float avg, out float _);
            bossEntryInfo.Add($"{bm.allyCount}명 {avg * 100f:F0}%");
        }
        if (bossMet && !kingDown)
        {
            bool kingAlive = false;
            foreach (EnemySlime s in Object.FindObjectsByType<EnemySlime>(FindObjectsSortMode.None))
                if (s.isKing && !s.isDead) { kingAlive = true; break; }
            if (kingAlive) kingSeen = true;
            else if (kingSeen) { kingDown = true; kingKills++; }
        }

        // 넉백: 적이 밀려나기 시작/끝나는 순간을 잡음
        float now = Time.time;
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            BattleUnit u = obj.GetComponent<BattleUnit>();
            if (u == null || u.isDead) continue;
            float left = (float)kbTimeField.GetValue(u);
            float lastAttack = (float)lastAttackField.GetValue(u);

            if (left > 0f && !knocking.ContainsKey(u))
            {
                knocking[u] = new KnockState { start = u.transform.position, lastAttack = lastAttack, startTime = now };
                kbEndTime.Remove(u);
            }
            else if (left <= 0f && knocking.TryGetValue(u, out KnockState ks))
            {
                knocking.Remove(u);
                float dist = Vector3.Distance(ks.start, u.transform.position);
                kbCount++; kbDistSum += dist; kbDistMin = Mathf.Min(kbDistMin, dist); kbDistMax = Mathf.Max(kbDistMax, dist);
                if (MapGenerator.Instance != null && !MapGenerator.Instance.IsOnFloor(u.transform.position)) kbOffFloor++;
                // 경직 중에는 쿨타임이 멈춰 lastAttackTime이 경과 시간만큼 늘어남 → 그보다 크게 바뀌었을 때만 공격으로 봄
                if (lastAttack - ks.lastAttack > (now - ks.startTime) + 0.05f) kbAttackedDuring++;
                kbEndTime[u] = now;
            }
            else if (left <= 0f && kbEndTime.TryGetValue(u, out float end) && lastAttack > end)
            {
                // 넉백이 끝나고 다음 공격까지 걸린 시간
                kbNextAttackSum += lastAttack - end; kbNextAttackSamples++;
                kbEndTime.Remove(u);
            }
        }
    }

    // 던전이 끝났을 때 보스전 결과를 정리
    static void CloseBossFight(BattleManager bm, string result)
    {
        dungeonsRun++;
        if (!bossMet) return;
        bossFights++;
        if (result == "승리") bossWins++; else bossFailOutcomes.Add(result);
        bossDeaths += Mathf.Max(0, bossEntryAlive - (bm != null ? bm.allyCount : 0));
        bossSeconds.Add(Mathf.RoundToInt(Time.time - bossStartTime));
        bossMet = false;
    }

    static string Tally(List<string> outcomes)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (string o in outcomes) counts[o] = counts.TryGetValue(o, out int c) ? c + 1 : 1;
        List<string> parts = new List<string>();
        foreach (var pair in counts) parts.Add($"{pair.Key} {pair.Value}");
        return string.Join(", ", parts);
    }

    static void ResetRun()
    {
        step = 0; weeksPlayed = 0; party = null;
        ledger.Clear(); runOutcomes.Clear();
        totalReward = totalExtra = totalHire = totalSalary = totalRevive = totalLootValue = totalShop = 0;
    }

    static string Stat(List<int> values)
    {
        if (values.Count == 0) return "-";
        int min = int.MaxValue, max = int.MinValue; float sum = 0f;
        foreach (int v in values) { sum += v; if (v < min) min = v; if (v > max) max = v; }
        return $"평균 {sum / values.Count:F1} (최소 {min}, 최대 {max})";
    }

    static void Go(int next) { step = next; stepStart = EditorApplication.timeSinceStartup; }
    static double Elapsed => EditorApplication.timeSinceStartup - stepStart;
    static void Stop(string why) { Time.timeScale = 1f; EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; SceneManager.sceneLoaded -= OnSceneLoaded; Debug.Log($"[Play] 종료: {why}"); }

    static bool Healthy(Adventurer a) => !a.isDead && !a.isStranded && a.recoveryWeeks <= 0;
    static int Salary(Adventurer a) => salaryBase[Mathf.Clamp((int)a.rank, 0, salaryBase.Length - 1)] + a.level * 10;

    static string Roster(GameManager gm)
    {
        List<string> parts = new List<string>();
        foreach (Adventurer a in gm.adventurers)
        {
            string state = a.isStranded ? $"낙오({a.strandedWeeksRemaining}주)" : a.recoveryWeeks > 0 ? $"요양{a.recoveryWeeks}주" : "정상";
            parts.Add($"{a.name}({DisplayNames.JobToKorean(a.job)} {a.rank} Lv{a.level} exp{a.exp}/{a.expToNextLevel} {a.currentHp}/{a.hp} {state})");
        }
        return string.Join(", ", parts);
    }

    // ─── 로비에서 한 주를 준비하고 던전으로 출발 ───
    static void PlanWeek(GameManager gm)
    {
        weekDate = $"{gm.month}월 {gm.week}주차";
        weekStartGold = gm.gold;
        hireSpent = reviveSpent = lootValue = lootCount = shopEarned = 0;
        fled = false; outcome = ""; questInfo = ""; chosenQuest = null;
        bossMet = false; knocking.Clear(); kbEndTime.Clear();
        Debug.Log($"[Play] ── {weekDate} 시작: 골드 {gm.gold}, 명성 {gm.reputation}, 모험가 {gm.adventurers.Count}명, 월급 예정 {gm.GetTotalSalary()}G");

        // 0. 상점: 지난 던전에서 가져온 전리품을 모두 판매
        if (LobbyManager.Instance.shopPanel != null)
        {
            int goldBeforeShop = gm.gold;
            LobbyManager.Instance.shopPanel.OnClickSellEverything();
            shopEarned = gm.gold - goldBeforeShop;
            if (shopEarned > 0) Debug.Log($"[Play] 상점: 전리품 판매 +{shopEarned}G");
        }

        // 1. 신전: 영혼석이 있는 전사자를 월급 여유가 될 때 부활
        foreach (Adventurer dead in new List<Adventurer>(gm.deadAdventurers))
        {
            int cost = gm.GetReviveCost(dead.rank);
            if (!gm.HasSpiritStone(dead) || gm.gold - cost < gm.GetTotalSalary() + HIRE_BUFFER) continue;
            if (gm.ReviveWithSpiritStone(dead)) { reviveSpent += cost; Debug.Log($"[Play] 신전 부활: {dead.name} (-{cost}G)"); }
        }

        if (configs[configIndex].partyRank.HasValue && weeksPlayed == 0) ReplaceRoster(gm);
        if (weeksPlayed == 0)
        {
            // 회차마다 GameManager가 새로 만들어지므로 매번 한 번씩 곱함
            Config config = configs[configIndex];
            if (config.enemyAtkScale > 0f)
                for (int i = 0; i < gm.enemyScaling.attackMultiplierPerRank.Length; i++) gm.enemyScaling.attackMultiplierPerRank[i] *= config.enemyAtkScale;
            if (config.enemyHpScale > 0f)
                for (int i = 0; i < gm.enemyScaling.hpMultiplierPerRank.Length; i++) gm.enemyScaling.hpMultiplierPerRank[i] *= config.enemyHpScale;
            if (config.injuryThreshold > 0f) gm.injuryHpThreshold = config.injuryThreshold;
            if (config.minorWeeks > 0) gm.minorInjuryWeeks = config.minorWeeks;
            if (config.noChemistry)
            {
                gm.chemistry.statBonus = new float[gm.chemistry.statBonus.Length];
                gm.chemistry.depressionChance = new float[gm.chemistry.depressionChance.Length];
            }
            if (runIndex == 0)
                Debug.Log($"[Play] 부상 기준 {gm.injuryHpThreshold * 100f:F0}%, 경상 {gm.minorInjuryWeeks}주, 중상 {gm.severeInjuryWeeks}주");
            if (runIndex == 0)
                Debug.Log($"[Play] 적 배율: 공격력 [{string.Join(", ", gm.enemyScaling.attackMultiplierPerRank)}], 체력 [{string.Join(", ", gm.enemyScaling.hpMultiplierPerRank)}]");
        }

        // 2. 접수처: 방문 모험가 확인 후 고용
        gm.EnsureVisitors();
        Config cfg = configs[configIndex];
        if (cfg.warriorVisitor && gm.adventurers.FindAll(IsWarrior).Count < 2 && gm.visitors.Count > 0 && !gm.visitors.Exists(v => v.job == JobType.Warrior))
        {
            RankType r = gm.visitors[0].rank;
            gm.visitors[0] = AdventurerGenerator.Generate(r, JobType.Warrior);
            Debug.Log($"[Play] 전사 방문 보장: 방문자 한 명을 전사 {r}로 교체");
        }
        List<string> visitorInfo = new List<string>();
        foreach (Adventurer v in gm.visitors) visitorInfo.Add($"{DisplayNames.JobToKorean(v.job)} {v.rank}({gm.GetHireCost(v.rank)}G)");
        Debug.Log($"[Play] 접수처 방문자: {string.Join(", ", visitorInfo)}");

        while (!configs[configIndex].partyRank.HasValue && gm.adventurers.Count < ROSTER_TARGET)
        {
            Adventurer best = null;
            bool needWarrior = cfg.warriorFirst && gm.adventurers.FindAll(IsWarrior).Count < 2;
            foreach (Adventurer v in gm.visitors)
            {
                int cost = gm.GetHireCost(v.rank);
                if (gm.gold - cost < gm.GetTotalSalary() + Salary(v) + HIRE_BUFFER) continue; // 월급날을 넘길 수 있을 때만
                if (needWarrior && v.job != JobType.Warrior && gm.adventurers.Count >= ROSTER_TARGET - 1) continue; // 전사 자리 하나는 비워 둠
                bool better = best == null
                    || (needWarrior && v.job == JobType.Warrior && best.job != JobType.Warrior)
                    || ((!needWarrior || (v.job == JobType.Warrior) == (best.job == JobType.Warrior)) && v.rank > best.rank);
                if (better) best = v;
            }
            if (best == null) break;

            int hireCost = gm.GetHireCost(best.rank);
            if (!gm.TryHireVisitor(best, out string fail)) { Debug.Log($"[Play] 고용 실패: {fail}"); break; }
            hireSpent += hireCost;
            if (best.job == JobType.Warrior)
            {
                warriorHires++;
                if (gm.adventurers.FindAll(IsWarrior).Count == 2) { secondWarriorWeek += weeksPlayed + 1; secondWarriorRuns++; }
            }
            Debug.Log($"[Play] 고용: {best.name} ({DisplayNames.JobToKorean(best.job)} {best.rank}) -{hireCost}G, 예상 월급 {Salary(best)}G");
        }

        // 3. 파티 편성: 건강한 모험가 중 전사/사제를 우선 넣고 나머지는 공격력 순
        if (party == null)
        {
            party = new Party("1파티");
            gm.partyList.Add(party);
            gm.AssignPartyToRoom(party, 0);
        }
        int partyIndex = gm.partyList.IndexOf(party);
        List<Adventurer> previous = party.members.FindAll(a => !a.isDead && !a.isStranded && gm.adventurers.Contains(a));
        foreach (Adventurer m in new List<Adventurer>(party.members)) { party.members.Remove(m); m.assignedPartyIndex = -1; }

        List<Adventurer> pool = gm.adventurers.FindAll(Healthy);
        if (noHealer) pool.RemoveAll(a => a.job == JobType.Healer);
        pool.Sort((a, b) => b.atk.CompareTo(a.atk));
        List<Adventurer> picked = new List<Adventurer>();
        // 시험용: 1번 방을 지정한 레벨까지 증축 (비용은 돌려줘서 경제 측정에는 영향 없음)
        while (cfg.roomLevel > gm.GetRoomLevel(0) && gm.GetRoomExpandCost(0) >= 0)
        {
            int cost = gm.GetRoomExpandCost(0);
            gm.gold += cost;
            if (!gm.ExpandRoom(0, out string expandMsg)) { gm.gold -= cost; break; }
            Debug.Log($"[Play] {expandMsg}");
        }
        int cap = gm.GetPartyCapacity(party);
        if (cfg.fixedParty) foreach (Adventurer a in previous) if (picked.Count < cap) picked.Add(a); // 지난주 파티원은 다쳤어도 그대로
        Adventurer tank = pool.Find(a => a.job == JobType.Warrior && !picked.Contains(a)); if (tank != null && picked.Count < cap && !picked.Exists(a => a.job == JobType.Warrior)) picked.Add(tank);
        Adventurer healer = pool.Find(a => a.job == JobType.Healer && !picked.Contains(a)); if (healer != null && picked.Count < cap && !picked.Exists(a => a.job == JobType.Healer)) picked.Add(healer);
        // 교대: 둘째 전사는 다른 직업으로 4명을 못 채울 때만 넣음
        if (cfg.warriorFirst)
            foreach (Adventurer a in pool) { if (picked.Count >= cap) break; if (!picked.Contains(a) && a.job != JobType.Warrior) picked.Add(a); }
        foreach (Adventurer a in pool) { if (picked.Count >= cap) break; if (!picked.Contains(a)) picked.Add(a); }
        // 자리가 남으면 체력이 절반 이상 남은 부상자를 넣음 (다친 체력 그대로 싸움)
        if (picked.Count < cap)
        {
            List<Adventurer> hurt = gm.adventurers.FindAll(a => !a.isDead && !a.isStranded && a.recoveryWeeks > 0 && a.currentHp >= a.hp * 0.5f && !(noHealer && a.job == JobType.Healer));
            hurt.Sort((a, b) => ((float)b.currentHp / b.hp).CompareTo((float)a.currentHp / a.hp));
            foreach (Adventurer a in hurt) { if (picked.Count >= cap) break; picked.Add(a); }
        }
        foreach (Adventurer a in picked) { party.members.Add(a); a.assignedPartyIndex = partyIndex; }
        weekChemLevel = gm.GetChemistryLevel(party);
        weekHadWarrior = picked.Exists(a => a.job == JobType.Warrior);
        weekWarriorsOnRoster = gm.adventurers.FindAll(IsWarrior).Count;

        List<string> names = new List<string>();
        foreach (Adventurer a in party.members)
            names.Add($"{a.name}({DisplayNames.JobToKorean(a.job)} {a.rank} Lv{a.level}{(a.recoveryWeeks > 0 ? $" 부상 {a.currentHp}/{a.hp}" : "")})");
        Debug.Log($"[Play] 파티 편성 {party.members.Count}명 (명단 전사 {weekWarriorsOnRoster}명): {string.Join(", ", names)}");

        if (party.members.Count < 2)
        {
            outcome = "휴식(보낼 인원 부족)";
            rewardOfChosen = 0;
            Debug.Log("[Play] 건강한 모험가가 2명 미만이라 이번 주는 쉼");
            goldBeforeQuest = gm.gold;
            LobbyManager.Instance.OnClickConfirmPassWeek();
            FinishWeek(gm, goldBeforeQuest);
            return;
        }

        // 4. 퀘스트 보드: 구출 > 유해 수습 > 일반(가장 낮은 랭크 중 보상이 큰 것)
        QuestBoardPopup[] boards = Object.FindObjectsByType<QuestBoardPopup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (boards.Length == 0) { Stop("QuestBoardPopup 없음"); return; }
        if (cfg.gangSize > 0 && boards[0].questGenerator != null)
            boards[0].questGenerator.banditGang.sizePerRank = new[] { cfg.gangSize, cfg.gangSize, cfg.gangSize, cfg.gangSize, cfg.gangSize };
        if (boards[0].questGenerator != null) ApplySpawnOverrides(boards[0].questGenerator.roomSpawn, cfg);
        boards[0].gameObject.SetActive(false);
        boards[0].gameObject.SetActive(true);
        // 지역을 고른 뒤 그 지역의 의뢰 목록 (기본: 0번 지역 = 초원). 구출·유해 수습은 다른 지역에 있어도 챙김
        // 잠긴 지역을 시험할 때는 명성을 그 지역이 열리는 만큼 올려 둠
        DungeonRegion wanted = boards[0].questGenerator != null ? boards[0].questGenerator.GetRegion(cfg.region) : null;
        if (wanted != null) gm.reputation = Mathf.Max(gm.reputation, ReputationRules.GetRequiredReputation(gm.reputationLevel, wanted.requiredReputationLevel));
        boards[0].SelectRegion(cfg.region);
        List<QuestData> quests = new List<QuestData>(boards[0].ActiveQuests);
        foreach (QuestData q in gm.rescueQuests) if (q != null && !quests.Contains(q)) quests.Insert(0, q);
        foreach (QuestData q in gm.corpseQuests) if (q != null && !quests.Contains(q)) quests.Add(q);

        List<string> board = new List<string>();
        foreach (QuestData q in quests) board.Add($"[{q.rank}{(q.isRescueQuest ? " 구출" : q.isCorpseQuest ? " 유해" : "")}] {q.rewardGold}G");
        Debug.Log($"[Play] 퀘스트 보드: {string.Join(", ", board)}");

        chosenQuest = quests.Find(q => q.isRescueQuest) ?? quests.Find(q => q.isCorpseQuest);
        // 시험용: 지정한 종류의 의뢰 (게시판에 없으면 그 지역에서 새로 만듦)
        if (chosenQuest == null && cfg.questType.HasValue)
        {
            chosenQuest = quests.Find(q => q.questType == cfg.questType.Value && !q.isRescueQuest && !q.isCorpseQuest);
            if (chosenQuest != null && forceRank.HasValue && chosenQuest.rank != forceRank.Value.ToString()) chosenQuest = null;
            if (chosenQuest == null && boards[0].questGenerator != null)
                chosenQuest = forceRank.HasValue
                    ? boards[0].questGenerator.Generate(cfg.region, forceRank.Value, cfg.questType.Value)
                    : boards[0].questGenerator.GenerateInRegion(cfg.region, cfg.questType.Value);
        }
        if (chosenQuest == null && forceRank.HasValue && boards[0].questGenerator != null)
            chosenQuest = boards[0].questGenerator.Generate(cfg.region, forceRank.Value); // 지정 지역에서 그 랭크로
        if (chosenQuest == null)
        {
            foreach (QuestData q in quests)
            {
                EnemyScalingRules.TryParseRank(q.rank, out RankType r);
                RankType bestRank = RankType.S;
                if (chosenQuest != null) EnemyScalingRules.TryParseRank(chosenQuest.rank, out bestRank);
                if (chosenQuest == null || r < bestRank || (r == bestRank && q.rewardGold > chosenQuest.rewardGold)) chosenQuest = q;
            }
        }
        if (chosenQuest == null) { Stop("고를 의뢰가 없음"); return; }

        // 시험용: 보스방의 보스를 지정한 보스로 바꿈 (따라온 무리는 뺌)
        if (!string.IsNullOrEmpty(cfg.bossOverride) && chosenQuest.stages != null)
        {
            GameObject forced = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Characters/{cfg.bossOverride}.prefab");
            foreach (StageData st in chosenQuest.stages)
                if (st != null && st.isBossStage && forced != null) { st.enemyPrefabs.Clear(); st.enemyPrefabs.Add(forced); }
        }

        // 시험용: 보스방 말고는 전부 휴식 방으로 (전투 없이 보스방까지 감)
        if (cfg.bossOnly && chosenQuest.layout != null && chosenQuest.layout.rooms != null)
            for (int i = 0; i < chosenQuest.layout.rooms.Count; i++)
                if (i != chosenQuest.layout.bossRoom) chosenQuest.layout.rooms[i].type = DungeonRoomType.Rest;

        // 시험용: 보스방 밖의 적을 전부 일반 슬라임으로 (인원은 그대로)
        if (cfg.slimeOnly && chosenQuest.stages != null)
        {
            GameObject slime = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Enemy_Slime.prefab");
            foreach (StageData st in chosenQuest.stages)
                if (st != null && !st.isBossStage && slime != null)
                    for (int i = 0; i < st.enemyPrefabs.Count; i++) st.enemyPrefabs[i] = slime;
        }

        questInfo = $"[{chosenQuest.rank}{(chosenQuest.isRescueQuest ? " 구출" : chosenQuest.isCorpseQuest ? " 유해" : "")}] 보상 {chosenQuest.rewardGold}G";
        Debug.Log($"[Play] 의뢰 선택: {chosenQuest.questName} ({questInfo})");
        if (chosenQuest.stages != null && chosenQuest.layout != null)
        {
            List<string> roomCounts = new List<string>();
            for (int i = 0; i < chosenQuest.stages.Count && i < chosenQuest.layout.rooms.Count; i++)
            {
                DungeonRoomType t = chosenQuest.layout.rooms[i].type;
                if (t == DungeonRoomType.Normal && chosenQuest.stages[i] != null && !chosenQuest.stages[i].isBossStage)
                    roomCounts.Add(chosenQuest.stages[i].enemyPrefabs.Count.ToString());
            }
            Debug.Log($"[Play] 전투 방 적 수: {string.Join(", ", roomCounts)}");
        }

        goldBeforeQuest = gm.gold;
        startAllies = 0;
        roomMoves = 0;
        gm.StartQuest(party, chosenQuest);
        Go(3);
    }

    // ─── 결과 확인 후 주간 기록 ───
    static void FinishWeek(GameManager gm, int goldBeforeConfirm)
    {
        int salary = goldBeforeConfirm - gm.gold;
        int dungeonGold = goldBeforeConfirm - goldBeforeQuest;
        // 의뢰 보상은 실제로 지급된 금액 (재료가 모자라면 보스를 잡아도 0, 구출·재료 수급은 귀환·도주로도 지급)
        int reward = paidReward;
        bool won = outcome == "승리";
        int extra = dungeonGold - reward;

        totalReward += reward; totalExtra += extra; totalHire += hireSpent; totalSalary += salary; totalRevive += reviveSpent; totalLootValue += lootValue; totalShop += shopEarned;
        string line = $"{weekDate} | {(questInfo == "" ? "-" : questInfo)} | {outcome} | 의뢰 보상 +{reward} | 기타 수입 +{extra} | 전리품 판매 +{shopEarned} | 고용 -{hireSpent} | 부활 -{reviveSpent} | 월급 -{salary} | " +
                      $"골드 {weekStartGold} → {gm.gold} | 명성 {gm.reputation} | 전리품 {lootCount}개(판매가 합 {lootValue}G, 다음 주 상점에서 판매)";
        ledger.Add(line);
        Debug.Log($"[Play] 주간 결산: {line}");
        Debug.Log($"[Play] 명단: {Roster(gm)} / 전사자 {gm.deadAdventurers.Count}명");

        weeksPlayed++;
        if (!outcome.StartsWith("휴식"))
        {
            if (weekHadWarrior) { withWarriorWeeks++; if (won) withWarriorWins++; }
            else { noWarriorWeeks++; if (won) noWarriorWins++; noWarriorOutcomes.Add(outcome); }
        }
        runOutcomes.Add(outcome);
        if (!outcome.StartsWith("휴식"))
        {
            chemWeeks[weekChemLevel]++;
            if (won) chemWins[weekChemLevel]++;
        }
        foreach (Adventurer a in gm.adventurers) if (gm.depressionTrait != null && a.traits.Contains(gm.depressionTrait)) depressedWeeks++;
        allOutcomes.Add(outcome);
        if (weeksPlayed == 1) week1Outcomes.Add(outcome);
        bool gameOver = LobbyManager.Instance.gameOverPopup != null && LobbyManager.Instance.gameOverPopup.activeSelf;
        if (gameOver || weeksPlayed >= Weeks)
        {
            Debug.Log("[Play] ════ 4주 요약 ════");
            foreach (string l in ledger) Debug.Log($"[Play] {l}");
            Debug.Log($"[Play] 합계: 의뢰 보상 +{totalReward}, 기타 수입 +{totalExtra}, 전리품 판매 +{totalShop}, 고용 -{totalHire}, 부활 -{totalRevive}, 월급 -{totalSalary}, 획득한 전리품 가치 {totalLootValue}G (마지막 주 분은 아직 안 팔림)");
            Debug.Log($"[Play] 최종: 골드 {gm.gold}, 명성 {gm.reputation}, 모험가 {gm.adventurers.Count}명, 다음 달 월급 {gm.GetTotalSalary()}G, 파산={gameOver}, " +
                      $"가장 싼 업그레이드 {gm.facilityUpgradeCosts[0]}G");

            int wins = runOutcomes.FindAll(o => o == "승리").Count;
            int leveled = gm.adventurers.FindAll(a => a.level >= 2).Count;
            int strandedNow = gm.adventurers.FindAll(a => a.isStranded).Count;
            runWarriors.Add(gm.adventurers.FindAll(IsWarrior).Count);
            runGold.Add(gm.gold); runRep.Add(gm.reputation); runWins.Add(wins); runDead.Add(gm.deadAdventurers.Count); runLeveled.Add(leveled);
            runSummaries.Add($"{runIndex + 1}회차: 골드 {gm.gold}, 명성 {gm.reputation}, [{string.Join("/", runOutcomes)}], 모험가 {gm.adventurers.Count}명(Lv2 이상 {leveled}명, 낙오 {strandedNow}명), 전사자 {gm.deadAdventurers.Count}명{(gameOver ? ", 파산" : "")}");
            Debug.Log($"[Play] 회차 결과 {runSummaries[runSummaries.Count - 1]}");

            if (runIndex + 1 < Runs)
            {
                // 다음 회차: 이 길드를 버리고 타이틀에서 새 길드로 (파산 뒤 타이틀로 돌아가는 것과 같은 경로)
                runIndex++;
                ResetRun();
                gm.OnClickGameOverConfirm();
                Go(0);
                return;
            }

            Debug.Log($"[Play] ════ [{configs[configIndex].label}] {Weeks}주 x {Runs}회 반복 요약 ════");
            Debug.Log($"[Play] 전체 결과 분포: {Tally(allOutcomes)}");
            Debug.Log($"[Play] 첫 주(새 Lv1 파티) 결과 분포: {Tally(week1Outcomes)}");
            foreach (string l in runSummaries) Debug.Log($"[Play] {l}");
            Debug.Log($"[Play] 최종 골드: {Stat(runGold)}");
            Debug.Log($"[Play] 최종 명성: {Stat(runRep)}");
            Debug.Log($"[Play] 4주 중 승리 횟수: {Stat(runWins)}");
            Debug.Log($"[Play] 전사자 수: {Stat(runDead)}");
            Debug.Log($"[Play] Lv2 이상 인원: {Stat(runLeveled)}");
            List<string> chemParts = new List<string>();
            for (int lv = 0; lv <= 4; lv++) if (chemWeeks[lv] > 0) chemParts.Add($"Lv.{lv} {chemWeeks[lv]}전 {chemWins[lv]}승");
            List<string> distParts = new List<string>();
            foreach (var kv in roleDistSum) distParts.Add($"{kv.Key} {kv.Value / roleDistCount[kv.Key]:F2}");
            distParts.Sort();
            Debug.Log($"[Play] 가장 가까운 상대와의 평균 거리: {string.Join(" / ", distParts)}");
            Debug.Log($"[Play] 아군 우회(도적) 공격 {flankAttacks}번: 후열 대상 {flankOnBackline}, 후열 아닌 대상 중 등 뒤에서 {flankBehind}/{flankAttacks - flankOnBackline} / 적 우회(고블린·도적단) 공격 {enemyFlankAttacks}번 중 우리 후열 대상 {enemyFlankOnBackline}");
            List<string> deathParts = new List<string>();
            foreach (var kv in deathsByJob) deathParts.Add($"{kv.Key} {kv.Value}");
            Debug.Log($"[Play] 직업별 전사: {string.Join(", ", deathParts)}");
            List<string> spawnParts = new List<string>();
            foreach (var kv in enemySpawns) spawnParts.Add($"{kv.Key} {kv.Value}");
            Debug.Log($"[Play] 등장한 적: {string.Join(", ", spawnParts)}");
            Debug.Log($"[Play] 실제 전사(로그 기준) {deathsLogged}명, 우울함 발생 {depressionLogged}번, 교착으로 도주 {stalemates}번, 사제 전체 힐 {partyHeals}번, 늑대 출혈 공격 {wolfBleeds}번, 출혈로 쓰러진 아군 {bleedKills}명");
            Debug.Log($"[Play] 케미(출발 때 레벨): {string.Join(" / ", chemParts)}, 우울한 모험가 (사람x주) {depressedWeeks}");
            Debug.Log($"[Play] 전사 유무: 전사 있음 {withWarriorWeeks}전 {withWarriorWins}승 / 전사 없음 {noWarriorWeeks}전 {noWarriorWins}승 ({Tally(noWarriorOutcomes)})");
            Debug.Log($"[Play] 전사 고용 {warriorHires}명, 전사가 2명이 된 횟수 {secondWarriorRuns}" +
                      (secondWarriorRuns > 0 ? $" (평균 {(float)secondWarriorWeek / secondWarriorRuns:F1}주차)" : "") + $", 마지막 명단의 전사 수 {Stat(runWarriors)}");

            int bossMinutesSum = 0; foreach (int sec in bossSeconds) bossMinutesSum += sec;
            Debug.Log($"[Play] 보스전: 던전 {dungeonsRun}개 중 보스방 도달 {bossFights}번, 보스전 승리 {bossWins}번, 실패 {bossFights - bossWins}번 ({Tally(bossFailOutcomes)}), " +
                      $"킹 슬라임 처치 {kingKills}번, 보스방에서 쓰러진 아군 {bossDeaths}명, 보스전 평균 {(bossSeconds.Count > 0 ? bossMinutesSum / (float)bossSeconds.Count : 0f):F0}초(게임 시간)");
            if (bossEntryInfo.Count > 0) Debug.Log($"[Play] 보스방 입장 때 아군(인원, 평균 체력): {string.Join(" / ", bossEntryInfo)}");
            Debug.Log($"[Play] 넉백: {kbCount}번 관찰, 밀린 거리 평균 {(kbCount > 0 ? kbDistSum / kbCount : 0f):F2} (최소 {(kbCount > 0 ? kbDistMin : 0f):F2}, 최대 {kbDistMax:F2}), " +
                      $"바닥 밖으로 밀림 {kbOffFloor}번, 밀리는 중 공격 {kbAttackedDuring}번, 넉백 끝나고 다음 공격까지 평균 {(kbNextAttackSamples > 0 ? kbNextAttackSum / kbNextAttackSamples : 0f):F2}초({kbNextAttackSamples}건)");
            Debug.Log($"[Play] 바닥 밖 유닛: {offFloorSamples}/{unitSamples} 프레임·유닛 ({(unitSamples > 0 ? 100f * offFloorSamples / unitSamples : 0f):F3}%), 나간 적 있는 유닛 {offFloorUnits.Count}개 [{string.Join(", ", new List<string>(offFloorUnits).GetRange(0, Mathf.Min(8, offFloorUnits.Count)))}]");
            Debug.Log($"[Play] ════ 모닥불 기록 {campfires.Count}건 ════");
            Debug.Log($"[Play] 승리한 던전의 전체 방 수: {Stat(totalRoomsOnWin)} / 정리한 방 수: {Stat(clearedOnWin)} / 방 이동 횟수: {Stat(movesOnWin)}");
            Debug.Log($"[Play] 정리한 방 종류: 전투 {roomTypeCounts[(int)DungeonRoomType.Normal]}, 휴식 {roomTypeCounts[(int)DungeonRoomType.Rest]}, 보물 {roomTypeCounts[(int)DungeonRoomType.Treasure]}, 웨이브 {roomTypeCounts[(int)DungeonRoomType.Wave]}");
            for (int room = 1; room <= 12; room++)
            {
                List<CampfireRecord> list = campfires.FindAll(c => c.room == room);
                if (list.Count == 0) continue;
                float before = 0f, minBefore = 0f, after = 0f, lowest = 1f; int full = 0, capped = 0, lost = 0;
                foreach (CampfireRecord c in list)
                {
                    before += c.before; minBefore += c.minBefore; after += c.after;
                    if (c.minBefore < lowest) lowest = c.minBefore;
                    if (c.before >= 0.999f) full++;
                    if (c.after >= 0.999f) capped++;
                    if (c.alive < c.startAllies) lost++;
                }
                int n = list.Count;
                Debug.Log($"[Play] {room}번째 방 뒤 {n}건: 회복 전 평균 {before / n * 100f:F1}% (가장 다친 사람 평균 {minBefore / n * 100f:F1}%, 전체 최저 {lowest * 100f:F1}%) -> 회복 후 평균 {after / n * 100f:F1}%, " +
                          $"실제로 찬 양 평균 {(after - before) / n * 100f:F1}%p / 회복 전 이미 전원 만피 {full}건 / 회복 후 전원 만피 {capped}건 / 쓰러진 동료 있음 {lost}건");
            }
            if (victoryHp.Count > 0)
            {
                float sum = 0f, low = 1f;
                foreach (float v in victoryHp) { sum += v; if (v < low) low = v; }
                Debug.Log($"[Play] 마지막 방 승리 순간 평균 체력 {victoryHp.Count}건: 평균 {sum / victoryHp.Count * 100f:F1}%, 최저 {low * 100f:F1}%");
            }
            Debug.Log($"[Play] ════ [{configs[configIndex].label}] 끝 ════");
            if (configIndex + 1 < configs.Length)
            {
                configIndex++;
                ResetConfig();
                gm.OnClickGameOverConfirm();
                Go(0);
                return;
            }
            Stop("완료");
            return;
        }
        Go(2);
    }

    static int offFloorSamples, unitSamples, offFloorDetailLogs;
    static readonly Dictionary<int, int> offStreak = new Dictionary<int, int>();
    static readonly HashSet<string> offFloorUnits = new HashSet<string>();
    static int rewardOfChosen;
    static int paidReward; // 이번 주에 실제로 받은 의뢰 보상 ("📜 [의뢰 성공] ... 보상 N G" 로그로 셈)

    // 시험용: 보스가 마나를 가득 채운 채 시작 (스킬이 실제로 나가는지 보기 위해. 한 마리에 한 번만)
    static readonly HashSet<int> filledBosses = new HashSet<int>();
    static void FillBossMp()
    {
        foreach (BattleUnit u in BattleUnit.Active)
        {
            if (u == null || u.isDead || !u.CompareTag("Enemy")) continue;
            bool boss = u is EnemyAlphaWolf || u is EnemySkeletonNecromancer || u is EnemyGoblinShamanKing || u is EnemyOrcCenturion
                        || (u is EnemyDragon d && d.useBreath) || (u is EnemySlime ks && ks.isKing);
            if (boss && filledBosses.Add(u.GetInstanceID())) u.currentMp = u.maxMp;
        }
    }

    static void Tick()
    {
        if (!Application.isPlaying) { EditorApplication.update -= Tick; return; }
        if (Elapsed > 900) { Stop($"시간 초과 (step {step}, {weeksPlayed}주 진행)"); return; }

        GameManager gm = GameManager.Instance;
        BattleManager bm = BattleManager.Instance;
        if (configs[configIndex].bossFullMp && bm != null) FillBossMp();
        // 바닥 밖에 있는 유닛 집계 (맵 이탈 버그 확인용)
        if (bm != null && bm.isBattleActive && MapGenerator.Instance != null)
            foreach (BattleUnit u in BattleUnit.Active)
            {
                if (u == null || u.isDead) continue;
                unitSamples++;
                int id = u.GetInstanceID();
                if (!MapGenerator.Instance.IsOnFloor(u.transform.position))
                {
                    offFloorSamples++; offFloorUnits.Add(u.name);
                    offStreak.TryGetValue(id, out int n); offStreak[id] = ++n;
                    if (n == 30 && offFloorDetailLogs < 15)
                    {
                        offFloorDetailLogs++;
                        Vector3 nf = MapGenerator.Instance.NearestFloor(u.transform.position);
                        Debug.Log($"[Play] 바닥 밖 30프레임 연속: {u.name} pos {u.transform.position} 가장 가까운 바닥까지 {Vector3.Distance(nf, u.transform.position):F2}, 대상 {(typeof(BattleUnit).GetField("target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(u) as BattleUnit)?.name}, 쉬는중 {typeof(BattleUnit).GetField("isResting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(u)}, 상자 {typeof(BattleUnit).GetField("isLooting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.GetValue(u)}");
                    }
                }
                else offStreak.Remove(id);
            }
        if (configs[configIndex].kingHopInterval > 0f && bm != null)
            foreach (BattleUnit u in BattleUnit.Active)
                if (u is EnemySlime ks && ks.isKing) ks.hopInterval = configs[configIndex].kingHopInterval; // 시험용: 플레이 중에만
        if (configs[configIndex].kingDamage > 0f && bm != null && gm != null && gm.currentQuest != null && EnemyScalingRules.TryParseRank(gm.currentQuest.rank, out RankType kingRank))
            foreach (BattleUnit u in BattleUnit.Active)
                if (u is EnemySlime ks && ks.isKing) ks.attackPower = configs[configIndex].kingDamage * EnemyScalingRules.GetAttackMultiplier(gm.enemyScaling, kingRank); // 시험용: 플레이 중에만
        string scene = SceneManager.GetActiveScene().name;
        bool inLobby = scene == "LobbyScene" && LobbyManager.Instance != null;

        switch (step)
        {
            case 0: // 타이틀 -> 새 게임
            {
                TitleManager title = Object.FindFirstObjectByType<TitleManager>();
                if (scene != "TitleScene" || title == null || gm == null || Elapsed < 0.5) return;
                title.OnClickNewGame();
                Go(1);
                break;
            }
            case 1: // 길드 생성
            {
                GuildCreationManager creation = Object.FindFirstObjectByType<GuildCreationManager>();
                if (scene != "GuildCreationScene" || creation == null || Elapsed < 0.5) return;
                creation.nameInput.text = $"경제테스트{runIndex + 1}";
                creation.OnClickSign();
                Go(2);
                break;
            }
            case 2: // 로비: 한 주 준비
            {
                if (!inLobby || Elapsed < 1.0) return;
                Time.timeScale = 1f;
                PlanWeek(gm);
                if (chosenQuest != null) rewardOfChosen = chosenQuest.rewardGold;
                paidReward = 0;
                break;
            }
            case 3: // 던전 전투
            {
                if (scene != "DungeonScene" || bm == null) return;
                Time.timeScale = DUNGEON_TIME_SCALE;
                if (configs[configIndex].campfireHeal.HasValue) bm.campfireHealRatio = configs[configIndex].campfireHeal.Value;
                ObserveBattle(bm);

                bool win  = bm.victoryPanel != null && bm.victoryPanel.activeSelf;
                bool lose = bm.defeatPanel  != null && bm.defeatPanel.activeSelf;
                if (bm.isBattleActive && startAllies == 0) startAllies = bm.allyCount;

                // 게임이 스스로 도주시킨 경우 (사제만 남았을 때 자동 도주)
                if (!win && !lose && bm.fleePanel != null && bm.fleePanel.activeInHierarchy)
                {
                    Debug.Log("[Play] 게임이 자동 도주시킴");
                    fled = true;
                    CloseBossFight(bm, "도주");
                    Go(4);
                    return;
                }
                if (!win && !lose && bm.isBattleActive && Elapsed > STALEMATE_SECONDS)
                {
                    stalemates++;
                    Debug.Log($"[Play] 교착: 던전이 {STALEMATE_SECONDS}초 넘게 끝나지 않음 (아군 {bm.allyCount}명) -> 도주");
                    fled = true;
                    CountLoot(bm);
                    CloseBossFight(bm, "도주");
                    bm.OnClickFleeButton();
                    Go(4);
                    return;
                }
                if (!win && !lose && bm.isBattleActive && startAllies >= 2 && bm.allyCount <= startAllies / 2)
                {
                    Debug.Log($"[Play] 아군 {bm.allyCount}/{startAllies}명 남음 (정리한 방 {bm.ClearedRoomCount}개) -> 도주");
                    fled = true;
                    CountLoot(bm);
                    CloseBossFight(bm, "도주");
                    bm.OnClickFleeButton();
                    Go(4);
                    return;
                }
                // 방 사이 모닥불: 계속 갈지 돌아갈지, 간다면 어느 방향으로 갈지 판단
                if (!win && !lose && bm.IsResting)
                {
                    Snapshot(out int alive, out float avgHp, out float _);
                    int cleared = bm.ClearedRoomCount;
                    DungeonRoomType hereType = bm.GetRoomType(bm.currentStageIndex);
                    if (bm.LastRestHealed) roomTypeCounts[(int)hereType]++;
                    // 방을 새로 정리해 회복이 있었을 때만 기록 (정리한 방을 지나갈 때는 회복 없음)
                    if (bm.LastRestHealed && hereType != DungeonRoomType.Rest)
                        campfires.Add(new CampfireRecord { room = cleared, alive = alive, startAllies = startAllies, before = snapAvg, minBefore = snapMin, after = avgHp });
                    string hpInfo = bm.LastRestHealed
                        ? $"생존 {alive}/{startAllies}, 회복 전 평균 {snapAvg * 100f:F0}%(최저 {snapMin * 100f:F0}%) -> 회복 후 평균 {avgHp * 100f:F0}%"
                        : $"생존 {alive}/{startAllies}, 평균 {avgHp * 100f:F0}% (지나가는 중)";
                    if ((startAllies - alive) * 2 >= startAllies || avgHp < 0.5f)
                    {
                        Debug.Log($"[Play] 모닥불({cleared}번째 방 뒤): {hpInfo} -> 귀환");
                        outcome = "귀환";
                        CountLoot(bm);
                        CloseBossFight(bm, "귀환");
                        bm.OnClickCampfireReturn();
                        Go(5);
                    }
                    else
                    {
                        int direction = PickDirection(bm);
                        int next = bm.Layout.GetNeighbor(bm.currentStageIndex, direction);
                        roomMoves++;
                        Debug.Log($"[Play] 모닥불({cleared}번째 방 뒤): {hpInfo} -> {DungeonLayoutRules.GetDirectionName(direction)} ({(bm.IsRoomVisited(next) ? "지나온 방" : "새 방")}: {DungeonLayoutRules.GetRoomTypeName(bm.GetRoomType(next))})");
                        bm.OnClickMove(direction);
                    }
                    return;
                }
                // 모닥불에 앉기 직전의 체력을 알 수 있도록 매 틱 기록 (회복은 모닥불 화면이 뜨는 순간 적용됨)
                if (!win && !lose) Snapshot(out snapAlive, out snapAvg, out snapMin);
                if (!win && !lose) return;

                outcome = win ? "승리" : "전멸";
                CloseBossFight(bm, outcome);
                if (win)
                {
                    CountLoot(bm); victoryHp.Add(snapAvg);
                    clearedOnWin.Add(bm.ClearedRoomCount); movesOnWin.Add(roomMoves); totalRoomsOnWin.Add(bm.Layout.rooms.Count);
                }
                Debug.Log($"[Play] 전투 결과: {outcome} (남은 아군 {bm.allyCount}/{startAllies}, 전리품 {lootCount}개)");
                bm.OnClick_ReturnToLobby();
                Go(5);
                break;
            }
            case 4: // 도주 결과창 -> 로비
            {
                if (bm == null || bm.fleePanel == null || !bm.fleePanel.activeInHierarchy) return;
                outcome = "도주";
                bm.OnClick_ReturnToLobby();
                Go(5);
                break;
            }
            case 5: // 로비: 결과 확인 -> 한 주 경과
            {
                if (!inLobby || Elapsed < 1.0) return;
                Time.timeScale = 1f;
                int goldBeforeConfirm = gm.gold;
                LobbyManager.Instance.OnClickConfirmBattleResult();
                FinishWeek(gm, goldBeforeConfirm);
                break;
            }
        }
    }

    static void CountLoot(BattleManager bm)
    {
        lootCount = bm.earnedItems.Count;
        lootValue = 0;
        foreach (MonsterDropData item in bm.earnedItems) if (item != null) lootValue += item.price;
    }
}
