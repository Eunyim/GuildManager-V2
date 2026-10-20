using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// [메뉴] Tools > 몬스터 무리 프리팹 만들기
/// 고블린·스켈레톤·오크를 직업별 무리로 나누고, 드래곤을 전용 스크립트로 바꿉니다.
///  - 고블린: 전사 / 사냥꾼(작살) / 사제(지휘자, 무리마다 1명)
///  - 스켈레톤: 전사 / 궁수 / 탱커(갑옷)
///  - 오크: 하급 전사 / 기수(초원 늑대를 탐, 낙마하면 늑대가 남음)
///  - 드래곤: 슬라임 스크립트 → EnemyDragon
/// 기존 Enemy_Goblin / Enemy_Skeleton / Enemy_Orc 는 적 목록의 '무리 대표'로 남고, 뽑히면 구성원 무리로 바뀝니다 (QuestGenerator.packs).
/// 그래픽은 0x72 타일셋의 고블린·스켈레톤·오크 스프라이트 + 손에 든 무기(임시).
/// 이미 있으면 스탯·그래픽·연결만 다시 맞추므로 다시 실행해도 안전합니다.
/// </summary>
public class MonsterFamilyBuilder : Editor
{
    const string FOLDER      = "Assets/Prefabs/Characters";
    const string TEMPLATE    = FOLDER + "/Enemy_Goblin.prefab";
    const string FRAMES      = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames";
    const string ARROW       = "Assets/Prefabs/Objects/Prefab_Arrow.prefab";
    const string HARPOON     = "Assets/Prefabs/Objects/Projectile_Harpoon.prefab";
    const string WOLF        = FOLDER + "/Enemy_PrairieWolf.prefab";
    const string DRAGON      = FOLDER + "/Enemy_Dragon.prefab";
    const string LOBBY_SCENE = "Assets/Scenes/LobbyScene.unity";

    // 스탯과 그래픽 (수치는 임시. 프리팹 값으로 조정)
    struct Spec
    {
        public string file, body, weapon;
        public float hp, atk, range, speed, cooldown, aggro, mpPerHit, scale;
        public Color tint;
        public Vector2 weaponPos; public float weaponAngle, weaponScale;
        public Vector2 collider;
    }

    static Spec Make(string file, string body, string weapon, float hp, float atk, float range, float speed, float cooldown,
                     float aggro = 10f, float scale = 1f, Color? tint = null, float weaponAngle = -35f)
    {
        return new Spec
        {
            file = file, body = body, weapon = weapon, hp = hp, atk = atk, range = range, speed = speed, cooldown = cooldown,
            aggro = aggro, mpPerHit = 20f, scale = scale, tint = tint ?? Color.white,
            weaponPos = new Vector2(0.42f, -0.18f), weaponAngle = weaponAngle, weaponScale = 0.45f, // 몸(16px)보다 무기(21~30px)가 커서 줄여 손 높이에 듦
            collider = new Vector2(0.8f, 0.9f),
        };
    }

    // 고블린 (예전 고블린 HP 60 / 공격 16 / 이동 3.0 / 간격 0.8)
    static readonly Spec goblinWarrior = Make("Enemy_GoblinWarrior", "goblin_idle_anim_f0", "weapon_rusty_sword", 75f, 13f, 0.9f, 2.4f, 1.0f);
    static readonly Spec goblinHunter  = Make("Enemy_GoblinHunter",  "goblin_idle_anim_f0", "weapon_spear",       45f, 12f, 4.5f, 2.6f, 1.6f, tint: new Color(0.9f, 0.8f, 0.6f), weaponAngle: -20f);
    static readonly Spec goblinPriest  = Make("Enemy_GoblinPriest",  "goblin_idle_anim_f0", "weapon_green_magic_staff", 40f, 7f, 4.0f, 2.4f, 1.6f, aggro: 5f, tint: new Color(0.9f, 0.82f, 1f), weaponAngle: -5f);
    // 스켈레톤 (예전 스켈레톤 HP 120 / 공격 24 / 이동 1.5 / 간격 1.5)
    static readonly Spec skeletonWarrior = Make("Enemy_SkeletonWarrior", "skelet_idle_anim_f0", "weapon_regular_sword", 110f, 20f, 1.0f, 1.6f, 1.4f, scale: 1.1f);
    static readonly Spec skeletonArcher  = Make("Enemy_SkeletonArcher",  "skelet_idle_anim_f0", "weapon_bow",           70f, 16f, 5.0f, 1.6f, 1.7f, scale: 1.1f, weaponAngle: 0f);
    static readonly Spec skeletonTank    = Make("Enemy_SkeletonTank",    "skelet_idle_anim_f0", "weapon_mace",          160f, 12f, 1.0f, 1.2f, 1.6f, aggro: 20f, scale: 1.3f, tint: new Color(0.62f, 0.68f, 0.82f));
    // 오크 (예전 오크 HP 250 / 공격 50 / 이동 1.2 / 간격 2.0)
    static readonly Spec orcGrunt = Make("Enemy_OrcGrunt", "orc_warrior_idle_anim_f0", "weapon_waraxe", 220f, 48f, 1.3f, 1.3f, 2.0f);
    static readonly Spec orcRider = Make("Enemy_OrcRider", "masked_orc_idle_anim_f0",  "weapon_spear",  150f, 30f, 1.4f, 3.8f, 1.4f);
    // 드래곤 (S랭크 배율 HP x2.5 / 공격 x2.0 이 곱해짐)
    const float DRAGON_HP = 600f, DRAGON_ATK = 45f, DRAGON_RANGE = 2.0f, DRAGON_SPEED = 1.0f, DRAGON_COOLDOWN = 2.0f;

    [MenuItem("Tools/몬스터 무리 프리팹 만들기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[MonsterFamilyBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TEMPLATE) == null) { Debug.LogError($"[MonsterFamilyBuilder] {TEMPLATE} 없음"); return; }

        GameObject arrow   = AssetDatabase.LoadAssetAtPath<GameObject>(ARROW);
        GameObject harpoon = BuildHarpoon();
        GameObject wolf    = AssetDatabase.LoadAssetAtPath<GameObject>(WOLF);
        if (wolf == null) Debug.LogWarning("[MonsterFamilyBuilder] 초원 늑대 프리팹이 없습니다. 먼저 'Tools > 초원 늑대 프리팹 만들기'를 실행하세요. (오크 기수가 낙마해도 늑대가 남지 않음)");

        // 대표 프리팹의 전리품을 구성원에게 그대로 물려줌
        List<DropItem> goblinDrops   = CopyDrops(FOLDER + "/Enemy_Goblin.prefab");
        List<DropItem> skeletonDrops = CopyDrops(FOLDER + "/Enemy_Skeleton.prefab");
        List<DropItem> orcDrops      = CopyDrops(FOLDER + "/Enemy_Orc.prefab");

        Create<EnemyGoblinWarrior>(goblinWarrior, u => u.dropTable = goblinDrops);
        Create<EnemyGoblinHunter>(goblinHunter,   u => { u.dropTable = goblinDrops; u.harpoonPrefab = harpoon; });
        Create<EnemyGoblinPriest>(goblinPriest,   u => u.dropTable = goblinDrops);
        Create<EnemySkeletonWarrior>(skeletonWarrior, u => u.dropTable = skeletonDrops);
        Create<EnemySkeletonArcher>(skeletonArcher,   u => { u.dropTable = skeletonDrops; u.arrowPrefab = arrow; });
        Create<EnemySkeletonTank>(skeletonTank,       u => u.dropTable = skeletonDrops);
        Create<EnemyOrcGrunt>(orcGrunt, u => u.dropTable = orcDrops);
        Create<EnemyOrcRider>(orcRider, u => { u.dropTable = orcDrops; u.mountPrefab = wolf; }, MountOnWolf);

        BuildDragon();
        // 드래곤 둥지의 일반 몬스터: 어른 드래곤 HP·공격력의 20% / 40%, 그림은 드래곤을 작게
        BuildYoungDragon("Enemy_DragonHatchling", 0.2f, 0.3f, 1.0f);
        BuildYoungDragon("Enemy_BabyDragon",      0.4f, 0.45f, 1.3f);
        AssetDatabase.SaveAssets();
        ConnectQuestGenerator();
        Debug.Log("=== 몬스터 무리 프리팹 만들기 완료! (고블린·스켈레톤·오크 무리, 드래곤) ===");
    }

    // 고블린 프리팹을 본떠 구성원 프리팹을 만들고 스크립트·스탯·그래픽을 맞춤
    static void Create<T>(Spec spec, System.Action<T> configure, System.Action<GameObject, Spec> customGraphic = null) where T : BattleUnit
    {
        string path = $"{FOLDER}/{spec.file}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(TEMPLATE, path))
        {
            Debug.LogError($"[MonsterFamilyBuilder] {path} 복사 실패");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.name = spec.file;
        root.tag = "Enemy";

        BattleUnit old = root.GetComponent<BattleUnit>();
        GameObject hpBar = old != null ? old.hpBarPrefab : null;
        if (old != null && !(old is T)) DestroyImmediate(old);
        T unit = root.GetComponent<T>();
        if (unit == null) unit = root.AddComponent<T>();
        if (hpBar != null) unit.hpBarPrefab = hpBar;

        unit.maxHp          = spec.hp;
        unit.attackPower    = spec.atk;
        unit.attackRange    = spec.range;
        unit.moveSpeed      = spec.speed;
        unit.attackCooldown = spec.cooldown;
        unit.aggro          = spec.aggro;
        unit.mpRegenOnHit   = spec.mpPerHit;
        configure(unit);

        if (customGraphic != null) customGraphic(root, spec);
        else ApplyGraphic(root, spec);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // 몸 스프라이트 + 손에 든 무기 (자식 'Weapon')
    static void ApplyGraphic(GameObject root, Spec spec)
    {
        root.transform.localScale = new Vector3(spec.scale, spec.scale, 1f);
        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Sprite body = LoadFrame(spec.body);
            if (body != null) sr.sprite = body;
            sr.color = spec.tint;
        }
        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) { col.size = spec.collider; col.offset = Vector2.zero; }
        SetWeapon(root.transform, spec, sr);
    }

    static void SetWeapon(Transform parent, Spec spec, SpriteRenderer bodyRenderer)
    {
        Transform weapon = parent.Find("Weapon");
        if (weapon == null)
        {
            weapon = new GameObject("Weapon").transform;
            weapon.SetParent(parent, false);
        }
        weapon.localPosition = new Vector3(spec.weaponPos.x, spec.weaponPos.y, 0f);
        weapon.localRotation = Quaternion.Euler(0f, 0f, spec.weaponAngle);
        weapon.localScale = new Vector3(spec.weaponScale, spec.weaponScale, 1f);

        SpriteRenderer wsr = weapon.GetComponent<SpriteRenderer>();
        if (wsr == null) wsr = weapon.gameObject.AddComponent<SpriteRenderer>();
        wsr.sprite = LoadFrame(spec.weapon);
        if (bodyRenderer != null)
        {
            wsr.sortingLayerID = bodyRenderer.sortingLayerID;
            wsr.sortingOrder = bodyRenderer.sortingOrder + 1; // 몸 앞에 그림
        }
    }

    // 오크 기수: 몸은 초원 늑대, 그 등에 오크(자식 'Rider')가 타고 창을 듦
    static void MountOnWolf(GameObject root, Spec spec)
    {
        const float wolfScale = 0.6f;
        root.transform.localScale = new Vector3(wolfScale, wolfScale, 1f);

        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        Sprite wolfSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/PrairieWolf/prairie_wolf.png");
        if (sr != null)
        {
            if (wolfSprite != null) sr.sprite = wolfSprite;
            sr.color = Color.white;
        }
        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) { col.size = new Vector2(2.2f, 1.8f); col.offset = new Vector2(0f, 0.25f); }

        Transform rider = root.transform.Find("Rider");
        if (rider == null)
        {
            rider = new GameObject("Rider").transform;
            rider.SetParent(root.transform, false);
        }
        // 화면 기준: 늑대 등 위(가운데보다 조금 뒤), 오크는 화면에서 0.8 크기
        const float riderWorldScale = 0.8f;
        rider.localPosition = new Vector3(-0.15f, 0.95f, 0f);
        rider.localRotation = Quaternion.identity;
        rider.localScale = new Vector3(riderWorldScale / wolfScale, riderWorldScale / wolfScale, 1f);

        SpriteRenderer rsr = rider.GetComponent<SpriteRenderer>();
        if (rsr == null) rsr = rider.gameObject.AddComponent<SpriteRenderer>();
        rsr.sprite = LoadFrame(spec.body);
        if (sr != null) { rsr.sortingLayerID = sr.sortingLayerID; rsr.sortingOrder = sr.sortingOrder + 1; }

        Spec weaponSpec = spec;
        weaponSpec.weaponPos = new Vector2(0.4f, 0f);
        weaponSpec.weaponAngle = -60f; // 창을 앞으로 겨눔
        SetWeapon(rider, weaponSpec, rsr);

        // 예전 무기 자식이 몸(늑대)에 붙어 있으면 지움
        Transform stray = root.transform.Find("Weapon");
        if (stray != null) DestroyImmediate(stray.gameObject);
    }

    // 작살 투사체: 화살 프리팹을 본떠 그림만 창으로 (화살은 진행 방향(+x)으로 회전하므로, 세로 창 그림을 -90도 눕힌 자식으로 붙임)
    static GameObject BuildHarpoon()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ARROW) == null) { Debug.LogWarning("[MonsterFamilyBuilder] 화살 프리팹이 없어 작살을 만들지 못했습니다. 사냥꾼은 투사체 없이 바로 맞힙니다."); return null; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(HARPOON) == null && !AssetDatabase.CopyAsset(ARROW, HARPOON)) return null;

        GameObject root = PrefabUtility.LoadPrefabContents(HARPOON);
        root.name = "Projectile_Harpoon";
        root.transform.localScale = Vector3.one;
        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false; // 화살용 네모 그림은 숨김
        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) col.size = new Vector2(0.9f, 0.25f);

        Transform visual = root.transform.Find("Visual");
        if (visual == null)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
        }
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.Euler(0f, 0f, -90f);
        visual.localScale = new Vector3(0.6f, 0.6f, 1f);
        SpriteRenderer vsr = visual.GetComponent<SpriteRenderer>();
        if (vsr == null) vsr = visual.gameObject.AddComponent<SpriteRenderer>();
        vsr.sprite = LoadFrame("weapon_spear");
        if (sr != null) { vsr.sortingLayerID = sr.sortingLayerID; vsr.sortingOrder = sr.sortingOrder; }

        PrefabUtility.SaveAsPrefabAsset(root, HARPOON);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(HARPOON);
    }

    // 드래곤: 슬라임 스크립트를 드래곤 전용 스크립트로 (그림·크기는 'Tools > 기본 몬스터 임시 스프라이트 입히기')
    static void BuildDragon()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(DRAGON) == null) { Debug.LogWarning($"[MonsterFamilyBuilder] {DRAGON} 없음"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(DRAGON);

        BattleUnit old = root.GetComponent<BattleUnit>();
        GameObject hpBar = old != null ? old.hpBarPrefab : null;
        List<DropItem> drops = old is EnemySlime slime && slime.dropTable != null ? new List<DropItem>(slime.dropTable) : null;
        if (old != null && !(old is EnemyDragon)) DestroyImmediate(old);
        EnemyDragon dragon = root.GetComponent<EnemyDragon>();
        if (dragon == null) dragon = root.AddComponent<EnemyDragon>();
        if (hpBar != null) dragon.hpBarPrefab = hpBar;
        if (drops != null) dragon.dropTable = drops;

        dragon.maxHp          = DRAGON_HP;
        dragon.attackPower    = DRAGON_ATK;
        dragon.attackRange    = DRAGON_RANGE;
        dragon.moveSpeed      = DRAGON_SPEED;
        dragon.attackCooldown = DRAGON_COOLDOWN;
        dragon.aggro          = 10f;

        PrefabUtility.SaveAsPrefabAsset(root, DRAGON);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // 새끼 드래곤: 드래곤 프리팹을 복사해 크기만 줄이고, 능력치는 소환될 때 어른 드래곤 x 비율 (EnemyDragon.statSource)
    static void BuildYoungDragon(string file, float statRatio, float scale, float attackRange)
    {
        GameObject adult = AssetDatabase.LoadAssetAtPath<GameObject>(DRAGON);
        if (adult == null || adult.GetComponent<EnemyDragon>() == null) { Debug.LogWarning($"[MonsterFamilyBuilder] 드래곤 프리팹이 없어 {file}을 만들지 못했습니다."); return; }

        string path = $"{FOLDER}/{file}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !AssetDatabase.CopyAsset(DRAGON, path))
        {
            Debug.LogError($"[MonsterFamilyBuilder] {path} 복사 실패");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.name = file;
        root.tag = "Enemy";
        root.transform.localScale = new Vector3(scale, scale, 1f); // 충돌 박스도 같은 비율로 작아짐

        EnemyDragon young = root.GetComponent<EnemyDragon>();
        young.statSource  = adult;
        young.statRatio   = statRatio;
        young.attackRange = attackRange;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
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

    static Sprite LoadFrame(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{FRAMES}/{name}.png");
        if (sprite == null) Debug.LogWarning($"[MonsterFamilyBuilder] 스프라이트 {name} 없음");
        return sprite;
    }

    static GameObject Load(string file) => AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{file}.prefab");

    // 로비 씬 QuestGenerator의 무리 목록(packs)에 고블린·스켈레톤·오크 무리를 넣음 (이미 있으면 구성만 다시 맞춤)
    static void ConnectQuestGenerator()
    {
        var families = new[]
        {
            new EnemyPack { prefab = Load("Enemy_Goblin"), leader = Load(goblinPriest.file),
                            members = new List<GameObject> { Load(goblinWarrior.file), Load(goblinHunter.file) },
                            sizePerRank = new[] { 3, 3, 4, 4, 5 } },
            new EnemyPack { prefab = Load("Enemy_Skeleton"),
                            members = new List<GameObject> { Load(skeletonWarrior.file), Load(skeletonArcher.file), Load(skeletonTank.file) },
                            sizePerRank = new[] { 3, 3, 3, 4, 5 } },
            new EnemyPack { prefab = Load("Enemy_Orc"),
                            members = new List<GameObject> { Load(orcGrunt.file), Load(orcRider.file) },
                            sizePerRank = new[] { 2, 2, 2, 3, 4 } },
        };

        Scene lobby = SceneManager.GetSceneByPath(LOBBY_SCENE);
        bool openedHere = !lobby.isLoaded;
        if (openedHere) lobby = EditorSceneManager.OpenScene(LOBBY_SCENE, OpenSceneMode.Additive);

        int connected = 0;
        foreach (GameObject go in lobby.GetRootGameObjects())
        {
            foreach (QuestGenerator generator in go.GetComponentsInChildren<QuestGenerator>(true))
            {
                if (generator.packs == null) generator.packs = new List<EnemyPack>();
                foreach (EnemyPack family in families)
                {
                    if (family.prefab == null) { Debug.LogWarning("[MonsterFamilyBuilder] 무리 대표 프리팹을 찾지 못했습니다."); continue; }
                    generator.packs.RemoveAll(p => p != null && p.prefab == family.prefab);
                    generator.packs.Add(family);
                }
                EditorUtility.SetDirty(generator);
                connected++;
            }
        }

        if (connected == 0) Debug.LogWarning("[MonsterFamilyBuilder] 로비 씬에서 QuestGenerator를 찾지 못했습니다.");
        else
        {
            EditorSceneManager.MarkSceneDirty(lobby);
            EditorSceneManager.SaveScene(lobby);
            Debug.Log($"[MonsterFamilyBuilder] QuestGenerator {connected}개에 고블린·스켈레톤·오크 무리 연결");
        }
        if (openedHere) EditorSceneManager.CloseScene(lobby, true);
    }
}
