using System;

// 모험가 방어력 (GameManager 인스펙터에서 조정)
[Serializable]
public class DefenseSettings
{
    [UnityEngine.Tooltip("받는 피해 = 피해 × 이 값 ÷ (이 값 + 방어력). 100이면 방어 50 → 33% 감소, 100 → 50% 감소")]
    public float armorConstant = 100f;

    [UnityEngine.Header("Lv.1 기본 방어력 (D랭크 기준)")]
    public float warriorBase = 15f;
    public float healerBase  = 8f;
    public float archerBase  = 5f;
    public float mageBase    = 5f;
    public float rogueBase   = 5f;

    [UnityEngine.Header("레벨당 방어력 상승 (D랭크 기준)")]
    public float warriorPerLevel = 2f;
    public float healerPerLevel  = 1f;
    public float archerPerLevel  = 1f;
    public float magePerLevel    = 1f;
    public float roguePerLevel   = 1f;

    [UnityEngine.Tooltip("기본값에 곱하는 랭크 배율 = 1 + 랭크 × 이 값 (HP·공격력과 같은 0.5)")]
    public float rankBaseStep = 0.5f;
    [UnityEngine.Tooltip("레벨당 상승에 곱하는 랭크 배율 = 1 + 랭크 × 이 값 (HP·공격력과 같은 0.2)")]
    public float rankGainStep = 0.2f;
}

// 방어력 규칙 (순수 로직)
public static class DefenseRules
{
    // 방어력을 적용한 피해 (방어력 0 이하면 그대로)
    public static float Apply(float damage, float defense, float armorConstant)
    {
        if (damage <= 0f || defense <= 0f || armorConstant <= 0f) return damage;
        return damage * armorConstant / (armorConstant + defense);
    }

    // 방어력만큼 체력이 늘어난 것과 같은 배율 (동시 파견 전투력 계산용)
    public static float EffectiveHpMultiplier(float defense, float armorConstant)
    {
        if (defense <= 0f || armorConstant <= 0f) return 1f;
        return 1f + defense / armorConstant;
    }

    // Lv.1 방어력 (randomVar: HP·공격력과 같은 ±10% 무작위 값)
    public static int GetBase(DefenseSettings s, JobType job, RankType rank, float randomVar)
    {
        s = s ?? new DefenseSettings();
        float value = BaseOf(s, job) * (1f + (int)rank * s.rankBaseStep) * randomVar;
        return Math.Max(0, (int)Math.Round(value, MidpointRounding.AwayFromZero));
    }

    // 레벨이 하나 오를 때 늘어나는 방어력
    public static int GetLevelGain(DefenseSettings s, JobType job, RankType rank, float growthMultiplier = 1f)
    {
        return Math.Max(0, (int)Math.Round(GetLevelGainRaw(s, job, rank, growthMultiplier), MidpointRounding.AwayFromZero));
    }

    // 레벨당 방어력 상승 (소수 포함, 레벨업 때 소수 이월에 씀)
    public static float GetLevelGainRaw(DefenseSettings s, JobType job, RankType rank, float growthMultiplier = 1f)
    {
        s = s ?? new DefenseSettings();
        return PerLevelOf(s, job) * (1f + (int)rank * s.rankGainStep) * growthMultiplier;
    }

    // 방어력이 없던 예전 저장 파일용: 무작위 없이 지금 레벨까지의 방어력
    public static int Recompute(DefenseSettings s, JobType job, RankType rank, int level)
    {
        int def = GetBase(s, job, rank, 1f);
        for (int lv = 2; lv <= level; lv++) def += GetLevelGain(s, job, rank);
        return def;
    }

    static float BaseOf(DefenseSettings s, JobType job)
    {
        switch (job)
        {
            case JobType.Warrior: return s.warriorBase;
            case JobType.Healer:  return s.healerBase;
            case JobType.Archer:  return s.archerBase;
            case JobType.Mage:    return s.mageBase;
            case JobType.Rogue:   return s.rogueBase;
            default:              return s.archerBase;
        }
    }

    static float PerLevelOf(DefenseSettings s, JobType job)
    {
        switch (job)
        {
            case JobType.Warrior: return s.warriorPerLevel;
            case JobType.Healer:  return s.healerPerLevel;
            case JobType.Archer:  return s.archerPerLevel;
            case JobType.Mage:    return s.magePerLevel;
            case JobType.Rogue:   return s.roguePerLevel;
            default:              return s.archerPerLevel;
        }
    }
}
