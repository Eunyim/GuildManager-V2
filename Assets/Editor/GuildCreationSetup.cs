using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [메뉴] Tools > 길드 신청서 화면 세팅
/// GuildCreationScene에 촛불 켜진 책상 + 신청서 화면(Tools/creation_art_gen.py 그림)을 만들고 GuildCreationScreen 연출을 연결합니다.
/// 기존 이름 입력칸과 [서명] 버튼은 연결을 그대로 둔 채 옮기고, [추천]·[돌아가기] 버튼을 새로 만들며, 예전 Canvas는 끕니다.
/// 다시 실행하면 화면을 지우고 새로 만듭니다.
/// </summary>
public static class GuildCreationSetup
{
    const string ScenePath = "Assets/Scenes/GuildCreationScene.unity";
    const string ArtDir = "Assets/Art/Creation/";
    const string RootName = "CreationCanvas";
    const float K = 4f;          // 책상 그림 1픽셀 = UI 4
    const float PK = 2f;         // 신청서 그림 1픽셀 = UI 2
    const float LW = 512f, LH = 288f;
    const float PW = 368f, PH = 464f;
    static readonly Vector2 PaperCenter = new Vector2(-150f, 0f);

    // creation_art_gen.py와 같은 값
    const float NameLineY = 178f, SignLineY = 426f;
    static readonly Vector2 StampCenter = new Vector2(290f, 414f);
    static readonly Vector2 CandleWick = new Vector2(72f, 115f);
    static readonly Vector2 InkwellMouth = new Vector2(430f, 210f);

    static readonly Color Ink = new Color32(0x3a, 0x20, 0x14, 0xff);

    [MenuItem("Tools/길드 신청서 화면 세팅")]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogError("[GuildCreationSetup] 플레이를 끄고 실행하세요."); return; }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        GuildCreationManager manager = Object.FindFirstObjectByType<GuildCreationManager>(FindObjectsInactive.Include);
        if (manager == null || manager.nameInput == null) { Debug.LogError("[GuildCreationSetup] GuildCreationManager 또는 이름 입력칸이 없습니다."); return; }
        TMP_InputField input = manager.nameInput;
        Button signButton = FindButtonCalling(nameof(GuildCreationManager.OnClickSign));
        if (signButton == null) { Debug.LogError("[GuildCreationSetup] OnClickSign에 연결된 버튼이 없습니다."); return; }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DungGeunMo SDF.asset");

        // 1. 옮길 것들을 잠시 루트로, 예전 화면 정리
        Canvas oldRoot = input.GetComponentInParent<Canvas>(true);
        GameObject oldCanvas = oldRoot != null ? oldRoot.rootCanvas.gameObject : null;
        input.transform.SetParent(null, false);
        signButton.transform.SetParent(null, false);
        EventSystem es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es != null && oldCanvas != null && es.transform.IsChildOf(oldCanvas.transform)) es.transform.SetParent(null, false);
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            // 예전에 만든 [추천]·[돌아가기]는 새로 만듦
            Object.DestroyImmediate(existing);
        }
        if (oldCanvas != null && oldCanvas.name != RootName) oldCanvas.SetActive(false);

        // 2. 캔버스
        GameObject rootGO = new GameObject(RootName, typeof(RectTransform));
        rootGO.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = rootGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = rootGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        rootGO.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)rootGO.transform;
        GuildCreationScreen screen = rootGO.AddComponent<GuildCreationScreen>();

        Stretch(NewImage("Backdrop", root, null, Color.black).rectTransform);
        RectTransform shake = NewRect("Shake", root);
        Stretch(shake);
        screen.shakeRoot = shake;

        // 3. 책상 + 촛불
        Image desk = NewImage("Desk", shake, Art("creation_desk"), Color.white);
        Center(desk.rectTransform, Vector2.zero, new Vector2(LW, LH) * K);
        Image glow = NewImage("CandleGlow", desk.rectTransform, Art("creation_glow"), Color.white);
        Stretch(glow.rectTransform);
        screen.candleGlow = glow;
        Sprite flame0 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/title_flame_0.png");
        Image flame = NewImage("CandleFlame", shake, flame0, Color.white);
        flame.rectTransform.anchorMin = flame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        flame.rectTransform.pivot = new Vector2(0.5f, 0f);
        flame.rectTransform.anchoredPosition = DeskToCanvas(CandleWick);
        flame.rectTransform.sizeDelta = flame0 != null ? flame0.rect.size * K : Vector2.zero;
        screen.candleFlame = flame;
        screen.flameFrames = new Sprite[4];
        for (int i = 0; i < 4; i++) screen.flameFrames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Title/title_flame_{i}.png");
        RectTransform dust = NewRect("DustArea", shake);
        Center(dust, DeskToCanvas(CandleWick) + new Vector2(60f, -40f), new Vector2(520f, 560f));
        screen.dustArea = dust;
        screen.dustSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/title_firefly.png");

        // 4. 신청서
        Image paper = NewImage("Paper", shake, Art("creation_paper"), Color.white);
        Center(paper.rectTransform, PaperCenter, new Vector2(PW, PH) * PK);
        RectTransform pr = paper.rectTransform;
        screen.paper = pr;

        // 이름 입력칸 (기존 것을 옮겨 꾸밈)
        RectTransform ir = (RectTransform)input.transform;
        ir.SetParent(pr, false);
        OnPaper(ir, 34f, NameLineY - 25f, new Vector2(0f, 1f), new Vector2(PW - 34f - 84f, 25f));
        ir.localScale = Vector3.one;
        Image inputBg = input.GetComponent<Image>();
        if (inputBg != null) { inputBg.sprite = null; inputBg.color = new Color(1f, 1f, 1f, 0f); }
        input.characterLimit = manager.maxNameLength;
        input.customCaretColor = true;
        input.caretColor = Ink;
        input.caretWidth = 3;
        input.selectionColor = new Color(0.85f, 0.6f, 0.3f, 0.45f);
        input.transition = Selectable.Transition.None;
        if (input.textViewport != null) { Stretch(input.textViewport); input.textViewport.sizeDelta = new Vector2(-8f, 0f); }
        if (input.textComponent != null)
        {
            TMP_Text t = input.textComponent;
            if (font != null) t.font = font;
            t.fontSize = 48;
            t.color = Ink;
            t.alignment = TextAlignmentOptions.BottomLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(t.rectTransform);
            t.rectTransform.pivot = new Vector2(0f, 0f); // 글자를 칠 때 튀는 기준이 왼쪽 아래
        }
        if (input.placeholder is TMP_Text ph)
        {
            if (font != null) ph.font = font;
            ph.text = "여기에 길드 이름을 적으세요";
            ph.fontSize = 32;
            ph.color = new Color(0.42f, 0.3f, 0.2f, 0.55f);
            ph.alignment = TextAlignmentOptions.BottomLeft;
            Stretch(ph.rectTransform);
        }
        screen.nameInput = input;

        // [추천] 버튼
        Button suggest = MakeButton("Btn_Suggest", pr, Art("creation_button_small"), Art("creation_button_small_hover"), "추천", 28, font);
        OnPaper((RectTransform)suggest.transform, PW - 36f - 40f, NameLineY - 20f, new Vector2(0f, 1f), new Vector2(40f, 16f));
        UnityEventTools.AddPersistentListener(suggest.onClick, manager.OnClickSuggestName);

        // 이름 줄 번쩍임, 안내 글
        Image flash = NewImage("NameLineFlash", pr, null, new Color(1f, 0.2f, 0.15f, 0f));
        OnPaper(flash.rectTransform, 34f, NameLineY - 1f, new Vector2(0f, 1f), new Vector2(PW - 69f, 4f));
        screen.nameLineFlash = flash;
        screen.errorText = PaperText("Txt_Error", pr, font, 24, new Color(0.72f, 0.16f, 0.12f), 36f, NameLineY + 2f, new Vector2(300f, 13f), "");

        // 창립일·초기 지원 값
        screen.dateText = PaperText("Txt_Date", pr, font, 32, Ink, 108f, 196f, new Vector2(110f, 17f), "1월 1주차");
        screen.supportText = PaperText("Txt_Support", pr, font, 32, Ink, 122f, 224f, new Vector2(210f, 17f), "");

        // 서명 (가로로 채워짐)
        Image sig = NewImage("Signature", pr, Art("creation_signature"), Color.white);
        OnPaper(sig.rectTransform, 40f, SignLineY - 36f, new Vector2(0f, 1f), new Vector2(180f, 40f));
        sig.type = Image.Type.Filled;
        sig.fillMethod = Image.FillMethod.Horizontal;
        sig.fillOrigin = (int)Image.OriginHorizontal.Left;
        sig.fillAmount = 0f;
        screen.signature = sig;

        // 승인 도장
        Image stamp = NewImage("Stamp", pr, Art("creation_stamp"), Color.white);
        Sprite stampSprite = stamp.sprite;
        OnPaper(stamp.rectTransform, StampCenter.x, StampCenter.y, new Vector2(0.5f, 0.5f), stampSprite != null ? stampSprite.rect.size : new Vector2(72f, 72f));
        stamp.gameObject.SetActive(false);
        screen.stamp = stamp;
        screen.inkSprite = Art("creation_ink");

        // 5. 오른쪽 버튼: [서명하기](기존 버튼) / [돌아가기](새로)
        screen.buttons.Clear();
        RectTransform sr = (RectTransform)signButton.transform;
        sr.SetParent(shake, false);
        StyleButton(signButton, Art("creation_button_sign"), Art("creation_button_sign_hover"), Art("creation_button_sign_pressed"), "서명하기", 36, font);
        Center(sr, new Vector2(600f, 100f), new Vector2(88f, 18f) * K);
        screen.buttons.Add(sr);

        Sprite wood = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/title_button.png");
        Sprite woodHover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/title_button_hover.png");
        Sprite woodPressed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/title_button_pressed.png");
        Button back = MakeButton("Btn_Back", shake, wood, woodHover, "돌아가기", 32, font);
        back.spriteState = new SpriteState { highlightedSprite = woodHover, pressedSprite = woodPressed, selectedSprite = wood };
        Center((RectTransform)back.transform, new Vector2(600f, -4f), new Vector2(88f, 18f) * K);
        UnityEventTools.AddPersistentListener(back.onClick, manager.OnClickBack);
        screen.buttons.Add((RectTransform)back.transform);

        // 6. 깃펜 (잉크병에 꽂혀 있음, 기준점 = 펜촉)
        Sprite quillSprite = Art("creation_quill");
        Image quill = NewImage("Quill", shake, quillSprite, Color.white);
        quill.rectTransform.anchorMin = quill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        quill.rectTransform.pivot = new Vector2(2.5f / 22f, 1.5f / 52f);
        quill.rectTransform.anchoredPosition = DeskToCanvas(InkwellMouth);
        quill.rectTransform.sizeDelta = quillSprite != null ? quillSprite.rect.size * K : Vector2.zero;
        screen.quill = quill.rectTransform;

        // 7. 승인 안내 (그림자 + 글자)
        RectTransform toast = NewRect("Toast", shake);
        Center(toast, PaperCenter + new Vector2(0f, 40f), new Vector2(1200f, 120f));
        Image band = NewImage("Band", toast, null, new Color(0.09f, 0.04f, 0.03f, 0.88f));
        Center(band.rectTransform, Vector2.zero, new Vector2(1160f, 128f));
        foreach (float y in new[] { 60f, -60f })
        {
            Image line = NewImage("Line", toast, null, new Color(0.85f, 0.66f, 0.3f, 1f));
            Center(line.rectTransform, new Vector2(0f, y), new Vector2(1160f, 4f));
        }
        Text(toast, "Shadow", font, 64, new Color(0.1f, 0.04f, 0.02f, 0.9f), new Vector2(5f, -5f), "길드 설립이 승인되었습니다!");
        Text(toast, "Label", font, 64, new Color(1f, 0.85f, 0.38f), Vector2.zero, "길드 설립이 승인되었습니다!");
        screen.toast = toast.gameObject.AddComponent<CanvasGroup>();
        screen.toast.blocksRaycasts = false;

        // 8. 화면 전환용 검은 막
        Image fade = NewImage("Fade", root, null, Color.black);
        Stretch(fade.rectTransform);
        screen.fade = fade;

        Camera cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; EditorUtility.SetDirty(cam); }

        EditorUtility.SetDirty(screen);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(rootGO.scene);
        EditorSceneManager.SaveScene(rootGO.scene);
        Debug.Log("=== 길드 신청서 화면 세팅 완료 (GuildCreationScene 저장됨) ===");
    }

    // ─── 도우미 ──────────────────────────────────────────

    static Sprite Art(string name)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
        if (s == null) Debug.LogError($"[GuildCreationSetup] {ArtDir}{name}.png 스프라이트가 없습니다. python Tools/creation_art_gen.py 실행 후 'Tools > 도트 그림 다시 가져오기'를 하세요.");
        return s;
    }

    static Vector2 DeskToCanvas(Vector2 p) => new Vector2((p.x - LW / 2f) * K, (LH / 2f - p.y) * K);

    // 신청서 그림 좌표(왼쪽 위 원점, 그림 픽셀)로 배치
    static void OnPaper(RectTransform rt, float px, float py, Vector2 pivot, Vector2 sizePx)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = pivot;
        rt.anchoredPosition = new Vector2(px * PK, -py * PK);
        rt.sizeDelta = sizePx * PK;
    }

    static TMP_Text PaperText(string name, RectTransform paper, TMP_FontAsset font, float size, Color color, float px, float py, Vector2 sizePx, string text)
    {
        RectTransform rt = NewRect(name, paper);
        OnPaper(rt, px, py, new Vector2(0f, 1f), sizePx);
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = TextAlignmentOptions.BottomLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    static void Text(RectTransform parent, string name, TMP_FontAsset font, float size, Color color, Vector2 offset, string text)
    {
        RectTransform rt = NewRect(name, parent);
        Stretch(rt);
        rt.anchoredPosition = offset;
        TextMeshProUGUI t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.text = text;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
    }

    static Button MakeButton(string name, RectTransform parent, Sprite normal, Sprite hover, string label, float fontSize, TMP_FontAsset font)
    {
        RectTransform rt = NewRect(name, parent);
        rt.gameObject.AddComponent<Image>();
        Button b = rt.gameObject.AddComponent<Button>();
        RectTransform lr = NewRect("Label", rt);
        lr.gameObject.AddComponent<TextMeshProUGUI>();
        StyleButton(b, normal, hover, normal, label, fontSize, font);
        return b;
    }

    static void StyleButton(Button b, Sprite normal, Sprite hover, Sprite pressed, string label, float fontSize, TMP_FontAsset font)
    {
        Image img = b.GetComponent<Image>();
        img.sprite = normal;
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.raycastTarget = true;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { highlightedSprite = hover, pressedSprite = pressed, selectedSprite = normal };
        b.transform.localScale = Vector3.one;
        b.transform.localRotation = Quaternion.identity;

        TMP_Text t = b.GetComponentInChildren<TMP_Text>(true);
        if (t != null)
        {
            t.text = label;
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyles.Normal;
            t.enableAutoSizing = false;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = new Color(0.98f, 0.92f, 0.78f);
            t.raycastTarget = false;
            Stretch(t.rectTransform);
            t.rectTransform.anchoredPosition = new Vector2(0f, 2f);
        }
        TitleMenuButton fx = b.GetComponent<TitleMenuButton>();   // 올리면 커지고 글자가 금빛 (커서는 타이틀에만 있음)
        if (fx == null) fx = b.gameObject.AddComponent<TitleMenuButton>();
        fx.label = t;
    }

    static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rt = NewRect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static Button FindButtonCalling(string method)
    {
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == method) return b;
        return null;
    }
}
