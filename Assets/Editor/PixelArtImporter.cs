using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 생성기(Tools/lobby_art_gen.py, Tools/town_art_gen.py)가 만든 도트 그림의 임포트 설정
//  - 도트가 번지지 않도록 Point·무압축, PPU 64 (바닥 한 칸 = 64x32px)
//  - 피벗은 같은 폴더의 pivots.txt (이름 x y) 값 → 오브젝트 위치 = 바닥에 닿는 점
public class PixelArtImporter : AssetPostprocessor
{
    static readonly string[] Folders = { "Assets/Art/Lobby/Pixel/", "Assets/Art/Town/", "Assets/Art/Title/", "Assets/Art/Creation/", "Assets/Art/Dungeon/Campfire/" };

    void OnPreprocessTexture()
    {
        string folder = null;
        foreach (string f in Folders) if (assetPath.StartsWith(f)) folder = f;
        if (folder == null) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        if (ReadPivots(folder).TryGetValue(name, out Vector2 pivot))
        {
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
        }
        else settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
    }

    static Dictionary<string, Vector2> ReadPivots(string folder)
    {
        var result = new Dictionary<string, Vector2>();
        string path = Path.Combine(folder, "pivots.txt");
        if (!File.Exists(path)) return result;
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.StartsWith("#")) continue;
            string[] parts = line.Split(' ');
            if (parts.Length < 3) continue;
            if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                result[parts[0]] = new Vector2(x, y);
        }
        return result;
    }

    [MenuItem("Tools/도트 그림 다시 가져오기 (로비·거리)")]
    static void ReimportAll()
    {
        foreach (string f in Folders)
        {
            if (!AssetDatabase.IsValidFolder(f.TrimEnd('/'))) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { f.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("[PixelArtImporter] 도트 그림을 다시 가져왔습니다.");
    }
}
