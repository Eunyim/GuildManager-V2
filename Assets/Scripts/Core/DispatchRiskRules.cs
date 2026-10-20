using System;

// 파견 경고 기준 (GameManager 인스펙터에서 조정)
[Serializable]
public class DispatchRiskSettings
{
    // 파티 평균 레벨이 권장 레벨 이상일 때, 의뢰보다 몇 랭크 낮은 파티까지 경고 없이 보낼 수 있는가
    public float allowedRankGapAtRecommendedLevel = 1f;
}

// 의뢰에 비해 파티가 약한지 판정 (순수 로직). 경고만 하고 파견을 막지는 않음
public static class DispatchRiskRules
{
    // averageRank: 파티원 랭크(D=0 ~ S=4)의 평균
    // 안전: 파티 평균 랭크가 의뢰 랭크 이상이거나, 평균 레벨이 권장 레벨 이상이면서 랭크 차이가 허용 범위 안
    public static bool IsRisky(DispatchRiskSettings settings, float averageLevel, float averageRank, int recommendedLevel, RankType questRank)
    {
        float rankGap = (int)questRank - averageRank;
        if (rankGap <= 0f) return false;

        float allowedGap = settings != null ? settings.allowedRankGapAtRecommendedLevel : 1f;
        return averageLevel < recommendedLevel || rankGap > allowedGap;
    }
}
