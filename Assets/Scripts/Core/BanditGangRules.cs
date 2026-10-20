using System;

// 도적단 무리 수치 (QuestGenerator 인스펙터에서 조정)
// 적 목록에서 '도적단'이 한 번 뽑히면, 그 자리에 도적단 구성원(전사·암살자·궁수 중 무작위) 여러 명이 들어감
[Serializable]
public class BanditGangSettings
{
    // 의뢰 랭크별 무리 인원 (D, C, B, A, S 순서)
    public int[] sizePerRank = { 2, 2, 3, 4, 5 };
}

// 도적단 규칙 (순수 로직)
public static class BanditGangRules
{
    // 표가 비어 있으면 1명, 랭크가 표 길이를 넘으면 마지막 값을 사용
    public static int GetSize(BanditGangSettings settings, RankType rank) =>
        EnemyPackRules.GetSize(settings != null ? settings.sizePerRank : null, rank);
}
