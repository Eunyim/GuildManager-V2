using UnityEngine;

public static class AdventurerGenerator
{
    // ─── 이름 생성 ────────────────────────────────────────────────────

    private static readonly string[] NameFront = {
        "아", "카", "레", "미", "세", "리", "나", "에", "소", "타",
        "가", "로", "엘", "투", "라", "케", "발", "시", "오", "드"
    };
    private static readonly string[] NameMid = {
        "르", "미", "나", "레", "아", "에", "리", "드", "로", "스"
    };
    private static readonly string[] NameBack = {
        "린", "론", "셀", "엔", "드", "나", "온", "딘", "크", "벨",
        "인", "안", "스", "르", "브"
    };

    private static string GenerateUniqueName()
    {
        for (int i = 0; i < 10; i++)
        {
            string candidate = GenerateName();
            if (!IsNameTaken(candidate)) return candidate;
        }

        // 10번 시도 후에도 중복이면 숫자 접미사를 붙여 고유성 보장
        string baseName = GenerateName();
        int suffix = 2;
        while (IsNameTaken(baseName + suffix))
            suffix++;
        return baseName + suffix;
    }

    // 소속 모험가뿐 아니라 접수처에서 대기 중인 방문 모험가와도 겹치지 않게 함
    private static bool IsNameTaken(string candidate)
    {
        if (GameManager.Instance == null) return false;
        return GameManager.Instance.adventurers.Exists(a => a.name == candidate)
            || GameManager.Instance.visitors.Exists(a => a.name == candidate);
    }

    private static string GenerateName()
    {
        string front = NameFront[Random.Range(0, NameFront.Length)];
        string back  = NameBack[Random.Range(0, NameBack.Length)];

        if (Random.value < 0.3f)
        {
            string mid = NameMid[Random.Range(0, NameMid.Length)];
            return front + mid + back;
        }
        return front + back;
    }

    // ─── 메인 생성 함수 ───────────────────────────────────────────────

    // fixedJob을 주면 직업을 그대로 쓰고, 생략하면 무작위로 정함
    public static Adventurer Generate(RankType rank, JobType? fixedJob = null)
    {
        Adventurer adv = new Adventurer();

        adv.name  = GenerateUniqueName();
        adv.job   = fixedJob ?? (JobType)Random.Range(0, System.Enum.GetValues(typeof(JobType)).Length);
        adv.trait = (TraitType)Random.Range(0, System.Enum.GetValues(typeof(TraitType)).Length);
        adv.rank  = rank;
        adv.look  = AdventurerSprites.RandomLook(adv.job); // 성별·체형·헤어·옷·무기·색을 무작위로 조립

        // 랭크에 따른 시작 레벨 결정
        adv.level = DetermineStartingLevel(adv.rank);

        // 잠재력 생성 (랭크가 높을수록 높은 잠재력 확률). 시작 레벨까지의 성장량에도 반영되므로 먼저 정함
        adv.potential = DeterminePotential(adv.rank);

        // Lv.1 기준 스탯 계산
        ApplyBaseStats(adv);

        // Lv.2 이상이면 레벨업 스탯 추가 적용
        for (int lv = 2; lv <= adv.level; lv++)
            ApplyLevelUpStats(adv);

        // 경험치 초기화 (현재 레벨 기준)
        adv.exp           = 0;
        adv.expToNextLevel = EconomyRules.ExpToNextLevel(GameManager.Instance != null ? GameManager.Instance.economy : null, adv.level);

        // 체력 완전 회복
        adv.currentHp = adv.hp;

        // 고랭크 모험가는 시작 특성 보유 가능
        TryGrantStartingTrait(adv);

        return adv;
    }

    // ─── 시작 레벨 (랭크별) ──────────────────────────────────────────

    private static int DetermineStartingLevel(RankType rank)
    {
        // 기본: D·C 1, B 2~3, A 4~5, S 6~8 (GameManager.jobStats)
        return RollInclusive(StatGrowthRules.StartLevelMin(JobStats, rank), StatGrowthRules.StartLevelMax(JobStats, rank));
    }

    // min~max 정수 (둘 다 포함). 범위가 한 값이면 무작위를 뽑지 않음
    static int RollInclusive(int min, int max) => min >= max ? min : Random.Range(min, max + 1);

    // ─── Lv.1 기준 스탯 ──────────────────────────────────────────────

    private static void ApplyBaseStats(Adventurer adv)
    {
        JobStatSettings s = JobStats ?? new JobStatSettings();
        StatGrowthRules.BaseStats b = StatGrowthRules.Base(s, adv.job);

        float rankMult  = StatGrowthRules.RankBaseFactor(s, adv.rank);
        float randomVar = Random.Range(s.baseRandomMin, s.baseRandomMax);

        adv.hp    = Mathf.RoundToInt(b.hp  * rankMult * randomVar);
        adv.atk   = Mathf.RoundToInt(b.atk * rankMult * randomVar);
        adv.def   = DefenseRules.GetBase(Defense, adv.job, adv.rank, randomVar);
        adv.speed = b.speed;
    }

    // ─── 레벨당 스탯 성장 (GameManager.LevelUp과 동일 공식) ──────────

    private static void ApplyLevelUpStats(Adventurer adv)
    {
        float growth = PotentialRules.GrowthMultiplier(GameManager.Instance != null ? GameManager.Instance.potentialSettings : null, adv.potential);
        adv.hp  += StatGrowthRules.TakeWithCarry(StatGrowthRules.HpPerLevel(JobStats, adv.job, adv.rank, growth), ref adv.hpGrowthCarry);
        adv.atk += StatGrowthRules.TakeWithCarry(StatGrowthRules.AtkPerLevel(JobStats, adv.job, adv.rank, growth), ref adv.atkGrowthCarry);
        adv.def += StatGrowthRules.TakeWithCarry(DefenseRules.GetLevelGainRaw(Defense, adv.job, adv.rank, growth), ref adv.defGrowthCarry);
    }

    static DefenseSettings Defense => GameManager.Instance != null ? GameManager.Instance.defense : null;
    static JobStatSettings JobStats => GameManager.Instance != null ? GameManager.Instance.jobStats : null;

    // ─── 잠재력 결정 ─────────────────────────────────────────────────

    private static int DeterminePotential(RankType rank)
    {
        // 기본 범위: D=1~2, C=1~3, B=2~4, A=3~5, S=4~5 (GameManager.jobStats)
        return RollInclusive(StatGrowthRules.PotentialMin(JobStats, rank), StatGrowthRules.PotentialMax(JobStats, rank));
    }

    // ─── 고랭크 시작 특성 ─────────────────────────────────────────────

    // 랭크별 긍정 특성 시작 확률: B=30%, A=60%, S=100%
    private static void TryGrantStartingTrait(Adventurer adv)
    {
        if (GameManager.Instance == null) return;
        TryGrantSpecialTraits(adv);
        if (GameManager.Instance.positiveTraitPool == null ||
            GameManager.Instance.positiveTraitPool.Count == 0) return;

        float chance = StatGrowthRules.StartingTraitChance(JobStats, adv.rank);
        if (chance <= 0f) return; // D, C는 시작 특성 없음

        if (Random.value < chance)
        {
            var pool = GameManager.Instance.positiveTraitPool;
            TraitData trait = pool[Random.Range(0, pool.Count)];
            if (!adv.traits.Contains(trait))
            {
                adv.traits.Add(trait);
                Debug.Log($"✨ [{adv.name}] 시작 특성 '{trait.traitName}' 보유 (Rank {adv.rank})");
            }
        }
    }

    // 풀 밖의 특별 특성: 용사의 기운(A 5% / S 10%), 육감(모든 랭크 10%)
    private static void TryGrantSpecialTraits(Adventurer adv)
    {
        GameManager gm = GameManager.Instance;
        float heroChance = adv.rank == RankType.S ? gm.heroAuraChanceS
                         : adv.rank == RankType.A ? gm.heroAuraChanceA : 0f;
        if (Random.value < heroChance) gm.GrantTrait(adv, gm.heroAuraTrait, $"시작 특성, Rank {adv.rank}");
        if (Random.value < gm.sixthSenseChance) gm.GrantTrait(adv, gm.sixthSenseTrait, "시작 특성");
    }
}
