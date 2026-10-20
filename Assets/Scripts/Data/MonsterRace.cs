using UnityEngine;
using System;
using System.Collections.Generic;

// 몬스터 종족 (QuestGenerator 인스펙터에서 설정. 'Tools > 던전 지역 설정 연결'이 기본값을 넣음)
// 종족마다 보스가 하나 있고, 지역의 보스방에는 그 지역에 사는 종족들의 보스 중 하나가 무작위로 나옴
[Serializable]
public class MonsterRace
{
    public string name = "슬라임";

    [Tooltip("이 종족에 속하는 몬스터 (지역 적 목록에 들어가는 프리팹. 무리 대표 포함)")]
    public List<GameObject> members = new List<GameObject>();

    [Tooltip("이 종족의 보스")]
    public GameObject boss;

    [Tooltip("켜면 보스가 이 종족의 무리와 함께 나옴 (무리의 지휘자 자리를 보스가 대신함. 예: 샤먼킹 + 고블린 무리)")]
    public bool bossLeadsPack;

    [Tooltip("재료 수급 의뢰에서 모아 오라고 할 수 있는 이 종족의 재료와 드랍 확률(%) (필요 개수 계산용)")]
    public List<DropItem> materials = new List<DropItem>();
}
