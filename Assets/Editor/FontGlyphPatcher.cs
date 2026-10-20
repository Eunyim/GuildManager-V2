using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// 둥근모 폰트 에셋(정적 아틀라스)에 빠진 글자를 원본 TTF에서 추가
// 아틀라스를 만들 때 범위(32-126, 한글, 자모, 8200-9900)에 들지 않은 글자: 가운뎃점(·) 등
public static class FontGlyphPatcher
{
    const string FontAssetPath = "Assets/Fonts/DungGeunMo SDF.asset";
    const string SourceFontPath = "Assets/Fonts/DungGeunMo.ttf";
    const string Characters = "·×÷°±";

    [MenuItem("Tools/폰트에 빠진 글자 추가 (·×÷°±)")]
    static void Patch()
    {
        TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (fontAsset == null || source == null) { Debug.LogError("[FontPatch] 폰트 에셋이나 TTF를 찾지 못했습니다."); return; }

        // 아틀라스 텍스처를 쓸 수 있어야 글자를 더 그릴 수 있음
        Texture2D atlas = fontAsset.atlasTexture;
        string atlasPath = AssetDatabase.GetAssetPath(atlas);
        Debug.Log($"[FontPatch] 아틀라스 {atlas.name} readable={atlas.isReadable} ({atlasPath})");

        SerializedObject so = new SerializedObject(fontAsset);
        SerializedProperty src = so.FindProperty("m_SourceFontFile");
        SerializedProperty srcEditor = so.FindProperty("m_SourceFontFile_EditorRef");
        if (src != null) src.objectReferenceValue = source;
        if (srcEditor != null) srcEditor.objectReferenceValue = source;
        so.ApplyModifiedPropertiesWithoutUndo();

        AtlasPopulationMode before = fontAsset.atlasPopulationMode;
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        bool ok = fontAsset.TryAddCharacters(Characters, out string missing);
        fontAsset.atlasPopulationMode = before;

        // 정적으로 되돌릴 때 원본 폰트 참조는 빼 둠 (빌드에 TTF가 끌려 들어가지 않도록)
        so.Update();
        if (src != null) { src.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FontPatch] 결과 ok={ok}, 못 넣은 글자='{missing}'");
    }
}
