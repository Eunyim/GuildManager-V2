using UnityEngine;
using System;
using System.Collections.Generic;

// 무리로 나오는 몬스터 (QuestGenerator 인스펙터에서 설정)
// 적 목록에서 이 프리팹이 한 번 뽑히면, 그 자리에 랭크별 마릿수만큼 무리가 들어감
//  - 구성원 목록이 비어 있으면 같은 몬스터로 채움 (예: 초원 늑대)
//  - 구성원 목록이 있으면 그중에서 무작위로 채움 (예: 고블린 → 전사·사냥꾼)
//  - 지휘자가 있으면 무리마다 반드시 1명 넣음 (예: 고블린 사제)
[Serializable]
public class EnemyPack
{
    [Tooltip("적 목록에 들어 있는 대표 프리팹 (이게 뽑히면 무리로 바뀜)")]
    public GameObject prefab;

    [Tooltip("무리 구성원. 비워 두면 대표 프리팹으로 채움")]
    public List<GameObject> members = new List<GameObject>();

    [Tooltip("무리마다 반드시 1명 들어가는 지휘자 (없으면 비워 둠)")]
    public GameObject leader;

    [Tooltip("의뢰 랭크별 무리 마릿수 (D, C, B, A, S 순서). 지휘자도 이 수에 포함")]
    public int[] sizePerRank = { 3, 3, 4, 4, 5 };
}
