using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;

/// <summary>
/// [메뉴] Tools > 로비 타일맵 세팅
/// rect_gray 바닥 타일 4종으로 Tile 에셋을 생성하고,
/// Grid/Tilemap 오브젝트를 자동 세팅합니다.
/// </summary>
public class LobbyTilemapSetup : Editor
{
    const string FLOOR_TILE_FOLDER = "Assets/Art/Tiles/LobbyTileset/FloorTiles";
    const string ISO_TILE_FOLDER   = "Assets/Art/Tiles/LobbyTileset/IsoFloorTiles";
    const string RESOURCE_FOLDER   = "Assets/Resources/LobbyTiles";
    const int    TILE_SIZE         = 32;
    const int    PPU               = 32;  // 32px = 1월드유닛
    const int    FLOOR_TILE_COUNT  = 4;

    [MenuItem("Tools/로비 타일맵 세팅")]
    static void Setup()
    {
        // ── 1단계: 텍스처 임포트 설정 ──
        if (!ConfigureTextures())
        {
            Debug.LogError("[LobbyTilemapSetup] 텍스처 설정 실패!");
            return;
        }

        // ── 2단계: Tile 에셋 생성 ──
        int tileCount = CreateTileAssets();
        if (tileCount == 0)
        {
            Debug.LogError("[LobbyTilemapSetup] Tile 에셋 생성 실패!");
            return;
        }

        // ── 3단계: Resources 폴더에 타일 복사 ──
        CopyTilesToResources();

        // ── 4단계: Scene에 Grid + Tilemap 추가 ──
        SetupSceneObjects();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"=== 로비 타일맵 세팅 완료! ===\n" +
                  $"바닥 타일: {tileCount}종 생성 (rect_gray 0~3)\n" +
                  "벽 없음 — 검은 배경 사용\n" +
                  "씬을 저장(Ctrl+S)하세요.");
    }

    /// <summary>
    /// 열려 있는 씬의 LobbyMapGenerator로 타일을 씬에 구워, 플레이 전에도 타일맵이 보이게 합니다.
    /// </summary>
    [MenuItem("Tools/로비 타일맵 굽기")]
    static void Bake()
    {
        LobbyMapGenerator gen = Object.FindFirstObjectByType<LobbyMapGenerator>();
        if (gen == null)
        {
            Debug.LogError("[LobbyTilemapSetup] 씬에 LobbyMapGenerator가 없습니다. LobbyScene을 열고 다시 시도하세요.");
            return;
        }

        gen.BakeInEditor();
        Debug.Log("[LobbyTilemapSetup] 로비 타일맵 굽기 완료! 씬을 저장(Ctrl+S)하세요.");
    }

    // ═══════════════════════════════════════════
    // 아이소메트릭 전환: 마름모 타일 생성 + Grid 레이아웃 변경 + 굽기
    // ═══════════════════════════════════════════
    [MenuItem("Tools/로비 아이소메트릭 전환")]
    static void ConvertToIsometric()
    {
        LobbyMapGenerator gen = Object.FindFirstObjectByType<LobbyMapGenerator>();
        Grid grid = gen != null && gen.floorTilemap != null ? gen.floorTilemap.layoutGrid as Grid : null;
        if (grid == null)
        {
            Debug.LogError("[LobbyTilemapSetup] 씬에 LobbyMapGenerator/Grid가 없습니다. LobbyScene을 열고 다시 시도하세요.");
            return;
        }

        int tileCount = CreateIsoTileAssets();
        if (tileCount == 0)
        {
            Debug.LogError("[LobbyTilemapSetup] 아이소메트릭 타일 생성 실패!");
            return;
        }

        // 2:1 마름모 (가로 1유닛, 세로 0.5유닛)
        Undo.RecordObject(grid, "로비 아이소메트릭 전환");
        grid.cellLayout = GridLayout.CellLayout.Isometric;
        grid.cellSize   = new Vector3(1f, 0.5f, 1f);
        EditorUtility.SetDirty(grid);

        // 아이소메트릭은 뒤(위쪽) 타일부터 그려야 겹침 순서가 맞음
        foreach (TilemapRenderer r in grid.GetComponentsInChildren<TilemapRenderer>(true))
        {
            Undo.RecordObject(r, "로비 아이소메트릭 전환");
            r.sortOrder = TilemapRenderer.SortOrder.TopRight;
            EditorUtility.SetDirty(r);
        }

        AssetDatabase.SaveAssets();
        gen.BakeInEditor();

        Debug.Log($"=== 로비 아이소메트릭 전환 완료! ===\n" +
                  $"마름모 바닥 타일: {tileCount}종 생성 (iso_gray 0~3)\n" +
                  "씬을 저장(Ctrl+S)하세요.");
    }

    /// <summary>
    /// rect_gray 사각 타일을 45도 돌리고 세로로 절반 눌러 2:1 마름모 타일(iso_gray)로 변환합니다.
    /// </summary>
    static int CreateIsoTileAssets()
    {
        EnsureFolder(ISO_TILE_FOLDER);
        EnsureFolder(RESOURCE_FOLDER);

        int count = 0;
        for (int i = 0; i < FLOOR_TILE_COUNT; i++)
        {
            string srcPath  = $"{FLOOR_TILE_FOLDER}/rect_gray{i}.png";
            string pngPath  = $"{ISO_TILE_FOLDER}/iso_gray{i}.png";
            string tilePath = $"{ISO_TILE_FOLDER}/iso_gray{i}.asset";

            if (!File.Exists(srcPath))
            {
                Debug.LogWarning($"[LobbyTilemapSetup] 원본 타일을 찾을 수 없습니다: {srcPath}");
                continue;
            }

            // 임포트 설정(Read/Write)과 무관하게 픽셀을 읽기 위해 파일에서 직접 로드
            Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(srcPath));

            int w = src.width * 2;
            int h = src.height;
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

                    dst.SetPixel(dx, dy, src.GetPixel((int)(a * src.width), (int)(b * src.height)));
                }
            }

            File.WriteAllBytes(pngPath, dst.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
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

            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sp == null)
            {
                Debug.LogWarning($"[LobbyTilemapSetup] 스프라이트 로드 실패: {pngPath}");
                continue;
            }

            Tile t = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(t, tilePath);
            }
            t.sprite = sp;
            EditorUtility.SetDirty(t);

            // 빌드용 Resources 복사
            string resPath = $"{RESOURCE_FOLDER}/iso_gray{i}.asset";
            AssetDatabase.DeleteAsset(resPath);
            AssetDatabase.SaveAssetIfDirty(t);
            AssetDatabase.CopyAsset(tilePath, resPath);

            count++;
        }
        return count;
    }

    // ═══════════════════════════════════════════
    // 1단계: 개별 PNG 임포트 설정 (32x32, PPU 32, Point)
    // ═══════════════════════════════════════════
    static bool ConfigureTextures()
    {
        bool allOk = true;
        for (int i = 0; i < FLOOR_TILE_COUNT; i++)
        {
            string path = $"{FLOOR_TILE_FOLDER}/rect_gray{i}.png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[LobbyTilemapSetup] {path}를 찾을 수 없습니다!");
                allOk = false;
                continue;
            }

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PPU;

            TextureImporterSettings texSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(texSettings);
            texSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(texSettings);

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
        return allOk;
    }

    // ═══════════════════════════════════════════
    // 2단계: Tile 에셋 생성 (각 PNG → .asset)
    // ═══════════════════════════════════════════
    static int CreateTileAssets()
    {
        int count = 0;
        for (int i = 0; i < FLOOR_TILE_COUNT; i++)
        {
            string pngPath  = $"{FLOOR_TILE_FOLDER}/rect_gray{i}.png";
            string tilePath = $"{FLOOR_TILE_FOLDER}/rect_gray{i}.asset";

            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sp == null)
            {
                Debug.LogWarning($"[LobbyTilemapSetup] 스프라이트 로드 실패: {pngPath}");
                continue;
            }

            Tile t = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(t, tilePath);
            }
            t.sprite = sp;
            EditorUtility.SetDirty(t);
            count++;
        }
        return count;
    }

    // ═══════════════════════════════════════════
    // 3단계: Resources 폴더에 타일 복사 (빌드용)
    // ═══════════════════════════════════════════
    static void CopyTilesToResources()
    {
        EnsureFolder(RESOURCE_FOLDER);

        int copied = 0;
        for (int i = 0; i < FLOOR_TILE_COUNT; i++)
        {
            string srcPath = $"{FLOOR_TILE_FOLDER}/rect_gray{i}.asset";
            string dstPath = $"{RESOURCE_FOLDER}/rect_gray{i}.asset";

            if (!AssetDatabase.CopyAsset(srcPath, dstPath))
            {
                AssetDatabase.DeleteAsset(dstPath);
                AssetDatabase.CopyAsset(srcPath, dstPath);
            }
            copied++;
        }

        Debug.Log($"[LobbyTilemapSetup] Resources에 타일 {copied}개 복사 완료");
    }

    // ═══════════════════════════════════════════
    // 4단계: Scene에 Grid + Tilemap + 검은 배경 추가
    // ═══════════════════════════════════════════
    static void SetupSceneObjects()
    {
        // 이미 LobbyMapGenerator가 있으면 검은 배경만 추가
        LobbyMapGenerator existingGen = Object.FindFirstObjectByType<LobbyMapGenerator>();
        if (existingGen != null)
        {
            // 카메라 배경색 검은색으로 설정
            SetCameraBackgroundBlack();
            DisableBackgroundImage();
            return;
        }

        // Grid 오브젝트 생성
        GameObject gridGO = new GameObject("Grid_Lobby");
        Grid grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(1, 1, 0);

        // LobbyMapGenerator 컴포넌트 추가
        LobbyMapGenerator mapGen = gridGO.AddComponent<LobbyMapGenerator>();

        // Floor 타일맵 (Order: -10, 맨 뒤)
        GameObject floorGO = new GameObject("Tilemap_Floor");
        floorGO.transform.SetParent(gridGO.transform, false);
        Tilemap floorTM = floorGO.AddComponent<Tilemap>();
        TilemapRenderer floorR = floorGO.AddComponent<TilemapRenderer>();
        floorR.sortingOrder = -10;
        mapGen.floorTilemap = floorTM;

        // Wall 타일맵 (사용하지 않지만 호환성 유지)
        GameObject wallGO = new GameObject("Tilemap_Wall");
        wallGO.transform.SetParent(gridGO.transform, false);
        Tilemap wallTM = wallGO.AddComponent<Tilemap>();
        TilemapRenderer wallR = wallGO.AddComponent<TilemapRenderer>();
        wallR.sortingOrder = -9;
        mapGen.wallTilemap = wallTM;

        // Decoration 타일맵
        GameObject decoGO = new GameObject("Tilemap_Decor");
        decoGO.transform.SetParent(gridGO.transform, false);
        Tilemap decoTM = decoGO.AddComponent<Tilemap>();
        TilemapRenderer decoR = decoGO.AddComponent<TilemapRenderer>();
        decoR.sortingOrder = -8;
        mapGen.decorTilemap = decoTM;

        // 카메라 배경색을 검은색으로 설정
        SetCameraBackgroundBlack();

        // BackGround Image 비활성화 시도
        DisableBackgroundImage();

        EditorUtility.SetDirty(gridGO);
        EditorUtility.SetDirty(mapGen);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[LobbyTilemapSetup] Grid_Lobby 생성 완료!\n" +
                  "  ├─ Tilemap_Floor  (Order: -10)\n" +
                  "  ├─ Tilemap_Wall   (Order: -9, 미사용)\n" +
                  "  └─ Tilemap_Decor  (Order: -8)");
    }

    /// <summary>
    /// 메인 카메라의 배경색을 검은색으로 설정합니다.
    /// </summary>
    static void SetCameraBackgroundBlack()
    {
        Camera cam = Camera.main;
        if (cam == null)
            cam = Object.FindFirstObjectByType<Camera>();

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            EditorUtility.SetDirty(cam);
            Debug.Log("[LobbyTilemapSetup] 카메라 배경색을 검은색으로 설정 완료");
        }
        else
        {
            Debug.LogWarning("[LobbyTilemapSetup] 카메라를 찾을 수 없습니다. 수동으로 배경색을 검은색으로 설정해주세요.");
        }
    }

    static void DisableBackgroundImage()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform bg = canvas.transform.Find("BackGround Image");
            if (bg != null)
            {
                bg.gameObject.SetActive(false);
                EditorUtility.SetDirty(bg.gameObject);
                Debug.Log("[LobbyTilemapSetup] 'BackGround Image' 비활성화 완료");
                return;
            }
        }
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
