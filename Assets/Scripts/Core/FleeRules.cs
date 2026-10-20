using System;
using System.Collections.Generic;

// 도주 시 남은 체력 구간별 낙오 확률
[Serializable]
public class StrandTier
{
    public float maxHpRatio;   // 남은 체력 비율이 이 값 이하일 때 적용 (0~1)
    public float strandChance; // 낙오 확률 (0~1)
}

// 도주 밸런스 수치 (GameManager 인스펙터에서 조정)
[Serializable]
public class FleeSettings
{
    // 빈사(10% 이하) / 중상(30% 이하) / 경상(70% 이하). 그보다 체력이 많으면 낙오하지 않음
    public List<StrandTier> strandTiers = new List<StrandTier>
    {
        new StrandTier { maxHpRatio = 0.1f, strandChance = 0.8f },
        new StrandTier { maxHpRatio = 0.3f, strandChance = 0.3f },
        new StrandTier { maxHpRatio = 0.7f, strandChance = 0.1f },
    };
}

// 도주 규칙 (순수 로직)
public static class FleeRules
{
    // 남은 체력 비율에 해당하는 낙오 확률 (조건을 만족하는 구간 중 maxHpRatio가 가장 낮은 것)
    public static float GetStrandChance(FleeSettings settings, float hpRatio)
    {
        if (settings == null || settings.strandTiers == null) return 0f;

        StrandTier best = null;
        foreach (StrandTier tier in settings.strandTiers)
        {
            if (tier == null || hpRatio > tier.maxHpRatio) continue;
            if (best == null || tier.maxHpRatio < best.maxHpRatio) best = tier;
        }
        return best != null ? Math.Min(Math.Max(best.strandChance, 0f), 1f) : 0f;
    }

    // roll01: 0 이상 1 미만의 난수
    public static bool RollStranded(FleeSettings settings, float hpRatio, float roll01)
    {
        return roll01 < GetStrandChance(settings, hpRatio);
    }
}
