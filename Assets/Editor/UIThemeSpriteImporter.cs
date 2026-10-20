using UnityEditor;
using UnityEngine;

// Resources/UI/Theme 아래 픽셀 UI 스프라이트의 임포트 설정 (Tools/ui_theme_gen.py로 생성)
// 도트가 번지지 않도록 Point·무압축, 9-슬라이스 테두리는 파일 이름으로 정함
public class UIThemeSpriteImporter : AssetPostprocessor
{
    const string Folder = "Assets/Resources/UI/"; // Theme(창틀·버튼·아이콘), Regions(의뢰 게시판 지역 그림)

    // 생성기로 PNG를 다시 만든 뒤, 또는 설정이 빠졌을 때
    [MenuItem("Tools/UI 테마 스프라이트 다시 가져오기")]
    static void ReimportAll()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("[UITheme] 테마 스프라이트를 다시 가져왔습니다.");
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f; // 캔버스 기준(100)과 같게 두고, 화면 배율은 Image.pixelsPerUnitMultiplier로 정함
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // 9-슬라이스는 FullRect여야 함
        settings.spriteBorder = Border(System.IO.Path.GetFileNameWithoutExtension(assetPath));
        importer.SetTextureSettings(settings);
    }

    // (왼쪽, 아래, 오른쪽, 위) 테두리 픽셀 수
    static Vector4 Border(string name)
    {
        if (name.StartsWith("ui_button_")) return new Vector4(5, 5, 5, 5);
        switch (name)
        {
            case "ui_panel":       return new Vector4(8, 8, 8, 8);
            case "ui_panel_small": return new Vector4(4, 4, 4, 4);
            case "ui_inset":       return new Vector4(3, 3, 3, 3);
            case "ui_knob":        return new Vector4(3, 3, 3, 3);
            case "ui_hud_bar":     return new Vector4(0, 4, 0, 2);
            default:               return Vector4.zero;
        }
    }
}
