using System;

// 재료 수급 의뢰의 필요 개수·드랍 규칙 (순수 로직)
public static class GatherRules
{
    // 필요 개수 = 던전에 나오는 재료 몬스터들의 원래 드랍률(%) 합 ÷ 100 을 반올림 (최소 1)
    public static int RequiredCount(float sumDropRatesPercent)
    {
        return Math.Max(1, (int)Math.Round(sumDropRatesPercent / 100f, MidpointRounding.AwayFromZero));
    }

    // 의뢰 재료가 이번에 떨어질 확률(%)
    //  - 이미 다 모았으면 0 (더 떨어지지 않음)
    //  - 막판 보장: 이 몬스터가 의뢰 계산에 들어간 재료 몬스터이고, 남은 재료 몬스터 수(이 몬스터 포함)가 아직 못 모은 개수 이하이면 100
    //  - 그 밖에는 원래 확률 × 배율 (최대 100)
    public static float EffectiveRate(float baseRate, float multiplier, int gathered, int needed,
                                      bool countedSource, int remainingSourcesIncludingThis, bool guaranteeAtEnd)
    {
        if (gathered >= needed) return 0f;
        if (guaranteeAtEnd && countedSource && remainingSourcesIncludingThis <= needed - gathered) return 100f;
        return Math.Min(100f, Math.Max(0f, baseRate * multiplier));
    }
}
