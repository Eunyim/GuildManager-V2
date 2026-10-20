using UnityEngine;

using System.Collections.Generic;

using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour

{

    // [싱글톤 패턴] 전역에서 접근 가능한 유일한 인스턴스

    public static GameManager Instance;

    [Header("현재 파견 정보")]

    public Party currentDispatchParty; // 누가 갔는가

    public QuestData currentQuest; // 어떤 퀘스트인가

    [Header("프리팹 연결 (순서 중요!)")]
    // ★ 이 줄이 없어서 에러가 났던 겁니다. 추가해주세요!
    public GameObject[] unitPrefabs; // 직업별 아군 유닛 프리팹 (순서 무관: 프리팹의 Ally_* 컴포넌트로 직업을 판별)

    [Header("길드 데이터")]

    public string guildName = "용감한 모험가들"; // 길드 이름

    public int gold = 1000;          // 초기 자금

    public int reputation = 0;       // 명성

    [Header("명성 레벨 (갈 수 있는 던전 지역이 늘어남)")]
    public ReputationLevelSettings reputationLevel = new ReputationLevelSettings();

    public int GetReputationLevel() => ReputationRules.GetLevel(reputationLevel, reputation);

    public int day = 1;              // 날짜

    [Header("건물 정보")]

    public int guildLevel = 0; // 0: 텐트/판잣집, 1: 목조 건물, 2: 석조 건물 ...

    public List<Adventurer> adventurers = new List<Adventurer>(); //모험가 명단

    [Header("파티 데이터")]

    public List<Party> partyList = new List<Party>();

    [Header("길드 인벤토리")]
    public Dictionary<string, int> guildInventory = new Dictionary<string, int>();

    [Header("트라우마(정신, 신전에서 치료) / 결함(신체, 치유소에서 치료). 비어 있으면 코드 기본값")]
    public List<TraitData> traumaPool = new List<TraitData>();
    public List<TraitData> defectPool = new List<TraitData>();
    [Tooltip("중상·빈사로 돌아오거나 낙오했을 때 트라우마·결함 중 하나를 얻을 확률 (이미 가진 것은 제외)")]
    public AfflictionSettings affliction = new AfflictionSettings();

    [Header("긍정 특성 풀 (고용 시 시작 특성, 레벨업 보상)")]
    public List<TraitData> positiveTraitPool = new List<TraitData>();

    [Header("특별 특성 (풀에서 나오지 않고 정해진 방법으로만 얻음)")]
    [Tooltip("용사의 기운: 고용 시 A랭크 5% / S랭크 10%, 혼자 끝까지 살아남아 던전을 돌파하면 30%")]
    public float heroAuraChanceA = 0.05f;
    public float heroAuraChanceS = 0.1f;
    public float heroAuraSoloClearChance = 0.3f;
    [Tooltip("육감: 고용 시 모든 랭크에서 이 확률")]
    public float sixthSenseChance = 0.1f;
    [System.NonSerialized] public TraitData heroAuraTrait, sixthSenseTrait, dragonSlayerTrait;

    [Header("성격 효과")]
    public PersonalitySettings personality = new PersonalitySettings();

    [Header("UI 반응 (창 열기·버튼)")]
    public UIPolishSettings uiPolish = new UIPolishSettings();

    [Header("UI 테마 (판타지 픽셀)")]
    public UIThemeSettings uiTheme = new UIThemeSettings();

    [Header("모험가 기본 치명타·회피 (레벨업으로 오르지 않음)")]
    public CombatStatSettings combatStats = new CombatStatSettings();

    [Header("관계 (연인·친우·의형제·라이벌·불화)")]
    public RelationshipSettings relationship = new RelationshipSettings();

    [Header("무드 (길드 생활의 기분, 사기와 별개)")]
    public MoodSettings moodSettings = new MoodSettings();

    [Header("치유소: 결함(신체) 치료 비용 (랭크 D~S)")]
    public int[] clinicCureCost = { 150, 300, 600, 1000, 1800 };

    [Header("레벨업 로그 (결과창 전달용)")]
    public List<string> levelUpLog = new List<string>();

    // 중상·빈사·낙오 때 트라우마나 결함 하나를 얻는지 판정 (이미 가진 것은 제외. 태평한 성격은 정신적 트라우마 확률 감소)
    // 얻은 특성을 돌려줌 (없으면 null)
    public TraitData TryGiveAffliction(Adventurer adv, AfflictionCause cause)
    {
        if (adv == null) return null;
        var candidates = new List<TraitData>();
        foreach (TraitData t in traumaPool) if (t != null && !adv.traits.Contains(t)) candidates.Add(t);
        foreach (TraitData t in defectPool) if (t != null && !adv.traits.Contains(t)) candidates.Add(t);

        float mental = adv.trait == TraitType.Carefree ? personality.carefreeTraumaMultiplier : 1f;
        TraitData picked = AfflictionRules.Pick(candidates, t => t.category == TraitCategory.Trauma,
                                                AfflictionRules.Chance(affliction, cause), mental,
                                                Random.value, Random.value, Random.value);
        if (picked == null) return null;

        adv.traits.Add(picked);
        string kind = picked.category == TraitCategory.Defect ? "결함" : "트라우마";
        Debug.Log($"😱 [{kind} 발생] {adv.name}님이 '{picked.traitName}'을(를) 얻었습니다. ({cause})");
        return picked;
    }

    // 특별 특성 하나를 줌 (이미 있으면 false)
    public bool GrantTrait(Adventurer adv, TraitData trait, string reason)
    {
        if (adv == null || trait == null || adv.traits.Contains(trait)) return false;
        adv.traits.Add(trait);
        Debug.Log($"✨ [특성 획득] {adv.name} '{trait.traitName}' ({reason})");
        return true;
    }

    // 던전을 끝까지 돌파했을 때: 혼자 살아남았으면 용사의 기운 판정
    public void OnDungeonCleared(List<Adventurer> survivors)
    {
        if (survivors == null || survivors.Count != 1 || Random.value >= heroAuraSoloClearChance) return;
        if (GrantTrait(survivors[0], heroAuraTrait, "혼자 끝까지 던전을 돌파"))
            levelUpLog.Add($"{survivors[0].name}이(가) 혼자 던전을 돌파해 특성 '{heroAuraTrait.traitName}' 획득!");
    }

    // 사기가 오르내리는 폭의 배율 (나르시시스트는 크게)
    public float GetMoraleSwing(Adventurer adv) =>
        adv != null && adv.trait == TraitType.Narcissist ? personality.narcissistMoraleMultiplier : 1f;

    // 파티가 얻는 골드 배율 (덜렁거리는 성격 한 명마다 곱함)
    public float GetGoldGainMultiplier(IEnumerable<Adventurer> members)
    {
        var personalities = new List<TraitType>();
        if (members != null) foreach (Adventurer m in members) if (m != null && !m.isDead) personalities.Add(m.trait);
        return PersonalityRules.StackedMultiplier(personalities, TraitType.Clumsy, personality.clumsyGoldMultiplier);
    }

    [Header("구출 퀘스트 목록")]
    public List<QuestData> rescueQuests = new List<QuestData>(); // 현재 활성 구출 퀘스트
    [System.NonSerialized]
    public List<string> lastExpiredStrandedNames = new List<string>(); // 방금 지난 주에 구출 기한이 끝나 사망한 낙오자 (로비 알림용)

    [Header("전사자 목록 (유해 수습 대상)")]
    public List<Adventurer> deadAdventurers = new List<Adventurer>(); // 유해가 남아있는 전사자
    public List<QuestData> corpseQuests = new List<QuestData>();     // 현재 활성 유해 수습 퀘스트

    [Header("결산 데이터 (로비 전달용)")]
    public bool hasPendingResult = false; // 보여줄 결산 창이 있는가?
    public bool lastBattleWon = false;    // 승리했는가? (도주/패배는 false)
    public Dictionary<string, int> lastEarnedLoot = new Dictionary<string, int>(); // 방금 던전에서 얻은 템들
    public int lastEarnedGold = 0;        // 방금 파견에서 번 골드 (의뢰 보상 등)
    public int lastEarnedReputation = 0;  // 방금 파견에서 변한 명성
    public bool lastResultIsMultiDispatch = false;                // 방금 결과가 동시 파견(결과만 확인)인가?
    public List<string> lastDispatchReport = new List<string>();  // 동시 파견의 파티별 결과 요약

    [Header("동시 파견")]
    public AutoResolveSettings autoResolve = new AutoResolveSettings(); // 결과만 계산하는 파견의 승률 공식
    public int minMultiDispatchParties = 2; // 동시 파견에 필요한 최소 파티 수
    
    [Header("길드 시설 레벨 (1이 기본, 업그레이드로 증가)")]
    public int receptionLevel = 1;  // 접수처: 방문 모험가 수 + 고용 전 공개 정보 (1=직업/랭크/스탯, 2=성격/특성, 3=잠재력)
    public int innLevel       = 1;  // 여관: 최대 모험가 슬롯 결정 (기본 8명, +4/레벨)
    public int pubLevel       = 1;  // 주점: 길드에 있는 모험가 사기 회복 (기본 제공, 레벨당 5%)
    public int templeLevel    = 1;  // 신전: 특성 제거 비용 감소
    public int questBoardLevel= 1;  // 퀘스트 보드: 동시 파견 슬롯 수

    [Header("여관 레벨별 방 수 = 수용 가능한 파티 수 (index 0 = Lv1)")]
    public int[] partiesPerInnLevel = { 1, 2, 3, 4, 5 };

    [Header("접수처 고용 (방문 모험가)")]
    public RecruitmentSettings recruitment = new RecruitmentSettings();
    public List<Adventurer> visitors = new List<Adventurer>(); // 이번 주 길드를 찾아와 대기 중인 모험가
    private bool visitorsGenerated = false;

    [Header("새 게임 시작 시 지급하는 모험가 (이름과 성격은 무작위)")]
    public JobType[] starterJobs = { JobType.Warrior, JobType.Mage, JobType.Rogue, JobType.Healer };
    public RankType starterRank = RankType.D;

    [Header("부상 요양 기간 (주). 결과 확인 때 한 주가 지나므로 1주는 다음 파견 전에 풀림")]
    [Range(0f, 1f)]
    public float injuryHpThreshold = 0.7f; // 남은 체력이 이 비율을 넘으면 부상으로 치지 않음 (가벼운 상처는 돌아오면 바로 회복)
    public int minorInjuryWeeks  = 2; // 경상
    public int severeInjuryWeeks = 4; // 중상 (체력 30% 이하)

    [Header("유해 수습 보상 (전사자 랭크별 영혼석 확률 / 골드)")]
    public CorpseRecoverySettings corpseRecovery = new CorpseRecoverySettings();

    [Header("사기 (상승·하락 범위, 던전 돌파 시 상승량, 시간 경과)")]
    public MoraleSettings moraleSettings = new MoraleSettings();

    [Header("파티 케미")]
    public PartyChemistrySettings chemistry = new PartyChemistrySettings();
    [System.NonSerialized] public TraitData depressionTrait; // '우울함' (트라우마 풀과 별개로, 케미 Lv.3 이상 파티원이 사망했을 때만 붙음)

    [Header("모험가 랭크별 최대 레벨")]
    public AdventurerLevelSettings adventurerLevel = new AdventurerLevelSettings();

    [Header("모험가 방어력")]
    public DefenseSettings defense = new DefenseSettings();

    [Header("잠재력 (레벨당 성장 배율)")]
    public PotentialSettings potentialSettings = new PotentialSettings();

    [Header("의뢰 랭크별 적 능력치 배율")]
    public EnemyScalingSettings enemyScaling = new EnemyScalingSettings();

    [Header("파견 경고 기준 (의뢰에 비해 파티가 약할 때)")]
    public DispatchRiskSettings dispatchRisk = new DispatchRiskSettings();

    [Header("도주 (남은 체력별 낙오 확률)")]
    public FleeSettings flee = new FleeSettings();

    [Header("경제 (월급·경험치·신전 비용·명성·여관 인원·시설 최대 레벨)")]
    public EconomySettings economy = new EconomySettings();

    [Header("직업 스탯 (기본 스탯·레벨당 성장·시작 레벨·잠재력)")]
    public JobStatSettings jobStats = new JobStatSettings();

    [Header("퀘스트 보드 업그레이드 비용 (레벨별)")]
    public int[] facilityUpgradeCosts = { 500, 1000, 2000, 4000, 8000 };

    [Header("시간 및 경제 시스템")]
    public int month = 1;       // 현재 월
    public int week = 1;        // 현재 주차 (1~4주)
    public int weeksPerMonth = 4; // 한 달은 4주(4턴)로 고정

    [Header("게임오버 UI")]
    public GameObject gameOverPanel; // 파산 시 표시할 패널 (Inspector에서 연결)

    private void Awake()
    {
        // 싱글톤 보장 로직
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeDefaultTraits(); // 특성 풀 자동 초기화
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 인스펙터에서 연결하지 않아도 기본 특성들을 런타임에 자동 생성합니다. (풀이 비어 있을 때만. 인스펙터 설정 우선)
    // 배율은 곱해짐 (예: 외팔이 0.7 × 괴수 공포증 0.9). '스탯'은 공격력·방어력·이동속도
    private void InitializeDefaultTraits()
    {
        const TraitConditionType Always = TraitConditionType.Always;
        const TraitConditionType VsFamily = TraitConditionType.TargetIsFamily;

        if (positiveTraitPool == null || positiveTraitPool.Count == 0)
        {
            positiveTraitPool = new List<TraitData>
            {
                MakeTrait("광전사", "잃은 체력에 비례해 공격력이 오른다. (체력이 바닥에 가까울수록 최대 +30%)",
                    TraitCategory.Positive, Always, "", 0f, TraitEffectType.AttackByMissingHp, 0.3f),
                MakeTrait("빠른 발", "이동속도 +15%, 회피율 +10%.",
                    TraitCategory.Positive, Always, "", 0f, TraitEffectType.ModifyMoveSpeed, 1.15f, null,
                    Extra(TraitEffectType.ModifyEvasion, 0.1f)),
                MakeTrait("외로운 늑대", "살아 있는 파티원이 출발 인원의 절반 이상이면 스탯 -10%, 절반 미만으로 떨어지면 스탯 +20%. (스탯: 공격력·방어력·이동속도)",
                    TraitCategory.Positive, TraitConditionType.AllyRatioAtLeast, "", 0.5f, TraitEffectType.ModifyAllStats, 0.9f, 1.2f),
                MakeTrait("단단한 피부", "방어력 +10%, 출혈 저항 +5%. (받는 출혈 피해 5% 감소)",
                    TraitCategory.Positive, Always, "", 0f, TraitEffectType.ModifyDefense, 1.1f, null,
                    Extra(TraitEffectType.ModifyBleedTaken, 0.95f)),
                MakeTrait("괴수 사냥꾼", "늑대·슬라임에게 주는 피해 +10%.",
                    TraitCategory.Positive, VsFamily, EnemyFamilyRules.Beast, 0f, TraitEffectType.ModifyDamageDealt, 1.1f),
                MakeTrait("인간 사냥꾼", "도적단에게 주는 피해 +10%.",
                    TraitCategory.Positive, VsFamily, EnemyFamilyRules.Human, 0f, TraitEffectType.ModifyDamageDealt, 1.1f),
                MakeTrait("언데드 사냥꾼", "스켈레톤에게 주는 피해 +10%.",
                    TraitCategory.Positive, VsFamily, EnemyFamilyRules.Undead, 0f, TraitEffectType.ModifyDamageDealt, 1.1f),
                MakeTrait("그린스킨 사냥꾼", "오크·고블린에게 주는 피해 +10%.",
                    TraitCategory.Positive, VsFamily, EnemyFamilyRules.Greenskin, 0f, TraitEffectType.ModifyDamageDealt, 1.1f),
                MakeTrait("강인함", "받는 스턴 시간 -10%.",
                    TraitCategory.Positive, Always, "", 0f, TraitEffectType.ModifyStunTaken, 0.9f),
            };
        }

        // 풀에서 나오지 않는 특별 특성
        if (heroAuraTrait == null)
            heroAuraTrait = MakeTrait("용사의 기운", "스탯 +20%. (스탯: 공격력·방어력·이동속도)",
                TraitCategory.Positive, Always, "", 0f, TraitEffectType.ModifyAllStats, 1.2f);
        if (sixthSenseTrait == null)
            sixthSenseTrait = MakeTrait("육감", "던전에서 지금 방과 이어진 다음 방들의 종류를 미리 볼 수 있다.",
                TraitCategory.Positive, Always, "", 0f, TraitEffectType.RevealNextRoom, 1f);
        if (dragonSlayerTrait == null)
            dragonSlayerTrait = MakeTrait("드래곤슬레이어", "드래곤을 쓰러뜨린 자의 증표. 드래곤에게 주는 피해 +20%.",
                TraitCategory.Positive, VsFamily, EnemyFamilyRules.Dragon, 0f, TraitEffectType.ModifyDamageDealt, 1.2f);

        // 트라우마 (정신. 신전에서 치료)
        if (traumaPool == null || traumaPool.Count == 0)
        {
            traumaPool = new List<TraitData>
            {
                MakeTrait("괴수 공포증", "늑대·슬라임에게 주는 피해 -10%.",
                    TraitCategory.Trauma, VsFamily, EnemyFamilyRules.Beast, 0f, TraitEffectType.ModifyDamageDealt, 0.9f),
                MakeTrait("도적 공포증", "도적단에게 주는 피해 -10%.",
                    TraitCategory.Trauma, VsFamily, EnemyFamilyRules.Human, 0f, TraitEffectType.ModifyDamageDealt, 0.9f),
                MakeTrait("언데드 공포증", "스켈레톤에게 주는 피해 -10%.",
                    TraitCategory.Trauma, VsFamily, EnemyFamilyRules.Undead, 0f, TraitEffectType.ModifyDamageDealt, 0.9f),
                MakeTrait("그린스킨 공포증", "오크·고블린에게 주는 피해 -10%.",
                    TraitCategory.Trauma, VsFamily, EnemyFamilyRules.Greenskin, 0f, TraitEffectType.ModifyDamageDealt, 0.9f),
                MakeTrait("드래곤 공포증", "드래곤에게 주는 피해 -10%.",
                    TraitCategory.Trauma, VsFamily, EnemyFamilyRules.Dragon, 0f, TraitEffectType.ModifyDamageDealt, 0.9f),
            };
        }

        // 결함 (신체. 신전에서는 치료할 수 없음)
        if (defectPool == null || defectPool.Count == 0)
        {
            defectPool = new List<TraitData>
            {
                MakeTrait("외팔이", "주는 피해 -30%.",
                    TraitCategory.Defect, Always, "", 0f, TraitEffectType.ModifyDamageDealt, 0.7f),
                MakeTrait("절름발이", "이동속도 -30%.",
                    TraitCategory.Defect, Always, "", 0f, TraitEffectType.ModifyMoveSpeed, 0.7f),
                MakeTrait("외눈박이", "회피율 30% 감소. (도적 30% → 21%, 다른 직업 10% → 7%)",
                    TraitCategory.Defect, Always, "", 0f, TraitEffectType.MultiplyEvasion, 0.7f),
                MakeTrait("약골", "받는 피해 +30%.",
                    TraitCategory.Defect, Always, "", 0f, TraitEffectType.ModifyDamageTaken, 1.3f),
            };
        }

        // 우울함 (트라우마로 분류. 케미가 깊던 파티원이 사망했을 때만 붙음)
        if (depressionTrait == null)
        {
            depressionTrait = MakeTrait(
                "우울함", $"함께하던 동료를 잃은 슬픔으로 공격력이 {Mathf.RoundToInt((1f - chemistry.depressionAttackMultiplier) * 100f)}% 감소한다. 시간이 지나거나 새 동료들과 가까워지면 사라진다.",
                TraitCategory.Trauma, Always, "", 0f, TraitEffectType.ModifyAttackPower, chemistry.depressionAttackMultiplier);
        }
    }

    // ─── 케미 (모험가 사이의 관계) ─────────────────────────────────────
    // 같은 파티로 던전을 완수(승리, 모닥불 귀환)할 때마다 살아 돌아온 파티원 둘씩의 관계가 +1.
    // 모험가 한 명의 케미 = 지금 파티 동료들과의 관계 중 가장 높은 레벨 (신입은 관계가 쌓이는 만큼 따라 올라감). 그 케미로 HP·공격력 보너스.
    // 동료가 죽으면 그와 관계가 깊던 사람만 영향: 우울함 확률, 걸리지 않으면 케미 한 단계 하락 (Lv.1 아래로는 안 떨어짐)

    private readonly Dictionary<string, int> bonds = new Dictionary<string, int>(); // 관계 키(이름 둘) → 함께 완수한 횟수

    public int GetBond(Adventurer a, Adventurer b)
    {
        if (a == null || b == null || a == b) return 0;
        return bonds.TryGetValue(PartyChemistryRules.PairKey(a.name, b.name), out int c) ? c : 0;
    }

    void SetBond(Adventurer a, Adventurer b, int completions)
    {
        if (a == null || b == null || a == b) return;
        bonds[PartyChemistryRules.PairKey(a.name, b.name)] = Mathf.Max(0, completions);
    }

    // 모험가 한 명의 케미 레벨 (그 파티 동료 기준. 혼자면 0 = 케미 없음)
    public int GetChemistryLevel(Adventurer adv, Party party)
    {
        if (adv == null || party == null) return 0;
        var withTeammates = new List<int>();
        foreach (Adventurer mate in party.members)
            if (mate != adv) withTeammates.Add(GetBond(adv, mate));
        return PartyChemistryRules.IndividualLevel(chemistry, party.members.Count, withTeammates);
    }

    public float GetChemistryStatMultiplier(Adventurer adv, Party party) =>
        PartyChemistryRules.GetStatMultiplier(chemistry, GetChemistryLevel(adv, party));

    // 파티 표시용 레벨: 파티원 케미의 평균 (반올림). 혼자면 0
    public int GetChemistryLevel(Party party)
    {
        if (party == null || party.members.Count == 0) return 0;
        int sum = 0;
        foreach (Adventurer member in party.members) sum += GetChemistryLevel(member, party);
        return Mathf.RoundToInt((float)sum / party.members.Count);
    }

    // 파티 전체 전투력 보너스 (동시 파견 승률용): 파티원 보너스의 평균
    public float GetChemistryStatMultiplier(Party party)
    {
        if (party == null || party.members.Count == 0) return 1f;
        float sum = 0f;
        foreach (Adventurer member in party.members) sum += GetChemistryStatMultiplier(member, party);
        return sum / party.members.Count;
    }

    // 목록 표시용: "케미 평균 Lv.2"
    public string GetChemistryShortText(Party party)
    {
        int level = GetChemistryLevel(party);
        return level <= 0 ? "케미 없음" : $"케미 평균 Lv.{level}";
    }

    // 파티 상세 제목용: "케미 평균 Lv.2 약간 좋음"
    public string GetChemistryText(Party party)
    {
        int level = GetChemistryLevel(party);
        if (level <= 0) return "케미 없음";
        return $"케미 평균 Lv.{level} {PartyChemistryRules.GetLevelName(chemistry, level)}";
    }

    // 멤버 칸 표시용: "케미 Lv.3 좋음 +7%" (신입은 "적응 중")
    public string GetMemberChemistryText(Adventurer adv, Party party)
    {
        int level = GetChemistryLevel(adv, party);
        if (level <= 0) return "";
        float bonus = PartyChemistryRules.GetStatMultiplier(chemistry, level) - 1f;
        // '적응 중': 이미 서로 친한 동료들 사이에 이 사람만 아직 관계가 얕을 때
        bool settled = false, othersClose = false;
        foreach (Adventurer mate in party.members)
        {
            if (mate == adv) continue;
            if (PartyChemistryRules.BondLevel(chemistry, GetBond(adv, mate)) >= chemistry.countedBondLevel) settled = true;
            foreach (Adventurer other in party.members)
                if (other != adv && other != mate && PartyChemistryRules.BondLevel(chemistry, GetBond(mate, other)) >= chemistry.countedBondLevel) othersClose = true;
        }
        string tag = !settled && othersClose ? "적응 중" : PartyChemistryRules.GetLevelName(chemistry, level);
        return $"케미 Lv.{level} {tag} +{bonus * 100f:F0}%";
    }

    // 던전이 끝났을 때 호출. 사망자를 파티에서 빼기 전에 불러야 함 (죽은 동료와의 관계로 우울함을 정함)
    //  completed: 승리 또는 모닥불 귀환 (도주·전멸·동시 파견 실패는 false). 사망자가 나온 던전은 관계가 쌓이지 않음
    public void ResolvePartyChemistry(Party party, bool completed, List<Adventurer> deadMembers)
    {
        if (party == null) return;

        List<Adventurer> survivors = party.members.FindAll(m => !m.isDead && !m.isStranded && (deadMembers == null || !deadMembers.Contains(m)));
        ResolveRelationsAndMood(party, completed, deadMembers, survivors);

        if (deadMembers != null && deadMembers.Count > 0)
        {
            var shaken = new List<Adventurer>(); // 우울함에 걸리지 않고 케미가 떨어질 사람
            foreach (Adventurer survivor in survivors)
            {
                // 죽은 동료 중 관계가 가장 깊었던 사람 기준 (관계가 얕았으면 영향 없음)
                int closest = 0;
                foreach (Adventurer dead in deadMembers)
                    closest = Mathf.Max(closest, PartyChemistryRules.BondLevel(chemistry, GetBond(survivor, dead)));
                if (closest < chemistry.countedBondLevel) continue;

                // 관계에서 받는 스트레스(외향적인 +, 내향적인 -), 태평한 성격은 정신적 트라우마 확률 감소
                float chance = PartyChemistryRules.GetDepressionChance(chemistry, closest)
                               * PersonalityRules.StressMultiplier(personality, survivor.trait)
                               * (survivor.trait == TraitType.Carefree ? personality.carefreeTraumaMultiplier : 1f);
                // 겸손한 동료가 곁에 있으면 스트레스가 덜함 (한 명마다 곱함, 자기 자신은 제외)
                foreach (Adventurer mate in survivors)
                    if (mate != survivor && mate.trait == TraitType.Humble) chance *= personality.humbleTeammateStressMultiplier;
                if (Random.value < chance) GiveDepression(survivor);
                else shaken.Add(survivor);
            }

            // 우울함에 걸리지 않은 사람은 남은 동료들과의 관계가 한 단계 떨어짐 (Lv.1이면 그대로).
            // 둘 다 흔들린 사이라도 그 관계는 한 번만 떨어짐
            var before = new Dictionary<Adventurer, int>();
            foreach (Adventurer m in survivors) before[m] = GetChemistryLevel(m, party);
            var dropped = new HashSet<string>();
            foreach (Adventurer survivor in shaken)
                foreach (Adventurer mate in survivors)
                {
                    if (mate == survivor || !dropped.Add(PartyChemistryRules.PairKey(survivor.name, mate.name))) continue;
                    SetBond(survivor, mate, PartyChemistryRules.DropOneLevel(chemistry, GetBond(survivor, mate)));
                }
            foreach (Adventurer m in survivors)
            {
                int after = GetChemistryLevel(m, party);
                if (after < before[m]) Debug.Log($"💔 [케미] {m.name}님의 케미가 동료를 잃은 충격으로 떨어졌습니다. (Lv.{before[m]} → Lv.{after})");
            }
            return;
        }

        if (!completed || survivors.Count < Mathf.Max(chemistry.minMembers, 1)) return;

        var before2 = new Dictionary<Adventurer, int>();
        foreach (Adventurer m in survivors) before2[m] = GetChemistryLevel(m, party);

        // 외향적인 / 내향적인 성격은 관계가 쌓이는 속도가 다름 (소수 배율은 확률로 반올림)
        for (int i = 0; i < survivors.Count; i++)
            for (int j = i + 1; j < survivors.Count; j++)
            {
                if (GetRelation(survivors[i], survivors[j]) == RelationType.Feud) continue; // 불화 중인 사이는 케미가 쌓이지 않음
                float gain = PersonalityRules.BondGainMultiplier(personality, survivors[i].trait, survivors[j].trait);
                SetBond(survivors[i], survivors[j], GetBond(survivors[i], survivors[j]) + PersonalityRules.RoundRandomly(gain, Random.value));
            }

        foreach (Adventurer m in survivors)
        {
            int level = GetChemistryLevel(m, party);
            if (level > before2[m])
                Debug.Log($"💞 [케미] {m.name}님의 케미 Lv.{level} {PartyChemistryRules.GetLevelName(chemistry, level)}!");
            // 새 동료들과 가까워지면 우울함이 사라짐
            if (level >= chemistry.depressionClearLevel && depressionTrait != null && m.traits.Contains(depressionTrait))
                ClearDepression(m, $"'{party.partyName}' 동료들과 가까워짐");
        }
    }

    // 예전 저장 파일: 파티 케미(같은 구성원으로 완수한 횟수)를 그 구성원끼리의 관계로 옮김
    void MigratePartyChemistry(Party party, int completions)
    {
        if (party == null || completions <= 0) return;
        for (int i = 0; i < party.members.Count; i++)
            for (int j = i + 1; j < party.members.Count; j++)
                SetBond(party.members[i], party.members[j], Mathf.Max(GetBond(party.members[i], party.members[j]), completions));
    }

    // ─── 관계 (연인·친우·의형제·라이벌·불화) / 무드 ─────────────────────
    // 관계는 사건으로 드물게 생기고 한 쌍에 하나. 케미(숫자)가 관계로 가는 길이고, 관계는 행동·무드에 관여.
    // 무드는 길드 생활의 기분 (-1 ~ +1). 사기와 별개로 행동(멍때리기, 관계가 생기는 확률)에 관여

    private readonly Dictionary<string, RelationType> relations = new Dictionary<string, RelationType>();

    public RelationType GetRelation(Adventurer a, Adventurer b)
    {
        if (a == null || b == null || a == b) return RelationType.None;
        return relations.TryGetValue(PartyChemistryRules.PairKey(a.name, b.name), out RelationType t) ? t : RelationType.None;
    }

    // 이 모험가의 관계 목록 (현역 모험가 중에서만)
    public List<KeyValuePair<Adventurer, RelationType>> GetRelationsOf(Adventurer adv)
    {
        var list = new List<KeyValuePair<Adventurer, RelationType>>();
        if (adv == null) return list;
        foreach (Adventurer other in adventurers)
        {
            RelationType t = GetRelation(adv, other);
            if (t != RelationType.None) list.Add(new KeyValuePair<Adventurer, RelationType>(other, t));
        }
        return list;
    }

    int CountRelations(Adventurer adv, RelationType type)
    {
        int n = 0;
        foreach (var pair in relations)
            if (pair.Value == type && pair.Key.Split('|') is string[] names && (names[0] == adv.name || names[1] == adv.name)) n++;
        return n;
    }

    // 관계를 맺음 (규칙에 맞지 않으면 false). 결과창에 한 줄 알림
    bool TryFormRelation(Adventurer a, Adventurer b, RelationType type)
    {
        RelationType current = GetRelation(a, b);
        if (!RelationshipRules.CanReplace(current, type)) return false;
        if (type == RelationType.Lover && (CountRelations(a, RelationType.Lover) > 0 || CountRelations(b, RelationType.Lover) > 0)) return false;
        if (type == RelationType.SwornSibling &&
            (CountRelations(a, RelationType.SwornSibling) >= relationship.maxSwornSiblings || CountRelations(b, RelationType.SwornSibling) >= relationship.maxSwornSiblings)) return false;

        relations[PartyChemistryRules.PairKey(a.name, b.name)] = type;
        string msg = $"{a.name}와(과) {b.name}이(가) {RelationshipRules.GetName(type)} 사이가 되었습니다!";
        levelUpLog.Add(msg);
        Debug.Log($"💞 [관계] {msg}");

        float moodDelta = RelationshipRules.IsPositive(type) ? moodSettings.relationFormed : type == RelationType.Feud ? moodSettings.feudFormed : 0f;
        ChangeMood(a, moodDelta, "관계");
        ChangeMood(b, moodDelta, "관계");
        return true;
    }

    // 무드를 바꿈 (로비에서 말풍선으로 보이도록 바뀐 양을 모아 둠)
    public void ChangeMood(Adventurer adv, float delta, string reason)
    {
        if (adv == null || delta == 0f) return;
        float before = adv.mood;
        adv.mood = RelationshipRules.ClampMood(adv.mood + delta);
        adv.moodShownDelta += adv.mood - before;
    }

    // 좋은 관계가 생기는 확률 배율 (성격, 무드)
    float PositiveRelationMultiplier(Adventurer a, Adventurer b, RelationType type)
    {
        float m = 1f;
        foreach (Adventurer x in new[] { a, b })
        {
            if (x.trait == TraitType.Extrovert) m *= relationship.extrovertMultiplier;
            if (x.trait == TraitType.Introvert) m *= relationship.introvertMultiplier;
            if (type == RelationType.Friend && x.trait == TraitType.Humble) m *= relationship.humbleFriendMultiplier;
        }
        if (a.mood >= moodSettings.highMood && b.mood >= moodSettings.highMood) m *= relationship.goodMoodMultiplier;
        return m;
    }

    static bool SeverelyHurt(Adventurer m) => m.hp > 0 && (float)m.currentHp / m.hp <= 0.3f;

    // 던전이 끝났을 때 (ResolvePartyChemistry 처음에서): 동료를 잃은 슬픔·분노, 무드, 새 관계
    void ResolveRelationsAndMood(Party party, bool completed, List<Adventurer> deadMembers, List<Adventurer> survivors)
    {
        bool anyDead = deadMembers != null && deadMembers.Count > 0;

        // 1. 동료를 잃음
        if (anyDead)
            foreach (Adventurer dead in deadMembers)
                foreach (Adventurer s in survivors)
                {
                    RelationType rel = GetRelation(s, dead);
                    if (rel == RelationType.Lover)
                    {
                        ChangeMood(s, moodSettings.loverDied, "연인 사망");
                        GiveDepression(s); // 연인을 잃으면 반드시 우울함
                    }
                    else if (rel == RelationType.Friend || rel == RelationType.SwornSibling || rel == RelationType.Rival)
                        ChangeMood(s, moodSettings.friendDied, "동료 사망");
                    else
                        ChangeMood(s, moodSettings.partyMemberDied, "파티원 사망");

                    // 의형제의 복수심: 쓰러뜨린 적 계열의 사냥꾼 특성
                    if (rel == RelationType.SwornSibling && !string.IsNullOrEmpty(dead.lastKillerFamily) && Random.value < relationship.swornRevengeTraitChance)
                    {
                        TraitData hunter = positiveTraitPool.Find(t => t != null && t.conditionType == TraitConditionType.TargetIsFamily
                                                                     && t.conditionTag == dead.lastKillerFamily && t.HasEffect(TraitEffectType.ModifyDamageDealt));
                        if (GrantTrait(s, hunter, $"의형제 {dead.name}의 복수"))
                            levelUpLog.Add($"{s.name}이(가) 의형제 {dead.name}의 복수를 다짐하며 특성 '{hunter.traitName}' 획득!");
                    }
                }

        // 2. 던전 결과와 부상
        foreach (Adventurer s in survivors)
        {
            ChangeMood(s, completed ? moodSettings.dungeonCleared : moodSettings.dungeonFailed, completed ? "완수" : "실패");
            if (SeverelyHurt(s)) ChangeMood(s, moodSettings.seriouslyInjured, "중상");
        }

        // 3. 새 관계 (살아 돌아온 두 사람마다)
        bool crisis = anyDead || survivors.Exists(SeverelyHurt);
        for (int i = 0; i < survivors.Count; i++)
            for (int j = i + 1; j < survivors.Count; j++)
            {
                Adventurer a = survivors[i], b = survivors[j];
                RelationType current = GetRelation(a, b);
                int bondLevel = PartyChemistryRules.BondLevel(chemistry, GetBond(a, b));

                if (completed)
                {
                    if (bondLevel >= relationship.loverBondLevel && (SeverelyHurt(a) || SeverelyHurt(b))
                        && Random.value < relationship.loverChance * PositiveRelationMultiplier(a, b, RelationType.Lover)
                        && TryFormRelation(a, b, RelationType.Lover)) continue;
                    if (crisis && bondLevel >= relationship.swornBondLevel
                        && Random.value < relationship.swornChance * PositiveRelationMultiplier(a, b, RelationType.SwornSibling)
                        && TryFormRelation(a, b, RelationType.SwornSibling)) continue;
                    if (bondLevel >= relationship.friendBondLevel
                        && Random.value < relationship.friendChance * PositiveRelationMultiplier(a, b, RelationType.Friend)
                        && TryFormRelation(a, b, RelationType.Friend)) continue;
                }

                if (current != RelationType.None) continue;
                int narcissists = (a.trait == TraitType.Narcissist ? 1 : 0) + (b.trait == TraitType.Narcissist ? 1 : 0);
                if (narcissists > 0 && Random.value < relationship.rivalChance * narcissists && TryFormRelation(a, b, RelationType.Rival)) continue;
                if (!completed)
                {
                    bool hot = a.trait == TraitType.HotTempered || b.trait == TraitType.HotTempered;
                    if (Random.value < relationship.feudChance * (hot ? 2f : 1f)) TryFormRelation(a, b, RelationType.Feud);
                }
            }
    }

    // 매주: 무드가 보통 쪽으로 돌아가고, 같은 파티의 관계에 따라 오르내림. 의형제는 기분을 함께 느낌
    void UpdateWeeklyMood()
    {
        foreach (Adventurer adv in adventurers)
        {
            if (adv.isDead || adv.isStranded) continue;
            float delta = RelationshipRules.Drift(adv.mood, moodSettings.weeklyDrift) - adv.mood;

            foreach (var pair in GetRelationsOf(adv))
            {
                Adventurer other = pair.Key;
                bool together = adv.assignedPartyIndex >= 0 && adv.assignedPartyIndex == other.assignedPartyIndex && !other.isStranded;
                switch (pair.Value)
                {
                    case RelationType.Lover:        delta += together ? moodSettings.loverTogether : moodSettings.loverApart; break;
                    case RelationType.Friend:
                    case RelationType.SwornSibling: if (together) delta += moodSettings.friendTogether; break;
                    case RelationType.Feud:         if (together) delta += moodSettings.feudTogether; break;
                    case RelationType.Rival:        if (together) delta += moodSettings.rivalTogether; break;
                }
            }
            ChangeMood(adv, delta, "주간");
        }

        // 의형제끼리 무드가 닮아감 (한 번에 계산해 순서 영향 없게)
        var shared = new Dictionary<Adventurer, float>();
        foreach (Adventurer adv in adventurers)
        {
            if (adv.isDead || adv.isStranded) continue;
            float sum = adv.mood; int n = 1;
            foreach (var pair in GetRelationsOf(adv))
                if (pair.Value == RelationType.SwornSibling && !pair.Key.isStranded) { sum += pair.Key.mood; n++; }
            if (n > 1) shared[adv] = (sum / n - adv.mood) * moodSettings.swornShare;
        }
        foreach (var pair in shared) ChangeMood(pair.Key, pair.Value, "의형제");
    }

    // ─── 여관 방 증축 (방 레벨 = 그 방에 묵는 파티의 레벨, 레벨마다 최대 인원 +1) ───

    [Header("여관 방 증축 (방마다 최대 인원 4 → 6)")]
    public InnRoomSettings innRooms = new InnRoomSettings();
    private readonly List<int> roomLevels = new List<int>(); // 방 번호별 레벨 (없으면 Lv.1)

    public int GetRoomLevel(int roomIndex) =>
        roomIndex >= 0 && roomIndex < roomLevels.Count ? Mathf.Max(1, roomLevels[roomIndex]) : 1;

    public int GetPartyLevel(Party party) => party != null ? GetRoomLevel(party.roomIndex) : 1;

    // 파티 최대 인원: 묵고 있는 방의 레벨로 정해짐 (방이 없으면 기본 인원)
    public int GetPartyCapacity(Party party) => InnRoomRules.Capacity(innRooms, GetPartyLevel(party));

    public int GetRoomExpandCost(int roomIndex) => InnRoomRules.ExpandCost(innRooms, GetRoomLevel(roomIndex));

    // 방 한 칸 증축. 성공하면 true
    public bool ExpandRoom(int roomIndex, out string message)
    {
        message = "";
        if (roomIndex < 0 || roomIndex >= GetMaxParties()) { message = "열려 있는 방이 아닙니다."; return false; }
        int cost = GetRoomExpandCost(roomIndex);
        if (cost < 0) { message = $"이미 최대 인원({innRooms.maxMembers}명)입니다."; return false; }
        if (gold < cost) { message = $"골드가 부족합니다. (필요 {cost:N0} G)"; return false; }

        while (roomLevels.Count <= roomIndex) roomLevels.Add(1);
        gold -= cost;
        roomLevels[roomIndex] = GetRoomLevel(roomIndex) + 1;
        message = $"{roomIndex + 1}번 방을 증축했습니다! (방 Lv.{roomLevels[roomIndex]}, 최대 {InnRoomRules.Capacity(innRooms, roomLevels[roomIndex])}명, -{cost:N0} G)";
        Debug.Log($"🏠 [여관] {message}");
        return true;
    }

    void GiveDepression(Adventurer adv)
    {
        if (depressionTrait == null) return;
        if (!adv.traits.Contains(depressionTrait)) adv.traits.Add(depressionTrait);
        adv.depressionWeeks = Mathf.Max(1, chemistry.depressionMonths * weeksPerMonth); // 이미 우울하면 기간을 다시 채움
        Debug.Log($"😢 [우울함] {adv.name}님이 동료를 잃고 우울해졌습니다. ({adv.depressionWeeks}주 뒤 회복)");
    }

    void ClearDepression(Adventurer adv, string reason)
    {
        adv.traits.Remove(depressionTrait);
        adv.depressionWeeks = 0;
        Debug.Log($"🌤️ [우울함 회복] {adv.name} ({reason})");
    }

    // ─── 저장 / 불러오기 ─────────────────────────────────────────────
    // 저장 시점: 한 주가 지날 때, 로비에서 게임을 끌 때. 파산하면 저장을 지움.
    // 진행 중인 의뢰(구출/유해 수습 포함)는 저장하지 않으며, 퀘스트 보드를 열 때 다시 만들어짐.

    private bool isGameOver = false;
    public bool IsGameOver => isGameOver;

    // 튜토리얼 진행 (TutorialManager가 읽고 씀). 새 게임은 길드 생성 화면에서 NewGame으로 바꿈.
    // 예전 저장 파일이나 로비에서 바로 실행한 경우에는 끝난 것으로 보고 상황별 도움말만 띄움
    [System.NonSerialized] public TutorialProgress tutorial = TutorialProgress.Finished();

    // 한 판의 기록 (엔딩 화면용) — 저장 파일에 함께 저장
    [System.NonSerialized] public GuildRecord record = new GuildRecord();
    [Header("장비 (무기·방어구·강화)")]
    public EquipmentSettings equipment = new EquipmentSettings();
    [System.NonSerialized] public List<EquipmentItem> equipmentStorage = new List<EquipmentItem>(); // 길드 창고 (끼지 않은 장비)
    [System.NonSerialized] public int equipmentSerial;

    // 장비가 더해 주는 능력치
    public int EquipAtk(Adventurer a) => a == null ? 0 : EquipmentRules.Atk(equipment, a.weapon);
    public int EquipHp(Adventurer a)  => a == null ? 0 : EquipmentRules.Hp(equipment, a.armor);
    public int EquipDef(Adventurer a) => a == null ? 0 : EquipmentRules.Def(equipment, a.armor);

    // 새 장비를 만들어 창고에 넣음
    public EquipmentItem CreateEquipment(EquipSlot slot, RankType grade, JobType job)
    {
        EquipmentItem item = EquipmentRules.Create(slot, grade, job, ++equipmentSerial);
        equipmentStorage.Add(item);
        return item;
    }

    // 던전 보상으로 장비 한 개를 굴림 (칸은 무작위, 무기는 파티 직업 중 하나). 창고에는 아직 넣지 않음
    public EquipmentItem RollEquipmentDrop(RankType grade, List<Adventurer> party)
    {
        var jobs = new List<JobType>();
        if (party != null) foreach (Adventurer a in party) if (a != null) jobs.Add(a.job);
        EquipSlot slot = EquipmentRules.RollSlot(Random.value);
        return EquipmentRules.Create(slot, grade, EquipmentRules.PickJob(jobs, Random.value), ++equipmentSerial);
    }

    // 창고의 장비를 모험가에게 낌 (끼고 있던 것은 창고로)
    public bool Equip(Adventurer adv, EquipmentItem item, out string message)
    {
        message = "";
        if (adv == null || EquipmentRules.IsEmpty(item) || !equipmentStorage.Contains(item)) { message = "장비를 찾을 수 없습니다."; return false; }
        if (!EquipmentRules.CanEquip(item, adv.job)) { message = $"{DisplayNames.JobToKorean(item.job)} 전용 무기입니다."; return false; }
        EquipmentItem old = adv.GetEquipment(item.slot);
        equipmentStorage.Remove(item);
        if (!EquipmentRules.IsEmpty(old)) equipmentStorage.Add(old);
        adv.SetEquipment(item.slot, item);
        message = $"{adv.name}이(가) {EquipmentRules.Name(equipment, item)}을(를) 장착했습니다.";
        return true;
    }

    public void Unequip(Adventurer adv, EquipSlot slot)
    {
        if (adv == null) return;
        EquipmentItem old = adv.GetEquipment(slot);
        if (EquipmentRules.IsEmpty(old)) return;
        equipmentStorage.Add(old);
        adv.SetEquipment(slot, null);
    }

    // 모험가의 장비를 모두 창고로 (해고)
    public void ReturnEquipmentToStorage(Adventurer adv)
    {
        Unequip(adv, EquipSlot.Weapon);
        Unequip(adv, EquipSlot.Armor);
    }

    // 강화 (골드 + 전리품, 실패 없음). 낀 장비도 창고 장비도 가능
    public bool TryUpgradeEquipment(EquipmentItem item, out string message)
    {
        message = "";
        if (!EquipmentRules.CanUpgrade(equipment, item)) { message = "더 강화할 수 없습니다."; return false; }
        int cost = EquipmentRules.UpgradeGold(equipment, item);
        string mat = EquipmentRules.UpgradeMaterial(equipment, item);
        int need = EquipmentRules.UpgradeMaterialCount(item);
        int have = GetItemCount(mat);
        if (gold < cost) { message = $"골드가 부족합니다. ({cost:N0} G 필요)"; return false; }
        if (!string.IsNullOrEmpty(mat) && have < need) { message = $"{mat}이(가) 부족합니다. ({have}/{need})"; return false; }
        gold -= cost;
        if (!string.IsNullOrEmpty(mat))
        {
            guildInventory[mat] = have - need;
            if (guildInventory[mat] <= 0) guildInventory.Remove(mat);
        }
        item.plus++;
        message = $"{EquipmentRules.Name(equipment, item)} 강화 성공! (-{cost:N0} G, {mat} -{need})";
        Debug.Log($"🔨 [강화] {message}");
        return true;
    }

    public bool TryBuyEquipment(EquipSlot slot, RankType grade, JobType job, out string message)
    {
        int price = EquipmentRules.ShopPrice(equipment, slot, grade);
        if (price <= 0) { message = "상점에서 팔지 않는 장비입니다."; return false; }
        if (gold < price) { message = $"골드가 부족합니다. ({price:N0} G 필요)"; return false; }
        gold -= price;
        EquipmentItem item = CreateEquipment(slot, grade, job);
        message = $"{EquipmentRules.Name(equipment, item)} 구입 (-{price:N0} G)";
        return true;
    }

    public bool SellEquipment(EquipmentItem item, out string message)
    {
        message = "";
        if (!equipmentStorage.Contains(item)) return false;
        int price = EquipmentRules.SellPrice(equipment, item);
        equipmentStorage.Remove(item);
        gold += price;
        message = $"{EquipmentRules.Name(equipment, item)} 판매 (+{price:N0} G)";
        return true;
    }

    // 던전에서 얻은 장비를 창고에 넣고 결과창 목록에도 올림
    public void AddEquipmentLoot(List<EquipmentItem> items)
    {
        if (items == null) return;
        foreach (EquipmentItem item in items)
        {
            if (EquipmentRules.IsEmpty(item)) continue;
            equipmentStorage.Add(item);
            string key = $"[장비] {EquipmentRules.Name(equipment, item)}";
            lastEarnedLoot[key] = lastEarnedLoot.TryGetValue(key, out int n) ? n + 1 : 1;
            Debug.Log($"🗡️ [장비 획득] {EquipmentRules.Name(equipment, item)} ({EquipmentRules.StatText(equipment, item)})");
        }
        items.Clear();
    }

    [Header("데모 엔딩")]
    [Tooltip("엔딩 화면에서 쓰러지는 드래곤 그림 (Tools > 데모 엔딩 연결)")]
    public MonsterSpriteSet endingDragonSet;

    // 로비에 돌아가면 데모 엔딩 화면을 띄움 (EndingScreen이 보고 끔)
    [System.NonSerialized] public bool pendingEnding;

    public void NoteFallen(Adventurer adv) { if (adv != null) GuildRecordRules.NoteFallen(record, adv.name); }
    public void NoteQuestResult(bool success) => GuildRecordRules.NoteQuest(record, success);

    // 데모 엔딩 예약: 드래곤을 쓰러뜨리고 살아 돌아온 사람을 기록
    public void ReserveDemoEnding(List<Adventurer> survivors)
    {
        if (record == null) record = new GuildRecord();
        record.endingSeen = true;
        record.endingMonth = month;
        record.endingWeek = week;
        record.dragonSlayers = new List<string>();
        if (survivors != null)
            foreach (Adventurer a in survivors)
                if (a != null && !a.isDead && !a.isStranded) record.dragonSlayers.Add(a.name);
        pendingEnding = true;
        Debug.Log($"🐉 [데모 엔딩] 드래곤 토벌! ({month}월 {week}주차, 파티: {string.Join(", ", record.dragonSlayers)})");
    }

    // 엔딩 화면의 [타이틀로]: 저장하고 이 매니저를 없앤 뒤 타이틀로 (타이틀 씬의 GameManager가 새로 자리잡음)
    public void ReturnToTitleAfterEnding()
    {
        Time.timeScale = 1f;
        SaveGame();
        if (Instance == this) Instance = null;
        Destroy(gameObject);
        SceneManager.LoadScene("TitleScene");
    }

    // 지금 저장해도 되는 상태인가 (던전에 나가 있거나 파산한 뒤에는 저장하지 않음)
    public bool CanSaveNow()
    {
        // currentDispatchParty는 인스펙터 직렬화 때문에 파견 중이 아니어도 빈 객체로 채워져 있을 수 있어 상태로 판별
        bool onQuest = currentDispatchParty != null && currentDispatchParty.state == PartyState.OnQuest;
        return !isGameOver && !onQuest && SceneManager.GetActiveScene().name == "LobbyScene";
    }

    public bool SaveGame()
    {
        if (!CanSaveNow()) return false;
        return SaveSystem.Save(CaptureSave());
    }

    private void OnApplicationQuit()
    {
        if (Instance == this) SaveGame();
    }

    public SaveData CaptureSave()
    {
        SaveData data = new SaveData
        {
            guildName = guildName,
            gold = gold,
            reputation = reputation,
            day = day,
            guildLevel = guildLevel,
            month = month,
            week = week,
            receptionLevel = receptionLevel,
            innLevel = innLevel,
            pubLevel = pubLevel,
            templeLevel = templeLevel,
            questBoardLevel = questBoardLevel,
            visitorsGenerated = visitorsGenerated,
        };

        foreach (Adventurer adv in adventurers)     data.adventurers.Add(ToSave(adv));
        foreach (Adventurer adv in deadAdventurers) data.deadAdventurers.Add(ToSave(adv));
        foreach (Adventurer adv in visitors)        data.visitors.Add(ToSave(adv));

        foreach (Party party in partyList)
        {
            PartySave partySave = new PartySave { partyName = party.partyName, roomIndex = party.roomIndex };
            foreach (Adventurer member in party.members)
            {
                int index = adventurers.IndexOf(member);
                if (index >= 0) partySave.memberIndices.Add(index);
            }
            data.parties.Add(partySave);
        }

        foreach (var pair in bonds)
        {
            string[] names = pair.Key.Split('|');
            if (names.Length == 2 && pair.Value > 0) data.bonds.Add(new BondSave { a = names[0], b = names[1], completions = pair.Value });
        }
        foreach (var pair in relations)
        {
            string[] names = pair.Key.Split('|');
            if (names.Length == 2 && pair.Value != RelationType.None) data.relations.Add(new RelationSave { a = names[0], b = names[1], type = pair.Value });
        }
        data.roomLevels.AddRange(roomLevels);

        foreach (var pair in guildInventory)
            data.inventory.Add(new InventoryEntrySave { itemName = pair.Key, count = pair.Value });

        data.tutorial = tutorial;
        data.record = record;
        data.equipment = new List<EquipmentItem>(equipmentStorage);
        data.equipmentSerial = equipmentSerial;
        return data;
    }

    public void ApplySave(SaveData data)
    {
        if (data == null) return;

        guildName = data.guildName;
        gold = data.gold;
        reputation = data.reputation;
        day = data.day;
        guildLevel = data.guildLevel;
        month = data.month;
        week = data.week;
        receptionLevel = data.receptionLevel;
        innLevel = data.innLevel;
        pubLevel = data.pubLevel;
        templeLevel = data.templeLevel;
        questBoardLevel = data.questBoardLevel;

        adventurers.Clear();
        deadAdventurers.Clear();
        visitors.Clear();
        foreach (AdventurerSave s in data.adventurers)     adventurers.Add(FromSave(s));
        foreach (AdventurerSave s in data.deadAdventurers) deadAdventurers.Add(FromSave(s));
        foreach (AdventurerSave s in data.visitors)        visitors.Add(FromSave(s));
        visitorsGenerated = data.visitorsGenerated;

        bonds.Clear();
        if (data.bonds != null)
            foreach (BondSave bond in data.bonds)
                if (!string.IsNullOrEmpty(bond.a) && !string.IsNullOrEmpty(bond.b))
                    bonds[PartyChemistryRules.PairKey(bond.a, bond.b)] = bond.completions;
        relations.Clear();
        if (data.relations != null)
            foreach (RelationSave r in data.relations)
                if (!string.IsNullOrEmpty(r.a) && !string.IsNullOrEmpty(r.b) && r.type != RelationType.None)
                    relations[PartyChemistryRules.PairKey(r.a, r.b)] = r.type;
        roomLevels.Clear();
        if (data.roomLevels != null) roomLevels.AddRange(data.roomLevels);

        partyList.Clear();
        foreach (PartySave partySave in data.parties)
        {
            Party party = new Party(partySave.partyName) { roomIndex = partySave.roomIndex };
            foreach (int index in partySave.memberIndices)
                if (index >= 0 && index < adventurers.Count) party.members.Add(adventurers[index]);
            MigratePartyChemistry(party, partySave.chemistryCompletions); // 예전 저장 파일의 파티 케미 → 관계
            partyList.Add(party);
        }

        guildInventory.Clear();
        foreach (InventoryEntrySave entry in data.inventory)
            guildInventory[entry.itemName] = entry.count;

        // 진행 중이던 것들은 초기화 (의뢰는 퀘스트 보드를 열 때 다시 생성됨)
        foreach (QuestData quest in new List<QuestData>(rescueQuests)) DestroyRescueQuest(quest);
        foreach (QuestData quest in new List<QuestData>(corpseQuests)) DestroyCorpseQuest(quest);
        currentDispatchParty = null;
        currentQuest = null;
        hasPendingResult = false;
        lastEarnedLoot.Clear();
        ResetDispatchResult();
        isGameOver = false;
        tutorial = data.tutorial != null && data.tutorial.seenTips != null ? data.tutorial : TutorialProgress.Finished(); // 예전 저장 파일은 튜토리얼 없이
        record = data.record ?? new GuildRecord();
        equipmentStorage = data.equipment != null ? data.equipment.FindAll(e => !EquipmentRules.IsEmpty(e)) : new List<EquipmentItem>();
        equipmentSerial = data.equipmentSerial;
        if (record.fallen == null) record.fallen = new List<string>();
        if (record.dragonSlayers == null) record.dragonSlayers = new List<string>();

        Debug.Log($"📂 [불러오기] '{guildName}' {month}월 {week}주차, 모험가 {adventurers.Count}명, 파티 {partyList.Count}개");
    }

    private AdventurerSave ToSave(Adventurer adv)
    {
        AdventurerSave s = new AdventurerSave
        {
            name = adv.name,
            job = adv.job,
            rank = adv.rank,
            trait = adv.trait,
            level = adv.level,
            exp = adv.exp,
            expToNextLevel = adv.expToNextLevel,
            hp = adv.hp,
            atk = adv.atk,
            def = adv.def,
            hpGrowthCarry = adv.hpGrowthCarry,
            atkGrowthCarry = adv.atkGrowthCarry,
            defGrowthCarry = adv.defGrowthCarry,
            speed = adv.speed,
            currentHp = adv.currentHp,
            isDead = adv.isDead,
            potential = adv.potential,
            recoveryWeeks = adv.recoveryWeeks,
            diedAtQuestName = adv.diedAtQuestName,
            lastQuestRegion = adv.lastQuestRegion,
            lastQuestRank = adv.lastQuestRank,
            corpseRecovered = adv.corpseRecovered,
            isStranded = adv.isStranded,
            strandedWeeksRemaining = adv.strandedWeeksRemaining,
            morale = adv.morale,
            assignedPartyIndex = adv.assignedPartyIndex,
            depressionWeeks = adv.depressionWeeks,
            mood = adv.mood,
            look = adv.look,
            weapon = adv.weapon,
            armor = adv.armor,
        };
        if (adv.traits != null)
            foreach (TraitData trait in adv.traits)
                if (trait != null) s.traitNames.Add(trait.traitName);
        return s;
    }

    private Adventurer FromSave(AdventurerSave s)
    {
        Adventurer adv = new Adventurer
        {
            name = s.name,
            job = s.job,
            rank = s.rank,
            trait = s.trait,
            level = s.level,
            exp = s.exp,
            expToNextLevel = s.expToNextLevel,
            hp = s.hp,
            atk = s.atk,
            def = s.def > 0 ? s.def : DefenseRules.Recompute(defense, s.job, s.rank, s.level),
            hpGrowthCarry = s.hpGrowthCarry,
            atkGrowthCarry = s.atkGrowthCarry,
            defGrowthCarry = s.defGrowthCarry,
            speed = s.speed,
            currentHp = s.currentHp,
            isDead = s.isDead,
            potential = s.potential,
            recoveryWeeks = s.recoveryWeeks,
            diedAtQuestName = s.diedAtQuestName ?? "",
            lastQuestRegion = s.lastQuestRegion ?? "",
            lastQuestRank = s.lastQuestRank ?? "",
            corpseRecovered = s.corpseRecovered,
            isStranded = s.isStranded,
            strandedWeeksRemaining = s.strandedWeeksRemaining,
            morale = s.morale != 0f ? s.morale : -s.moralePenalty, // 예전 저장 파일은 하락량만 있었음
            assignedPartyIndex = s.assignedPartyIndex,
            depressionWeeks = s.depressionWeeks,
            mood = s.mood,
            look = s.look != null && s.look.IsValid ? s.look : null,
            weapon = EquipmentRules.IsEmpty(s.weapon) ? new EquipmentItem() : s.weapon,
            armor = EquipmentRules.IsEmpty(s.armor) ? new EquipmentItem() : s.armor,
        };
        foreach (string traitName in s.traitNames)
        {
            TraitData trait = FindTraitByName(traitName);
            if (trait != null) adv.traits.Add(trait);
            else Debug.LogWarning($"⚠️ [불러오기] '{adv.name}'의 특성 '{traitName}'을(를) 특성 풀에서 찾지 못해 제외했습니다.");
        }
        return adv;
    }

    // 특성 분류 (모험가 정보에서 특성 / 트라우마 / 결함을 나눠 보여줄 때 사용)
    public TraitCategory GetTraitCategory(TraitData trait)
    {
        if (trait == null) return TraitCategory.Positive;
        if (trait.isPhysical) return TraitCategory.Defect;
        return trait.category;
    }

    // 특성 툴팁 아래에 붙는 보충 설명 (치료 장소, 남은 기간)
    public string GetTraitNote(Adventurer adv, TraitData trait)
    {
        if (trait == null) return "";
        switch (GetTraitCategory(trait))
        {
            case TraitCategory.Defect: return "결함 · 치유소에서 치료 (신전에서는 치료할 수 없음)";
            case TraitCategory.Trauma:
                return trait == depressionTrait && adv.depressionWeeks > 0
                    ? $"트라우마 · {adv.depressionWeeks}주 뒤 사라짐 · 신전에서 치료 가능"
                    : "트라우마 · 신전에서 치료 가능";
            default: return "";
        }
    }

    private TraitData FindTraitByName(string traitName)
    {
        TraitData found = positiveTraitPool.Find(t => t != null && t.traitName == traitName);
        if (found == null) found = traumaPool.Find(t => t != null && t.traitName == traitName);
        if (found == null) found = defectPool.Find(t => t != null && t.traitName == traitName);
        if (found == null)
            foreach (TraitData special in new[] { depressionTrait, heroAuraTrait, sixthSenseTrait, dragonSlayerTrait })
                if (special != null && special.traitName == traitName) { found = special; break; }
        return found;
    }

    // TraitData ScriptableObject를 런타임에 생성하는 헬퍼. elseValue를 주면 조건을 못 채울 때 그 값으로 같은 효과
    private TraitData MakeTrait(string traitName, string description, TraitCategory category,
        TraitConditionType condType, string condTag, float condValue,
        TraitEffectType effType, float effValue, float? elseValue = null, params TraitExtraEffect[] extras)
    {
        TraitData t       = ScriptableObject.CreateInstance<TraitData>();
        t.traitName       = traitName;
        t.description     = description;
        t.category        = category;
        t.conditionType   = condType;
        t.conditionTag    = condTag;
        t.conditionValue  = condValue;
        t.effectType      = effType;
        t.effectValue     = effValue;
        t.isPhysical      = category == TraitCategory.Defect;
        t.hasElseEffect   = elseValue.HasValue;
        t.elseEffectValue = elseValue ?? 1f;
        t.extraEffects    = new List<TraitExtraEffect>(extras);
        return t;
    }

    static TraitExtraEffect Extra(TraitEffectType type, float value) => new TraitExtraEffect { effectType = type, effectValue = value };

    // 파티원마다 이번 의뢰의 지역과 랭크를 기록 (죽거나 낙오하면 구출·유해 수습 의뢰가 같은 지역·같은 랭크로 생김)
    void RecordQuestOrigin(Party party, QuestData quest)
    {
        if (party == null || quest == null) return;
        foreach (Adventurer member in party.members)
        {
            if (member == null) continue;
            member.lastQuestRegion = quest.region ?? "";
            member.lastQuestRank = quest.rank ?? "";
        }
    }

    public void StartQuest(Party party, QuestData quest) //퀘스트 파견 시작 함수

    {

        // 1. 짐 챙기기 (데이터 저장)

        currentDispatchParty = party;

        currentQuest = quest;

        // 이전 파견의 결산 기록 초기화
        ResetDispatchResult();
        RecordQuestOrigin(party, quest);

        // 2. 파티 상태 변경

        party.state = PartyState.OnQuest;

        Debug.Log($"🚀 '{party.partyName}' 파티가 '{quest.questName}' 의뢰(적: {quest.enemyPrefab.name})를 수행하러 갑니다!");

        // 3. 던전 씬 로딩

        // (Build Settings에 'Dungeon' 씬이 등록되어 있어야 합니다!)

        SceneManager.LoadScene("DungeonScene");

    }

    // 직업(JobType)을 주면 해당 프리팹을 뱉어내는 함수
    public GameObject GetUnitPrefab(JobType job)
    {
        if (unitPrefabs == null || unitPrefabs.Length == 0) return null;

        // 배열 순서가 씬마다 달라도 맞는 프리팹이 나가도록, 프리팹에 붙은 유닛 스크립트로 직업을 확인
        foreach (GameObject prefab in unitPrefabs)
            if (prefab != null && IsUnitPrefabOfJob(prefab, job)) return prefab;

        Debug.LogWarning($"⚠️ {job} 직업에 맞는 유닛 프리팹이 unitPrefabs에 없습니다. 첫 번째 프리팹을 대신 사용합니다.");
        return unitPrefabs[0];
    }

    private static bool IsUnitPrefabOfJob(GameObject prefab, JobType job)
    {
        switch (job)
        {
            case JobType.Warrior: return prefab.GetComponent<Ally_Warrior>() != null;
            case JobType.Mage:    return prefab.GetComponent<Ally_Mage>() != null;
            case JobType.Archer:  return prefab.GetComponent<Ally_Archer>() != null;
            case JobType.Healer:  return prefab.GetComponent<Ally_Healer>() != null;
            case JobType.Rogue:   return prefab.GetComponent<Ally_Rogue>() != null;
            default:              return false;
        }
    }

    // 테스트용 함수

    public void AddGold(int amount)

    {

        gold += amount;

        Debug.Log($"[재정] 현재 골드: {gold} G");

    }

    // ★ 전투가 끝나고 BattleManager가 전리품을 싸들고 와서 보고하는 함수
    public void AddLootToGuild(List<MonsterDropData> dungeonLoot, bool isWin, List<EquipmentItem> equipmentLoot = null)
    {
        lastEarnedLoot.Clear(); // 이전 결산 기록 지우기
        AddEquipmentLoot(equipmentLoot);

        foreach (MonsterDropData item in dungeonLoot)
        {
            // MonsterDropData 안에 있는 아이템 이름 가져오기
            string itemName = item.itemName; 

            // 1. 길드 창고에 아이템 쑤셔넣기 (있으면 +1, 없으면 새로 등록)
            if (guildInventory.ContainsKey(itemName))
                guildInventory[itemName]++;
            else
                guildInventory.Add(itemName, 1);

            // 2. 결산 창에 띄워주기 위해 임시 바구니에도 담기
            if (lastEarnedLoot.ContainsKey(itemName))
                lastEarnedLoot[itemName]++;
            else
                lastEarnedLoot.Add(itemName, 1);
        }

        lastBattleWon = isWin;
        hasPendingResult = true; // "로비야, 결산 창 띄울 준비 해!"
        
        Debug.Log($"📦 길드 창고 저장 완료! (총 {dungeonLoot.Count}개의 전리품을 정리했습니다)");
    }

    // ★ 전투 후 생환 파티원에게 경험치를 지급하고 레벨업을 처리합니다.
    // BattleManager.OnClick_ReturnToLobby에서 승리 시 호출합니다.
    public void GainExpAfterBattle(List<Adventurer> survivors, string questRank, float expMultiplier = 1f)
    {
        if (survivors == null || survivors.Count == 0) return;

        int expGain = Mathf.RoundToInt(GetExpByRank(questRank) * expMultiplier); // 동시 파견은 배율이 1보다 작음

        foreach (Adventurer adv in survivors)
        {
            if (adv.isDead || adv.isStranded) continue;

            int maxLevel = AdventurerLevelRules.GetMaxLevel(adventurerLevel, adv.rank);
            if (adv.level >= maxLevel)
            {
                adv.exp = 0; // 최대 레벨이면 경험치를 쌓지 않음
                continue;
            }

            adv.exp += expGain;
            Debug.Log($"⭐ [{adv.name}] 경험치 +{expGain} (현재: {adv.exp}/{adv.expToNextLevel})");

            // 연속 레벨업 처리 (한 번 전투로 여러 레벨 오를 수 있음, 최대 레벨에서 멈춤)
            while (adv.level < maxLevel && adv.exp >= adv.expToNextLevel)
            {
                adv.exp -= adv.expToNextLevel;
                LevelUp(adv);
            }
            if (adv.level >= maxLevel)
            {
                adv.exp = 0;
                Debug.Log($"🏅 [{adv.name}] {adv.rank}랭크 최대 레벨(Lv.{maxLevel}) 도달");
            }
        }
    }

    private void LevelUp(Adventurer adv)
    {
        adv.level++;
        adv.expToNextLevel = EconomyRules.ExpToNextLevel(economy, adv.level);

        float growth = PotentialRules.GrowthMultiplier(potentialSettings, adv.potential); // 잠재력이 높을수록 많이 오름
        // 소수 이월: 레벨마다 반올림한 값을 받고 남은 소수는 다음 레벨업에 더함
        int hpGain  = StatGrowthRules.TakeWithCarry(StatGrowthRules.HpPerLevel(jobStats, adv.job, adv.rank, growth), ref adv.hpGrowthCarry);
        int atkGain = StatGrowthRules.TakeWithCarry(StatGrowthRules.AtkPerLevel(jobStats, adv.job, adv.rank, growth), ref adv.atkGrowthCarry);
        int defGain = StatGrowthRules.TakeWithCarry(DefenseRules.GetLevelGainRaw(defense, adv.job, adv.rank, growth), ref adv.defGrowthCarry);
        adv.hp      += hpGain;
        adv.atk     += atkGain;
        adv.def     += defGain;
        adv.currentHp = adv.hp; // 레벨업 시 체력 완전 회복

        string msg = $"{adv.name} Lv.{adv.level - 1} → Lv.{adv.level}! (HP+{hpGain} ATK+{atkGain} DEF+{defGain})";
        levelUpLog.Add(msg);
        Debug.Log($"🎉 [레벨업] {msg}");
        ChangeMood(adv, moodSettings.levelUp, "레벨업");

        // 3레벨마다 50% 확률로 긍정 특성 획득
        if (adv.level % 3 == 0 && positiveTraitPool.Count > 0)
        {
            if (Random.Range(0, 100) < 50)
            {
                TraitData newTrait = positiveTraitPool[Random.Range(0, positiveTraitPool.Count)];
                if (!adv.traits.Contains(newTrait))
                {
                    adv.traits.Add(newTrait);
                    string traitMsg = $"{adv.name}이(가) 특성 '{newTrait.traitName}' 획득!";
                    levelUpLog.Add(traitMsg);
                    Debug.Log($"✨ [특성 획득] {traitMsg}");
                }
            }
        }
    }

    private int GetExpByRank(string rank) => EconomyRules.QuestExp(economy, rank);

    // 영혼석으로 전사자를 레벨 1로 부활시킵니다.
    // SpiritStonePopup에서 호출합니다.
    public bool ReviveWithSpiritStone(Adventurer deceased)
    {
        string stoneKey = $"{deceased.name}의 영혼석";
        int goldCost    = GetReviveCost(deceased.rank);

        if (!guildInventory.ContainsKey(stoneKey) || guildInventory[stoneKey] <= 0)
        {
            Debug.LogWarning($"❌ [부활 실패] '{stoneKey}'이(가) 없습니다.");
            return false;
        }
        if (gold < goldCost)
        {
            Debug.LogWarning($"❌ [부활 실패] 골드가 부족합니다. (필요: {goldCost}G, 보유: {gold}G)");
            return false;
        }

        // 자원 소모
        guildInventory[stoneKey]--;
        if (guildInventory[stoneKey] <= 0) guildInventory.Remove(stoneKey);
        gold -= goldCost;

        // 레벨 1로 초기화 후 현역 복귀
        deceased.level            = 1;
        deceased.isDead           = false;
        deceased.corpseRecovered  = false;
        deceased.isStranded       = false;
        deceased.strandedWeeksRemaining = 0;
        deceased.recoveryWeeks    = 2; // 부활 후 2주 요양
        deceased.morale           = 0f;
        deceased.assignedPartyIndex = -1;
        deceased.currentHp        = deceased.hp;

        // 트라우마(부정 특성)만 제거, 긍정 특성은 보존
        deceased.traits.RemoveAll(t => traumaPool.Contains(t) || t.isPhysical || t == depressionTrait);
        deceased.depressionWeeks = 0;

        deadAdventurers.Remove(deceased);
        adventurers.Add(deceased);

        // 해당 유해 수습 퀘스트 제거
        QuestData cq = corpseQuests.Find(q => q.corpseTargetName == deceased.name);
        if (cq != null) DestroyCorpseQuest(cq);

        Debug.Log($"✨ [부활] {deceased.name}이(가) 영혼석의 힘으로 레벨 1로 되살아났습니다! (2주 요양 필요)");
        return true;
    }

    public int GetReviveCost(RankType rank) => EconomyRules.ReviveCost(economy, rank);

    // ─── 상점: 전리품 판매 ───────────────────────────────────────────

    // 창고에 있는 아이템 개수
    public int GetItemCount(string itemName)
    {
        return (itemName != null && guildInventory.TryGetValue(itemName, out int count)) ? count : 0;
    }

    // 전리품을 팔아 골드로 바꿈. 가진 것보다 많이 팔 수는 없으며, 번 골드를 반환 (판매가는 상점이 아이템 데이터에서 읽어 넘김)
    public int SellItem(string itemName, int count, int unitPrice)
    {
        count = Mathf.Min(count, GetItemCount(itemName));
        if (count <= 0 || unitPrice <= 0) return 0;

        guildInventory[itemName] -= count;
        if (guildInventory[itemName] <= 0) guildInventory.Remove(itemName);

        int earned = count * unitPrice;
        gold += earned;
        Debug.Log($"🪙 [상점] {itemName} x{count} 판매 (+{earned}G, 보유 {gold}G)");
        return earned;
    }

    // 보유한 모든 영혼석 총 개수 (로비 상단 표시용)
    public int GetSpiritStoneCount()
    {
        int total = 0;
        foreach (var pair in guildInventory)
            if (pair.Key.EndsWith("의 영혼석")) total += pair.Value;
        return total;
    }

    // 특정 모험가의 영혼석 보유 여부
    public bool HasSpiritStone(Adventurer deceased)
    {
        string key = $"{deceased.name}의 영혼석";
        return guildInventory.ContainsKey(key) && guildInventory[key] > 0;
    }

    // 유해 수습 완료: 전사자 랭크에 따른 확률로 그 모험가의 영혼석을 얻고, 얻지 못하면 골드를 받습니다.
    // (장비 시스템이 아직 없어서 유품 대신 골드로 보상)
    // BattleManager에서 승리 직후, CleanupCurrentQuest 전에 호출합니다.
    public void CompleteCorpseQuest(QuestData quest)
    {
        if (quest == null || !quest.isCorpseQuest) return;

        Adventurer deceased = deadAdventurers.Find(a => a.name == quest.corpseTargetName);
        if (deceased != null)
        {
            // 유해와 함께 남아 있던 장비를 되찾음
            var recovered = new List<EquipmentItem>();
            if (!EquipmentRules.IsEmpty(deceased.weapon)) recovered.Add(deceased.weapon);
            if (!EquipmentRules.IsEmpty(deceased.armor)) recovered.Add(deceased.armor);
            deceased.SetEquipment(EquipSlot.Weapon, null);
            deceased.SetEquipment(EquipSlot.Armor, null);
            AddEquipmentLoot(recovered);

            if (CorpseRecoveryRules.RollSpiritStone(corpseRecovery, deceased.rank, Random.value))
            {
                string stoneKey = $"{deceased.name}의 영혼석";
                if (guildInventory.ContainsKey(stoneKey))
                    guildInventory[stoneKey]++;
                else
                    guildInventory.Add(stoneKey, 1);

                if (lastEarnedLoot.ContainsKey(stoneKey))
                    lastEarnedLoot[stoneKey]++;
                else
                    lastEarnedLoot.Add(stoneKey, 1);

                // 신전에서 부활시킬 수 있도록 전사자 목록에 남겨 둠 (유해 수습 의뢰만 끝남)
                deceased.corpseRecovered = true;

                Debug.Log($"💎 [유해 수습 완료] {deceased.name}의 유해를 수습했습니다. '{stoneKey}' 획득! (신전에서 부활 가능)");
            }
            else
            {
                // 영혼석을 얻지 못함: 골드만 받고, 되살릴 수 없으므로 전사자 목록에서 지움
                int recoveredGold = CorpseRecoveryRules.RollGold(corpseRecovery, deceased.rank, Random.value);
                gold += recoveredGold;
                lastEarnedGold += recoveredGold;

                Debug.Log($"⚰️ [유해 수습 완료] {deceased.name}의 유해를 수습했지만 영혼석은 찾지 못했습니다. (+{recoveredGold}G)");

                deadAdventurers.Remove(deceased);
            }
        }

        corpseQuests.Remove(quest);
    }

    // 유해 수습 퀘스트를 목록에서 제거하고 ScriptableObject 인스턴스를 파괴합니다.
    public void DestroyCorpseQuest(QuestData quest)
    {
        if (quest == null) return;
        corpseQuests.Remove(quest);
        if (quest.stages != null)
            foreach (StageData s in quest.stages)
                if (s != null) Destroy(s);
        Destroy(quest);
    }

    // 구출 퀘스트 완료: 낙오 모험가를 구출하고 퀘스트를 목록에서 제거합니다.
    // BattleManager에서 승리 직후, CleanupCurrentQuest 전에 호출합니다.
    public void CompleteRescueQuest(QuestData quest)
    {
        if (quest == null || !quest.isRescueQuest) return;

        Adventurer rescued = adventurers.Find(a => a.name == quest.rescueTargetName && a.isStranded);
        if (rescued != null)
        {
            rescued.isStranded             = false;
            rescued.strandedWeeksRemaining = 0;
            rescued.currentHp              = Mathf.Max(1, rescued.hp / 2); // 생환, 절반 체력
            rescued.recoveryWeeks          = 1;                             // 1주 요양 필요
            rescued.assignedPartyIndex     = -1;
            rescued.diedAtQuestName        = "";
            Debug.Log($"🎉 [구출 완료] {rescued.name}이(가) 무사히 귀환했습니다! (1주 요양 후 파견 가능)");
        }

        rescueQuests.Remove(quest);
        // 인스턴스 파괴는 CleanupCurrentQuest에서 처리
    }

    // 구출 퀘스트를 목록에서 제거하고 ScriptableObject 인스턴스를 파괴합니다.
    private void DestroyRescueQuest(QuestData quest)
    {
        if (quest == null) return;
        rescueQuests.Remove(quest);
        if (quest.stages != null)
            foreach (StageData s in quest.stages)
                if (s != null) Destroy(s);
        Destroy(quest);
    }

    // QuestGenerator로 생성된 런타임 ScriptableObject 인스턴스를 정리합니다.
    // BattleManager에서 퀘스트가 끝날 때 호출하세요.
    public void CleanupCurrentQuest()
    {
        if (currentQuest == null) return;

        // 완수하지 못한 구출/유해 수습 의뢰는 목록에 남아 있으므로 파괴하지 않음 (다시 도전 가능)
        if (rescueQuests.Contains(currentQuest) || corpseQuests.Contains(currentQuest))
        {
            currentQuest = null;
            return;
        }

        if (currentQuest.stages != null)
            foreach (StageData s in currentQuest.stages)
                if (s != null) Destroy(s);

        Destroy(currentQuest);
        currentQuest = null;
    }

    public void AddQuestRewards(int earnedGold, int earnedRep, bool won)
    {
        gold += earnedGold;
        reputation += earnedRep;
        lastEarnedGold += earnedGold;
        lastEarnedReputation += earnedRep;
        lastBattleWon = won;
        hasPendingResult = true; // 로비에서 결과창을 띄우도록 예약
        
        Debug.Log($"💰 [보상 정산] 골드: +{earnedGold}, 명성: +{earnedRep} (현재 골드: {gold})");
    }

    // ★ [실행 페이즈 -> 정산 페이즈] 1주가 지나는 함수 (로비 결산창에서 확인을 누를 때 실행!)
    public void PassWeek()
    {
        week++;
        Debug.Log($"⏳ 시간이 1주 흘렀습니다. (현재 날짜: {month}월 {week}주차)");

        // 4주차가 넘어가면(즉 5주차가 되면) 다음 달로 넘기고 월급 정산!
        if (week > weeksPerMonth)
        {
            week = 1; 
            month++;  
            
            MonthlySettlement(); // 💸 월급 정산 실행!
        }
        
        // 부상자 회복 처리
        foreach (Adventurer adv in adventurers)
        {
            if (!adv.isDead && adv.recoveryWeeks > 0)
            {
                adv.recoveryWeeks--;
                if (adv.recoveryWeeks <= 0)
                {
                    adv.currentHp     = adv.hp;
                    adv.recoveryWeeks = 0;
                    if (adv.morale < 0f) adv.morale = 0f; // 다 나으면 떨어진 사기도 회복 (오른 사기는 그대로)
                    Debug.Log($"💖 [회복 완료] {adv.name}님이 완치되었습니다! (다시 파견 가능)");
                }
            }
        }

        // 우울함: 기간이 지나면 저절로 사라짐
        foreach (Adventurer adv in adventurers)
        {
            if (adv.isDead || adv.depressionWeeks <= 0) continue;
            adv.depressionWeeks--;
            if (adv.depressionWeeks <= 0) ClearDepression(adv, "시간이 지남");
        }

        // 낙오 모험가 구출 기한 타이머 처리
        List<Adventurer> expiredList = new List<Adventurer>();
        lastExpiredStrandedNames.Clear();
        foreach (Adventurer adv in adventurers)
        {
            if (!adv.isStranded) continue;

            adv.strandedWeeksRemaining--;
            Debug.Log($"⏳ [낙오] {adv.name} - 구출 기한 {adv.strandedWeeksRemaining}주 남음");

            if (adv.strandedWeeksRemaining <= 0)
            {
                Debug.Log($"💀 [사망] {adv.name}이(가) 구출되지 못하고 던전에서 사망했습니다...");
                expiredList.Add(adv);
                lastExpiredStrandedNames.Add(adv.name);

                // 해당 구출 퀘스트도 제거
                QuestData rq = rescueQuests.Find(q => q.rescueTargetName == adv.name);
                if (rq != null) DestroyRescueQuest(rq);
            }
        }
        // 구출 기한이 지난 낙오자는 전사자로 처리 (전사자 목록에 넣어 유해 수습·신전 부활 대상이 됨)
        foreach (Adventurer dead in expiredList)
        {
            dead.isStranded             = false;
            dead.strandedWeeksRemaining = 0;
            dead.isDead                 = true;
            NoteFallen(dead);
            dead.currentHp              = 0;
            if (string.IsNullOrEmpty(dead.diedAtQuestName)) dead.diedAtQuestName = "알 수 없는 던전";
            adventurers.Remove(dead);
            if (!deadAdventurers.Contains(dead)) deadAdventurers.Add(dead);
        }

        UpdateWeeklyMood();

        // 사기: 주점은 떨어진 사기를 회복(레벨당 5%), 오른 사기는 매주 조금씩 식음. 길드에 있는 모든 모험가 대상, 던전에 남은 낙오자는 제외
        float pubRecovery = pubLevel > 0 ? pubLevel * 0.05f : 0f;
        int pubRecovered = 0;
        foreach (Adventurer adv in adventurers)
        {
            if (adv.isDead || adv.isStranded || adv.morale == 0f) continue;
            float before = adv.morale;
            adv.morale = before < 0f ? MoraleRules.PubRecover(before, pubRecovery) : MoraleRules.WeeklyDecay(moraleSettings, before);
            if (before < 0f && adv.morale > before)
            {
                pubRecovered++;
                Debug.Log(adv.morale >= 0f ? $"🍺 [주점] {adv.name}의 사기가 완전히 회복되었습니다!"
                                           : $"🍺 [주점] {adv.name} 사기 회복 중 ({before * 100f:F0}% → {adv.morale * 100f:F0}%)");
            }
        }
        if (pubRecovered > 0)
            Debug.Log($"🍺 [주점 Lv.{pubLevel}] {pubRecovered}명의 모험가 사기 회복 (주당 {pubRecovery * 100f:F0}%)");

        // 접수처 방문 모험가 교체 (고용하지 않은 모험가는 떠남)
        RefreshVisitors();

        // 한 주가 지날 때마다 자동 저장 (파산했다면 저장하지 않음)
        SaveGame();
    }

    // ─── 접수처: 방문 모험가 고용 ───────────────────────────────────────

    // 아직 이번 턴 방문 모험가를 만들지 않았다면 생성 (접수처 화면을 열 때 호출)
    public void EnsureVisitors()
    {
        if (!visitorsGenerated) RefreshVisitors();
    }

    // 접수처 레벨만큼의 방문 모험가를 새로 생성. 랭크는 명성에 따른 확률표로 결정
    public void RefreshVisitors()
    {
        visitors.Clear();
        visitorsGenerated = true;

        int count = RecruitmentRules.GetVisitorCount(recruitment, receptionLevel);
        float[] weights = RecruitmentRules.GetRankWeights(recruitment, reputation);

        for (int i = 0; i < count; i++)
        {
            RankType rank = RecruitmentRules.RollRank(weights, Random.value);
            visitors.Add(AdventurerGenerator.Generate(rank));
        }

        Debug.Log($"🚪 [접수처 Lv.{receptionLevel}] 모험가 {count}명이 길드를 찾아왔습니다. (명성: {reputation})");
    }

    public int GetHireCost(RankType rank)
    {
        return RecruitmentRules.GetHireCost(recruitment, rank);
    }

    // 새 게임을 시작할 때 기본 모험가를 지급 (길드 생성 화면에서 호출). 직업과 랭크만 정해져 있고 나머지는 무작위
    public void GrantStarterAdventurers()
    {
        if (starterJobs == null) return;

        foreach (JobType job in starterJobs)
        {
            Adventurer adv = AdventurerGenerator.Generate(starterRank, job);
            adventurers.Add(adv);
            Debug.Log($"🎁 [시작 모험가] {adv.name} ({adv.job}, Rank {adv.rank}, {adv.GetTraitName()})");
        }
    }

    // 방문 모험가 고용. 실패 시 failMessage에 사유를 담아 false 반환
    public bool TryHireVisitor(Adventurer visitor, out string failMessage)
    {
        failMessage = "";
        if (visitor == null || !visitors.Contains(visitor)) return false;

        int cost = GetHireCost(visitor.rank);
        if (gold < cost)
        {
            failMessage = $"골드가 부족합니다! (필요: {cost}G)";
            return false;
        }
        if (adventurers.Count >= GetMaxAdventurers())
        {
            failMessage = $"여관이 꽉 찼습니다! ({adventurers.Count}/{GetMaxAdventurers()})";
            return false;
        }

        gold -= cost;
        visitors.Remove(visitor);
        adventurers.Add(visitor);
        if (record != null) record.hired++;

        Debug.Log($"🤝 [고용] {visitor.name} ({visitor.job}, Rank {visitor.rank}) -{cost}G");
        return true;
    }

    // 방문 모험가를 고용하지 않고 돌려보냄
    public void DismissVisitor(Adventurer visitor)
    {
        if (visitors.Remove(visitor))
            Debug.Log($"👋 [돌려보냄] {visitor.name} ({visitor.job}, Rank {visitor.rank})");
    }

    // 이번 달 모험가 월급 합계 (던전에 낙오해 있는 모험가는 월급을 받지 않음)
    public int GetTotalSalary()
    {
        int totalSalary = 0;
        foreach (Adventurer adv in adventurers)
        {
            if (adv.isStranded) continue;
            int salary = EconomyRules.Salary(economy, adv.rank, adv.level);
            if (adv.trait == TraitType.Humble) salary = Mathf.RoundToInt(salary * personality.humbleSalaryMultiplier); // 겸손한 성격
            totalSalary += salary;
        }
        return totalSalary;
    }

    // 지금 한 주를 넘기면 달이 바뀌어 월급을 지급하게 되는지
    public bool IsNextWeekPayday()
    {
        return week >= weeksPerMonth;
    }

    // ★ 월급 정산 (파산의 위험)
    private void MonthlySettlement()
    {
        Debug.Log($"📅 {month}월 1주차가 되었습니다! [길드 유지비 및 월급 정산의 날]");
        int totalSalary = GetTotalSalary();

        gold -= totalSalary; // 길드 자금에서 뭉텅이로 차감!

        Debug.Log($"💸 모험가 {adventurers.Count}명의 월급으로 총 {totalSalary}G가 지출되었습니다. (남은 골드: {gold}G)");

        if (gold < 0)
        {
            Debug.LogError("🚨 [파산] 길드 자금이 마이너스입니다! 길드가 파산했습니다!");
            TriggerGameOver();
        }
    }

    private void TriggerGameOver()
    {
        // 파산한 길드는 이어할 수 없음
        isGameOver = true;
        SaveSystem.Delete();

        Time.timeScale = 0f; // 게임 일시정지
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);
        else if (LobbyManager.Instance != null && LobbyManager.Instance.gameOverPopup != null)
            LobbyManager.Instance.ShowGameOver(); // GameManager는 씬을 넘나들기 때문에 로비 쪽 팝업을 사용
        else
            Debug.LogWarning("⚠️ gameOverPanel이 Inspector에 연결되지 않았습니다.");
    }

    // 파산 팝업의 [타이틀로 돌아가기] 버튼에 연결
    public void OnClickGameOverConfirm()
    {
        Time.timeScale = 1f;

        // 파산한 길드의 데이터가 새 게임으로 넘어가지 않도록 이 매니저를 없앰 (타이틀 씬의 GameManager가 새로 자리잡음)
        if (Instance == this) Instance = null;
        Destroy(gameObject);

        SceneManager.LoadScene("TitleScene");
    }

    // ─── 시설 관련 함수 ─────────────────────────────────────────────────

    // 여관 레벨에 따른 최대 모험가 수
    public int GetMaxAdventurers() => GetMaxAdventurers(innLevel);

    public int GetMaxAdventurers(int level) => EconomyRules.MaxAdventurers(economy, level); // 기본 8, 레벨당 +4

    // 여관 레벨에 따른 방 수 (한 방에 파티 하나 → 길드가 수용 가능한 파티 수)
    public int GetMaxParties()
    {
        return GetMaxParties(innLevel);
    }

    public int GetMaxParties(int level)
    {
        if (partiesPerInnLevel == null || partiesPerInnLevel.Length == 0) return 0;
        int index = Mathf.Clamp(level - 1, 0, partiesPerInnLevel.Length - 1);
        return partiesPerInnLevel[index];
    }

    // 방 번호(0부터)를 쓰려면 필요한 여관 레벨. 어떤 레벨로도 열 수 없으면 -1
    public int GetInnLevelForRoom(int roomIndex)
    {
        if (partiesPerInnLevel == null) return -1;
        for (int i = 0; i < partiesPerInnLevel.Length; i++)
            if (partiesPerInnLevel[i] > roomIndex) return i + 1;
        return -1;
    }

    // 파티 해체. 모험가는 소속 파티를 목록 순번(assignedPartyIndex)으로 기억하므로,
    // 해체된 파티의 멤버는 무소속으로 돌리고 뒤쪽 파티 멤버들의 순번을 한 칸씩 당긴다
    public void DisbandParty(Party party)
    {
        int removedIndex = partyList.IndexOf(party);
        if (removedIndex < 0) return;

        foreach (Adventurer member in party.members)
            member.assignedPartyIndex = -1;

        partyList.RemoveAt(removedIndex);

        foreach (Adventurer adv in adventurers)
            if (adv.assignedPartyIndex > removedIndex) adv.assignedPartyIndex--;
    }

    // 해당 방에 묵고 있는 파티 (없으면 null)
    public Party GetPartyInRoom(int roomIndex)
    {
        if (roomIndex < 0) return null;
        return partyList.Find(p => p.roomIndex == roomIndex);
    }

    // 파티를 빈 방에 배정. 잠긴 방이거나 이미 다른 파티가 있으면 false
    public bool AssignPartyToRoom(Party party, int roomIndex)
    {
        if (party == null || !partyList.Contains(party)) return false;
        if (roomIndex < 0 || roomIndex >= GetMaxParties()) return false;

        Party occupant = GetPartyInRoom(roomIndex);
        if (occupant != null && occupant != party) return false;

        party.roomIndex = roomIndex;
        Debug.Log($"🛏️ [여관] '{party.partyName}' 파티가 {roomIndex + 1}번 방에 배정되었습니다.");
        return true;
    }

    // 파티를 방에서 내보냄 (파티는 유지, 방만 비움)
    public void VacateRoom(Party party)
    {
        if (party == null || party.roomIndex < 0) return;
        Debug.Log($"🛏️ [여관] '{party.partyName}' 파티가 {party.roomIndex + 1}번 방을 비웠습니다.");
        party.roomIndex = -1;
    }

    // 퀘스트 보드 레벨에 따른 동시 파견 슬롯 수
    public int GetMaxDispatchSlots()
    {
        return questBoardLevel + 1; // GDD: 기본 2(Lv1), 레벨당 +1 → Lv5=6
    }

    // 시설 업그레이드 (비용 차감 후 레벨 증가)
    // facilityType: "reception", "inn", "pub", "temple", "questboard"
    public bool UpgradeFacility(string facilityType)
    {
        int currentLevel = GetFacilityLevel(facilityType);
        int maxLevel = GetFacilityMaxLevel(facilityType);
        if (currentLevel >= maxLevel)
        {
            Debug.Log($"⚠️ [{facilityType}] 이미 최대 레벨입니다.");
            return false;
        }
        int cost = (currentLevel - 1 < facilityUpgradeCosts.Length)
            ? facilityUpgradeCosts[currentLevel - 1] : 9999;
        if (gold < cost)
        {
            Debug.Log($"⚠️ [{facilityType}] 골드 부족 (필요: {cost}G)");
            return false;
        }
        gold -= cost;
        SetFacilityLevel(facilityType, currentLevel + 1);
        Debug.Log($"✅ [{facilityType}] Lv{currentLevel} → Lv{currentLevel + 1} 업그레이드! (-{cost}G)");
        return true;
    }

    public int GetFacilityMaxLevel(string facilityType)
    {
        return EconomyRules.FacilityMaxLevel(economy, facilityType);
    }

    // 화면에 보여줄 레벨 (모든 시설이 Lv1부터 시작)
    public int GetFacilityDisplayLevel(string facilityType)
    {
        return GetFacilityLevel(facilityType);
    }

    public int GetFacilityLevel(string facilityType)
    {
        switch (facilityType)
        {
            case "reception":  return receptionLevel;
            case "inn":        return innLevel;
            case "pub":        return pubLevel;
            case "temple":     return templeLevel;
            case "questboard": return questBoardLevel;
            default: return 1;
        }
    }

    // 시설 업그레이드 비용 조회 (LobbyManager에서 미리보기 표시용)
    public int GetUpgradeCost(string facilityType)
    {
        int currentLevel = GetFacilityLevel(facilityType);
        int idx = currentLevel - 1;
        return (idx >= 0 && idx < facilityUpgradeCosts.Length)
            ? facilityUpgradeCosts[idx] : 9999;
    }

    private void SetFacilityLevel(string facilityType, int value)
    {
        switch (facilityType)
        {
            case "reception":  receptionLevel  = value; break;
            case "inn":        innLevel        = value; break;
            case "pub":        pubLevel        = value; break;
            case "temple":     templeLevel     = value; break;
            case "questboard": questBoardLevel = value; break;
        }
    }

    // ─── 신전: 정신적 부정 특성 제거 ────────────────────────────────────
    // 골드를 지불하고 모험가의 특정 트라우마(부정 특성)를 제거한다.
    public bool RemoveTrait(Adventurer adv, TraitData trait)
    {
        if (adv == null || trait == null) return false;
        if (trait.isPhysical)
        {
            Debug.LogWarning($"❌ [신전] '{trait.traitName}'은(는) 신체적 특성으로 신전에서 치유할 수 없습니다.");
            return false;
        }
        if (!adv.traits.Contains(trait))
        {
            Debug.LogWarning($"⚠️ {adv.name}은(는) '{trait.traitName}' 특성을 갖고 있지 않습니다.");
            return false;
        }

        // 비용: 랭크에 따라 차등 (신전 레벨이 높을수록 10%씩 할인)
        int baseCost = GetTraitRemoveCost(adv.rank);
        float discount = 1f - (templeLevel - 1) * 0.1f;
        int finalCost = Mathf.RoundToInt(baseCost * discount);

        if (gold < finalCost)
        {
            Debug.LogWarning($"❌ [특성 제거 실패] 골드 부족 (필요: {finalCost}G, 보유: {gold}G)");
            return false;
        }

        gold -= finalCost;
        adv.traits.Remove(trait);
        if (trait == depressionTrait) adv.depressionWeeks = 0;
        Debug.Log($"✨ [신전] {adv.name}의 '{trait.traitName}' 특성 제거! (-{finalCost}G)");
        return true;
    }

    public int GetTraitRemoveCost_Public(RankType rank) => GetTraitRemoveCost(rank);

    // ─── 치유소: 결함(신체) 치료 ──────────────────────────────────────
    public int GetDefectCureCost(RankType rank)
    {
        int i = (int)rank;
        if (clinicCureCost == null || clinicCureCost.Length == 0) return 0;
        return clinicCureCost[Mathf.Clamp(i, 0, clinicCureCost.Length - 1)];
    }

    // 골드를 내고 결함 하나를 치료. 실패하면 message에 이유
    public bool CureDefect(Adventurer adv, TraitData trait, out string message)
    {
        message = "";
        if (adv == null || trait == null || !adv.traits.Contains(trait)) { message = "치료할 결함이 없습니다."; return false; }
        if (GetTraitCategory(trait) != TraitCategory.Defect) { message = $"'{trait.traitName}'은(는) 치유소에서 치료할 수 없습니다."; return false; }
        if (adv.assignedPartyIndex >= 0 && adv.assignedPartyIndex < partyList.Count && partyList[adv.assignedPartyIndex].state == PartyState.OnQuest)
        { message = $"{adv.name}은(는) 의뢰를 나가 있습니다."; return false; }

        int cost = GetDefectCureCost(adv.rank);
        if (gold < cost) { message = $"골드가 부족합니다. (필요 {cost:N0} G)"; return false; }

        gold -= cost;
        adv.traits.Remove(trait);
        message = $"{adv.name}의 '{trait.traitName}'을(를) 치료했습니다. (-{cost:N0} G)";
        Debug.Log($"🩺 [치유소] {message}");
        return true;
    }

    private int GetTraitRemoveCost(RankType rank) => EconomyRules.TraumaCureCost(economy, rank);

    // ─── 동시 파견: 전투를 보지 않고 결과만 산출 ────────────────────────

    // 새 파견을 시작하기 전에 이전 결산 기록을 비움
    void ResetDispatchResult()
    {
        lastEarnedGold = 0;
        lastEarnedReputation = 0;
        lastEarnedLoot.Clear();
        levelUpLog.Clear();
        lastDispatchReport.Clear();
        lastResultIsMultiDispatch = false;
    }

    // 동시 파견에 보낼 수 있는 파티인가 (여관 방 배정 + 멤버 있음 + 대기 중)
    public bool CanDispatch(Party party)
    {
        return party != null && party.roomIndex >= 0 && party.members.Count > 0 && party.state == PartyState.Idle;
    }

    // 여러 파티를 각각의 의뢰에 한꺼번에 보내고 결과만 계산 (parties[i]가 quests[i]를 수행)
    // 결과는 lastDispatchReport 등에 담기고, 로비 결과창이 뜨도록 예약됨
    public void RunMultiDispatch(List<Party> parties, List<QuestData> quests)
    {
        ResetDispatchResult();

        bool anyWin = false;
        for (int i = 0; i < parties.Count && i < quests.Count; i++)
            anyWin |= AutoResolveQuest(parties[i], quests[i]);

        lastBattleWon = anyWin;
        lastResultIsMultiDispatch = true;
        hasPendingResult = true;
    }

    // 파티 전투력과 의뢰 등급으로 계산한 예상 승률 (공식과 수치는 AutoResolveRules / autoResolve 설정)
    // 직접 지휘 전투와 같은 불이익을 반영: 요양 중인 멤버는 남은 체력 비율만큼, 사기가 떨어진 멤버는 그만큼 덜 기여
    public float GetAutoResolveWinChance(Party party, QuestData quest)
    {
        float partyPower = 0f;
        foreach (Adventurer adv in party.members)
        {
            float hpRatio = (adv.recoveryWeeks > 0 && adv.hp > 0) ? (float)adv.currentHp / adv.hp : 1f;
            float moraleMultiplier = MoraleRules.Multiplier(adv.morale);
            float effectiveHp = (adv.hp + EquipHp(adv)) * DefenseRules.EffectiveHpMultiplier(adv.def + EquipDef(adv), defense.armorConstant); // 방어력만큼 더 버팀 (장비 포함)
            partyPower += AutoResolveRules.GetMemberPower(autoResolve, adv.atk + EquipAtk(adv), effectiveHp, hpRatio, moraleMultiplier);
        }

        partyPower *= GetChemistryStatMultiplier(party); // 케미 보너스 (HP·공격력이 같은 비율로 오름)

        EnemyScalingRules.TryParseRank(quest.rank, out RankType rank); // 알 수 없는 랭크는 D로 계산
        return AutoResolveRules.GetWinChance(autoResolve, partyPower, rank);
    }

    // 한 파티의 의뢰 결과를 전투 없이 산출. 승리 여부를 반환하고, 요약을 lastDispatchReport에 추가
    public bool AutoResolveQuest(Party party, QuestData quest)
    {
        if (party == null || quest == null) return false;
        RecordQuestOrigin(party, quest);

        float winChance = GetAutoResolveWinChance(party, quest);
        bool won = Random.value < winChance;

        Debug.Log($"⚡ [Auto-Resolve] '{party.partyName}' vs '{quest.questName}' → 승리 확률: {winChance * 100f:F0}% → {(won ? "승리" : "패배")}");

        string header = $"{party.partyName} - {quest.questName}"; // 의뢰 이름에 등급 표시([D] 등)가 이미 들어 있음
        List<string> injuredNames  = new List<string>();
        List<string> deadNames     = new List<string>();
        List<string> strandedNames = new List<string>();

        NoteQuestResult(won);
        if (won)
        {
            // 직접 지휘보다 적게 받음 (autoResolve.rewardMultiplier). 전리품·방 골드는 원래 없음
            int baseReward = Mathf.RoundToInt(quest.rewardGold * autoResolve.rewardMultiplier);
            int rewardRep = EconomyRules.SuccessReputation(economy, baseReward);
            int rewardGold = Mathf.RoundToInt(baseReward * GetGoldGainMultiplier(party.members)); // 덜렁거리는 성격은 골드 감소
            AddQuestRewards(rewardGold, rewardRep, true);
            lastDispatchReport.Add($"<color=#7CFC7C>{header}: 성공</color>  (+{rewardGold:N0} G, 명성 +{rewardRep})");

            // 승리해도 부상 발생 가능
            foreach (Adventurer adv in party.members)
            {
                if (Random.value < autoResolve.winInjuryChance)
                {
                    adv.recoveryWeeks = minorInjuryWeeks;
                    adv.currentHp = Mathf.RoundToInt(adv.hp * Random.Range(0.5f, injuryHpThreshold)); // 부상 기준 이하로
                    injuredNames.Add(adv.name);
                }
            }

            // 생존 멤버 경험치 지급
            GainExpAfterBattle(party.members, quest.rank, autoResolve.expMultiplier);
            ResolvePartyChemistry(party, true, null);
        }
        else
        {
            // 패배 시: 부상/사망/낙오 처리
            List<Adventurer> deadInAuto = new List<Adventurer>();
            List<Adventurer> strandedInAuto = new List<Adventurer>();

            float deathCut  = autoResolve.failDeathChance;
            float strandCut = deathCut + autoResolve.failStrandChance;
            float injuryCut = strandCut + autoResolve.failInjuryChance;
            foreach (Adventurer adv in party.members)
            {
                float roll = Random.value;

                if (roll < deathCut) // 사망
                {
                    adv.isDead = true;
                    adv.currentHp = 0;
                    adv.diedAtQuestName = quest.questName;
                    NoteFallen(adv);
                    deadInAuto.Add(adv);
                    deadNames.Add(adv.name);
                    Debug.Log($"💀 [Auto-Resolve 전사] {adv.name}");
                }
                else if (roll < strandCut) // 낙오
                {
                    adv.isStranded = true;
                    adv.strandedWeeksRemaining = 4;
                    adv.currentHp = Mathf.RoundToInt(adv.hp * Random.Range(0.1f, 0.3f));
                    adv.assignedPartyIndex = -1;
                    adv.diedAtQuestName = quest.questName; // 구출 기한이 지나면 전사자가 됨 (유해 수습 의뢰에 사용)
                    strandedInAuto.Add(adv);
                    strandedNames.Add(adv.name);
                    Debug.Log($"😨 [Auto-Resolve 낙오] {adv.name} (구출 기한: 4주)");
                    TryGiveAffliction(adv, AfflictionCause.Stranded);
                }
                else if (roll < injuryCut) // 부상
                {
                    float hpRatio = Random.Range(0.2f, 0.7f);
                    adv.currentHp = Mathf.RoundToInt(adv.hp * hpRatio);
                    adv.recoveryWeeks = hpRatio <= 0.3f ? severeInjuryWeeks : minorInjuryWeeks;
                    injuredNames.Add(adv.name);

                    // 중상·빈사면 트라우마·결함 판정
                    if (hpRatio <= moraleSettings.nearDeathRatio) TryGiveAffliction(adv, AfflictionCause.NearDeath);
                    else if (hpRatio <= 0.3f) TryGiveAffliction(adv, AfflictionCause.Severe);
                }
                else // 무사
                {
                    float penalty = Random.Range(0.05f, 0.15f);
                    adv.morale = MoraleRules.Clamp(moraleSettings, adv.morale - penalty * GetMoraleSwing(adv)); // 지금 사기에서 뺌
                }
            }

            // 케미: 사망자를 빼기 전에 남은 파티원의 우울함 판정
            ResolvePartyChemistry(party, false, deadInAuto);

            // 전사자 처리
            foreach (Adventurer dead in deadInAuto)
            {
                party.members.Remove(dead);
                adventurers.Remove(dead);
                if (!deadAdventurers.Contains(dead))
                    deadAdventurers.Add(dead);
            }

            // 낙오자는 파티에서만 제거
            foreach (Adventurer stranded in strandedInAuto)
            {
                party.members.Remove(stranded);
            }

            int failRep = economy.autoDispatchFailReputation;
            AddQuestRewards(0, failRep, false);
            lastDispatchReport.Add($"<color=#FF7366>{header}: 실패</color>  (명성 {failRep:+0;-0;0})");
        }

        // 인명 피해 요약 (있을 때만)
        if (deadNames.Count > 0)     lastDispatchReport.Add($"    전사: {string.Join(", ", deadNames)}");
        if (strandedNames.Count > 0) lastDispatchReport.Add($"    낙오: {string.Join(", ", strandedNames)}");
        if (injuredNames.Count > 0)  lastDispatchReport.Add($"    부상: {string.Join(", ", injuredNames)}");

        party.state = PartyState.Idle;
        return won;
    }

}

