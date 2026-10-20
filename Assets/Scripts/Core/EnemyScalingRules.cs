using System;

// 의뢰 랭크별 적 능력치 배율 (GameManager 인스펙터에서 조정)
[Serializable]
public class EnemyScalingSettings
{
    // 적 프리팹의 기본 능력치에 곱하는 값 (D, C, B, A, S 순서)
    public float[] hpMultiplierPerRank     = { 1f, 1.2f,  1.4f, 1.7f, 2.5f };
    public float[] attackMultiplierPerRank = { 1f, 1.15f, 1.3f, 1.5f, 2.0f };
}

// 의뢰 랭크에 따른 적 능력치 규칙 (순수 로직)
public static class EnemyScalingRules
{
    public static float GetHpMultiplier(EnemyScalingSettings settings, RankType rank)
    {
        return settings != null ? GetMultiplier(settings.hpMultiplierPerRank, rank) : 1f;
    }

    public static float GetAttackMultiplier(EnemyScalingSettings settings, RankType rank)
    {
        return settings != null ? GetMultiplier(settings.attackMultiplierPerRank, rank) : 1f;
    }

    // QuestData.rank는 문자열("D" ~ "S")이라 변환이 필요함. 알 수 없는 값이면 false
    public static bool TryParseRank(string rankText, out RankType rank)
    {
        rank = RankType.D;
        if (string.IsNullOrEmpty(rankText)) return false;
        return Enum.TryParse(rankText.Trim(), true, out rank) && Enum.IsDefined(typeof(RankType), rank);
    }

    // 표가 비어 있으면 1배, 랭크가 표 길이를 넘으면 마지막 값을 사용
    static float GetMultiplier(float[] table, RankType rank)
    {
        if (table == null || table.Length == 0) return 1f;

        int index = Math.Min(Math.Max((int)rank, 0), table.Length - 1);
        return Math.Max(table[index], 0f);
    }
}
