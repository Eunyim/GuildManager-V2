using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [메뉴] Tools > 모험가 신상 창·접수처 배경 다듬기
//  1. 모험가 신상(Popup_Manage_Detail): 가로로 넓힌 창에
//     왼쪽 칸 = 이름 · 직업/랭크/레벨 · 능력치, 가운데 = 모험가 모습(조립 그림), 오른쪽 칸 = 상태(파티·무드·사기) · 성격/특성 · 관계
//  2. 접수처(ReceptionView) 배경을 로비와 같은 실내 그림(Assets/Art/Town/reception_bg)으로
// 여러 번 실행해도 결과는 같음. LobbyScene을 저장함
public static class DetailPopupLayout
{
    const string ScenePath = "Assets/Scenes/LobbyScene.unity";
    static readonly Color32 Gold = new Color32(0xF2, 0xCD, 0x6A, 0xFF);
    static readonly Color32 Body = new Color32(0xEF, 0xE0, 0xBF, 0xFF);
    static readonly Color32 Head = new Color32(0xC8, 0xA8, 0x60, 0xFF);

    [MenuItem("Tools/모험가 신상 창·접수처 배경 다듬기")]
    static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[DetailPopupLayout] 플레이 중에는 실행할 수 없습니다."); return; }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        LayoutDetail(scene);
        ReceptionBackground(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DetailPopupLayout] 모험가 신상 창과 접수처 배경을 적용하고 LobbyScene을 저장했습니다.");
    }

    static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }

    static Transform Child(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sprite, Color color)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "신상 창");
            go.transform.SetParent(parent, false);
            t = go.transform;
        }
        RectTransform rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = t.GetComponent<Image>();
        img.sprite = sprite;
        img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = 1f / 3f;
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // 칸 안에서 위에서부터 차례로 쌓는 글 (VerticalLayoutGroup이 높이를 맞춤)
    static void Stack(RectTransform panel)
    {
        VerticalLayoutGroup v = panel.GetComponent<VerticalLayoutGroup>();
        if (v == null) v = Undo.AddComponent<VerticalLayoutGroup>(panel.gameObject);
        v.padding = new RectOffset(34, 30, 30, 26);
        v.spacing = 8;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;
    }

    static TextMeshProUGUI Text(Transform t, RectTransform panel, float size, Color color, int order)
    {
        Undo.SetTransformParent(t, panel, "신상 창");
        t.SetSiblingIndex(order);
        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
        Undo.RecordObject(tmp, "신상 창");
        tmp.fontSize = size;
        tmp.enableAutoSizing = false;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.lineSpacing = 8f;
        tmp.margin = Vector4.zero;
        tmp.raycastTarget = true;   // 성격·관계 링크 툴팁
        return tmp;
    }

    static TextMeshProUGUI Header(RectTransform panel, string name, string label, int order, TMP_FontAsset font)
    {
        Transform t = panel.Find(name);
        if (t == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "신상 창");
            go.transform.SetParent(panel, false);
            t = go.transform;
        }
        t.SetSiblingIndex(order);
        TextMeshProUGUI tmp = t.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = $"<b>{label}</b>";
        tmp.fontSize = 26;
        tmp.color = Head;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.raycastTarget = false;
        LayoutElement le = t.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(t.gameObject);
        le.minHeight = 44;
        return tmp;
    }

    static void LayoutDetail(Scene scene)
    {
        Transform popup = Find(scene, "Popup_Manage_Detail");
        if (popup == null) { Debug.LogWarning("[DetailPopupLayout] Popup_Manage_Detail 없음"); return; }
        Transform window = popup.Find("Window");
        Transform outline = popup.Find("Outline");
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Theme/ui_panel_small.png");
        LobbyManager lm = Object.FindFirstObjectByType<LobbyManager>(FindObjectsInactive.Include);

        Undo.RecordObject(window, "신상 창");
        ((RectTransform)window).sizeDelta = new Vector2(1560f, 840f);
        if (outline != null) { Undo.RecordObject(outline, "신상 창"); ((RectTransform)outline).sizeDelta = new Vector2(1586f, 866f); }
        Transform title = window.Find("Title");
        if (title != null) { Undo.RecordObject(title, "신상 창"); ((RectTransform)title).anchoredPosition = new Vector2(0f, -62f); ((RectTransform)title).sizeDelta = new Vector2(1400f, 90f); }

        Color panelTint = new Color(0.78f, 0.72f, 0.66f, 1f);
        RectTransform left = Panel(window, "Panel_Left", new Vector2(-505f, -10f), new Vector2(470f, 600f), panelSprite, panelTint);
        RectTransform center = Panel(window, "Panel_Portrait", new Vector2(0f, -10f), new Vector2(480f, 600f), panelSprite, new Color(0.62f, 0.58f, 0.56f, 1f));
        RectTransform right = Panel(window, "Panel_Right", new Vector2(505f, -10f), new Vector2(470f, 600f), panelSprite, panelTint);
        Stack(left);
        Stack(right);

        // 가운데 바닥 (모험가가 서 있는 원판)
        RectTransform floor = Panel(center, "Portrait_Floor", Vector2.zero, new Vector2(300f, 70f), null, new Color(0f, 0f, 0f, 0.28f));
        floor.anchorMin = floor.anchorMax = new Vector2(0.5f, 0f);
        floor.anchoredPosition = new Vector2(0f, 96f);
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        floor.GetComponent<Image>().sprite = knob;
        floor.GetComponent<Image>().type = Image.Type.Simple;

        // 모험가 모습: 기준점(발) = 원판 가운데
        Transform portrait = Child(window, "Image_Portrait");
        if (portrait != null)
        {
            Undo.SetTransformParent(portrait, center, "신상 창");
            RectTransform pr = (RectTransform)portrait;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
            pr.pivot = new Vector2(0.5f, 0f);
            pr.anchoredPosition = new Vector2(0f, 100f);
            pr.sizeDelta = new Vector2(300f, 400f);
            portrait.SetAsLastSibling();
            Image pi = portrait.GetComponent<Image>();
            pi.raycastTarget = false;
            pi.preserveAspect = false;
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DungGeunMo SDF.asset");
        Transform info = Child(window, "Info_Panel");
        Transform tName = Child(window, "Txt_Name"), tInfo = Child(window, "Txt_Info"), tStats = Child(window, "Txt_Stats");
        Transform tParty = Child(window, "Txt_PartyStatus"), tTrait = Child(window, "Txt_Trait");
        if (tName == null || tInfo == null || tStats == null || tParty == null || tTrait == null)
        {
            Debug.LogWarning("[DetailPopupLayout] 신상 창 글자 오브젝트를 찾지 못했습니다.");
            return;
        }

        // 왼쪽: 이름 / 직업·랭크 / 능력치
        Text(tName, left, 46, Gold, 0);
        Text(tInfo, left, 28, Body, 1);
        Header(left, "Hdr_Stats", "능력치", 2, font);
        Text(tStats, left, 28, Body, 3);

        // 오른쪽: 상태 / 성격·특성 / 관계
        Header(right, "Hdr_Status", "상태", 0, font);
        Text(tParty, right, 26, Body, 1);
        Header(right, "Hdr_Trait", "성격 · 특성", 2, font);
        Text(tTrait, right, 26, Body, 3);
        Header(right, "Hdr_Relation", "관계", 4, font);
        Transform tRel = right.Find("Txt_Relations");
        if (tRel == null)
        {
            GameObject go = Object.Instantiate(tTrait.gameObject, right);
            Undo.RegisterCreatedObjectUndo(go, "신상 창");
            go.name = "Txt_Relations";
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
                if (!(mb is TextMeshProUGUI)) Object.DestroyImmediate(mb);
            tRel = go.transform;
        }
        TextMeshProUGUI relText = Text(tRel, right, 26, Body, 5);
        relText.text = "관계";

        if (info != null && info.childCount == 0) Undo.DestroyObjectImmediate(info.gameObject);

        // 버튼은 창 아래 양끝 (해고 / 닫기)
        Transform fire = window.Find("Btn_Fire"), close = window.Find("Btn_Close");
        if (fire != null) { Undo.RecordObject(fire, "신상 창"); ((RectTransform)fire).anchoredPosition = new Vector2(150f, 52f); ((RectTransform)fire).sizeDelta = new Vector2(200f, 56f); }
        if (close != null)
        {
            Undo.RecordObject(close, "신상 창");
            ((RectTransform)close).anchoredPosition = new Vector2(-150f, 52f);
            ((RectTransform)close).sizeDelta = new Vector2(200f, 56f);
            TextMeshProUGUI label = close.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) { Undo.RecordObject(label, "신상 창"); label.text = "닫기"; }
        }
        Transform tip = window.Find("Tooltip_Panel");
        if (tip != null) tip.SetAsLastSibling();

        if (lm != null)
        {
            SerializedObject so = new SerializedObject(lm);
            so.FindProperty("detailRelations").objectReferenceValue = relText;
            so.FindProperty("detailPortrait").objectReferenceValue = portrait != null ? portrait.GetComponent<Image>() : null;
            so.ApplyModifiedProperties();
        }
    }

    static void ReceptionBackground(Scene scene)
    {
        Transform view = Find(scene, "ReceptionView");
        Sprite bg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Town/reception_bg.png");
        if (view == null || bg == null) { Debug.LogWarning("[DetailPopupLayout] ReceptionView 또는 reception_bg 없음"); return; }
        Transform bgT = view.Find("Bg_Image");
        Image img = bgT != null ? bgT.GetComponent<Image>() : null;
        if (img == null) return;
        Undo.RecordObject(img, "접수처 배경");
        img.sprite = bg;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.color = Color.white;   // 무늬가 있는 스프라이트라 실행 중 UI 테마가 다시 칠하지 않음
    }
}
