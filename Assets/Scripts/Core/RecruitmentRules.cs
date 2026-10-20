using System;
using System.Collections.Generic;

// 명성 구간별 방문 모험가 랭크 확률표 (D, C, B, A, S 순서의 가중치)
[Serializable]
public class RecruitRankTier
{
    public int minReputation;                  // 이 명성 이상일 때 적용
    public float[] rankWeights = new float[5]; // D, C, B, A, S
}

// 접수처 고용 밸런스 수치 (GameManager 인스펙터에서 조정)
[Serializable]
public class RecruitmentSettings
{
    // 고용 비용 = baseHireCost + 랭크 단계(D=0 ~ S=4) * hireCostPerRank
    public int baseHireCost    = 100;
    public int hireCostPerRank = 100;

    // 접수처 레벨별 방문 모험가 수 (index 0 = Lv1)
    public int[] visitorsPerReceptionLevel = { 4, 5, 6 };

    // 명성이 높을수록 고랭크 방문 확률 증가 (minReputation 오름차순)
    public List<RecruitRankTier> rankTiers = new List<RecruitRankTier>
    {
        new RecruitRankTier { minReputation = 0,    rankWeights = new float[] { 70f, 25f, 5f,  0f,  0f } },
        new RecruitRankTier { minReputation = 100,  rankWeights = new float[] { 55f, 30f, 12f, 3f,  0f } },
        new RecruitRankTier { minReputation = 300,  rankWeights = new float[] { 40f, 30f, 20f, 9f,  1f } },
        new RecruitRankTier { minReputation = 600,  rankWeights = new float[] { 25f, 30f, 25f, 15f, 5f } },
        new RecruitRankTier { minReputation = 1000, rankWeights = new float[] { 10f, 25f, 35f, 22f, 8f } },
    };
}

// 접수처 고용 규칙 (순수 로직)
public static class RecruitmentRules
{
    public static int GetHireCost(RecruitmentSettings settings, RankType rank)
    {
        return settings.baseHireCost + (int)rank * settings.hireCostPerRank;
    }

    public static int GetVisitorCount(RecruitmentSettings settings, int receptionLevel)
    {
        int[] table = settings.visitorsPerReceptionLevel;
        if (table == null || table.Length == 0) return 0;

        int index = Math.Min(Math.Max(receptionLevel - 1, 0), table.Length - 1);
        return Math.Max(table[index], 0);
    }

    // 현재 명성에 해당하는 확률표 (조건을 만족하는 구간 중 minReputation이 가장 높은 것)
    public static float[] GetRankWeights(RecruitmentSettings settings, int reputation)
    {
        if (settings.rankTiers == null) return null;

        RecruitRankTier best   = null; // 명성 조건을 만족하는 가장 높은 구간
        RecruitRankTier lowest = null; // 명성이 음수 등으로 어떤 구간에도 못 미칠 때 쓰는 최저 구간
        foreach (RecruitRankTier tier in settings.rankTiers)
        {
            if (tier == null || tier.rankWeights == null) continue;

            if (lowest == null || tier.minReputation < lowest.minReputation) lowest = tier;
            if (reputation >= tier.minReputation && (best == null || tier.minReputation > best.minReputation))
                best = tier;
        }

        if (best == null) best = lowest;
        return best != null ? best.rankWeights : null;
    }

    // roll01: 0~1 사이의 난수
    public static RankType RollRank(float[] weights, float roll01)
    {
        if (weights == null) return RankType.D;

        int rankCount = Enum.GetValues(typeof(RankType)).Length;
        int count = Math.Min(weights.Length, rankCount);

        float total = 0f;
        for (int i = 0; i < count; i++) total += Math.Max(weights[i], 0f);
        if (total <= 0f) return RankType.D;

        float roll = roll01 * total;
        float sum = 0f;
        RankType last = RankType.D; // roll01이 정확히 1일 때는 확률이 있는 마지막 랭크
        for (int i = 0; i < count; i++)
        {
            float w = Math.Max(weights[i], 0f);
            if (w <= 0f) continue;
            sum += w;
            last = (RankType)i;
            if (roll < sum) return last;
        }
        return last;
    }
}
