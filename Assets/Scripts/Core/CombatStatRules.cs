using System;

// 모험가 기본 치명타·회피 (GameManager 인스펙터에서 조정). 레벨업으로 오르지 않고 직업으로만 정해짐
[Serializable]
public class CombatStatSettings
{
    [UnityEngine.Range(0f, 1f)] public float baseCritChance = 0.1f; // 기본 공격 치명타 확률
    [UnityEngine.Range(0f, 1f)] public float baseEvasion    = 0.1f; // 회피율 (회피하면 그 공격의 피해가 없음)
    [UnityEngine.Range(0f, 1f)] public float rogueCritChance = 0.3f;
    [UnityEngine.Range(0f, 1f)] public float rogueEvasion    = 0.3f;
    [UnityEngine.Tooltip("치명타 피해 배율 (도적은 도적 프리팹의 critMultiplier를 씀)")]
    public float critMultiplier = 1.5f;
}

// 직업별 기본 치명타·회피 (순수 로직)
public static class CombatStatRules
{
    public static float CritChance(CombatStatSettings s, JobType job) =>
        s == null ? 0f : job == JobType.Rogue ? s.rogueCritChance : s.baseCritChance;

    public static float Evasion(CombatStatSettings s, JobType job) =>
        s == null ? 0f : job == JobType.Rogue ? s.rogueEvasion : s.baseEvasion;
}
