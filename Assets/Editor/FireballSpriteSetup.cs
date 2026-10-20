using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// [메뉴] Tools > 화염구 스프라이트 만들기
/// 16x16 도트 화염구 스프라이트를 만들어 화염구 프리팹(Prefab_Fireball)에 적용합니다.
/// 화염구는 날아가는 방향으로 회전하므로, 그림은 오른쪽(+x)을 향하도록 그려져 있습니다.
/// 임포트 설정은 프로젝트 도트 규칙(PPU 16, Point 필터, 무압축)을 따릅니다.
/// 그림을 바꾸려면 아래 Pixels 배열을 고치고 다시 실행하거나, 만들어진 PNG를 직접 교체하세요.
/// </summary>
public class FireballSpriteSetup : Editor
{
    const string SPRITE_FOLDER = "Assets/Art/Effects";
    const string SPRITE_PATH   = SPRITE_FOLDER + "/fireball.png";
    const string PREFAB_PATH   = "Assets/Prefabs/Objects/Prefab_Fireball.prefab";
    const int    PIXELS_PER_UNIT = 16;

    // . 투명 / r 붉은 테두리 / o 주황 / y 노랑 / w 흰 중심   (위에서 아래로, 오른쪽이 진행 방향)
    static readonly string[] Pixels =
    {
        "................",
        "................",
        "..........rrr...",
        ".......rrrooorr.",
        ".....rrooooyyoor",
        "...rrooooyyyyyor",
        ".rroooyyyywwwyyo",
        "rrooyyyywwwwwyyo",
        "rrooyyyywwwwwyyo",
        ".rroooyyyywwwyyo",
        "...rrooooyyyyyor",
        ".....rrooooyyoor",
        ".......rrrooorr.",
        "..........rrr...",
        "................",
        "................",
    };

    static Color32 ColorOf(char c)
    {
        switch (c)
        {
            case 'r': return new Color32(178, 34, 24, 255);
            case 'o': return new Color32(240, 120, 24, 255);
            case 'y': return new Color32(255, 214, 64, 255);
            case 'w': return new Color32(255, 250, 220, 255);
            default:  return new Color32(0, 0, 0, 0);
        }
    }

    [MenuItem("Tools/화염구 스프라이트 만들기")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[FireballSpriteSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        Sprite sprite = CreateSprite();
        if (sprite == null)
        {
            Debug.LogError("[FireballSpriteSetup] 스프라이트 생성 실패");
            return;
        }

        if (!File.Exists(PREFAB_PATH))
        {
            Debug.LogError($"[FireballSpriteSetup] 화염구 프리팹을 찾을 수 없습니다: {PREFAB_PATH}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();

            renderer.sprite = sprite;
            renderer.color = Color.white; // 예전 임시 원에 입혀 둔 붉은 색조 제거 (그림 색 그대로 표시)
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"=== 화염구 스프라이트 적용 완료! ===\n{SPRITE_PATH} → {PREFAB_PATH}");
    }

    static Sprite CreateSprite()
    {
        if (!AssetDatabase.IsValidFolder(SPRITE_FOLDER))
            AssetDatabase.CreateFolder("Assets/Art", "Effects");

        int height = Pixels.Length;
        int width = Pixels[0].Length;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int row = 0; row < height; row++)
        {
            string line = Pixels[row];
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, height - 1 - row, ColorOf(x < line.Length ? line[x] : '.')); // 텍스처는 아래에서 위로 채움
        }

        File.WriteAllBytes(SPRITE_PATH, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SPRITE_PATH, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(SPRITE_PATH) as TextureImporter;
        if (importer == null) return null;

        importer.textureType         = TextureImporterType.Sprite;
        importer.spriteImportMode    = SpriteImportMode.Single;
        importer.filterMode          = FilterMode.Point;
        importer.textureCompression  = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled       = false;
        importer.spritePixelsPerUnit = PIXELS_PER_UNIT;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SPRITE_PATH);
    }
}
