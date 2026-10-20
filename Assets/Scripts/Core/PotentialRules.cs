using System;

// 잠재력 (GameManager 인스펙터에서 조정)
[Serializable]
public class PotentialSettings
{
    [UnityEngine.Tooltip("레벨당 HP·공격력·방어력 상승량에 곱하는 배율 (잠재력 1~5 순서). 잠재력 1 = 지금과 같음")]
    public float[] growthMultiplierPerPotential = { 1.0f, 1.1f, 1.2f, 1.3f, 1.4f };
}

// 잠재력 규칙 (순수 로직)
public static class PotentialRules
{
    // 잠재력에 따른 성장 배율 (표가 비면 1, 범위 밖이면 가장 가까운 값)
    public static float GrowthMultiplier(PotentialSettings s, int potential)
    {
        float[] table = s != null ? s.growthMultiplierPerPotential : null;
        if (table == null || table.Length == 0) return 1f;
        int index = Math.Min(Math.Max(potential - 1, 0), table.Length - 1);
        return Math.Max(table[index], 0f);
    }
}
