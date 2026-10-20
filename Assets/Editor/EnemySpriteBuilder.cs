using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// [메뉴] Tools > 기본 몬스터 임시 스프라이트 입히기
/// 네모(Unity 기본 스프라이트)에 색만 칠해져 있던 몬스터 프리팹 8개에 임시 그림을 입힙니다.
///  - 도적단 암살자·전사·궁수, 고블린·오크·스켈레톤(무리 대표), 슬라임: 0x72 타일셋 스프라이트 + 손에 든 무기
///  - 드래곤: 타일셋에 드래곤이 없어 48x40 도트를 코드로 그려 PNG로 저장 (실행할 때마다 다시 그림)
/// 충돌 박스는 화면 크기 0.8 x 0.9(드래곤은 1.65 x 1.2)로 맞춥니다. 다시 실행해도 안전합니다.
/// </summary>
public class EnemySpriteBuilder : Editor
{
    const string FRAMES = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames";
    const string FOLDER = "Assets/Prefabs/Characters";
    const string DRAGON_SPRITE = "Assets/Art/Characters/Dragon/dragon.png";

    struct Spec
    {
        public string prefab, body, weapon;
        public float scale, weaponAngle, weaponScale;
        public Color tint;
        public Vector2 colliderWorld;
    }

    static Spec Make(string prefab, string body, string weapon, float scale, Color tint, float weaponAngle = -35f, float weaponScale = 0.45f) =>
        new Spec { prefab = prefab, body = body, weapon = weapon, scale = scale, tint = tint, weaponAngle = weaponAngle, weaponScale = weaponScale,
                   colliderWorld = new Vector2(0.8f, 0.9f) };

    static readonly Spec[] specs =
    {
        // 도적단 (사람 크기. 아군과 헷갈리지 않게 아군이 안 쓰는 그림 + 붉은·주황 색조)
        Make("Enemy_Bandit",        "necromancer_anim_f0",  "weapon_knife",             0.65f, new Color(1f, 0.72f, 0.72f), -45f, 0.55f),
        Make("Enemy_BanditWarrior", "dwarf_m_idle_anim_f0", "weapon_baton_with_spikes", 0.6f,  new Color(0.95f, 0.82f, 0.72f), -35f, 0.55f),
        Make("Enemy_BanditArcher",  "elf_f_idle_anim_f0",   "weapon_bow",               0.6f,  new Color(1f, 0.8f, 0.6f), 0f, 0.55f),
        // 무리 대표 (뽑히면 구성원 무리로 바뀌어 보통은 전투에 안 나오지만, 직접 소환될 때를 위해)
        Make("Enemy_Goblin",   "goblin_idle_anim_f0",      "weapon_rusty_sword",   1f,   Color.white),
        Make("Enemy_Orc",      "orc_warrior_idle_anim_f0", "weapon_waraxe",        1f,   Color.white),
        Make("Enemy_Skeleton", "skelet_idle_anim_f0",      "weapon_regular_sword", 1.1f, Color.white),
        // 슬라임 (녹색 늪 덩어리. 분열체는 이 크기의 0.6배, 킹 슬라임은 보스 배율만큼 커짐)
        Make("Enemy_Slime",    "swampy_anim_f0",           null,                   1f,   Color.white),
    };

    // 드래곤: 48x40 임시 도트 (오른쪽을 봄). 붉은 비늘, 배 비늘판, 펼친 날개, 등 가시, 뿔, 노란 눈, 이빨
    // 크기: 48x40px / PPU 16 x 스케일 0.75 = 화면 2.25 x 1.88 (아군 높이 1.0의 두 배 가까이)
    const float DRAGON_SCALE = 0.75f;
    static readonly string[] dragonPixels =
    {
        "........................A.......................",
        "................A......KD.......................",
        "................DK....KWDK......................",
        "...............KWDKK.KWWDK......................",
        ".........A.....KWDWWKWWWDWK.........A...........",
        ".........DK...KWWWDWWWWWDWK.........AA..........",
        ".........KDKK.KWWWDWWWWWDWWK.........AA.........",
        ".........KWDWKWWWWWDWWWWDWWK........KMMKK.......",
        ".........KWWDWWWWWWDWWWWDWWK.....K.KMKKKMKKK....",
        "........KWWWWDWWWWWWDWWWDWK......AKMMEEMMMMMKK..",
        ".....A..KWWWWWDWWWWWWDWWWDK.....KAKMKKMMMMMMMMKK",
        ".....KDDKWWWWWWDDWWWWDWWWDK...K..KMMMMMMMMMMMMMK",
        "......KKDDWWWWWWWDWWWWDWWDK...AKKGMMMMMMMKTKTKTK",
        "......KWWWDDWWWWWWDWWKDWKDK..KAKGMMMMMMMKOOOOOK.",
        "......KWWWWWDDDKWWADWAWDADKK..KGMMLMMMMMKTKTKKK.",
        "......KWWWWWWWWDDKAKDAKDAD.A.KGMMMLLMMMMMMMMMK..",
        ".......KWWWWWWKAKDDGGDGGDDKAKGMMMMKLLLLLKKKKK...",
        ".......KKKKKWWWWGGMDDMDMMDGGGMMMMMMMKKKK........",
        "............KWGGMMMMMMMMMMMMMMMMMMMLK...........",
        ".............KMMMMMMMMMMMMMMMMMMMMMLK...........",
        "............KGMMMMMMMMMMMMMMMMMMMMLK............",
        "...........KGMMMMMMMMMMMMMMMMMMMMMLK............",
        "..........AGMMMMMMMMMMMMMMMMMMMMMLMK............",
        "..........KMMMMMMMMMMMMMMMMMMMMMMLK.............",
        ".........KGMMMMMMMMMMMMMMMMMMMMMLMK.............",
        ".......AKGMMMMMMMMMMMMMMMMMMMMMMLDK.............",
        ".K.....KGDMMMMMMMMMMMMMDDDDMMMMLMDK.............",
        ".KKKKKKGDDDMMMLLLLLLLLLLLLLLLLLLMK..............",
        "KGGGGGGDDKDDMMBBBBBBBBBDDDDBBBBBK...............",
        "KDMDDDKKK.KDMMLLLLLLLLLDDDDLLLLK................",
        ".KDDDK.....KDMBBBBBBBBBDDDDBBBBK................",
        ".KDKKK.....KDMLLLLLLLLLDDDDKKLLLK...............",
        "..K.........KMMMDDKKKKKDDDK..KMMK...............",
        ".............KMMDK.....KDDK..KMMK...............",
        ".............KMMK......KDDK..KMMK...............",
        ".............KDDK......KDDK..KDDK...............",
        "............KDDDK.....KDDDK.KDDDDK..............",
        "............AKAKAK....AKAKK.AKAKAK..............",
        "................................................",
        "................................................",
    };

    static Color DragonColor(char c)
    {
        switch (c)
        {
            case 'K': return new Color32(30, 12, 16, 255);    // 외곽선
            case 'D': return new Color32(104, 22, 30, 255);   // 짙은 빨강 (날개 뼈대, 아래쪽 그림자)
            case 'M': return new Color32(164, 38, 40, 255);   // 빨간 비늘
            case 'G': return new Color32(212, 78, 58, 255);   // 밝은 비늘 (등)
            case 'L': return new Color32(236, 196, 124, 255); // 배 비늘판
            case 'B': return new Color32(196, 146, 84, 255);  // 배 비늘판 그림자
            case 'W': return new Color32(120, 34, 48, 255);   // 날개 막
            case 'E': return new Color32(255, 222, 64, 255);  // 눈
            case 'T': return new Color32(246, 240, 226, 255); // 이빨
            case 'A': return new Color32(226, 214, 190, 255); // 뿔·가시·발톱
            case 'O': return new Color32(90, 10, 16, 255);    // 입속
            default:  return new Color(0f, 0f, 0f, 0f);
        }
    }

    [MenuItem("Tools/기본 몬스터 임시 스프라이트 입히기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[EnemySpriteBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }

        foreach (Spec spec in specs) Dress(spec, LoadFrame(spec.body));

        Sprite dragon = DrawDragon();
        Dress(new Spec { prefab = "Enemy_Dragon", scale = DRAGON_SCALE, tint = Color.white, colliderWorld = new Vector2(1.65f, 1.2f) }, dragon);

        AssetDatabase.SaveAssets();
        Debug.Log("=== 기본 몬스터 임시 스프라이트 입히기 완료! (8개) ===");
    }

    static void Dress(Spec spec, Sprite body)
    {
        string path = $"{FOLDER}/{spec.prefab}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[EnemySpriteBuilder] {path} 없음"); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.transform.localScale = new Vector3(spec.scale, spec.scale, 1f);

        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (body != null) sr.sprite = body;
            sr.color = spec.tint;
        }
        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) { col.size = spec.colliderWorld / spec.scale; col.offset = Vector2.zero; }

        Transform weapon = root.transform.Find("Weapon");
        if (string.IsNullOrEmpty(spec.weapon))
        {
            if (weapon != null) DestroyImmediate(weapon.gameObject);
        }
        else
        {
            if (weapon == null)
            {
                weapon = new GameObject("Weapon").transform;
                weapon.SetParent(root.transform, false);
            }
            float halfHeight = body != null ? body.bounds.extents.y : 0.5f;
            weapon.localPosition = new Vector3(0.42f, -halfHeight * 0.35f, 0f);
            weapon.localRotation = Quaternion.Euler(0f, 0f, spec.weaponAngle);
            weapon.localScale = new Vector3(spec.weaponScale, spec.weaponScale, 1f);
            SpriteRenderer wsr = weapon.GetComponent<SpriteRenderer>();
            if (wsr == null) wsr = weapon.gameObject.AddComponent<SpriteRenderer>();
            wsr.sprite = LoadFrame(spec.weapon);
            if (sr != null) { wsr.sortingLayerID = sr.sortingLayerID; wsr.sortingOrder = sr.sortingOrder + 1; }
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static Sprite DrawDragon()
    {
        int h = dragonPixels.Length, w = dragonPixels[0].Length;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
                tex.SetPixel(col, h - 1 - row, DragonColor(dragonPixels[row][col])); // 텍스처는 아래가 y=0
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(DRAGON_SPRITE));
        File.WriteAllBytes(DRAGON_SPRITE, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(DRAGON_SPRITE);

        TextureImporter importer = AssetImporter.GetAtPath(DRAGON_SPRITE) as TextureImporter;
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
        return AssetDatabase.LoadAssetAtPath<Sprite>(DRAGON_SPRITE);
    }

    static Sprite LoadFrame(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FRAMES}/{name}.png");
        if (sprite == null) Debug.LogWarning($"[EnemySpriteBuilder] 스프라이트 {name} 없음");
        return sprite;
    }
}
