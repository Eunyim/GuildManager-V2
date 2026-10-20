using System;

// 무리 인원 규칙 (순수 로직). 도적단·늑대 무리 등이 함께 씀
public static class EnemyPackRules
{
    // 랭크별 인원 표 (D, C, B, A, S 순서). 표가 비어 있으면 1, 랭크가 표 길이를 넘으면 마지막 값을 사용
    public static int GetSize(int[] sizePerRank, RankType rank)
    {
        if (sizePerRank == null || sizePerRank.Length == 0) return 1;
        int index = Math.Min(Math.Max((int)rank, 0), sizePerRank.Length - 1);
        return Math.Max(sizePerRank[index], 1);
    }
}
