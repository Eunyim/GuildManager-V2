using System;

// 방마다 적을 뽑는 횟수와 랭크 섞기 (QuestGenerator 인스펙터에서 조정)
[Serializable]
public class RoomSpawnSettings
{
    // 방마다 적을 뽑는 횟수 (D, C, B, A, S 순서, 최소~최대 포함). 보스방은 +1
    public int[] minPicksPerRank = { 1, 2, 3, 4, 5 };
    public int[] maxPicksPerRank = { 2, 3, 4, 5, 6 };

    [UnityEngine.Tooltip("C랭크 이상: 뽑는 횟수의 절반(올림)은 의뢰 랭크, 나머지는 한 단계 아래 랭크의 무리 크기로 뽑음. 능력치 배율은 의뢰 랭크 그대로")]
    public bool mixLowerRank = true;

    [UnityEngine.Tooltip("켜면 아래 랭크로 뽑힌 적은 능력치 배율도 한 단계 아래 (끄면 무리 크기만 아래)")]
    public bool lowerRankStats = false;

    [UnityEngine.Tooltip("무리 종족(늑대·고블린·스켈레톤·오크 등 packs에 있는 것)을 한 번 뽑을 때 뽑는 횟수를 몇 번으로 칠지 (1이면 보통 적과 같음). 10/18 결정: 2")]
    public int packPickCost = 2;
}

// 방 적 뽑기 규칙 (순수 로직)
public static class RoomSpawnRules
{
    // 이번 방에서 적을 뽑을 횟수 (randomInclusive: 최소~최대 사이 정수를 고르는 함수)
    public static int GetPickCount(RoomSpawnSettings s, RankType rank, bool isBossRoom, Func<int, int, int> randomInclusive)
    {
        int index = (int)rank;
        int min = Pick(s != null ? s.minPicksPerRank : null, index, 2);
        int max = Math.Max(min, Pick(s != null ? s.maxPicksPerRank : null, index, min));
        return randomInclusive(min, max) + (isBossRoom ? 1 : 0);
    }

    // count번 중 의뢰 랭크로 뽑는 횟수. 절반을 올림 (3번이면 2번). D랭크이거나 섞기를 끄면 전부
    public static int GetQuestRankPicks(RoomSpawnSettings s, RankType rank, int count)
    {
        if (count <= 0) return 0;
        if (s == null || !s.mixLowerRank || rank == RankType.D) return count;
        return (count + 1) / 2;
    }

    // j번째(0부터) 뽑기가 쓸 랭크: 앞쪽 questRankPicks번은 의뢰 랭크, 나머지는 한 단계 아래
    public static RankType GetPickRank(RankType rank, int j, int questRankPicks)
    {
        if (j < questRankPicks || rank == RankType.D) return rank;
        return (RankType)((int)rank - 1);
    }

    // 아래 랭크로 뽑혔을 때 능력치 배율을 몇 단계 낮출지 (0 또는 1)
    public static int GetStatsRankDown(RoomSpawnSettings s, RankType rank, RankType pickRank)
    {
        if (s == null || !s.lowerRankStats) return 0;
        return Math.Max(0, (int)rank - (int)pickRank);
    }

    // 이 적을 뽑을 때 쓰는 횟수 (무리 종족은 packPickCost, 그 밖은 1)
    public static int GetPickCost(RoomSpawnSettings s, bool isPack)
    {
        if (!isPack || s == null) return 1;
        return Math.Max(1, s.packPickCost);
    }

    static int Pick(int[] table, int index, int fallback)
    {
        if (table == null || table.Length == 0) return fallback;
        return Math.Max(table[Math.Min(Math.Max(index, 0), table.Length - 1)], 0);
    }
}
