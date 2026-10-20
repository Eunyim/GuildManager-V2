using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 룸 템플릿을 기반으로 테마에 맞는 타일을 랜덤 배치합니다.
/// DungeonScene의 Grid 오브젝트 하위에 이 컴포넌트를 붙이세요.
/// 그리드가 Isometric이면 로비처럼 마름모 바닥(테마의 isoFloorTiles)만 깔고, 벽/장식은 두지 않습니다.
/// </summary>
public class MapGenerator : MonoBehaviour
{
    public static MapGenerator Instance;

    [Header("타일맵 레이어 연결")]
    [Tooltip("바닥 타일맵 (Floor 레이어)")]
    public Tilemap floorTilemap;
    [Tooltip("벽 타일맵 (Wall 레이어, floorTilemap보다 Order in Layer 높게)")]
    public Tilemap wallTilemap;
    [Tooltip("장식 타일맵 (Decoration 레이어, 가장 위)")]
    public Tilemap decorTilemap;

    [Header("맵 크기 (템플릿과 일치해야 함)")]
    public int mapWidth  = 24;
    public int mapHeight = 14;

    [Header("아이소메트릭 방 모양")]
    [Tooltip("켜면 방에 들어갈 때마다 모양을 새로 만듦 (RoomShapeRules). 끄면 아래 고정 템플릿 중 하나")]
    public bool proceduralRooms = true;
    [Tooltip("가장자리 칸을 깎는 확률 (울퉁불퉁함)")]
    [Range(0f, 0.5f)] public float edgeRoughness = 0.3f;
    [Tooltip("방 안쪽 구덩이 최대 개수")]
    [Range(0, 6)] public int maxPits = 3;

    [Header("아이소메트릭 방 크기")]
    [Tooltip("아이소메트릭 방 템플릿(18x18)을 이 칸 수로 늘려서 깜 (테두리 포함). 18 = 원래 크기, 22면 바닥이 약 20칸 (카메라 크기 6에서 화면 안에 들어옴)")]
    public int isoRoomSize = 22;

    [Header("타일맵 월드 오프셋")]
    [Tooltip("타일 (0,0)이 월드 좌표 어디에서 시작할지. 전투 씬 중앙에 오도록 조정하세요.")]
    public Vector3Int mapOffset = new Vector3Int(-12, -7, 0);

    // ─────────────────────────────────────────────
    // 룸 템플릿 (1 = 바닥, 0 = 벽/빈 공간)
    // 크기: 24 x 14 고정 (mapWidth x mapHeight)
    // ─────────────────────────────────────────────
    private static readonly string[][] RoomTemplates = new string[][]
    {
        // [0] 기본 직사각형 방
        new string[] {
            "000000000000000000000000",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "000000000000000000000000",
        },
        // [1] 기둥이 있는 방
        new string[] {
            "000000000000000000000000",
            "011111111111111111111110",
            "011001111111111111001110",
            "011001111111111111001110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011001111111111111001110",
            "011001111111111111001110",
            "011111111111111111111110",
            "011111111111111111111110",
            "000000000000000000000000",
        },
        // [2] 십자형 방 (가운데 통로 + 양 날개)
        new string[] {
            "000000001111111100000000",
            "000000001111111100000000",
            "000000001111111100000000",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "000000001111111100000000",
            "000000001111111100000000",
            "000000001111111100000000",
            "000000000000000000000000",
        },
        // [3] 대각 코너 깎인 방
        new string[] {
            "000000000000000000000000",
            "000011111111111111110000",
            "001111111111111111111000",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "001111111111111111111000",
            "000011111111111111110000",
            "000000000000000000000000",
            "000000000000000000000000",
        },
        // [4] 중앙 장벽이 있는 방 (보스방 느낌)
        new string[] {
            "000000000000000000000000",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111110000000111111110",
            "011111110000000111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111110000000111111110",
            "011111110000000111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "011111111111111111111110",
            "000000000000000000000000",
        },
    };

    // ─────────────────────────────────────────────
    // 아이소메트릭용 룸 템플릿 (1 = 바닥, 0 = 빈 공간), 18 x 18
    // 정사각형 칸 배치가 화면에서는 가로로 긴 마름모가 됩니다.
    // 템플릿의 왼쪽 위 모서리 = 화면 왼쪽 꼭짓점(아군 쪽), 오른쪽 아래 모서리 = 화면 오른쪽 꼭짓점(적 쪽)
    // 이므로 이 두 모서리와 그 사이 대각선은 항상 바닥으로 남겨 둡니다.
    // 유닛 이동에 충돌 판정이 없으므로 안쪽에 구멍(빈 칸)은 두지 않습니다. (구멍 위를 그냥 걸어 다니게 됨)
    // ─────────────────────────────────────────────
    private static readonly string[][] IsoRoomTemplates = new string[][]
    {
        // [0] 기본 마름모 방
        new string[] {
            "000000000000000000",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "000000000000000000",
        },
        // [1] 위아래 꼭짓점을 깎은 방
        new string[] {
            "000000000000000000",
            "011111111110000000",
            "011111111111000000",
            "011111111111100000",
            "011111111111110000",
            "011111111111111000",
            "011111111111111100",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "001111111111111110",
            "000111111111111110",
            "000011111111111110",
            "000001111111111110",
            "000000111111111110",
            "000000011111111110",
            "000000000000000000",
        },
        // [2] 위아래 꼭짓점을 조금만 깎은 방
        new string[] {
            "000000000000000000",
            "011111111111110000",
            "011111111111111000",
            "011111111111111100",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "011111111111111110",
            "001111111111111110",
            "000111111111111110",
            "000011111111111110",
            "000000000000000000",
        },
        // [3] 위아래를 많이 깎아 가로로 길쭉한 방
        new string[] {
            "000000000000000000",
            "011111110000000000",
            "011111111000000000",
            "011111111100000000",
            "011111111110000000",
            "011111111111000000",
            "011111111111100000",
            "011111111111110000",
            "001111111111111000",
            "000111111111111100",
            "000011111111111110",
            "000001111111111110",
            "000000111111111110",
            "000000011111111110",
            "000000001111111110",
            "000000000111111110",
            "000000000011111110",
            "000000000000000000",
        },
    };

    void Awake()
    {
        Instance = this;
        // 절벽 단면(WallTilemap)은 바닥 아래로 늘어지므로 바닥보다 먼저 그림
        if (IsIsometric && wallTilemap != null && floorTilemap != null)
        {
            TilemapRenderer wr = wallTilemap.GetComponent<TilemapRenderer>();
            TilemapRenderer fr = floorTilemap.GetComponent<TilemapRenderer>();
            if (wr != null && fr != null) wr.sortingOrder = fr.sortingOrder - 1;
        }
    }

    readonly System.Random roomRng = new System.Random();

    /// <summary>마지막으로 만든 방 모양 이름 (디버그용)</summary>
    public string LastShape { get; private set; }

    /// <summary>그리드가 아이소메트릭(로비와 같은 마름모 시점)인지</summary>
    public bool IsIsometric =>
        floorTilemap != null
        && floorTilemap.layoutGrid != null
        && floorTilemap.layoutGrid.cellLayout == GridLayout.CellLayout.Isometric;

    /// <summary>월드 좌표가 바닥 타일 위인지 (바닥이 아직 없으면 어디든 true)</summary>
    public bool IsOnFloor(Vector3 worldPos)
    {
        if (floorTilemap == null || floorTilemap.GetUsedTilesCount() == 0) return true;
        return floorTilemap.HasTile(floorTilemap.WorldToCell(worldPos));
    }

    /// <summary>가장 가까운 바닥 위치 (이미 바닥이면 그대로, 바닥이 없으면 그대로)</summary>
    public Vector3 NearestFloor(Vector3 worldPos)
    {
        if (IsOnFloor(worldPos)) return worldPos;
        BoundsInt bounds = floorTilemap.cellBounds;
        Vector3 best = worldPos;
        float bestDist = float.MaxValue;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!floorTilemap.HasTile(cell)) continue;
            Vector3 center = floorTilemap.GetCellCenterWorld(cell);
            center.z = worldPos.z;
            float d = (center - worldPos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = center; }
        }
        return best;
    }

    // ─── 바닥 길찾기 (구멍·오목한 가장자리를 돌아가는 경로) ───────────────────

    [Header("바닥 길찾기")]
    [Tooltip("두 지점 사이가 바닥으로 이어졌는지 검사할 때 선 위를 이 간격(월드 단위)으로 확인")]
    public float lineCheckStep = 0.12f;

    private bool[,] walkGrid;        // [x, y] = floorTilemap 칸 (cellBounds 기준)
    private BoundsInt walkBounds;
    private int walkTileCount = -1;  // 격자를 만들 때의 바닥 칸 수 (바뀌면 다시 만듦)
    private readonly List<(int x, int y)> cellPath = new List<(int x, int y)>();

    // 바닥 타일이 바뀌었으면 걸을 수 있는 칸 격자를 다시 만듦
    bool EnsureWalkGrid()
    {
        if (floorTilemap == null) return false;
        int count = floorTilemap.GetUsedTilesCount();
        if (count == 0) return false;
        if (walkGrid != null && count == walkTileCount && floorTilemap.cellBounds == walkBounds) return true;

        floorTilemap.CompressBounds();
        walkBounds = floorTilemap.cellBounds;
        walkGrid = new bool[walkBounds.size.x, walkBounds.size.y];
        for (int x = 0; x < walkBounds.size.x; x++)
            for (int y = 0; y < walkBounds.size.y; y++)
                walkGrid[x, y] = floorTilemap.HasTile(new Vector3Int(walkBounds.xMin + x, walkBounds.yMin + y, 0));
        walkTileCount = count;
        return true;
    }

    /// <summary>a에서 b까지 곧게 걸어가도 바닥 밖(구멍)을 밟지 않는지. endMargin: 양 끝에서 이 거리 안쪽은 검사하지 않음</summary>
    public bool IsLineClear(Vector3 a, Vector3 b, float endMargin = 0f)
    {
        if (floorTilemap == null || floorTilemap.GetUsedTilesCount() == 0) return true;
        Vector3 d = b - a;
        d.z = 0f;
        float len = d.magnitude;
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.02f, lineCheckStep)));
        for (int i = 1; i <= steps; i++)
        {
            float along = len * i / steps;
            if (along < endMargin || along > len - endMargin) continue;
            if (!IsOnFloor(a + d * (i / (float)steps))) return false;
        }
        return true;
    }

    /// <summary>
    /// from에서 to까지 바닥으로만 가는 경유지 목록 (첫 경유지부터, 마지막은 목적지 또는 그 근처 바닥).
    /// 곧게 갈 수 있는 구간은 하나로 합쳐서 꼭 꺾어야 하는 곳만 남김. 길이 없으면 false
    /// </summary>
    public bool FindPath(Vector3 from, Vector3 to, List<Vector3> waypoints)
    {
        waypoints.Clear();
        if (!EnsureWalkGrid()) { waypoints.Add(to); return true; }

        Vector3Int sc = floorTilemap.WorldToCell(from), gc = floorTilemap.WorldToCell(to);
        if (!FloorPathRules.NearestWalkable(walkGrid, sc.x - walkBounds.xMin, sc.y - walkBounds.yMin, out int sx, out int sy)) return false;
        if (!FloorPathRules.NearestWalkable(walkGrid, gc.x - walkBounds.xMin, gc.y - walkBounds.yMin, out int gx, out int gy)) return false;
        if (!FloorPathRules.FindPath(walkGrid, sx, sy, gx, gy, cellPath)) return false;

        // 칸 중심 → 월드. 목적지가 바닥이면 마지막은 목적지 그대로, 아니면 가장 가까운 바닥 칸 중심
        var points = new List<Vector3>(cellPath.Count);
        for (int i = 1; i < cellPath.Count; i++)
        {
            Vector3 c = floorTilemap.GetCellCenterWorld(new Vector3Int(cellPath[i].x + walkBounds.xMin, cellPath[i].y + walkBounds.yMin, 0));
            c.z = from.z;
            points.Add(c);
        }
        Vector3 goal = IsOnFloor(to) ? to : (points.Count > 0 ? points[points.Count - 1] : NearestFloor(to));
        goal.z = from.z;
        if (points.Count > 0) points[points.Count - 1] = goal; else points.Add(goal);

        // 경로 펴기: 지금 자리에서 곧게 닿는 가장 먼 경유지로 건너뜀
        Vector3 anchor = from;
        int index = 0;
        while (index < points.Count)
        {
            int far = index;
            for (int j = points.Count - 1; j > index; j--)
                if (IsLineClear(anchor, points[j])) { far = j; break; }
            waypoints.Add(points[far]);
            anchor = points[far];
            index = far + 1;
        }
        return true;
    }

    /// <summary>
    /// 새 스테이지 시작 시 호출. 테마에 맞는 타일로 맵을 랜덤 생성합니다.
    /// </summary>
    public void GenerateMap(MapThemeData theme)
    {
        if (theme == null)
        {
            Debug.LogWarning("[MapGenerator] MapThemeData가 없습니다. 맵 생성 건너뜀.");
            return;
        }

        ClearAll();

        if (IsIsometric)
        {
            // 아이소메트릭: 마름모 바닥만 배치. 맵이 화면 중앙에 오도록 템플릿 크기의 절반만큼 당김
            bool[,] isoMask;
            if (proceduralRooms)
            {
                var st = RoomShapeRules.Settings.Default;
                st.edgeRoughness = edgeRoughness;
                st.maxPits = maxPits;
                isoMask = RoomShapeRules.Generate(roomRng, Mathf.Max(12, isoRoomSize), st, out string shape);
                LastShape = shape;
            }
            else
            {
                string[] isoTemplate = IsoRoomTemplates[Random.Range(0, IsoRoomTemplates.Length)];
                isoMask = ScaleMask(ParseTemplate(isoTemplate), isoRoomSize);
                LastShape = "템플릿";
            }
            activeOffset = new Vector3Int(-isoMask.GetLength(0) / 2, -isoMask.GetLength(1) / 2, 0);

            TileBase[] isoTiles = theme.isoFloorTiles;
            if (isoTiles == null || isoTiles.Length == 0)
            {
                Debug.LogWarning($"[MapGenerator] '{theme.themeName}' 테마에 isoFloorTiles가 없습니다. Tools > 던전 아이소메트릭 전환을 실행하세요. (사각 바닥 타일로 대신 배치)");
                isoTiles = theme.floorTiles;
            }
            PlaceFloorTiles(isoMask, isoTiles);
            PlaceIsoEdges(isoMask, theme);
            return;
        }

        activeOffset = mapOffset;

        string[] template = RoomTemplates[Random.Range(0, RoomTemplates.Length)];
        bool[,] floorMask = ParseTemplate(template);

        PlaceFloorTiles(floorMask, theme.floorTiles);
        PlaceWallTiles(floorMask, theme);
        PlaceDecorations(floorMask, theme);
    }

    // ─────────────────────────────────────────────
    // 내부 구현
    // ─────────────────────────────────────────────

    // 지금 생성 중인 맵의 셀 오프셋 (사각: mapOffset, 아이소메트릭: 템플릿 중앙 정렬)
    private Vector3Int activeOffset;

    void ClearAll()
    {
        walkGrid = null; // 방이 바뀌면 길찾기 격자도 새로
        floorTilemap?.ClearAllTiles();
        wallTilemap?.ClearAllTiles();
        decorTilemap?.ClearAllTiles();
    }

    /// <summary>
    /// 문자열 배열 템플릿을 bool[x, y] 마스크로 변환.
    /// 문자열 첫 행이 화면 위쪽이 되도록 Y축 반전.
    /// </summary>
    bool[,] ParseTemplate(string[] template)
    {
        int rows = template.Length;
        int cols = template[0].Length;
        bool[,] mask = new bool[cols, rows];

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                mask[x, rows - 1 - y] = (template[y][x] == '1');
            }
        }
        return mask;
    }

    // 마스크를 size x size로 늘림 (가장 가까운 칸을 따라감). size가 원래 크기 이하면 그대로
    static bool[,] ScaleMask(bool[,] mask, int size)
    {
        int w = mask.GetLength(0), h = mask.GetLength(1);
        if (size <= w && size <= h) return mask;
        bool[,] scaled = new bool[size, size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                scaled[x, y] = mask[Mathf.Min(w - 1, x * w / size), Mathf.Min(h - 1, y * h / size)];
        return scaled;
    }

    void PlaceFloorTiles(bool[,] mask, TileBase[] tiles)
    {
        if (tiles == null || tiles.Length == 0) return;

        for (int x = 0; x < mask.GetLength(0); x++)
        {
            for (int y = 0; y < mask.GetLength(1); y++)
            {
                if (!mask[x, y]) continue;

                TileBase tile = tiles[Random.Range(0, tiles.Length)];
                floorTilemap.SetTile(CellPos(x, y), tile);
            }
        }
    }

    // 바닥 칸의 화면 아래쪽 이웃이 비었으면 그 변에 절벽 단면을 붙임 (WallTilemap)
    // 아이소메트릭 칸 (x, y)의 왼쪽 아래 변은 (x-1, y)와, 오른쪽 아래 변은 (x, y-1)과 맞닿음
    void PlaceIsoEdges(bool[,] mask, MapThemeData theme)
    {
        if (wallTilemap == null || theme == null) return;
        bool hasL = theme.isoEdgeLeft != null && theme.isoEdgeLeft.Length > 0;
        bool hasR = theme.isoEdgeRight != null && theme.isoEdgeRight.Length > 0;
        if (!hasL && !hasR) return;
        int w = mask.GetLength(0), h = mask.GetLength(1);
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (!mask[x, y]) continue;
                bool openL = x == 0 || !mask[x - 1, y];
                bool openR = y == 0 || !mask[x, y - 1];
                TileBase[] set = openL && openR && theme.isoEdgeBoth != null && theme.isoEdgeBoth.Length > 0 ? theme.isoEdgeBoth
                               : openL && hasL ? theme.isoEdgeLeft
                               : openR && hasR ? theme.isoEdgeRight : null;
                if (set == null) continue;
                wallTilemap.SetTile(CellPos(x, y), set[Random.Range(0, set.Length)]);
            }
        }
    }

    void PlaceWallTiles(bool[,] mask, MapThemeData theme)
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (mask[x, y]) continue; // 바닥이면 스킵

                bool adjN = IsFloor(mask, x,     y + 1);
                bool adjS = IsFloor(mask, x,     y - 1);
                bool adjE = IsFloor(mask, x + 1, y    );
                bool adjW = IsFloor(mask, x - 1, y    );

                // 인접 바닥이 전혀 없으면 배치하지 않음 (외곽 빈 공간)
                if (!adjN && !adjS && !adjE && !adjW) continue;

                TileBase wallTile = PickWallTile(theme, adjN, adjS, adjE, adjW);
                if (wallTile != null)
                    wallTilemap.SetTile(CellPos(x, y), wallTile);
            }
        }
    }

    /// <summary>
    /// 인접한 바닥 방향 조합으로 적절한 벽 타일을 선택합니다.
    /// adjN/S/E/W = 해당 방향에 바닥이 있는지 여부
    /// </summary>
    TileBase PickWallTile(MapThemeData t, bool adjN, bool adjS, bool adjE, bool adjW)
    {
        TileBase fallback = t.wallSolid;

        // 코너 (두 방향 인접, 나머지는 벽)
        if ( adjS &&  adjE && !adjN && !adjW) return t.wallCornerTL ?? fallback;
        if ( adjS &&  adjW && !adjN && !adjE) return t.wallCornerTR ?? fallback;
        if ( adjN &&  adjE && !adjS && !adjW) return t.wallCornerBL ?? fallback;
        if ( adjN &&  adjW && !adjS && !adjE) return t.wallCornerBR ?? fallback;

        // 단방향 면 (한 방향만 인접)
        if ( adjS && !adjN && !adjE && !adjW) return t.wallTop    ?? fallback;
        if ( adjN && !adjS && !adjE && !adjW) return t.wallBottom ?? fallback;
        if ( adjE && !adjN && !adjS && !adjW) return t.wallLeft   ?? fallback;
        if ( adjW && !adjN && !adjS && !adjE) return t.wallRight  ?? fallback;

        // T자·십자·다중 방향 인접 → solid
        return fallback;
    }

    void PlaceDecorations(bool[,] mask, MapThemeData theme)
    {
        if (theme.decorationTiles == null || theme.decorationTiles.Length == 0) return;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int y = 0; y < mapHeight; y++)
            {
                if (!mask[x, y]) continue;
                if (Random.value > theme.decorationDensity) continue;

                TileBase deco = theme.decorationTiles[Random.Range(0, theme.decorationTiles.Length)];
                decorTilemap.SetTile(CellPos(x, y), deco);
            }
        }
    }

    bool IsFloor(bool[,] mask, int x, int y)
    {
        if (x < 0 || x >= mapWidth || y < 0 || y >= mapHeight) return false;
        return mask[x, y];
    }

    Vector3Int CellPos(int x, int y) => new Vector3Int(x, y, 0) + activeOffset;
}
