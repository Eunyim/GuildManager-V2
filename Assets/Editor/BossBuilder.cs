using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// [메뉴] Tools > 보스 프리팹 만들기
/// 종족별 보스를 만듭니다. (킹 슬라임은 'Tools > 킹 슬라임 프리팹 만들기', 드래곤은 기존 Enemy_Dragon)
///  - 알파 (초원 늑대): 하울링으로 모험가 전원 0.8초 스턴. 그림: 초원 늑대를 크고 어둡게
///  - 스켈레톤 네크로맨서: 마나(150)가 차면 땅속에서 스켈레톤 전사 2마리 소환. 그림: 보라 기운 스켈레톤 + 지팡이
///  - 샤먼킹 (고블린): 고블린 무리와 함께 나와 사제를 대신함. 더 강한 축복 + 피해 감소 오라. 그림: 왕관 쓴 고블린 + 지팡이
///  - 오크 백부장: 지진으로 주변 범위 피해. 그림: 오거 + 양날 도끼
///  - 드래곤: 브레스(앞쪽 범위 피해 + 화상)를 켬. 새끼 드래곤은 브레스를 끔
/// 스탯은 임시값 (의뢰 랭크 배율이 곱해짐). 다시 실행해도 안전합니다.
/// </summary>
public class BossBuilder : Editor
{
    const string FOLDER = "Assets/Prefabs/Characters";
    const string FRAMES = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames";
    const string SHAMAN_SPRITE = "Assets/Art/Characters/GoblinShamanKing/goblin_shaman_king.png";

    [MenuItem("Tools/보스 프리팹 만들기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[BossBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }

        // 알파: 초원 늑대 프리팹을 본떠 (그림·충돌 박스 그대로, 크기 0.6 → 0.85)
        Make<EnemyAlphaWolf>("Enemy_AlphaWolf", "Enemy_PrairieWolf", "Enemy_PrairieWolf",
            hp: 450f, atk: 26f, range: 1.0f, speed: 3.4f, cooldown: 0.9f, mpPerHit: 25f,
            dress: root => Tint(root, 0.85f, new Color(0.58f, 0.56f, 0.66f)));

        // 스켈레톤 네크로맨서: 스켈레톤 전사를 본떠
        GameObject skeletonWarrior = Load("Enemy_SkeletonWarrior");
        Make<EnemySkeletonNecromancer>("Enemy_SkeletonNecromancer", "Enemy_SkeletonWarrior", "Enemy_Skeleton",
            hp: 380f, atk: 20f, range: 4.5f, speed: 1.4f, cooldown: 1.6f, mpPerHit: 20f, maxMp: 150f,
            configure: u => u.summonPrefab = skeletonWarrior,
            dress: root => { Tint(root, 1.4f, new Color(0.8f, 0.62f, 1f)); SetWeapon(root, "weapon_red_magic_staff", -5f, 0.45f); });

        // 샤먼킹: 고블린 사제를 본떠 (그림은 왕관 쓴 고블린)
        Sprite shaman = DrawShamanKing();
        Make<EnemyGoblinShamanKing>("Enemy_GoblinShamanKing", "Enemy_GoblinPriest", "Enemy_Goblin",
            hp: 300f, atk: 14f, range: 4.5f, speed: 2.2f, cooldown: 1.5f, mpPerHit: 20f, aggro: 5f,
            configure: u => { u.blessingMultiplier = 1.35f; u.blessingDuration = 8f; u.mpRegenPerSecond = 10f; u.auraRadius = 5f; u.auraDamageReduction = 0.2f; },
            dress: root =>
            {
                Tint(root, 1.4f, Color.white);
                SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
                if (sr != null && shaman != null) sr.sprite = shaman;
                SetWeapon(root, "weapon_green_magic_staff", -5f, 0.45f);
            });

        // 오크 백부장: 오크 하급 전사를 본떠 (그림은 오거)
        Make<EnemyOrcCenturion>("Enemy_OrcCenturion", "Enemy_OrcGrunt", "Enemy_Orc",
            hp: 900f, atk: 50f, range: 1.5f, speed: 1.1f, cooldown: 2.2f, mpPerHit: 25f,
            dress: root =>
            {
                const float scale = 0.8f;
                root.transform.localScale = new Vector3(scale, scale, 1f);
                SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
                Sprite ogre = AssetDatabase.LoadAssetAtPath<Sprite>($"{FRAMES}/ogre_idle_anim_f0.png");
                if (sr != null) { if (ogre != null) sr.sprite = ogre; sr.color = Color.white; }
                BoxCollider2D col = root.GetComponent<BoxCollider2D>();
                if (col != null) { col.size = new Vector2(1.3f, 1.5f) / scale; col.offset = Vector2.zero; }
                SetWeapon(root, "weapon_double_axe", -35f, 0.7f);
            });

        // 드래곤: 어른은 브레스, 새끼는 물어뜯기
        SetBreath("Enemy_Dragon", true);
        SetBreath("Enemy_DragonHatchling", false);
        SetBreath("Enemy_BabyDragon", false);

        AssetDatabase.SaveAssets();
        Debug.Log("=== 보스 프리팹 만들기 완료! (알파, 스켈레톤 네크로맨서, 샤먼킹, 오크 백부장, 드래곤 브레스) ===");
    }

    // template를 복사해 스크립트를 T로 바꾸고 스탯·그래픽을 맞춤. 전리품은 dropsFrom 프리팹의 것을 물려받음
    static void Make<T>(string file, string template, string dropsFrom,
                        float hp, float atk, float range, float speed, float cooldown, float mpPerHit,
                        float maxMp = 100f, float aggro = 10f,
                        System.Action<T> configure = null, System.Action<GameObject> dress = null) where T : BattleUnit
    {
        string path = $"{FOLDER}/{file}.prefab";
        string templatePath = $"{FOLDER}/{template}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(templatePath) == null) { Debug.LogError($"[BossBuilder] {templatePath} 없음"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(templatePath, path))
        {
            Debug.LogError($"[BossBuilder] {path} 복사 실패");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.name = file;
        root.tag = "Enemy";

        BattleUnit old = root.GetComponent<BattleUnit>();
        GameObject hpBar = old != null ? old.hpBarPrefab : null;
        if (old != null && old.GetType() != typeof(T)) DestroyImmediate(old);
        T unit = root.GetComponent<T>();
        if (unit == null) unit = root.AddComponent<T>();
        if (hpBar != null) unit.hpBarPrefab = hpBar;

        unit.maxHp          = hp;
        unit.attackPower    = atk;
        unit.attackRange    = range;
        unit.moveSpeed      = speed;
        unit.attackCooldown = cooldown;
        unit.mpRegenOnHit   = mpPerHit;
        unit.maxMp          = maxMp;
        unit.aggro          = aggro;
        SetDrops(unit, CopyDrops($"{FOLDER}/{dropsFrom}.prefab"));
        configure?.Invoke(unit);
        dress?.Invoke(root);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void Tint(GameObject root, float scale, Color tint)
    {
        root.transform.localScale = new Vector3(scale, scale, 1f); // 충돌 박스도 같은 비율로 커짐
        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = tint;
    }

    static void SetWeapon(GameObject root, string weaponSprite, float angle, float weaponScale)
    {
        Transform weapon = root.transform.Find("Weapon");
        if (weapon == null)
        {
            weapon = new GameObject("Weapon").transform;
            weapon.SetParent(root.transform, false);
        }
        SpriteRenderer body = root.GetComponent<SpriteRenderer>();
        float halfHeight = body != null && body.sprite != null ? body.sprite.bounds.extents.y : 0.5f;
        float halfWidth  = body != null && body.sprite != null ? body.sprite.bounds.extents.x : 0.5f;
        weapon.localPosition = new Vector3(halfWidth * 0.8f, -halfHeight * 0.35f, 0f);
        weapon.localRotation = Quaternion.Euler(0f, 0f, angle);
        weapon.localScale = new Vector3(weaponScale, weaponScale, 1f);
        SpriteRenderer wsr = weapon.GetComponent<SpriteRenderer>();
        if (wsr == null) wsr = weapon.gameObject.AddComponent<SpriteRenderer>();
        wsr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FRAMES}/{weaponSprite}.png");
        if (body != null) { wsr.sortingLayerID = body.sortingLayerID; wsr.sortingOrder = body.sortingOrder + 1; }
    }

    static void SetBreath(string file, bool useBreath)
    {
        string path = $"{FOLDER}/{file}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[BossBuilder] {path} 없음"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        EnemyDragon dragon = root.GetComponent<EnemyDragon>();
        if (dragon != null) dragon.useBreath = useBreath;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // 고블린 그림의 비어 있는 머리 위(3~5행)에 왕관 (킹 슬라임과 같은 색)
    static Sprite DrawShamanKing()
    {
        string goblinPath = $"{FRAMES}/goblin_idle_anim_f0.png";
        if (!File.Exists(goblinPath)) return null;
        string[] crown =
        {
            "KY.KYK.YK",
            "KYRYYYBYK",
            "KdddddddK",
        };
        const int left = 4, top = 3;

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(goblinPath));
        for (int row = 0; row < crown.Length; row++)
            for (int col = 0; col < crown[row].Length; col++)
            {
                char c = crown[row][col];
                if (c == '.') continue;
                Color32 color = c == 'K' ? new Color32(34, 34, 34, 255) : c == 'Y' ? new Color32(255, 204, 48, 255)
                              : c == 'd' ? new Color32(196, 128, 24, 255) : c == 'R' ? new Color32(220, 40, 60, 255) : new Color32(80, 170, 255, 255);
                tex.SetPixel(left + col, tex.height - 1 - (top + row), color);
            }
        tex.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(SHAMAN_SPRITE));
        File.WriteAllBytes(SHAMAN_SPRITE, tex.EncodeToPNG());
        DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SHAMAN_SPRITE);

        TextureImporter importer = AssetImporter.GetAtPath(SHAMAN_SPRITE) as TextureImporter;
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
        return AssetDatabase.LoadAssetAtPath<Sprite>(SHAMAN_SPRITE);
    }

    static List<DropItem> CopyDrops(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return new List<DropItem>();
        foreach (MonoBehaviour mb in prefab.GetComponents<MonoBehaviour>())
        {
            var field = mb != null ? mb.GetType().GetField("dropTable") : null;
            if (field != null && field.GetValue(mb) is List<DropItem> list) return new List<DropItem>(list);
        }
        return new List<DropItem>();
    }

    static void SetDrops(BattleUnit unit, List<DropItem> drops)
    {
        var field = unit.GetType().GetField("dropTable");
        if (field != null) field.SetValue(unit, drops);
    }

    static GameObject Load(string file) => AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{file}.prefab");
}
