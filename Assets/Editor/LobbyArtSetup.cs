using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 로비(길드 1층 홀·2층 여관)의 임시 큐브를 도트 오브젝트로 바꾸고, 뒤쪽 벽·횃불·벽난로를 놓음
// 그림: Assets/Art/Lobby/Pixel (Tools/lobby_art_gen.py로 생성). 여러 번 실행해도 결과는 같음
//  - 오브젝트 위치 = 그림의 발밑(바닥에 닿는 점). 화면 아래쪽에 있을수록 앞에 그려지도록 순서를 매김
//  - 클릭 영역(BoxCollider2D)은 그림 크기에 맞춤
public static class LobbyArtSetup
{
    const string ScenePath = "Assets/Scenes/LobbyScene.unity";
    const string ArtFolder = "Assets/Art/Lobby/Pixel/";

    // 바닥(16x10칸) 꼭짓점 (월드). 왼쪽 뒤 벽: 왼쪽 → 위, 오른쪽 뒤 벽: 오른쪽 → 위
    static readonly Vector2 LeftCorner = new Vector2(-6.5f, -0.75f);
    static readonly Vector2 RightCorner = new Vector2(6.5f, 0.75f);
    static readonly Vector2 AxisA = new Vector2(0.5f, 0.25f);  // 왼쪽 뒤 벽을 따라 한 칸
    static readonly Vector2 AxisB = new Vector2(-0.5f, 0.25f); // 오른쪽 뒤 벽을 따라 한 칸

    const int RugOrder = -7, WallOrder = -5, WallDecorOrder = -4, ObjectBaseOrder = 100, LabelOrder = 600;

    static Vector2 LeftWall(float s) => LeftCorner + AxisA * s;

    // 1층 큰 가구 줄: 모험가 관리 테이블(-3.3, -0.8)을 지나 바닥 격자(a축) 방향으로 이어지는 선. x만 주면 그 선 위의 점
    static readonly Vector2 RowOrigin = new Vector2(-3.3f, -0.8f);
    static readonly Vector2 RowShift = -AxisA * 1.2f; // 줄 전체를 왼쪽 아래로 1.2칸 (카펫·원탁 포함)
    static Vector2 Row(float x) => new Vector2(x, RowOrigin.y + (x - RowOrigin.x) * (AxisA.y / AxisA.x)) + RowShift;
    static Vector2 RightWall(float s) => RightCorner + AxisB * s;

    [MenuItem("Tools/로비 도트 오브젝트 적용")]
    static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[LobbyArtSetup] 플레이 중에는 실행할 수 없습니다."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        Transform floor1 = Find(scene, "Lobby_Facilities");
        Transform floor2 = Find(scene, "Lobby_Floor2");
        if (floor1 == null || floor2 == null) { Debug.LogError("[LobbyArtSetup] Lobby_Facilities / Lobby_Floor2를 찾지 못했습니다."); return; }

        // ── 1층 ──
        Apply(floor1, "Facility_Reception",  "reception", Row(1.9f), animated: true, fps: 6f);   // 출입문 앞: 들어오면 바로 보이는 자리
        Apply(floor1, "Facility_QuestBoard", "questboard", LeftWall(7.5f) + new Vector2(0.15f, -0.4f));
        Apply(floor1, "Facility_Adventurers", "adventurers", RowOrigin + RowShift);                        // 홀 왼쪽
        Apply(floor1, "Facility_Pub",        "pub", Row(-0.6f));                                // 카펫 위 홀 한가운데
        Apply(floor1, "Facility_StairsUp",   "stairs_up", RightWall(1.8f) + new Vector2(-0.25f, -0.125f));
        Apply(floor1, "Facility_Door",       "guild_door", RightWall(5.6f), flipX: true); // 오른쪽 뒤 벽에 난 출입문 (왼쪽 벽용 그림을 좌우로 뒤집음)

        Decor(floor1, "Wall_Left",  "wall_hall_left",  LeftCorner, WallOrder);
        Decor(floor1, "Wall_Right", "wall_hall_right", RightCorner, WallOrder);
        for (int i = 0; i < 4; i++)
            Decor(floor1, $"Torch_L{i}", "torch", LeftWall(4f * i + 2f) + new Vector2(0f, 1.0f), WallDecorOrder, animated: true, fps: 9f);
        Decor(floor1, "Torch_R0", "torch", RightWall(2f) + new Vector2(0f, 1.0f), WallDecorOrder, animated: true, fps: 9f);
        Decor(floor1, "Torch_R1", "torch", RightWall(9.3f) + new Vector2(0f, 1.0f), WallDecorOrder, animated: true, fps: 9f);
        Decor(floor1, "Fireplace", "fireplace", RightWall(7.8f) + new Vector2(-0.1f, -0.05f), 0, animated: true, fps: 8f, ySort: true);
        Decor(floor1, "Rug", "rug_hall", Row(-0.6f), RugOrder); // 주점 바로 밑 중앙
        Decor(floor1, "Deco_Crates", "crates", new Vector2(-5.7f, -0.75f), 0, ySort: true);
        Decor(floor1, "Deco_Table1", "tavern_table", Row(-0.6f) + new Vector2(-0.9f, -2.15f), 0, ySort: true);
        Decor(floor1, "Deco_Table2", "tavern_table", Row(-0.6f) + new Vector2(0f, -1.7f), 0, ySort: true);
        Decor(floor1, "Deco_Table3", "tavern_table", Row(-0.6f) + new Vector2(0.9f, -1.25f), 0, ySort: true);
        Decor(floor1, "Deco_Crates2", "crates", new Vector2(4.4f, 0.1f), 0, ySort: true);
        Decor(floor1, "Deco_WeaponRack", "weapon_rack", LeftWall(13.3f) + new Vector2(0.1f, -0.1f), 0, ySort: true);

        // ── 2층 ──
        Decor(floor2, "Wall_Left",  "wall_inn_left",  LeftCorner, WallOrder);
        Decor(floor2, "Wall_Right", "wall_inn_right", RightCorner, WallOrder);
        Apply(floor2, "Facility_StairsDown", "stairs_down", RightWall(1.8f) + new Vector2(-0.25f, -0.125f));
        for (int i = 0; i < 5; i++)
            Apply(floor2, $"Inn_Room{i + 1}", "inn_door", LeftWall(3f * i + 2.5f) + new Vector2(0.125f, -0.0625f), animated: true, fps: 4f, labelY: 1.55f, labelX: 0.25f);
        Apply(floor2, "Inn_Sign", "inn_sign", new Vector2(0.5f, -1f), labelY: 1.5f, labelX: 0.2f);
        Decor(floor2, "Rug", "rug_inn", LeftWall(8.5f) + new Vector2(0.55f, -0.3f), RugOrder);
        Decor(floor2, "Deco_Crates", "crates", new Vector2(3.3f, -0.35f), 0, ySort: true);

        // 잠긴 방: 시커멓게 보이지 않도록 살짝만 어둡게
        InnFloor inn = floor2.GetComponentInChildren<InnFloor>(true);
        if (inn == null) foreach (GameObject r in scene.GetRootGameObjects()) { inn = r.GetComponentInChildren<InnFloor>(true); if (inn != null) break; }
        if (inn != null)
        {
            Undo.RecordObject(inn, "로비 도트 오브젝트");
            inn.lockedColor = new Color(0.58f, 0.54f, 0.52f, 1f);
        }

        // 상단 정보줄 띠: 반투명 검은 판 → 나무판 + 금 줄
        Sprite hud = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Theme/ui_hud_bar.png");
        Transform topImage = Find(scene, "TopBar") != null ? Find(scene, "TopBar").Find("Image") : null;
        if (hud != null && topImage != null)
        {
            UnityEngine.UI.Image img = topImage.GetComponent<UnityEngine.UI.Image>();
            Undo.RecordObject(img, "로비 도트 오브젝트");
            img.sprite = hud;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / 3f;
            img.color = new Color(1f, 1f, 1f, 0.94f);
            // 시설 줄(아래 칸)까지 한 판으로 덮음
            RectTransform rt = (RectTransform)topImage;
            Undo.RecordObject(rt, "로비 도트 오브젝트");
            rt.offsetMin = new Vector2(0f, -44f);
            Transform facilityBar = Find(scene, "FacilityBar");
            UnityEngine.UI.Image fb = facilityBar != null ? facilityBar.GetComponent<UnityEngine.UI.Image>() : null;
            if (fb != null) { Undo.RecordObject(fb, "로비 도트 오브젝트"); fb.color = new Color(0f, 0f, 0f, 0f); }
        }
        Decor(floor2, "Torch_R0", "torch", RightWall(3f) + new Vector2(0f, 1.0f), WallDecorOrder, animated: true, fps: 9f);
        Decor(floor2, "Torch_R1", "torch", RightWall(7f) + new Vector2(0f, 1.0f), WallDecorOrder, animated: true, fps: 9f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[LobbyArtSetup] 로비 도트 오브젝트를 적용하고 LobbyScene을 저장했습니다.");
    }

    static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        }
        return null;
    }

    static Sprite[] LoadFrames(string name, bool animated)
    {
        if (!animated)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");
            return s != null ? new[] { s } : new Sprite[0];
        }
        var list = new List<Sprite>();
        for (int i = 0; ; i++)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}{name}_{i}.png");
            if (s == null) break;
            list.Add(s);
        }
        return list.ToArray();
    }

    // 화면 아래쪽(앞쪽)에 있을수록 나중에 그림
    static int SortOrder(float y) => ObjectBaseOrder + Mathf.RoundToInt(-y * 20f);

    // 기존 시설 오브젝트(클릭 가능)의 그림·위치·순서·클릭 영역을 바꿈
    static void Apply(Transform parent, string objName, string art, Vector2 pos, bool animated = false, float fps = 8f, float labelY = -1f, float labelX = 0f, bool flipX = false)
    {
        Transform t = parent.Find(objName);
        if (t == null) { Debug.LogWarning($"[LobbyArtSetup] {parent.name}/{objName} 없음"); return; }
        Sprite[] frames = LoadFrames(art, animated);
        if (frames.Length == 0) { Debug.LogWarning($"[LobbyArtSetup] 그림 없음: {art}"); return; }

        Undo.RecordObject(t, "로비 도트 오브젝트");
        t.localPosition = new Vector3(pos.x, pos.y, 0f);
        t.localScale = Vector3.one;

        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        Undo.RecordObject(sr, "로비 도트 오브젝트");
        sr.sprite = frames[0];
        sr.flipX = flipX; // 피벗을 기준으로 좌우가 뒤집힘
        sr.sortingOrder = SortOrder(pos.y);
        SetAnimator(t.gameObject, frames, fps);

        // 클릭 영역 = 그림 영역 (조금 작게)
        BoxCollider2D box = t.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Undo.RecordObject(box, "로비 도트 오브젝트");
            Bounds b = frames[0].bounds;
            box.size = new Vector2(b.size.x * 0.85f, b.size.y * 0.85f);
            box.offset = new Vector2(flipX ? -b.center.x : b.center.x, b.center.y);
        }

        // 이름표(2층 방·안내판)는 그림 위로
        if (labelY > 0f)
        {
            Transform label = t.Find("Label");
            if (label != null)
            {
                RectTransform rt = label as RectTransform;
                Undo.RecordObject(label, "로비 도트 오브젝트");
                if (rt != null) rt.anchoredPosition = new Vector2(labelX, labelY);
                else label.localPosition = new Vector3(labelX, labelY, 0f);
                TextMeshPro tmp = label.GetComponent<TextMeshPro>();
                if (tmp != null) { Undo.RecordObject(tmp, "로비 도트 오브젝트"); tmp.sortingOrder = LabelOrder; }
            }
        }
    }

    // 장식(벽·횃불·벽난로): 없으면 만듦
    static void Decor(Transform parent, string objName, string art, Vector2 pos, int order, bool animated = false, float fps = 8f, bool ySort = false)
    {
        Sprite[] frames = LoadFrames(art, animated);
        if (frames.Length == 0) { Debug.LogWarning($"[LobbyArtSetup] 그림 없음: {art}"); return; }

        Transform t = parent.Find(objName);
        if (t == null)
        {
            GameObject go = new GameObject(objName, typeof(SpriteRenderer));
            Undo.RegisterCreatedObjectUndo(go, "로비 도트 오브젝트");
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        Undo.RecordObject(t, "로비 도트 오브젝트");
        t.localPosition = new Vector3(pos.x, pos.y, 0f);
        SpriteRenderer sr = t.GetComponent<SpriteRenderer>();
        Undo.RecordObject(sr, "로비 도트 오브젝트");
        sr.sprite = frames[0];
        sr.sortingOrder = ySort ? SortOrder(pos.y) : order;
        SetAnimator(t.gameObject, frames, fps);
    }

    static void SetAnimator(GameObject go, Sprite[] frames, float fps)
    {
        SpriteFrameAnimator anim = go.GetComponent<SpriteFrameAnimator>();
        if (frames.Length <= 1)
        {
            if (anim != null) Undo.DestroyObjectImmediate(anim);
            return;
        }
        if (anim == null) anim = Undo.AddComponent<SpriteFrameAnimator>(go);
        Undo.RecordObject(anim, "로비 도트 오브젝트");
        anim.frames = frames;
        anim.fps = fps;
    }
}
