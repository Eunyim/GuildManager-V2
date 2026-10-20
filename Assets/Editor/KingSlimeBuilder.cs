using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

/// <summary>
/// [메뉴] Tools > 킹 슬라임 프리팹 만들기
/// 보스 킹 슬라임 전용 프리팹(Enemy_KingSlime)을 만듭니다.
///  - 그림: 일반 슬라임 그림(swampy_anim_f0)의 비어 있는 위쪽 3줄에 금관을 얹은 것. 몸은 일반 슬라임과 픽셀이 똑같음
///  - 처음부터 킹 슬라임 설정(isKing)이 켜져 있고, 쓰러지면 일반 슬라임(Enemy_Slime)으로 분열
///  - 로비 씬 QuestGenerator의 보스 프리팹(bossPrefab)에 연결 → 모든 보스방이 이 프리팹을 씀
/// 크기는 예전처럼 보스방에서 BattleManager가 "모험가 키의 N배"로 키웁니다. 다시 실행해도 안전합니다.
/// </summary>
public class KingSlimeBuilder : Editor
{
    const string FRAMES       = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames";
    const string SLIME_PREFAB = "Assets/Prefabs/Characters/Enemy_Slime.prefab";
    const string KING_PREFAB  = "Assets/Prefabs/Characters/Enemy_KingSlime.prefab";
    const string KING_SPRITE  = "Assets/Art/Characters/KingSlime/king_slime.png";
    const string LOBBY_SCENE  = "Assets/Scenes/LobbyScene.unity";

    // 금관 (슬라임 그림의 3~12열, 위에서 0~2행). K 외곽선, Y 금, d 짙은 금, R 루비, B 사파이어, . 투명(뾰족한 끝 사이)
    static readonly string[] crown =
    {
        "KY.KYYK.YK",
        "KYRYYYYBYK",
        "KddddddddK",
    };
    const int CROWN_LEFT = 3;

    static Color32 CrownColor(char c)
    {
        switch (c)
        {
            case 'K': return new Color32(34, 34, 34, 255);   // 슬라임 외곽선과 같은 색
            case 'Y': return new Color32(255, 204, 48, 255);
            case 'd': return new Color32(196, 128, 24, 255);
            case 'R': return new Color32(220, 40, 60, 255);
            case 'B': return new Color32(80, 170, 255, 255);
            default:  return new Color32(0, 0, 0, 0);
        }
    }

    [MenuItem("Tools/킹 슬라임 프리팹 만들기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[KingSlimeBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }
        GameObject slimeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SLIME_PREFAB);
        if (slimeAsset == null) { Debug.LogError($"[KingSlimeBuilder] {SLIME_PREFAB} 없음"); return; }

        Sprite kingSprite = DrawKingSprite();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(KING_PREFAB) == null && !AssetDatabase.CopyAsset(SLIME_PREFAB, KING_PREFAB))
        {
            Debug.LogError($"[KingSlimeBuilder] {KING_PREFAB} 복사 실패");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(KING_PREFAB);
        root.name = "Enemy_KingSlime";
        root.tag = "Enemy";
        root.transform.localScale = slimeAsset.transform.localScale; // 보스방에서 BattleManager가 키움

        EnemySlime king = root.GetComponent<EnemySlime>();
        if (king == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[KingSlimeBuilder] EnemySlime 스크립트가 없습니다."); return; }
        king.isKing = true;
        king.splitCount = 0;
        king.slimePrefab = slimeAsset; // 쓰러지면 일반 슬라임으로 분열

        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (kingSprite != null) sr.sprite = kingSprite;
            sr.color = Color.white;
        }

        PrefabUtility.SaveAsPrefabAsset(root, KING_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        ConnectBoss(AssetDatabase.LoadAssetAtPath<GameObject>(KING_PREFAB));
        Debug.Log("=== 킹 슬라임 프리팹 만들기 완료! ===");
    }

    // 일반 슬라임 그림 + 금관 → PNG
    static Sprite DrawKingSprite()
    {
        string slimePath = $"{FRAMES}/swampy_anim_f0.png";
        if (!File.Exists(slimePath)) { Debug.LogWarning($"[KingSlimeBuilder] {slimePath} 없음"); return null; }

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(slimePath));
        for (int row = 0; row < crown.Length; row++)
            for (int col = 0; col < crown[row].Length; col++)
                if (crown[row][col] != '.')
                    tex.SetPixel(CROWN_LEFT + col, tex.height - 1 - row, CrownColor(crown[row][col])); // 텍스처는 아래가 y=0
        tex.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(KING_SPRITE));
        File.WriteAllBytes(KING_SPRITE, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(KING_SPRITE);

        TextureImporter importer = AssetImporter.GetAtPath(KING_SPRITE) as TextureImporter;
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(KING_SPRITE);
    }

    static void ConnectBoss(GameObject kingAsset)
    {
        Scene lobby = SceneManager.GetSceneByPath(LOBBY_SCENE);
        bool openedHere = !lobby.isLoaded;
        if (openedHere) lobby = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Additive);

        int connected = 0;
        foreach (GameObject go in lobby.GetRootGameObjects())
        {
            foreach (QuestGenerator generator in go.GetComponentsInChildren<QuestGenerator>(true))
            {
                generator.bossPrefab = kingAsset;
                EditorUtility.SetDirty(generator);
                connected++;
            }
        }

        if (connected == 0) Debug.LogWarning("[KingSlimeBuilder] 로비 씬에서 QuestGenerator를 찾지 못했습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            Debug.Log($"[KingSlimeBuilder] QuestGenerator {connected}개의 보스 프리팹을 킹 슬라임으로 연결");
        }
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
    }
}
