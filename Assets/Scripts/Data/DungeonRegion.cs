using UnityEngine;
using System;
using System.Collections.Generic;

// 던전 지역 (QuestGenerator 인스펙터에서 설정. 'Tools > 던전 지역 설정 연결'이 기본값을 넣음)
// 의뢰는 지역 안에서 생기고, 의뢰 랭크는 지역의 두 랭크 중 하나 (상한선을 넘지 않음)
[Serializable]
public class DungeonRegion
{
    public string name = "초원";

    [Tooltip("이 명성 레벨부터 갈 수 있음 (명성 레벨은 GameManager.reputationLevel)")]
    public int requiredReputationLevel = 1;

    [Tooltip("이 지역에 들어오는 의뢰의 최저 랭크")]
    public RankType minRank = RankType.D;
    [Tooltip("이 지역에 들어오는 의뢰의 최고 랭크 (상한선)")]
    public RankType maxRank = RankType.C;
    [Tooltip("의뢰가 최고 랭크로 나올 확률 (나머지는 최저 랭크). 예: 초원 0.2 = D 80% / C 20%")]
    [Range(0f, 1f)] public float maxRankChance = 0.2f;

    [Tooltip("이 지역에 나오는 몬스터. 무리 대표(고블린·스켈레톤·오크·초원 늑대·도적단)는 뽑히면 무리로 바뀜")]
    public List<GameObject> enemies = new List<GameObject>();

    // 보스는 이 지역 몬스터가 속한 종족들의 보스 중 하나 (QuestGenerator.races)
}
