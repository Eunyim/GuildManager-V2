using UnityEngine;
using UnityEditor;

/// <summary>
/// [메뉴] Tools > 화살 임시 스프라이트 입히기
/// 화살 투사체(Prefab_Arrow)의 네모 그림을 0x72 타일셋 화살 그림으로 바꿉니다. (아군 궁수, 도적단 궁수, 스켈레톤 궁수가 같이 씀)
/// 화살은 날아가는 방향(+x)으로 회전하므로, 촉이 위를 향한 세로 그림을 -90도 눕힌 자식 'Visual'로 붙입니다.
/// 충돌 판정의 화면 크기는 예전 그대로 유지합니다. 다시 실행해도 안전합니다.
/// </summary>
public class ProjectileSpriteBuilder : Editor
{
    const string ARROW        = "Assets/Prefabs/Objects/Prefab_Arrow.prefab";
    const string ARROW_SPRITE = "Assets/Art/Tiles/0x72_DungeonTilesetII_v1.7/frames/weapon_arrow.png";
    static readonly Vector2 colliderWorld = new Vector2(0.32f, 0.02f); // 예전: 로컬 0.8 x 0.2 x 스케일 0.4 x 0.1
    const float VISUAL_SCALE = 0.5f; // 화살 그림 21px → 화면 길이 약 0.66

    [MenuItem("Tools/화살 임시 스프라이트 입히기")]
    static void Build()
    {
        if (Application.isPlaying) { Debug.LogError("[ProjectileSpriteBuilder] 플레이 모드에서는 실행할 수 없습니다."); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ARROW) == null) { Debug.LogError($"[ProjectileSpriteBuilder] {ARROW} 없음"); return; }
        Sprite arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ARROW_SPRITE);
        if (arrowSprite == null) { Debug.LogError($"[ProjectileSpriteBuilder] {ARROW_SPRITE} 없음"); return; }

        GameObject root = PrefabUtility.LoadPrefabContents(ARROW);
        root.transform.localScale = Vector3.one;

        SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false; // 네모 그림은 숨김 (컴포넌트는 정렬 기준으로 남겨 둠)
        BoxCollider2D col = root.GetComponent<BoxCollider2D>();
        if (col != null) { col.size = colliderWorld; col.offset = Vector2.zero; }

        Transform visual = root.transform.Find("Visual");
        if (visual == null)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
        }
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.Euler(0f, 0f, -90f); // 촉이 +x(진행 방향)를 향하게
        visual.localScale = new Vector3(VISUAL_SCALE, VISUAL_SCALE, 1f);
        SpriteRenderer vsr = visual.GetComponent<SpriteRenderer>();
        if (vsr == null) vsr = visual.gameObject.AddComponent<SpriteRenderer>();
        vsr.sprite = arrowSprite;
        vsr.color = Color.white;
        if (sr != null) { vsr.sortingLayerID = sr.sortingLayerID; vsr.sortingOrder = sr.sortingOrder; }

        PrefabUtility.SaveAsPrefabAsset(root, ARROW);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        Debug.Log("=== 화살 임시 스프라이트 입히기 완료! ===");
    }
}
