using UnityEngine;
using UnityEditor;

/// <summary>
/// [메뉴] Tools > 미믹 프리팹 만들기
/// 고블린 프리팹을 본떠 Enemy_Mimic 프리팹을 만들고, 보물상자 프리팹의 mimicEnemyPrefab에 연결합니다.
/// 이미 있으면 스탯·스프라이트만 다시 맞추므로 다시 실행해도 안전합니다.
/// </summary>
public class MimicPrefabBuilder : Editor
{
    const string TEMPLATE_PREFAB = "Assets/Prefabs/Characters/Enemy_Goblin.prefab";
    const string MIMIC_PREFAB    = "Assets/Prefabs/Characters/Enemy_Mimic.prefab";
    const string CHEST_PREFAB    = "Assets/Prefabs/Objects/Prefab_TreasureChest.prefab";
    const string SPRITE_FOLDER   = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames";

    [MenuItem("Tools/미믹 프리팹 만들기")]
    static void Build()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[MimicPrefabBuilder] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(MIMIC_PREFAB) == null
            && !AssetDatabase.CopyAsset(TEMPLATE_PREFAB, MIMIC_PREFAB))
        {
            Debug.LogError($"[MimicPrefabBuilder] {TEMPLATE_PREFAB} 복사 실패");
            return;
        }

        Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_FOLDER}/chest_mimic_open_anim_f1.png");
        Sprite bite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SPRITE_FOLDER}/chest_mimic_open_anim_f2.png");
        if (idle == null || bite == null)
            Debug.LogWarning("[MimicPrefabBuilder] 미믹 스프라이트를 찾지 못했습니다. 외형은 고블린 임시 스프라이트로 남습니다.");

        GameObject root = PrefabUtility.LoadPrefabContents(MIMIC_PREFAB);
        root.name = "Enemy_Mimic";
        root.tag  = "Enemy";
        root.transform.localScale = Vector3.one;

        // 고블린 스크립트 → 미믹 스크립트 (체력바 프리팹은 옮겨 담음)
        GameObject hpBar = null;
        BattleUnit oldUnit = root.GetComponent<BattleUnit>();
        if (oldUnit != null && !(oldUnit is EnemyMimic))
        {
            hpBar = oldUnit.hpBarPrefab;
            DestroyImmediate(oldUnit);
        }
        EnemyMimic mimic = root.GetComponent<EnemyMimic>();
        if (mimic == null) mimic = root.AddComponent<EnemyMimic>();
        if (hpBar != null) mimic.hpBarPrefab = hpBar;

        // 미믹 스탯: 혼자 파티 전체를 상대하므로 맷집이 좋고 한 방이 아픔, 대신 느림
        mimic.maxHp          = 180f;
        mimic.attackPower    = 28f;
        mimic.attackRange    = 1.0f;
        mimic.moveSpeed      = 1.0f;
        mimic.attackCooldown = 1.4f;
        mimic.aggro          = 10f;
        mimic.idleSprite     = idle;
        mimic.biteSprite     = bite;

        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (idle != null) sr.sprite = idle;
            sr.color = Color.white; // 고블린 임시 스프라이트의 빨간 색조 제거
        }

        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) col.size = new Vector2(1f, 1f);

        PrefabUtility.SaveAsPrefabAsset(root, MIMIC_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);

        // 보물상자에 연결
        GameObject mimicAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MIMIC_PREFAB);
        GameObject chestRoot  = PrefabUtility.LoadPrefabContents(CHEST_PREFAB);
        TreasureChest chest   = chestRoot.GetComponent<TreasureChest>();
        if (chest != null)
        {
            chest.mimicEnemyPrefab = mimicAsset;
            PrefabUtility.SaveAsPrefabAsset(chestRoot, CHEST_PREFAB);
        }
        else
        {
            Debug.LogError("[MimicPrefabBuilder] 보물상자 프리팹에 TreasureChest가 없습니다.");
        }
        PrefabUtility.UnloadPrefabContents(chestRoot);

        AssetDatabase.SaveAssets();
        Debug.Log("=== 미믹 프리팹 만들기 완료! (Enemy_Mimic → 보물상자 mimicEnemyPrefab 연결) ===");
    }
}
