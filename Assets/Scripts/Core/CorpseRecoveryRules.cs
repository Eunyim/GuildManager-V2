using System;

// 유해 수습 보상 수치 (GameManager 인스펙터에서 조정)
[Serializable]
public class CorpseRecoverySettings
{
    // 전사자 랭크별 영혼석 획득 확률 (D, C, B, A, S 순서). 랭크가 높을수록 되살리기 어려움
    public float[] spiritStoneChancePerRank = { 0.5f, 0.4f, 0.3f, 0.2f, 0.1f };

    // 영혼석을 얻지 못했을 때 받는 골드: minGold ~ 랭크별 최대치 사이에서 무작위 (D, C, B, A, S 순서)
    public int minGold = 1;
    public int[] maxGoldPerRank = { 50, 100, 150, 200, 300 };
}

// 유해 수습 보상 규칙 (순수 로직)
public static class CorpseRecoveryRules
{
    public static float GetSpiritStoneChance(CorpseRecoverySettings settings, RankType rank)
    {
        if (settings == null || settings.spiritStoneChancePerRank == null || settings.spiritStoneChancePerRank.Length == 0)
            return 0f;

        float[] table = settings.spiritStoneChancePerRank;
        float chance = table[ClampIndex((int)rank, table.Length)];
        return Math.Min(Math.Max(chance, 0f), 1f);
    }

    // roll01: 0 이상 1 미만의 난수
    public static bool RollSpiritStone(CorpseRecoverySettings settings, RankType rank, float roll01)
    {
        return roll01 < GetSpiritStoneChance(settings, rank);
    }

    public static int GetMaxGold(CorpseRecoverySettings settings, RankType rank)
    {
        if (settings == null || settings.maxGoldPerRank == null || settings.maxGoldPerRank.Length == 0) return 0;

        int[] table = settings.maxGoldPerRank;
        return Math.Max(table[ClampIndex((int)rank, table.Length)], 0);
    }

    // 영혼석 대신 받는 골드. roll01: 0 이상 1 미만의 난수 (최솟값 ~ 최댓값을 고르게 뽑음)
    public static int RollGold(CorpseRecoverySettings settings, RankType rank, float roll01)
    {
        int max = GetMaxGold(settings, rank);
        int min = Math.Min(Math.Max(settings != null ? settings.minGold : 0, 0), max);

        int span = max - min + 1;
        int offset = Math.Min((int)(Math.Max(roll01, 0f) * span), span - 1);
        return min + offset;
    }

    // 표 길이를 벗어나는 랭크는 가장 가까운 칸의 값을 사용
    static int ClampIndex(int index, int length)
    {
        return Math.Min(Math.Max(index, 0), length - 1);
    }
}
