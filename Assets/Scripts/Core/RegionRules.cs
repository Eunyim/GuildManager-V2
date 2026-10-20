// 던전 지역의 랭크 규칙 (순수 로직)
public static class RegionRules
{
    // 지역 의뢰의 랭크: roll(0~1)이 최고 랭크 확률보다 작으면 최고 랭크, 아니면 최저 랭크
    public static RankType RollRank(RankType minRank, RankType maxRank, float maxRankChance, float roll)
    {
        if (maxRank < minRank) { RankType t = minRank; minRank = maxRank; maxRank = t; }
        return roll < maxRankChance ? maxRank : minRank;
    }

    // 랭크를 지역의 랭크 범위 안으로 맞춤 (낮으면 최저 랭크, 높으면 상한선)
    public static RankType ClampRank(RankType rank, RankType minRank, RankType maxRank)
    {
        if (maxRank < minRank) { RankType t = minRank; minRank = maxRank; maxRank = t; }
        if (rank < minRank) return minRank;
        if (rank > maxRank) return maxRank;
        return rank;
    }

    public static bool Contains(RankType rank, RankType minRank, RankType maxRank) =>
        rank >= minRank && rank <= maxRank;

    public static bool IsUnlocked(int requiredReputationLevel, int reputationLevel) =>
        reputationLevel >= requiredReputationLevel;

    // 화면 표시용 "D~C 등급" / "S 등급"
    public static string RangeText(RankType minRank, RankType maxRank) =>
        minRank == maxRank ? $"{minRank} 등급" : $"{minRank}~{maxRank} 등급";
}
