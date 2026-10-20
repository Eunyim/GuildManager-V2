using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New MapTheme", menuName = "Dungeon/Map Theme")]
public class MapThemeData : ScriptableObject
{
    [Header("테마 정보")]
    public string themeName = "던전";
    [Tooltip("이 테마를 쓰는 지역 이름 (DungeonRegion.name, 예: 초원). Resources/DungeonThemes에 두면 그 지역 의뢰의 방이 이 바닥으로 깔림")]
    public string regionName = "";

    [Header("바닥 타일 (랜덤 배치)")]
    [Tooltip("여러 타일을 등록하면 랜덤하게 섞어서 배치됩니다.")]
    public TileBase[] floorTiles;

    [Header("아이소메트릭 바닥 타일 (Tools > 던전 아이소메트릭 전환이 floorTiles로부터 자동 생성)")]
    [Tooltip("그리드가 Isometric일 때 floorTiles 대신 사용하는 2:1 마름모 타일")]
    public TileBase[] isoFloorTiles;

    [Header("아이소메트릭 절벽 단면 (바닥 가장자리 아래로 늘어지는 흙·바위 면)")]
    [Tooltip("왼쪽 아래 변에 붙는 면 (그 쪽 이웃 칸이 비었을 때)")]
    public TileBase[] isoEdgeLeft;
    [Tooltip("오른쪽 아래 변에 붙는 면")]
    public TileBase[] isoEdgeRight;
    [Tooltip("양쪽 아래 변 모두")]
    public TileBase[] isoEdgeBoth;

    [Header("벽 타일 - 방향별 구분")]
    [Tooltip("바닥 위쪽에 붙은 벽 (아래에 바닥이 있는 벽의 윗면)")]
    public TileBase wallTop;
    [Tooltip("바닥 아래쪽에 붙은 벽")]
    public TileBase wallBottom;
    [Tooltip("바닥 오른쪽에 붙은 벽 (왼쪽 벽면)")]
    public TileBase wallLeft;
    [Tooltip("바닥 왼쪽에 붙은 벽 (오른쪽 벽면)")]
    public TileBase wallRight;

    [Header("벽 타일 - 코너")]
    public TileBase wallCornerTL;   // 좌상 코너 (오른쪽·아래쪽에 바닥)
    public TileBase wallCornerTR;   // 우상 코너 (왼쪽·아래쪽에 바닥)
    public TileBase wallCornerBL;   // 좌하 코너 (오른쪽·위쪽에 바닥)
    public TileBase wallCornerBR;   // 우하 코너 (왼쪽·위쪽에 바닥)

    [Tooltip("방향 판단이 어려운 벽 또는 내부 채움용 벽")]
    public TileBase wallSolid;

    [Header("장식 타일 (바닥 위에 배치)")]
    [Tooltip("나무, 풀, 돌, 기둥 등 바닥 위 장식 타일들")]
    public TileBase[] decorationTiles;

    [Range(0f, 0.25f)]
    [Tooltip("장식 타일이 배치될 확률 (0=없음, 0.25=25%)")]
    public float decorationDensity = 0.08f;
}
