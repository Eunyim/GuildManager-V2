using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// [메뉴] Tools > 아군·보물상자 임시 스프라이트 입히기
///  - 아군 5직업(캡슐 그림)에 0x72 타일셋 영웅 스프라이트 + 손에 든 무기를 입힘
///  - 보물상자(갈색 네모)에 미믹과 똑같은 상자 그림을 입힘:
///    미믹 그림(chest_mimic_open_anim_f1)에서 이빨·혀 픽셀만 원래 상자 그림(chest_empty_open_anim_f1)의 픽셀로 바꾼 것.
///    그 밖의 픽셀은 미믹과 100% 같음 → 미믹이면 상자가 그 자리에서 그대로 입을 벌리는 것처럼 보임
/// 다시 실행해도 안전합니다.
/// </summary>
public class AllyAndChestSpriteBuilder : Editor
{
    const string FRAMES       = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames";
    const string CHARACTERS   = "Assets/Prefabs/Characters";
    const string CHEST_PREFAB = "Assets/Prefabs/Objects/Prefab_TreasureChest.prefab";
    const string MIMIC_PREFAB = CHARACTERS + "/Enemy_Mimic.prefab";
    const string CHEST_SPRITE = "Assets/Art/Props/treasure_chest_from_mimic.png";

    // 아군: 화면 높이 약 1.05 (예전 캡슐 1.0과 비슷하게). 충돌 박스는 예전 화면 크기 그대로 유지
    const float ALLY_SCALE = 0.6f;
    static readonly Vector2 allyColliderWorld = new Vector2(1f, 0.5f); // 예전 캡슐 충돌체 (로컬 2x1 x 스케일 0.5)

    struct Ally { public string prefab, body, weapon; public float weaponAngle; }
    static readonly Ally[] allies =
    {
        new Ally { prefab = "Prefab_Warrior", body = "knight_m_idle_anim_f0", weapon = "weapon_knight_sword",      weaponAngle = -35f },
        new Ally { prefab = "Prefab_Archer",  body = "elf_m_idle_anim_f0",    weapon = "weapon_bow",               weaponAngle = 0f },
        new Ally { prefab = "Prefab_Mage",    body = "wizzard_m_idle_anim_f0", weapon = "weapon_red_magic_staff",  weaponAngle = -5f },
        new Ally { prefab = "Prefab_Healer",  body = "wizzard_f_idle_anim_f0", weapon = "weapon_green_magic_staff", weaponAngle = -5f },
        new Ally { prefab = "Prefab_Rogue",   body = "doc_idle_anim_f0",       weapon = "weapon_knife",            weaponAngle = -45f },
    };

    // 미믹의 몬스터 부분 색 (이빨, 혀)
    static readonly Color32 toothColor  = new Color32(253, 247, 237, 255);
    static readonly Color32 tongueColor = new Color32(220, 74, 123, 255);

    [MenuItem("Tools/아군·보물상자 임시 스프라이트 입히기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[AllyAndChestSpriteBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }

        foreach (Ally ally in allies) DressAlly(ally);
        BuildChest();
        AssetDatabase.SaveAssets();
        Debug.Log("=== 아군·보물상자 임시 스프라이트 입히기 완료! ===");
    }

    static void DressAlly(Ally ally)
    {
        string path = $"{CHARACTERS}/{ally.prefab}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[AllyAndChestSpriteBuilder] {path} 없음"); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.transform.localScale = new Vector3(ALLY_SCALE, ALLY_SCALE, 1f);

        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        Sprite body = LoadFrame(ally.body);
        if (sr != null)
        {
            if (body != null) sr.sprite = body;
            sr.color = Color.white;
        }

        CapsuleCollider2D capsule = root.GetComponent<CapsuleCollider2D>();
        if (capsule != null) capsule.size = allyColliderWorld / ALLY_SCALE;
        BoxCollider2D box = root.GetComponent<BoxCollider2D>();
        if (box != null) box.size = allyColliderWorld / ALLY_SCALE;

        // 손에 든 무기 (몸 앞, 손 높이). 몸 그림 높이에 맞춰 위치를 정함
        float halfHeight = body != null ? body.bounds.extents.y : 0.875f;
        Transform weapon = root.transform.Find("Weapon");
        if (weapon == null)
        {
            weapon = new GameObject("Weapon").transform;
            weapon.SetParent(root.transform, false);
        }
        weapon.localPosition = new Vector3(0.42f, -halfHeight * 0.35f, 0f);
        weapon.localRotation = Quaternion.Euler(0f, 0f, ally.weaponAngle);
        weapon.localScale = new Vector3(0.55f, 0.55f, 1f);
        SpriteRenderer wsr = weapon.GetComponent<SpriteRenderer>();
        if (wsr == null) wsr = weapon.gameObject.AddComponent<SpriteRenderer>();
        wsr.sprite = LoadFrame(ally.weapon);
        if (sr != null) { wsr.sortingLayerID = sr.sortingLayerID; wsr.sortingOrder = sr.sortingOrder + 1; }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void BuildChest()
    {
        // 1. 미믹 그림에서 이빨·혀만 원래 상자 픽셀로 바꿈
        Texture2D mimic = LoadPng($"{FRAMES}/chest_mimic_open_anim_f1.png");
        Texture2D plain = LoadPng($"{FRAMES}/chest_empty_open_anim_f1.png");
        if (mimic == null || plain == null || mimic.width != plain.width || mimic.height != plain.height)
        {
            Debug.LogError("[AllyAndChestSpriteBuilder] 미믹/상자 그림을 읽지 못했습니다.");
            return;
        }

        Color32[] m = mimic.GetPixels32(), p = plain.GetPixels32(), result = new Color32[m.Length];
        int replaced = 0;
        for (int i = 0; i < m.Length; i++)
        {
            bool monsterPart = Same(m[i], toothColor) || Same(m[i], tongueColor);
            result[i] = monsterPart ? p[i] : m[i];
            if (monsterPart) replaced++;
        }
        Texture2D chest = new Texture2D(mimic.width, mimic.height, TextureFormat.RGBA32, false);
        chest.SetPixels32(result);
        chest.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(CHEST_SPRITE));
        File.WriteAllBytes(CHEST_SPRITE, chest.EncodeToPNG());
        DestroyImmediate(mimic); DestroyImmediate(plain); DestroyImmediate(chest);
        AssetDatabase.ImportAsset(CHEST_SPRITE);

        // 미믹 그림과 같은 도트 설정
        TextureImporter importer = AssetImporter.GetAtPath(CHEST_SPRITE) as TextureImporter;
        TextureImporter mimicImporter = AssetImporter.GetAtPath($"{FRAMES}/chest_mimic_open_anim_f1.png") as TextureImporter;
        if (importer != null)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = mimicImporter != null ? mimicImporter.spritePixelsPerUnit : 16;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
            importer.SaveAndReimport();
        }
        Sprite chestSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CHEST_SPRITE);

        // 2. 상자 프리팹: 그림·크기를 미믹 프리팹과 똑같이
        GameObject mimicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MIMIC_PREFAB);
        Vector3 mimicScale = mimicPrefab != null ? mimicPrefab.transform.localScale : Vector3.one;
        SpriteRenderer mimicRenderer = mimicPrefab != null ? mimicPrefab.GetComponent<SpriteRenderer>() : null;

        GameObject root = PrefabUtility.LoadPrefabContents(CHEST_PREFAB);
        root.transform.localScale = new Vector3(Mathf.Abs(mimicScale.x), Mathf.Abs(mimicScale.y), 1f);
        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (chestSprite != null) sr.sprite = chestSprite;
            sr.color = mimicRenderer != null ? mimicRenderer.color : Color.white; // 갈색 색조 제거
        }
        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) col.size = new Vector2(1f, 0.8f); // 아군이 닿으면 열림 (트리거)
        PrefabUtility.SaveAsPrefabAsset(root, CHEST_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log($"[AllyAndChestSpriteBuilder] 보물상자 그림 생성: 미믹 그림에서 이빨·혀 {replaced}픽셀만 바꿈 → {CHEST_SPRITE}");
    }

    static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    static Texture2D LoadPng(string path)
    {
        if (!File.Exists(path)) return null;
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return tex.LoadImage(File.ReadAllBytes(path)) ? tex : null;
    }

    static Sprite LoadFrame(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FRAMES}/{name}.png");
        if (sprite == null) Debug.LogWarning($"[AllyAndChestSpriteBuilder] 스프라이트 {name} 없음");
        return sprite;
    }
}
