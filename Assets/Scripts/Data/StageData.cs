using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Stage", menuName = "Dungeon/Stage Data")]
public class StageData : ScriptableObject
{
    [Header("스테이지 정보")]
    public string stageName = "1층 복도";
    public Sprite background; // (나중에 배경 바꿀 때 사용)

    [Header("맵 테마")]
    [Tooltip("이 스테이지에서 사용할 타일 테마. 없으면 맵 생성을 건너뜁니다.")]
    public MapThemeData mapTheme;

    [Header("출현 몬스터")]
    public List<GameObject> enemyPrefabs; // 이 방에서 나올 적들 리스트 (웨이브 방은 모든 웨이브의 적을 차례로 담음)

    [Tooltip("enemyPrefabs와 같은 순서로, 능력치 배율을 의뢰 랭크보다 몇 단계 낮출지. 비었거나 짧으면 0")]
    public List<int> enemyStatsRankDown = new List<int>();

    public int GetStatsRankDown(int index) =>
        enemyStatsRankDown != null && index >= 0 && index < enemyStatsRankDown.Count ? enemyStatsRankDown[index] : 0;

    [Tooltip("적이 몰려오는 횟수. 2 이상이면 enemyPrefabs를 그 수만큼 나눠 차례로 등장시킵니다.")]
    public int waveCount = 1;

    [Tooltip("보스방이면 enemyPrefabs의 슬라임을 킹 슬라임으로 소환하고 화면 위에 보스 체력 바를 띄움")]
    public bool isBossStage = false;
}