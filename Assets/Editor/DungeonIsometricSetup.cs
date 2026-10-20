using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// [메뉴] Tools > 던전 아이소메트릭 전환
/// DungeonScene의 전투 필드를 로비와 같은 아이소메트릭(2:1 마름모) 시점으로 바꿉니다.
///  1. 모든 MapThemeData의 사각 바닥 타일로부터 마름모 바닥 타일(isoFloorTiles)을 생성
///  2. Grid를 Isometric(셀 1 x 0.5)으로 변경, 타일맵 정렬 순서 조정
///  3. 배경을 로비처럼 검은색으로 (초록 Ground 판은 끔)
///  4. 아군 스폰 위치를 마름모 바닥 안쪽으로 이동 (처음 전환할 때만)
/// 다시 실행하면 타일만 다시 만들고 스폰 위치는 건드리지 않습니다.
/// </summary>
public class DungeonIsometricSetup : Editor
{
    const string SCENE_PATH    = "Assets/Scenes/DungeonScene.unity";
    const string ISO_TILE_ROOT = "Assets/Art/Tiles/Generated/Iso";
    const string UNDO_NAME     = "던전 아이소메트릭 전환";
    // 씬 미리보기에 쓸 테마: 실제 던전에서 쓰는 0x72 타일 테마 (없으면 처음 찾은 테마)
    const string PREVIEW_THEME_PATH = "Assets/Art/Tiles/Themes/DungeonTheme.asset";

    // 사각 시점으로 되돌릴 때 쓰는 원래 값
    static readonly Color rectCameraBackground = new Color(0.19215687f, 0.3019608f, 0.4745098f, 0f);
    static readonly Vector3[] rectFrontPositions = { new Vector3(-5f, 1f, 0f),   new Vector3(-5f, -1f, 0f) };
    static readonly Vector3[] rectBackPositions  = { new Vector3(-7.5f, 1f, 0f), new Vector3(-7.5f, -1f, 0f) };

    // 마름모 바닥 안쪽에 들어오는 아군 스폰 위치 (월드 좌표). 전위가 적(오른쪽)에 더 가깝게 섭니다.
    static readonly Vector3[] frontPositions = { new Vector3(-3.5f, 0.75f, 0f), new Vector3(-3.5f, -0.75f, 0f) };
    static readonly Vector3[] backPositions  = { new Vector3(-5.5f, 0.5f, 0f),  new Vector3(-5.5f, -0.5f, 0f) };

    [MenuItem("Tools/던전 아이소메트릭 전환")]
    static void Convert()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[DungeonIsometricSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        MapGenerator[] generators = FindGenerators();
        if (generators.Length == 0)
        {
            // 다른 씬이 열려 있으면 DungeonScene으로 전환 (저장 안 된 변경이 있으면 먼저 물어봄)
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            generators = FindGenerators();
        }

        MapGenerator gen = generators.Length > 0 ? generators[0] : null;
        Grid grid = gen != null && gen.floorTilemap != null ? gen.floorTilemap.layoutGrid as Grid : null;
        if (grid == null)
        {
            Debug.LogError("[DungeonIsometricSetup] 씬에 MapGenerator/Grid가 없습니다. DungeonScene을 열고 다시 시도하세요.");
            return;
        }

        // 1. 테마별 마름모 바닥 타일 생성
        int themeCount = 0, tileCount = 0;
        MapThemeData previewTheme = null;
        foreach (string guid in AssetDatabase.FindAssets("t:MapThemeData"))
        {
            MapThemeData theme = AssetDatabase.LoadAssetAtPath<MapThemeData>(AssetDatabase.GUIDToAssetPath(guid));
            if (theme == null) continue;

            int created = CreateIsoFloorTiles(theme);
            if (created == 0) continue;

            themeCount++;
            tileCount += created;
            if (previewTheme == null) previewTheme = theme;
        }
        AssetDatabase.SaveAssets();

        if (tileCount == 0)
        {
            Debug.LogError("[DungeonIsometricSetup] 마름모 바닥 타일을 하나도 만들지 못했습니다. MapThemeData의 floorTiles를 확인하세요.");
            return;
        }

        // 미리보기는 던전에서 실제로 쓰는 테마로 (처음 찾은 테마가 쓰지 않는 LobbyTheme일 수 있음)
        MapThemeData dungeonTheme = AssetDatabase.LoadAssetAtPath<MapThemeData>(PREVIEW_THEME_PATH);
        if (dungeonTheme != null && dungeonTheme.isoFloorTiles != null && dungeonTheme.isoFloorTiles.Length > 0)
            previewTheme = dungeonTheme;

        // 2. Grid: 2:1 마름모 (가로 1유닛, 세로 0.5유닛)
        bool wasIsometric = grid.cellLayout == GridLayout.CellLayout.Isometric;
        Undo.RecordObject(grid, UNDO_NAME);
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize   = new Vector3(1f, 0.5f, 1f);
        EditorUtility.SetDirty(grid);

        foreach (TilemapRenderer r in grid.GetComponentsInChildren<TilemapRenderer>(true))
        {
            // 아이소메트릭은 뒤(위쪽) 타일부터 그려야 겹침 순서가 맞음
            Undo.RecordObject(r, UNDO_NAME);
            r.sortOrder = TilemapRenderer.SortOrder.TopRight;
            EditorUtility.SetDirty(r);
        }

        // 3. 배경: 로비처럼 검은 배경에 바닥만 보이도록
        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Undo.RecordObject(cam, UNDO_NAME);
            cam.backgroundColor = Color.black;
            EditorUtility.SetDirty(cam);
        }

        GameObject ground = FindGround(gen.gameObject.scene);
        if (ground != null)
        {
            Undo.RecordObject(ground, UNDO_NAME);
            ground.SetActive(false); // 사각 시점용 초록 바닥 판
        }

        // 4. 스폰 위치 (처음 전환할 때만. 이후 손으로 옮긴 위치는 유지)
        if (!wasIsometric)
        {
            BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
            if (battle != null)
            {
                MoveSpawnPoints(battle.frontSpawnPoints, frontPositions);
                MoveSpawnPoints(battle.backSpawnPoints, backPositions);
            }
        }

        // 5. 씬 뷰에서 결과가 보이도록 미리보기 맵을 한 번 생성 (플레이하면 스테이지마다 다시 생성됨)
        foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>(true))
        {
            Undo.RegisterCompleteObjectUndo(tilemap, UNDO_NAME);
            tilemap.ClearAllTiles();
        }
        if (previewTheme != null) gen.GenerateMap(previewTheme);

        EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
        Debug.Log($"=== 던전 아이소메트릭 전환 완료! ===\n" +
                  $"테마 {themeCount}개에 마름모 바닥 타일 {tileCount}개 생성. 미리보기 테마: {(previewTheme != null ? previewTheme.name : "없음")}. 씬을 저장(Ctrl+S)하세요.");
    }

    // ═══════════════════════════════════════════
    // 되돌리기: 아이소메트릭 전환 이전의 사각(위에서 내려다보는) 시점으로 복구
    // 맵 생성기는 그리드가 사각이면 예전 방식(0x72 바닥 + 벽 + 장식)으로 동작합니다.
    // ═══════════════════════════════════════════
    [MenuItem("Tools/던전 사각 시점으로 되돌리기")]
    static void RevertToRectangle()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[DungeonIsometricSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        MapGenerator[] generators = FindGenerators();
        MapGenerator gen = generators.Length > 0 ? generators[0] : null;
        Grid grid = gen != null && gen.floorTilemap != null ? gen.floorTilemap.layoutGrid as Grid : null;
        if (grid == null)
        {
            Debug.LogError("[DungeonIsometricSetup] 씬에 MapGenerator/Grid가 없습니다. DungeonScene을 열고 다시 시도하세요.");
            return;
        }

        const string undoName = "던전 사각 시점으로 되돌리기";

        Undo.RecordObject(grid, undoName);
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        grid.cellSize   = new Vector3(1f, 1f, 0f);
        EditorUtility.SetDirty(grid);

        foreach (TilemapRenderer r in grid.GetComponentsInChildren<TilemapRenderer>(true))
        {
            Undo.RecordObject(r, undoName);
            r.sortOrder = TilemapRenderer.SortOrder.BottomLeft;
            EditorUtility.SetDirty(r);
        }

        // 미리보기로 깔아 둔 마름모 타일 제거 (원래 씬에는 타일이 없고, 플레이할 때 생성됨)
        foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>(true))
        {
            Undo.RegisterCompleteObjectUndo(tilemap, undoName);
            tilemap.ClearAllTiles();
        }

        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Undo.RecordObject(cam, undoName);
            cam.backgroundColor = rectCameraBackground;
            EditorUtility.SetDirty(cam);
        }

        GameObject ground = FindGround(gen.gameObject.scene);
        if (ground != null)
        {
            Undo.RecordObject(ground, undoName);
            ground.SetActive(true);
        }

        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        if (battle != null)
        {
            MoveSpawnPoints(battle.frontSpawnPoints, rectFrontPositions);
            MoveSpawnPoints(battle.backSpawnPoints, rectBackPositions);
        }

        EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
        Debug.Log("=== 던전 사각 시점으로 되돌리기 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }

    // 꺼져 있어도 찾을 수 있도록 씬 루트에서 직접 찾음
    static GameObject FindGround(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Ground" && root.GetComponent<SpriteRenderer>() != null) return root;
        return null;
    }

    static MapGenerator[] FindGenerators()
    {
        return Object.FindObjectsByType<MapGenerator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    static void MoveSpawnPoints(Transform[] points, Vector3[] positions)
    {
        if (points == null) return;
        for (int i = 0; i < points.Length && i < positions.Length; i++)
        {
            if (points[i] == null) continue;
            Undo.RecordObject(points[i], UNDO_NAME);
            points[i].position = positions[i];
            EditorUtility.SetDirty(points[i]);
        }
    }

    // ═══════════════════════════════════════════
    // 테마의 사각 바닥 타일을 45도 돌리고 세로로 절반 눌러 2:1 마름모 타일로 변환
    // (로비의 iso_gray 타일과 같은 방식). 만든 타일 수를 반환
    // ═══════════════════════════════════════════
    static int CreateIsoFloorTiles(MapThemeData theme)
    {
        if (theme.floorTiles == null || theme.floorTiles.Length == 0) return 0;

        string folder = $"{ISO_TILE_ROOT}/{theme.name}";
        EnsureFolder(folder);

        List<TileBase> isoTiles = new List<TileBase>();
        foreach (TileBase source in theme.floorTiles)
        {
            Tile sourceTile = source as Tile;
            Sprite sprite = sourceTile != null ? sourceTile.sprite : null;
            if (sprite == null)
            {
                Debug.LogWarning($"[DungeonIsometricSetup] '{theme.name}'의 바닥 타일 '{(source != null ? source.name : "null")}'에서 스프라이트를 읽을 수 없어 건너뜁니다.");
                continue;
            }

            string pngPath  = $"{folder}/iso_{source.name}.png";
            string tilePath = $"{folder}/iso_{source.name}.asset";

            Sprite isoSprite = CreateIsoSprite(sprite, pngPath);
            if (isoSprite == null) continue;

            Tile isoTile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (isoTile == null)
            {
                isoTile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(isoTile, tilePath);
            }
            isoTile.sprite = isoSprite;
            EditorUtility.SetDirty(isoTile);

            isoTiles.Add(isoTile);
        }

        Undo.RecordObject(theme, UNDO_NAME);
        theme.isoFloorTiles = isoTiles.ToArray();
        EditorUtility.SetDirty(theme);
        return isoTiles.Count;
    }

    static Sprite CreateIsoSprite(Sprite source, string pngPath)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source.texture);
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            Debug.LogWarning($"[DungeonIsometricSetup] 원본 텍스처 파일을 찾을 수 없습니다: {source.name}");
            return null;
        }

        // 임포트 설정(Read/Write)과 무관하게 픽셀을 읽기 위해 파일에서 직접 로드
        Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(sourcePath));

        // 아틀라스에 들어 있는 스프라이트도 처리할 수 있도록 스프라이트 영역만 사용
        Rect rect = source.rect;
        int srcW = Mathf.RoundToInt(rect.width);
        int srcH = Mathf.RoundToInt(rect.height);

        int w = srcW * 2;
        int h = srcH;
        Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                float u = (dx + 0.5f) / w - 0.5f;
                float v = (dy + 0.5f) / h - 0.5f;

                // 마름모 좌표 → 원본 사각 타일 좌표 (0~1)
                float a = u + v + 0.5f;
                float b = v - u + 0.5f;

                if (a < 0f || a >= 1f || b < 0f || b >= 1f)
                {
                    dst.SetPixel(dx, dy, clear);
                    continue;
                }

                int sx = Mathf.RoundToInt(rect.x) + Mathf.Min((int)(a * srcW), srcW - 1);
                int sy = Mathf.RoundToInt(rect.y) + Mathf.Min((int)(b * srcH), srcH - 1);
                dst.SetPixel(dx, dy, src.GetPixel(sx, sy));
            }
        }

        File.WriteAllBytes(pngPath, dst.EncodeToPNG());
        Object.DestroyImmediate(src);
        Object.DestroyImmediate(dst);

        AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null) return null;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = w;  // 마름모 가로 = 1월드유닛

        TextureImporterSettings texSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(texSettings);
        texSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(texSettings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
    }

    static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
