using System;

// 직업별 기본 스탯·성장, 랭크별 시작 레벨·잠재력 (GameManager 인스펙터에서 조정). 랭크별 배열은 D, C, B, A, S 순서
[Serializable]
public class JobStatSettings
{
    [UnityEngine.Header("Lv.1 기본 HP / 공격력 / 이동 속도 (D랭크 기준)")]
    public float warriorHp = 150f; public float warriorAtk = 12f; public float warriorSpeed = 2.0f;
    public float archerHp  = 80f;  public float archerAtk  = 18f; public float archerSpeed  = 2.5f;
    public float mageHp    = 70f;  public float mageAtk    = 25f; public float mageSpeed    = 2.0f;
    public float rogueHp   = 60f;  public float rogueAtk   = 20f; public float rogueSpeed   = 3.0f;
    public float healerHp  = 90f;  public float healerAtk  = 8f;  public float healerSpeed  = 2.2f;

    [UnityEngine.Header("레벨당 HP / 공격력 상승 (D랭크 기준)")]
    public float warriorHpPerLevel = 15f; public float warriorAtkPerLevel = 3f;
    public float archerHpPerLevel  = 8f;  public float archerAtkPerLevel  = 4f;
    public float mageHpPerLevel    = 7f;  public float mageAtkPerLevel    = 5f;
    public float rogueHpPerLevel   = 6f;  public float rogueAtkPerLevel   = 4f;
    public float healerHpPerLevel  = 9f;  public float healerAtkPerLevel  = 2f;

    [UnityEngine.Tooltip("기본 HP·공격력에 곱하는 랭크 배율 = 1 + 랭크 × 이 값")]
    public float rankBaseStep = 0.5f;
    [UnityEngine.Tooltip("레벨당 상승에 곱하는 랭크 배율 = 1 + 랭크 × 이 값")]
    public float rankGrowthStep = 0.2f;
    [UnityEngine.Tooltip("기본 HP·공격력에 곱하는 무작위 범위 (최소~최대)")]
    public float baseRandomMin = 0.9f;
    public float baseRandomMax = 1.1f;

    [UnityEngine.Header("랭크별 시작 레벨 (최소~최대, 둘 다 포함)")]
    public int[] startLevelMinPerRank = { 1, 1, 2, 4, 6 };
    public int[] startLevelMaxPerRank = { 1, 1, 3, 5, 8 };

    [UnityEngine.Header("랭크별 잠재력 (최소~최대, 둘 다 포함, 1~5)")]
    public int[] potentialMinPerRank = { 1, 1, 2, 3, 4 };
    public int[] potentialMaxPerRank = { 2, 3, 4, 5, 5 };

    [UnityEngine.Tooltip("랭크별 긍정 특성을 하나 갖고 시작할 확률 (0~1)")]
    public float[] startingTraitChancePerRank = { 0f, 0f, 0.3f, 0.6f, 1f };
}

// 직업 기본 스탯과 레벨당 성장 (순수 로직). GameManager.LevelUp과 AdventurerGenerator(시작 레벨까지)가 함께 씀
public static class StatGrowthRules
{
    public struct BaseStats { public float hp, atk, speed; }

    public static BaseStats Base(JobStatSettings s, JobType job)
    {
        s = s ?? new JobStatSettings();
        switch (job)
        {
            case JobType.Warrior: return new BaseStats { hp = s.warriorHp, atk = s.warriorAtk, speed = s.warriorSpeed };
            case JobType.Archer:  return new BaseStats { hp = s.archerHp,  atk = s.archerAtk,  speed = s.archerSpeed };
            case JobType.Mage:    return new BaseStats { hp = s.mageHp,    atk = s.mageAtk,    speed = s.mageSpeed };
            case JobType.Rogue:   return new BaseStats { hp = s.rogueHp,   atk = s.rogueAtk,   speed = s.rogueSpeed };
            case JobType.Healer:  return new BaseStats { hp = s.healerHp,  atk = s.healerAtk,  speed = s.healerSpeed };
            default:              return new BaseStats { hp = 100f, atk = 10f, speed = 2.0f };
        }
    }

    // 기본 HP·공격력 랭크 배율
    public static float RankBaseFactor(JobStatSettings s, RankType rank) => 1f + (int)rank * (s ?? new JobStatSettings()).rankBaseStep;

    // 레벨당 HP 상승 (소수 포함) = 직업 기본값 × (1 + 랭크 × rankGrowthStep) × 잠재력 배율
    public static float HpPerLevel(JobStatSettings s, JobType job, RankType rank, float growth)
    {
        s = s ?? new JobStatSettings();
        float baseGain;
        switch (job)
        {
            case JobType.Warrior: baseGain = s.warriorHpPerLevel; break;
            case JobType.Healer:  baseGain = s.healerHpPerLevel;  break;
            case JobType.Archer:  baseGain = s.archerHpPerLevel;  break;
            case JobType.Mage:    baseGain = s.mageHpPerLevel;    break;
            case JobType.Rogue:   baseGain = s.rogueHpPerLevel;   break;
            default:              baseGain = 10f; break;
        }
        return baseGain * RankFactor(s, rank) * growth;
    }

    // 레벨당 공격력 상승 (소수 포함)
    public static float AtkPerLevel(JobStatSettings s, JobType job, RankType rank, float growth)
    {
        s = s ?? new JobStatSettings();
        float baseGain;
        switch (job)
        {
            case JobType.Mage:    baseGain = s.mageAtkPerLevel;    break;
            case JobType.Archer:  baseGain = s.archerAtkPerLevel;  break;
            case JobType.Rogue:   baseGain = s.rogueAtkPerLevel;   break;
            case JobType.Warrior: baseGain = s.warriorAtkPerLevel; break;
            case JobType.Healer:  baseGain = s.healerAtkPerLevel;  break;
            default:              baseGain = 3f; break;
        }
        return baseGain * RankFactor(s, rank) * growth;
    }

    public static int StartLevelMin(JobStatSettings s, RankType rank) => EconomyRules.ByRank((s ?? new JobStatSettings()).startLevelMinPerRank, rank, 1);
    public static int StartLevelMax(JobStatSettings s, RankType rank) => Math.Max(StartLevelMin(s, rank), EconomyRules.ByRank((s ?? new JobStatSettings()).startLevelMaxPerRank, rank, 1));
    public static int PotentialMin(JobStatSettings s, RankType rank) => EconomyRules.ByRank((s ?? new JobStatSettings()).potentialMinPerRank, rank, 1);
    public static int PotentialMax(JobStatSettings s, RankType rank) => Math.Max(PotentialMin(s, rank), EconomyRules.ByRank((s ?? new JobStatSettings()).potentialMaxPerRank, rank, 1));

    public static float StartingTraitChance(JobStatSettings s, RankType rank)
    {
        float[] t = (s ?? new JobStatSettings()).startingTraitChancePerRank;
        if (t == null || t.Length == 0) return 0f;
        return t[Math.Max(0, Math.Min((int)rank, t.Length - 1))];
    }

    // 소수 이월: 이번 상승량(소수) + 지난번에 남은 소수를 반올림해 정수로 주고, 남은 소수는 carry에 남김
    // → 레벨마다 받는 값은 반올림과 거의 같고, 누적 합은 정확한 값을 따라감 (예: 2.2씩 10번 = 22)
    public static int TakeWithCarry(float raw, ref float carry)
    {
        float total = raw + carry;
        int gain = (int)Math.Round(total, MidpointRounding.AwayFromZero);
        carry = total - gain;
        return Math.Max(0, gain);
    }

    static float RankFactor(JobStatSettings s, RankType rank) => 1f + (int)rank * s.rankGrowthStep;
}
