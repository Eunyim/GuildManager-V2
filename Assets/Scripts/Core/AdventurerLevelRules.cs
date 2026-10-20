using System;

// 모험가 랭크별 최대 레벨 (GameManager 인스펙터에서 조정)
[Serializable]
public class AdventurerLevelSettings
{
    // D, C, B, A, S 순서
    public int[] maxLevelPerRank = { 10, 15, 20, 25, 30 };
}

// 모험가 레벨 상한 규칙 (순수 로직)
public static class AdventurerLevelRules
{
    // 표가 비어 있으면 상한 없음, 랭크가 표 길이를 넘으면 마지막 값을 사용
    public static int GetMaxLevel(AdventurerLevelSettings settings, RankType rank)
    {
        int[] table = settings != null ? settings.maxLevelPerRank : null;
        if (table == null || table.Length == 0) return int.MaxValue;

        int index = Math.Min(Math.Max((int)rank, 0), table.Length - 1);
        return Math.Max(table[index], 1);
    }

    public static bool IsMaxLevel(AdventurerLevelSettings settings, RankType rank, int level)
    {
        return level >= GetMaxLevel(settings, rank);
    }
}
