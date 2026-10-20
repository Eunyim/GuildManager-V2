using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 로비 씬의 타일맵 배경을 생성합니다.
/// 바닥 타일 4종(rect_gray 0~3)을 랜덤 배치하여 자연스러운 석재 바닥을 만듭니다.
/// 벽은 사용하지 않고 검은 배경으로 처리합니다.
/// </summary>
public class LobbyMapGenerator : MonoBehaviour
{
    public static LobbyMapGenerator Instance;

    [Header("타일맵 레이어 연결")]
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public Tilemap decorTilemap;

    [Header("맵 크기")]
    public int mapWidth  = 20;
    public int mapHeight = 12;

    [Header("타일맵 월드 오프셋")]
    public Vector3Int mapOffset = new Vector3Int(-10, -6, 0);

    [Header("치비 이동 영역")]
    [Tooltip("바닥 가장자리에서 이 칸 수만큼은 치비가 걸어다니지 않습니다.")]
    public int walkableEdgeMargin = 1;

    private List<Vector2Int> walkableCells;

    // 바닥 타일 4종 (rect_gray 0~3)
    private TileBase[] floorTiles;
    private const int FLOOR_TILE_COUNT = 4;

    // 타일 에셋 경로
    private const string TILE_ASSET_PATH = "Assets/Art/Tiles/LobbyTileset/FloorTiles";
    private const string RESOURCE_PATH = "LobbyTiles";

    // 아이소메트릭 그리드용 마름모 타일 (Tools > 로비 아이소메트릭 전환으로 생성)
    private const string ISO_TILE_ASSET_PATH = "Assets/Art/Tiles/LobbyTileset/IsoFloorTiles";
    private const string RECT_TILE_NAME = "rect_gray";
    private const string ISO_TILE_NAME = "iso_gray";

    // ═══════════════════════════════════════════════════
    // 20x12 고정 레이아웃 — 길드 본부 내부
    // 0 = 바닥, -1 = 빈 칸 (검은 배경이 보임)
    // ═══════════════════════════════════════════════════
    private static readonly int[,] LobbyTileMap = new int[12, 20]
    {
        { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, -1, -1 },
        { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    };

    // 타일 변형 시드 (동일 위치에서 항상 같은 타일이 나오도록)
    private const int RANDOM_SEED = 12345;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GenerateMap();
    }

    public void GenerateMap()
    {
        LoadFloorTiles();

        if (floorTiles == null || floorTiles.Length == 0)
        {
            Debug.LogError("[LobbyMapGenerator] 바닥 타일 로드 실패! 타일 에셋이 존재하는지 확인하세요.");
            return;
        }

        ClearAll();
        PlaceTiles();
    }

#if UNITY_EDITOR
    /// <summary>
    /// 플레이하지 않고도 씬 뷰에서 타일맵이 보이도록 에디터에서 타일을 씬에 구워 넣습니다.
    /// 고정 시드를 쓰므로 런타임에 Start()가 다시 생성해도 같은 결과가 나옵니다.
    /// </summary>
    [ContextMenu("타일맵 굽기 (에디터)")]
    public void BakeInEditor()
    {
        if (floorTilemap == null)
        {
            Debug.LogError("[LobbyMapGenerator] floorTilemap이 연결되지 않았습니다.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(floorTilemap, "로비 타일맵 굽기");
        GenerateMap();

        EditorUtility.SetDirty(floorTilemap);
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    /// <summary>
    /// 바닥 타일 4종을 로드합니다.
    /// 그리드가 Isometric이면 마름모 타일(iso_gray), 아니면 사각 타일(rect_gray)을 씁니다.
    /// </summary>
    void LoadFloorTiles()
    {
        floorTiles = new TileBase[FLOOR_TILE_COUNT];

        bool isometric = floorTilemap != null
            && floorTilemap.layoutGrid != null
            && floorTilemap.layoutGrid.cellLayout == GridLayout.CellLayout.Isometric;
        string tileName = isometric ? ISO_TILE_NAME : RECT_TILE_NAME;

#if UNITY_EDITOR
        string assetFolder = isometric ? ISO_TILE_ASSET_PATH : TILE_ASSET_PATH;
        for (int i = 0; i < FLOOR_TILE_COUNT; i++)
        {
            string assetPath = $"{assetFolder}/{tileName}{i}.asset";
            floorTiles[i] = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);

            if (floorTiles[i] == null)
                Debug.LogWarning($"[LobbyMapGenerator] 타일 로드 실패: {assetPath}");
        }
#else
        for (int i = 0; i < FLOOR_TILE_COUNT; i++)
        {
            floorTiles[i] = Resources.Load<TileBase>($"{RESOURCE_PATH}/{tileName}{i}");

            if (floorTiles[i] == null)
                Debug.LogWarning($"[LobbyMapGenerator] Resources에서 타일 로드 실패: {RESOURCE_PATH}/{tileName}{i}");
        }
#endif
    }

    void ClearAll()
    {
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        decorTilemap?.ClearAllTiles();
    }

    void PlaceTiles()
    {
        int rows = LobbyTileMap.GetLength(0);
        int cols = LobbyTileMap.GetLength(1);

        // 고정 시드로 타일 변형 결정 (매번 동일한 패턴)
        System.Random rng = new System.Random(RANDOM_SEED);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int tileIndex = LobbyTileMap[row, col];
                if (tileIndex < 0) continue;

                // 4종 타일 중 랜덤 선택 (자연스러운 변형)
                int variant = rng.Next(FLOOR_TILE_COUNT);
                if (floorTiles[variant] == null) continue;

                int x = col;
                int y = rows - 1 - row;
                Vector3Int pos = CellPos(x, y);

                floorTilemap.SetTile(pos, floorTiles[variant]);
            }
        }
    }

    Vector3Int CellPos(int x, int y) => new Vector3Int(x, y, 0) + mapOffset;

    // ═══════════════════════════════════════════════════
    // 치비 이동 영역 — 바닥 타일 위의 랜덤 지점
    // ═══════════════════════════════════════════════════

    bool IsFloor(int row, int col)
    {
        return row >= 0 && row < LobbyTileMap.GetLength(0)
            && col >= 0 && col < LobbyTileMap.GetLength(1)
            && LobbyTileMap[row, col] >= 0;
    }

    // 가장자리 여유(walkableEdgeMargin) 안쪽의 바닥 칸만 모읍니다.
    void BuildWalkableCells()
    {
        walkableCells = new List<Vector2Int>();
        int rows = LobbyTileMap.GetLength(0);
        int cols = LobbyTileMap.GetLength(1);
        int m = Mathf.Max(0, walkableEdgeMargin);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                bool walkable = true;
                for (int dr = -m; dr <= m && walkable; dr++)
                    for (int dc = -m; dc <= m && walkable; dc++)
                        walkable = IsFloor(row + dr, col + dc);

                if (walkable)
                    walkableCells.Add(new Vector2Int(col, rows - 1 - row));
            }
        }
    }

    /// <summary>
    /// 바닥 타일 위의 랜덤 월드 좌표를 반환합니다. (사각/아이소메트릭 그리드 모두 지원)
    /// </summary>
    public bool TryGetRandomFloorWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (floorTilemap == null) return false;

        if (walkableCells == null) BuildWalkableCells();
        if (walkableCells.Count == 0) return false;

        Vector2Int cell = walkableCells[Random.Range(0, walkableCells.Count)];
        Vector3 cellPoint = new Vector3(
            cell.x + mapOffset.x + Random.value,
            cell.y + mapOffset.y + Random.value,
            0f);

        worldPoint = floorTilemap.LocalToWorld(floorTilemap.CellToLocalInterpolated(cellPoint));
        return true;
    }

    /// <summary>
    /// 바닥 타일 위의 랜덤 지점을, UI 오브젝트(child)의 anchoredPosition 값으로 변환해 반환합니다.
    /// 로비 치비처럼 캔버스 위에 있는 캐릭터를 타일맵 바닥에 맞춰 배치할 때 사용합니다.
    /// </summary>
    public bool TryGetRandomFloorAnchoredPosition(RectTransform child, out Vector2 anchoredPos)
    {
        anchoredPos = default;

        RectTransform parent = child != null ? child.parent as RectTransform : null;
        Camera worldCam = Camera.main;
        if (parent == null || worldCam == null) return false;
        if (!TryGetRandomFloorWorldPoint(out Vector3 world)) return false;

        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera uiCam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera
            : null;

        Vector2 screen = worldCam.WorldToScreenPoint(world);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCam, out Vector2 local))
            return false;

        // 부모 로컬 좌표 → child의 앵커 기준 좌표
        Vector2 anchor = (child.anchorMin + child.anchorMax) * 0.5f;
        anchoredPos = local - (parent.rect.min + Vector2.Scale(parent.rect.size, anchor));
        return true;
    }
}
