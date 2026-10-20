using UnityEngine;
using UnityEditor;

/// <summary>
/// Tools > Fix Tile PPU 로 실행
/// 선택된 폴더 안의 모든 스프라이트 PPU를 16으로 강제 변경합니다.
/// </summary>
public class FixTilePPU
{
    [MenuItem("Tools/Fix Tile PPU (선택 폴더)")]
    static void FixSelectedFolder()
    {
        Object selected = Selection.activeObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("오류", "Project 창에서 frames 폴더를 선택하고 실행하세요.", "확인");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(selected);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("오류", "폴더를 선택해주세요.", "확인");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
        int count = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;

                ti.textureType         = TextureImporterType.Sprite;
                ti.spriteImportMode    = SpriteImportMode.Single;
                ti.filterMode          = FilterMode.Point;
                ti.textureCompression  = TextureImporterCompression.Uncompressed;
                ti.spritePixelsPerUnit = 16;

                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();
                count++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("완료", $"{count}개 스프라이트 PPU=16 적용 완료!", "확인");
        Debug.Log($"[FixTilePPU] {count}개 처리 완료 (폴더: {folderPath})");
    }
}
