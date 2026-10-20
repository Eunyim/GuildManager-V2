using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// [메뉴] Tools > 던전 모닥불 그림 연결
/// DungeonScene의 모닥불 오브젝트(BattleManager.campfireObject)에 CampfireVisual을 붙이고 Tools/campfire_art_gen.py 그림을 연결합니다.
/// 다시 실행해도 안전합니다.
/// </summary>
public static class CampfireArtSetup
{
    const string ScenePath = "Assets/Scenes/DungeonScene.unity";
    const string ArtDir = "Assets/Art/Dungeon/Campfire/";

    [MenuItem("Tools/던전 모닥불 그림 연결")]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogError("[CampfireArtSetup] 플레이를 끄고 실행하세요."); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        BattleManager bm = Object.FindFirstObjectByType<BattleManager>(FindObjectsInactive.Include);
        if (bm == null || bm.campfireObject == null) { Debug.LogError("[CampfireArtSetup] BattleManager 또는 campfireObject가 없습니다. 먼저 'Tools > 던전 모닥불 세팅'을 실행하세요."); return; }

        GameObject fire = bm.campfireObject;
        CampfireVisual v = fire.GetComponent<CampfireVisual>();
        if (v == null) v = Undo.AddComponent<CampfireVisual>(fire);
        Undo.RecordObject(v, "모닥불 그림 연결");
        v.baseSprite = Art("campfire_base");
        v.glowSprite = Art("campfire_glow");
        v.seatLogSprite = Art("campfire_seat_log");
        v.seatStumpSprite = Art("campfire_seat_stump");
        v.emberSprite = Art("campfire_ember");
        v.smokeSprite = Art("campfire_smoke");
        v.flameFrames = new Sprite[6];
        for (int i = 0; i < 6; i++) v.flameFrames[i] = Art($"campfire_flame_{i}");

        // 예전 임시 그림(주황 네모)은 끔 — 크기도 1로
        SpriteRenderer own = fire.GetComponent<SpriteRenderer>();
        if (own != null) { Undo.RecordObject(own, "모닥불 그림 연결"); own.enabled = false; }
        Undo.RecordObject(fire.transform, "모닥불 그림 연결");
        fire.transform.localScale = Vector3.one;

        // 커진 화덕 둘레에 넉넉히 앉도록
        Undo.RecordObject(bm, "모닥불 그림 연결");
        bm.campfireGatherRadius = Mathf.Max(bm.campfireGatherRadius, 1.45f);
        bm.campfireMaxViewportX = 0.46f; // 둘레 자리까지 휴식 창(화면 오른쪽)에 가리지 않게
        EditorUtility.SetDirty(bm);

        EditorUtility.SetDirty(v);
        EditorSceneManager.MarkSceneDirty(fire.scene);
        EditorSceneManager.SaveScene(fire.scene);
        Debug.Log("=== 던전 모닥불 그림 연결 완료 (DungeonScene 저장됨) ===");
    }

    static Sprite Art(string name)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
        if (s == null) Debug.LogError($"[CampfireArtSetup] {ArtDir}{name}.png 스프라이트가 없습니다. python Tools/campfire_art_gen.py 실행 후 'Tools > 도트 그림 다시 가져오기'를 하세요.");
        return s;
    }
}
