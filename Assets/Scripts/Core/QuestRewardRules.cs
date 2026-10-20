using System;

// 의뢰비·추천 레벨 (QuestGenerator 인스펙터에서 조정). 랭크별 배열은 D, C, B, A, S 순서
[Serializable]
public class QuestRewardSettings
{
    [UnityEngine.Header("의뢰비 범위 (최소~최대, 둘 다 포함)")]
    public int[] rewardMinPerRank = { 100, 200, 400, 700, 1200 };
    public int[] rewardMaxPerRank = { 200, 400, 700, 1200, 2000 };

    [UnityEngine.Tooltip("의뢰 랭크별 추천 레벨 (표시용)")]
    public int[] recommendedLevelPerRank = { 1, 2, 4, 6, 8 };

    [UnityEngine.Header("특수 의뢰 배율")]
    [UnityEngine.Tooltip("낙오자 구출 의뢰비 = 기본 의뢰비 × 이 값")]
    public float rescueRewardMultiplier = 1.5f;
    [UnityEngine.Tooltip("유해 수습 의뢰비 = 기본 의뢰비 × 이 값 (내림)")]
    public float corpseRewardMultiplier = 0.5f;
}

// 의뢰비 계산 (순수 로직). 무작위 값은 호출하는 쪽에서 범위를 받아 뽑음
public static class QuestRewardRules
{
    public static int RewardMin(QuestRewardSettings s, RankType rank) => EconomyRules.ByRank((s ?? new QuestRewardSettings()).rewardMinPerRank, rank, 100);

    public static int RewardMax(QuestRewardSettings s, RankType rank) => Math.Max(RewardMin(s, rank), EconomyRules.ByRank((s ?? new QuestRewardSettings()).rewardMaxPerRank, rank, 100));

    // 범위의 평균 (재료 수급 의뢰비의 기준)
    public static int AverageReward(QuestRewardSettings s, RankType rank) => (RewardMin(s, rank) + RewardMax(s, rank)) / 2;

    public static int RecommendedLevel(QuestRewardSettings s, RankType rank) => EconomyRules.ByRank((s ?? new QuestRewardSettings()).recommendedLevelPerRank, rank, 1);

    public static int RescueReward(QuestRewardSettings s, int baseReward)
    {
        s = s ?? new QuestRewardSettings();
        return (int)Math.Round(baseReward * s.rescueRewardMultiplier, MidpointRounding.ToEven); // Mathf.RoundToInt와 같은 반올림
    }

    public static int CorpseReward(QuestRewardSettings s, int baseReward)
    {
        s = s ?? new QuestRewardSettings();
        return (int)Math.Floor(baseReward * s.corpseRewardMultiplier + 1e-4);
    }
}
