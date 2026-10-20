using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Window > GuildManager > Tileset Importer 에서 실행
/// 0x72 Dungeon Tileset의 frames 폴더를 자동으로 읽어 MapThemeData를 생성합니다.
/// </summary>
public class TilesetImporterTool : EditorWindow
{
    // ── 탭 ──
    int tab = 0;
    readonly string[] tabNames = { "frames 자동 매핑 (0x72)", "스프라이트시트 슬라이싱" };

    // ── 탭 0: frames 자동 매핑 ──
    DefaultAsset framesFolder;
    string themeDataFolder = "Assets/Art/Tiles/Themes";
    string themeName       = "DungeonTheme";

    // ── 탭 1: 스프라이트시트 슬라이싱 ──
    Texture2D targetTexture;
    int tileSize     = 16;
    string outputFolder = "Assets/Art/Tiles/Generated";

    Vector2 scrollPos;

    [MenuItem("Window/GuildManager/Tileset Importer")]
    public static void ShowWindow()
    {
        GetWindow<TilesetImporterTool>("Tileset Importer");
    }

    void OnGUI()
    {
        GUILayout.Label("═══ Tileset 자동 임포터 ═══", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        tab = GUILayout.Toolbar(tab, tabNames);
        EditorGUILayout.Space(8);

        if (tab == 0) DrawTab_FramesAutoMap();
        else          DrawTab_SheetSlicer();
    }

    // ══════════════════════════════════════════════
    // 탭 0 — frames 폴더 자동 매핑 (0x72 전용)
    // ══════════════════════════════════════════════
    void DrawTab_FramesAutoMap()
    {
        GUILayout.Label("0x72 frames 폴더 → MapThemeData 자동 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "0x72_DungeonTilesetII의 frames 폴더를 선택하면\n" +
            "파일 이름 기반으로 MapThemeData 슬롯을 자동 연결합니다.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        framesFolder    = (DefaultAsset)EditorGUILayout.ObjectField("frames 폴더", framesFolder, typeof(DefaultAsset), false);
        themeDataFolder = EditorGUILayout.TextField("출력 폴더", themeDataFolder);
        themeName       = EditorGUILayout.TextField("테마 이름", themeName);

        EditorGUILayout.Space(8);

        GUI.enabled = framesFolder != null;
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("자동 매핑 실행", GUILayout.Height(40)))
            RunAutoMap();
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "[매핑 규칙]\n" +
            "floor_1~8       → floorTiles[]\n" +
            "wall_top_mid    → wallTop\n" +
            "wall_mid        → wallSolid\n" +
            "wall_left       → wallLeft\n" +
            "wall_right      → wallRight\n" +
            "wall_top_left   → wallCornerTL\n" +
            "wall_top_right  → wallCornerTR\n" +
            "wall_outer_front_left  → wallCornerBL\n" +
            "wall_outer_front_right → wallCornerBR\n" +
            "wall_outer_mid_left    → wallBottom\n" +
            "column_wall, wall_goo, wall_banner_* → decorationTiles[]",
            MessageType.None);
    }

    void RunAutoMap()
    {
        string folderPath = AssetDatabase.GetAssetPath(framesFolder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("오류", "유효한 폴더를 선택하세요.", "확인");
            return;
        }

        // frames 폴더의 모든 PNG 경로 수집
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        // 이름 → 스프라이트 딕셔너리 구성
        Dictionary<string, Sprite> nameToSprite = new Dictionary<string, Sprite>();
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path    = AssetDatabase.GUIDToAssetPath(guid);
            string fname   = Path.GetFileNameWithoutExtension(path);

            // 임포트 설정 강제 통일 (PPU=16, Point filter)
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                bool needReimport = ti.textureType         != TextureImporterType.Sprite
                                 || ti.filterMode          != FilterMode.Point
                                 || ti.spritePixelsPerUnit != 16;
                if (needReimport)
                {
                    ti.textureType         = TextureImporterType.Sprite;
                    ti.spriteImportMode    = SpriteImportMode.Single;
                    ti.filterMode          = FilterMode.Point;
                    ti.textureCompression  = TextureImporterCompression.Uncompressed;
                    ti.spritePixelsPerUnit = 16;
                    EditorUtility.SetDirty(ti);
                    ti.SaveAndReimport();
                    fixedCount++;
                }
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                nameToSprite[fname] = sprite;
        }

        if (fixedCount > 0)
            Debug.Log($"[TilesetImporter] {fixedCount}개 텍스처 임포트 설정 업데이트");

        // ── Tile 에셋 생성 ──
        EnsureFolder(outputFolder + "/" + themeName);
        string tileFolder = outputFolder + "/" + themeName;

        Tile GetOrCreateTile(string fname)
        {
            if (!nameToSprite.TryGetValue(fname, out Sprite sp)) return null;
            string tilePath = $"{tileFolder}/{fname}.asset";
            Tile t = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (t == null)
            {
                t = CreateInstance<Tile>();
                AssetDatabase.CreateAsset(t, tilePath);
            }
            // 항상 최신 스프라이트로 갱신 (PPU 변경 후 ID가 바뀌어도 반영)
            t.sprite = sp;
            EditorUtility.SetDirty(t);
            return t;
        }

        // ── MapThemeData 생성 ──
        EnsureFolder(themeDataFolder);
        string assetPath = $"{themeDataFolder}/{themeName}.asset";
        MapThemeData theme = AssetDatabase.LoadAssetAtPath<MapThemeData>(assetPath)
                          ?? CreateInstance<MapThemeData>();
        theme.themeName = themeName;

        // 바닥 타일 (floor_1 ~ floor_8)
        List<TileBase> floorList = new List<TileBase>();
        for (int i = 1; i <= 8; i++)
        {
            Tile t = GetOrCreateTile($"floor_{i}");
            if (t != null) floorList.Add(t);
        }
        theme.floorTiles = floorList.ToArray();

        // 벽 타일 — 방향별
        theme.wallTop      = GetOrCreateTile("wall_top_mid");
        theme.wallBottom   = GetOrCreateTile("wall_outer_mid_left");   // 아래쪽 벽면
        theme.wallLeft     = GetOrCreateTile("wall_left");
        theme.wallRight    = GetOrCreateTile("wall_right");
        theme.wallCornerTL = GetOrCreateTile("wall_top_left");
        theme.wallCornerTR = GetOrCreateTile("wall_top_right");
        theme.wallCornerBL = GetOrCreateTile("wall_outer_front_left");
        theme.wallCornerBR = GetOrCreateTile("wall_outer_front_right");
        theme.wallSolid    = GetOrCreateTile("wall_mid");

        // 장식 타일
        List<TileBase> decoList = new List<TileBase>();
        void AddDeco(string fname) { Tile t = GetOrCreateTile(fname); if (t != null) decoList.Add(t); }

        AddDeco("column_wall");
        AddDeco("wall_goo");
        AddDeco("wall_goo_base");
        AddDeco("wall_hole_1");
        AddDeco("wall_hole_2");
        AddDeco("wall_banner_blue");
        AddDeco("wall_banner_red");
        AddDeco("wall_banner_green");
        AddDeco("wall_banner_yellow");

        theme.decorationTiles   = decoList.ToArray();
        theme.decorationDensity = 0.06f;

        // 저장
        if (AssetDatabase.LoadAssetAtPath<MapThemeData>(assetPath) == null)
            AssetDatabase.CreateAsset(theme, assetPath);
        else
            EditorUtility.SetDirty(theme);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = theme;
        EditorGUIUtility.PingObject(theme);

        int mapped = (theme.floorTiles?.Length ?? 0) +
                     (theme.decorationTiles?.Length ?? 0) +
                     (theme.wallSolid != null ? 1 : 0);

        EditorUtility.DisplayDialog(
            "완료",
            $"MapThemeData 생성 완료!\n\n" +
            $"• 바닥 타일: {theme.floorTiles?.Length ?? 0}개\n" +
            $"• 장식 타일: {theme.decorationTiles?.Length ?? 0}개\n" +
            $"• 벽 타일: 자동 연결\n\n" +
            $"위치: {assetPath}\n\n" +
            "Inspector에서 벽 슬롯이 비어있으면 직접 연결해주세요.",
            "확인");
    }

    // ══════════════════════════════════════════════
    // 탭 1 — 스프라이트시트 슬라이서 (atlas PNG용)
    // ══════════════════════════════════════════════
    void DrawTab_SheetSlicer()
    {
        GUILayout.Label("스프라이트시트 슬라이싱", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "atlas_floor-16x16.png 또는 atlas_walls_low-16x16.png 같은\n" +
            "스프라이트시트 PNG를 자동으로 슬라이싱하고 Tile 에셋을 생성합니다.",
            MessageType.Info);

        EditorGUILayout.Space(4);
        targetTexture = (Texture2D)EditorGUILayout.ObjectField("PNG 파일", targetTexture, typeof(Texture2D), false);
        tileSize      = EditorGUILayout.IntField("타일 크기 (px)", tileSize);
        outputFolder  = EditorGUILayout.TextField("Tile 출력 폴더", outputFolder);

        EditorGUILayout.Space(8);

        GUI.enabled = targetTexture != null;
        if (GUILayout.Button("① 슬라이싱 설정", GUILayout.Height(30)))
            SetupTextureImporter();

        EditorGUILayout.Space(4);

        if (GUILayout.Button("② Tile 에셋 생성", GUILayout.Height(30)))
            CreateTileAssets();

        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("한 번에 실행 (①+②)", GUILayout.Height(40)))
        {
            SetupTextureImporter();
            CreateTileAssets();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
    }

    void SetupTextureImporter()
    {
        string texPath = AssetDatabase.GetAssetPath(targetTexture);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (importer == null) return;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Multiple;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = tileSize;

        int cols = targetTexture.width  / tileSize;
        int rows = targetTexture.height / tileSize;
        string baseName = Path.GetFileNameWithoutExtension(texPath);

        List<SpriteMetaData> metas = new List<SpriteMetaData>();
        int idx = 0;
        for (int row = rows - 1; row >= 0; row--)
        {
            for (int col = 0; col < cols; col++)
            {
                metas.Add(new SpriteMetaData
                {
                    name      = $"{baseName}_{idx:D3}",
                    rect      = new Rect(col * tileSize, row * tileSize, tileSize, tileSize),
                    pivot     = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                });
                idx++;
            }
        }

        importer.spritesheet = metas.ToArray();
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        Debug.Log($"[TilesetImporter] 슬라이싱 완료: {cols}x{rows} = {idx}개");
        EditorUtility.DisplayDialog("완료", $"슬라이싱 완료! ({idx}개 스프라이트)", "확인");
    }

    void CreateTileAssets()
    {
        string texPath = AssetDatabase.GetAssetPath(targetTexture);
        Object[] sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(texPath);
        if (sprites == null || sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("오류", "슬라이싱된 스프라이트가 없습니다. 먼저 ①을 실행하세요.", "확인");
            return;
        }

        string folder = outputFolder + "/" + Path.GetFileNameWithoutExtension(texPath);
        EnsureFolder(folder);

        int count = 0;
        foreach (Object obj in sprites)
        {
            Sprite sp = obj as Sprite;
            if (sp == null) continue;

            string tilePath = $"{folder}/{sp.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Tile>(tilePath) != null) continue;

            Tile t = CreateInstance<Tile>();
            t.sprite = sp;
            AssetDatabase.CreateAsset(t, tilePath);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TilesetImporter] Tile 에셋 {count}개 생성 → {folder}");
        EditorUtility.DisplayDialog("완료", $"Tile 에셋 {count}개 생성!\n위치: {folder}", "확인");
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
