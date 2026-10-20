using System;

// 명성 레벨 수치 (GameManager 인스펙터에서 조정)
[Serializable]
public class ReputationLevelSettings
{
    // 레벨마다 필요한 최소 명성 (Lv.1, Lv.2, Lv.3, Lv.4 순서)
    public int[] minReputationPerLevel = { 0, 100, 500, 1000 };
}

// 명성 레벨 규칙 (순수 로직)
public static class ReputationRules
{
    // 명성으로 레벨(1부터)을 구함. 표가 비어 있으면 1
    public static int GetLevel(ReputationLevelSettings settings, int reputation)
    {
        int[] table = settings != null ? settings.minReputationPerLevel : null;
        if (table == null || table.Length == 0) return 1;
        int level = 1;
        for (int i = 0; i < table.Length; i++)
            if (reputation >= table[i]) level = i + 1;
        return level;
    }

    // 이 레벨에 필요한 최소 명성 (표 밖이면 마지막 값)
    public static int GetRequiredReputation(ReputationLevelSettings settings, int level)
    {
        int[] table = settings != null ? settings.minReputationPerLevel : null;
        if (table == null || table.Length == 0) return 0;
        return table[Math.Min(Math.Max(level - 1, 0), table.Length - 1)];
    }
}
