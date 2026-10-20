using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 길드 밖 거리(TownView)와 신전·치유소·건축 공방 창의 배경을 도트 그림으로 바꿈
// 그림: Assets/Art/Town (Tools/town_art_gen.py로 생성, 480x270 → 화면에 정확히 4배). 여러 번 실행해도 결과는 같음
public static class TownArtSetup
{
    const string ScenePath = "Assets/Scenes/LobbyScene.unity";
    const string Art = "Assets/Art/Town/";
    const float Scale = 4f;          // 도트 한 칸 = 화면 4px (1920x1080 기준)
    const float StreetBase = 272f;   // 건물 아랫변 (화면 아래에서)

    [MenuItem("Tools/길드 밖 거리·건물 창 도트 배경 적용")]
    static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[TownArtSetup] 플레이 중에는 실행할 수 없습니다."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DungGeunMo SDF.asset");
        Sprite plate = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Theme/ui_panel_small.png");

        Transform town = Find(scene, "TownView");
        if (town != null)
        {
            SetImage(town.GetComponent<Image>(), Load("town_bg"), Color.white);
            Transform ground = town.Find("Bg_Ground");
            if (ground != null) { Undo.RecordObject(ground.gameObject, "거리"); ground.gameObject.SetActive(false); }

            Building(town, "Building_Workshop", "facade_workshop", plate);
            Building(town, "Building_Temple",   "facade_temple", plate);
            Building(town, "Building_Clinic",   "facade_clinic", plate);
            Building(town, "Building_Shop",     "facade_shop", plate);

            // 공방: 굴뚝 연기, 대장간 불빛 (움직임)
            Transform ws = town.Find("Building_Workshop");
            if (ws != null)
            {
                Animated(ws, "Smoke", "smoke", 4, new Vector2(25.5f * Scale, 108f * Scale), new Vector2(16f, 40f) * Scale, 6f);
                Animated(ws, "ForgeGlow", "forge_glow", 4, new Vector2(-6f * Scale, 1f * Scale), new Vector2(37f, 35f) * Scale, 8f);
            }

            // 제목 뒤 나무판
            Transform title = town.Find("Txt_Title");
            if (title != null)
            {
                Transform platet = town.Find("TitlePlate");
                if (platet == null)
                {
                    GameObject go = new GameObject("TitlePlate", typeof(RectTransform), typeof(Image));
                    Undo.RegisterCreatedObjectUndo(go, "거리");
                    go.transform.SetParent(town, false);
                    platet = go.transform;
                }
                platet.SetSiblingIndex(title.GetSiblingIndex());
                RectTransform pr = (RectTransform)platet, tr = (RectTransform)title;
                pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f);
                pr.pivot = new Vector2(0.5f, 1f);
                pr.sizeDelta = new Vector2(440f, 96f);
                pr.anchoredPosition = new Vector2(0f, tr.anchoredPosition.y + 8f);
                Image pimg = platet.GetComponent<Image>();
                SetSliced(pimg, plate);
                pimg.raycastTarget = false;
                TextMeshProUGUI tt = title.GetComponent<TextMeshProUGUI>();
                if (tt != null) { Undo.RecordObject(tt, "거리"); tt.color = new Color32(0xF2, 0xCD, 0x6A, 0xFF); if (font != null) tt.font = font; }
            }
        }
        else Debug.LogWarning("[TownArtSetup] TownView를 찾지 못했습니다.");

        // 건물 창 뒤 배경 (실내 그림을 어둡게)
        PopupBackground(scene, "Popup_Temple", "interior_temple");
        PopupBackground(scene, "Popup_Clinic", "interior_clinic");
        PopupBackground(scene, "Popup_FacilityUpgrade", "interior_workshop");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TownArtSetup] 거리·건물 창 배경을 적용하고 LobbyScene을 저장했습니다.");
    }

    static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

    static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }

    static void SetImage(Image img, Sprite sprite, Color color)
    {
        if (img == null || sprite == null) return;
        Undo.RecordObject(img, "거리");
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.color = color;
    }

    static void SetSliced(Image img, Sprite sprite)
    {
        if (img == null || sprite == null) return;
        Undo.RecordObject(img, "거리");
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / 3f;
        img.color = Color.white;
    }

    // 건물: 정면 그림 + 아래쪽에 이름판
    static void Building(Transform town, string objName, string art, Sprite plate)
    {
        Transform b = town.Find(objName);
        Sprite facade = Load(art);
        if (b == null || facade == null) { Debug.LogWarning($"[TownArtSetup] {objName} 또는 {art} 없음"); return; }

        RectTransform rt = (RectTransform)b;
        Undo.RecordObject(rt, "거리");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(facade.rect.width, facade.rect.height) * Scale;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, StreetBase);
        SetImage(b.GetComponent<Image>(), facade, Color.white);

        // 마우스를 올리면 노란 외곽선
        if (b.GetComponent<UIHoverOutline>() == null) Undo.AddComponent<UIHoverOutline>(b.gameObject);

        Transform roof = b.Find("Roof");
        if (roof != null) { Undo.RecordObject(roof.gameObject, "거리"); roof.gameObject.SetActive(false); }

        Transform name = b.Find("NamePlate");
        if (name != null)
        {
            RectTransform nr = (RectTransform)name;
            Undo.RecordObject(nr, "거리");
            nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.sizeDelta = new Vector2(340f, 124f);
            nr.anchoredPosition = new Vector2(0f, -40f);
            Image ni = name.GetComponent<Image>();
            SetSliced(ni, plate);
            if (ni != null) ni.raycastTarget = false;
            foreach (TextMeshProUGUI t in name.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Undo.RecordObject(t, "거리");
                t.color = t.name == "Txt_Name" ? (Color)new Color32(0xF2, 0xCD, 0x6A, 0xFF) : new Color32(0xEF, 0xE0, 0xBF, 0xFF);
                RectTransform tr = t.rectTransform;
                tr.anchoredPosition = new Vector2(0f, t.name == "Txt_Name" ? 20f : -28f);
            }
        }
    }

    // 움직이는 장식 (UI 이미지 + 프레임 애니메이션)
    static void Animated(Transform parent, string objName, string art, int count, Vector2 pos, Vector2 size, float fps)
    {
        Sprite[] frames = new Sprite[count];
        for (int i = 0; i < count; i++) frames[i] = Load($"{art}_{i}");
        if (frames[0] == null) return;

        Transform t = parent.Find(objName);
        if (t == null)
        {
            GameObject go = new GameObject(objName, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "거리");
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        RectTransform rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = t.GetComponent<Image>();
        SetImage(img, frames[0], Color.white);
        img.raycastTarget = false;
        SpriteFrameAnimator anim = t.GetComponent<SpriteFrameAnimator>();
        if (anim == null) anim = Undo.AddComponent<SpriteFrameAnimator>(t.gameObject);
        anim.frames = frames;
        anim.fps = fps;
    }

    static void PopupBackground(Scene scene, string popupName, string art)
    {
        Transform popup = Find(scene, popupName);
        Sprite bg = Load(art);
        if (popup == null || bg == null) { Debug.LogWarning($"[TownArtSetup] {popupName} 또는 {art} 없음"); return; }
        Image img = popup.GetComponent<Image>();
        if (img == null)
        {
            // 화면 전체 배경 자식
            foreach (Transform c in popup)
            {
                RectTransform r = (RectTransform)c;
                if (r.anchorMin == Vector2.zero && r.anchorMax == Vector2.one && c.GetComponent<Image>() != null) { img = c.GetComponent<Image>(); break; }
            }
        }
        if (img == null) { Debug.LogWarning($"[TownArtSetup] {popupName}에 배경 이미지가 없습니다."); return; }
        SetImage(img, bg, new Color(0.62f, 0.58f, 0.56f, 1f)); // 창이 잘 읽히도록 어둡게
    }
}
