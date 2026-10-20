using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 던전 지역별 바닥 타일 적용: Assets/Art/Tiles/Dungeon/{테마}/floor_*.png (Tools/dungeon_tile_gen.py로 생성)
//  - 그림 가져오기 설정 (64x32 마름모, PPU 64 → 한 칸 = 1유닛, 점 필터, 압축 없음)
//  - 그림마다 Tile 에셋
//  - Resources/DungeonThemes/{테마}.asset (MapThemeData): 지역 이름을 적어 두면 QuestGenerator가 그 지역 의뢰에 씀
// 바닥 가장자리 아래 절벽 단면(edge_*)도 같은 폴더에서 가져와 테마에 넣음
// 수수한 바닥(번호 0~3)은 장식 있는 바닥(4~)보다 PlainWeight배 자주 깔리도록 목록에 여러 번 넣음. 여러 번 실행해도 결과는 같음
public static class DungeonRegionTileSetup
{
    const string ArtDir = "Assets/Art/Tiles/Dungeon";
    const string ThemeDir = "Assets/Resources/DungeonThemes";
    const int PlainCount = 4;
    const int PlainWeight = 3;

    static readonly (string folder, string themeName, string region)[] Themes =
    {
        ("Prairie", "초원", "초원"),
        ("Forest", "숲", "숲"),
        ("Crypt", "지하 묘지", "지하 묘지"),
        ("OrcVillage", "오크 부락", "오크 부락"),
        ("DragonNest", "드래곤 둥지", "드래곤 둥지"),
    };

    // 절벽 단면 edge_{L|R|B}_*.png (64x48): 기준점을 마름모 중심(위에서 16px)에 맞춰 바닥 칸과 겹치게
    static TileBase[] EdgeTiles(string dir, string side)
    {
        var list = new List<TileBase>();
        foreach (string png in Directory.GetFiles(dir, $"edge_{side}_*.png"))
        {
            string path = png.Replace('\\', '/');
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 64;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            TextureImporterSettings set = new TextureImporterSettings();
            ti.ReadTextureSettings(set);
            set.spriteAlignment = (int)SpriteAlignment.Custom;
            set.spritePivot = new Vector2(0.5f, 32f / 48f);
            ti.SetTextureSettings(set);
            ti.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            string tilePath = Path.ChangeExtension(path, ".asset");
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }
            tile.sprite = sprite;
            EditorUtility.SetDirty(tile);
            list.Add(tile);
        }
        return list.ToArray();
    }

    [MenuItem("Tools/던전 지역 타일 적용")]
    static void Run()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(ThemeDir)) AssetDatabase.CreateFolder("Assets/Resources", "DungeonThemes");

        int themes = 0;
        foreach (var (folder, themeName, region) in Themes)
        {
            string dir = $"{ArtDir}/{folder}";
            if (!AssetDatabase.IsValidFolder(dir)) { Debug.LogWarning($"[DungeonRegionTileSetup] {dir} 없음 — Tools/dungeon_tile_gen.py를 먼저 실행하세요."); continue; }

            var tiles = new List<(int index, TileBase tile)>();
            foreach (string png in Directory.GetFiles(dir, "floor_*.png"))
            {
                string path = png.Replace('\\', '/');
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 64;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                string tilePath = Path.ChangeExtension(path, ".asset");
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, tilePath);
                }
                tile.sprite = sprite;
                EditorUtility.SetDirty(tile);
                int.TryParse(Path.GetFileNameWithoutExtension(path).Substring("floor_".Length), out int index);
                tiles.Add((index, tile));
            }
            tiles.Sort((a, b) => a.index.CompareTo(b.index));

            var weighted = new List<TileBase>();
            foreach (var (index, tile) in tiles)
                for (int k = 0; k < (index < PlainCount ? PlainWeight : 1); k++) weighted.Add(tile);

            string themePath = $"{ThemeDir}/{folder}.asset";
            MapThemeData theme = AssetDatabase.LoadAssetAtPath<MapThemeData>(themePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<MapThemeData>();
                AssetDatabase.CreateAsset(theme, themePath);
            }
            theme.themeName = themeName;
            theme.regionName = region;
            theme.isoFloorTiles = weighted.ToArray();
            theme.floorTiles = weighted.ToArray();
            theme.decorationTiles = new TileBase[0];
            theme.isoEdgeLeft = EdgeTiles(dir, "L");
            theme.isoEdgeRight = EdgeTiles(dir, "R");
            theme.isoEdgeBoth = EdgeTiles(dir, "B");
            EditorUtility.SetDirty(theme);
            themes++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[DungeonRegionTileSetup] 지역 바닥 테마 {themes}개 적용 완료 ({ThemeDir})");
    }
}
