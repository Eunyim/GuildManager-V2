using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// UI 테마 수치 (GameManager 인스펙터에서 조정. GameManager가 없는 씬에서는 기본값)
[System.Serializable]
public class UIThemeSettings
{
    public bool enabled = true;
    [Tooltip("도트 한 칸이 화면(1920x1080 기준)에서 몇 픽셀인지")]
    public float pixelScale = 3f;
    [Tooltip("창틀 가장자리에 붙은 버튼·칸을 창틀 안쪽으로 옮김")]
    public bool fitInsideFrame = true;
    public float frameGap = 6f;           // 창틀 안쪽 여백 (창틀 두께에 더함)
    public float minEdgeButtonHeight = 40f; // 창 가장자리에 붙은 버튼의 최소 높이

    [Header("색")]
    public Color dimColor        = new Color(0.03f, 0.02f, 0.01f, 0.72f); // 창 뒤 어두운 배경
    public Color textColor       = new Color32(0xEF, 0xE0, 0xBF, 0xFF);   // 창 안 본문 (양피지색)
    public Color titleColor      = new Color32(0xF2, 0xCD, 0x6A, 0xFF);   // 창 제목 (금색)
    public Color buttonTextColor = new Color32(0xF6, 0xE7, 0xC8, 0xFF);
    public Color goldButtonTextColor = new Color32(0x2A, 0x1A, 0x0E, 0xFF);
    public Color inkTextColor    = new Color32(0x3B, 0x24, 0x12, 0xFF);   // 양피지 위 글자
}

// 판타지 픽셀 테마. 두 가지로 씀
//   굽기(에디터): Tools > UI 테마 굽기 가 씬·프리팹에 직접 저장 → 에디터에서 보면서 고칠 수 있음. 구운 창에는 UIThemeBaked가 붙어 실행 중에 다시 건드리지 않음
//   실행 중: UIPolish가 주기적으로 불러, 굽지 않은 창이나 코드로 새로 만든 UI(던전 지도 버튼, 툴팁 등)에 입힘
// 스프라이트: Resources/UI/Theme (Tools/ui_theme_gen.py로 생성, 임포트 설정은 Editor/UIThemeSpriteImporter)
//
// 무엇을 바꾸는지
//   창(Canvas 바로 아래 Popup_*/Panel_*): 화면 전체 배경 → 어둡게, 가운데 상자 → 금테 나무 창틀, Outline → 숨김
//   창 안의 단색 칸(목록 줄·초상화·입력 칸) → 안으로 파인 홈
//   버튼 → 원래 색 계열에 맞는 입체 버튼 (갈색 → 나무, 회색 → 돌, 초록·빨강·노랑은 그대로 계열 유지)
//   이름에 Tooltip이 들어간 상자 → 얇은 금테
//   글자: 창 안의 흰색·검은색 글자 → 양피지색, 가장 큰 글자(제목) → 금색. 색이 있는 글자는 의미가 있으므로 그대로
// 건드리지 않는 것: 그림이 있는 이미지(아이콘·초상화·시설), 월드 공간 UI(체력바), 던전 지도·미니맵, 슬라이더·토글
public static class UITheme
{
    static readonly HashSet<int> done = new HashSet<int>();
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    static readonly UIThemeSettings defaultSettings = new UIThemeSettings();
    static UIThemeSettings Settings =>
        BakeSettings != null ? BakeSettings :
        GameManager.Instance != null && GameManager.Instance.uiTheme != null ? GameManager.Instance.uiTheme : defaultSettings;

    // ─── 굽기 (에디터 도구가 씀) ──────────────────────────────────
    public static bool Baking { get; private set; }
    static UIThemeSettings BakeSettings;
    static System.Action<Object> beforeModify; // Undo 기록용

    public static void BeginBake(UIThemeSettings settings, System.Action<Object> recordUndo)
    {
        Baking = true;
        BakeSettings = settings ?? defaultSettings;
        beforeModify = recordUndo;
        done.Clear(); fitted.Clear();
    }

    public static void EndBake()
    {
        Baking = false;
        BakeSettings = null;
        beforeModify = null;
        done.Clear(); fitted.Clear();
    }

    static void Touch(Object o) { if (o != null) beforeModify?.Invoke(o); }

    public static bool Enabled => Settings.enabled;

    static Sprite Get(string name)
    {
        if (!sprites.TryGetValue(name, out Sprite s))
        {
            s = Resources.Load<Sprite>("UI/Theme/" + name);
            if (s == null) Debug.LogWarning($"[UITheme] Resources/UI/Theme/{name} 스프라이트가 없습니다.");
            sprites[name] = s;
        }
        return s;
    }

    // 테마로 바꿔도 되는 스프라이트: 없음, 유니티 기본 UI 스프라이트, 임시 나무·종이 무늬
    static bool IsPlain(Sprite s)
    {
        if (s == null) return true;
        switch (s.name)
        {
            case "UISprite": case "Background": case "InputFieldBackground": case "UIMask":
                return true;
        }
        // 나무·종이 무늬는 잘라 쓴 스프라이트일 수 있어 텍스처 이름으로 봄
        string tex = s.texture != null ? s.texture.name : "";
        return tex == "wood_texture_full_screen" || tex.StartsWith("Paper0");
    }

    // 지도·미니맵의 방 칸은 색으로 방 종류를 나타내므로 건드리지 않음 (지도의 이동 버튼은 입힘: allowMap)
    static bool Skip(Graphic g, bool allowMap = false)
    {
        if (g == null || done.Contains(g.GetInstanceID())) return true;
        Canvas c = g.canvas;
        if (c != null && c.renderMode == RenderMode.WorldSpace) return true;
        if (!allowMap && (g.GetComponentInParent<DungeonMapPanel>(true) != null || g.GetComponentInParent<DungeonMinimap>(true) != null)) return true;
        return false;
    }

    static void SetSprite(Image img, string sprite, Color color)
    {
        Sprite s = Get(sprite);
        if (s == null) return;
        Touch(img);
        img.sprite = s;
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = PixelMultiplier(img);
        img.color = color;
    }

    // ─── 창틀 안으로 맞추기 ───────────────────────────────────────
    // 창 가장자리에 딱 붙여 놓은 자식(스크롤 영역, 모서리 버튼, 제목)을 창틀 두께만큼 안쪽으로 옮김. 창마다 한 번만
    static readonly HashSet<int> fitted = new HashSet<int>();

    static void FitInsideFrame(RectTransform panel)
    {
        if (panel == null || !Settings.fitInsideFrame || !fitted.Add(panel.GetInstanceID())) return;
        float b = 8f * Settings.pixelScale + Settings.frameGap; // ui_panel 테두리 8칸

        foreach (Transform t in panel)
        {
            RectTransform c = t as RectTransform;
            if (c == null) continue;
            Touch(c);

            // 늘어나는 축: 여백을 테두리만큼 확보
            if (c.anchorMin.x == 0f && c.anchorMax.x == 1f)
                c.offsetMin = new Vector2(Mathf.Max(c.offsetMin.x, b), c.offsetMin.y);
            if (c.anchorMin.x == 0f && c.anchorMax.x == 1f)
                c.offsetMax = new Vector2(Mathf.Min(c.offsetMax.x, -b), c.offsetMax.y);
            if (c.anchorMin.y == 0f && c.anchorMax.y == 1f)
            {
                c.offsetMin = new Vector2(c.offsetMin.x, Mathf.Max(c.offsetMin.y, b));
                c.offsetMax = new Vector2(c.offsetMax.x, Mathf.Min(c.offsetMax.y, -b));
            }

            // 한 점에 붙은 것: 가장자리 안쪽으로
            bool pointX = c.anchorMin.x == c.anchorMax.x, pointY = c.anchorMin.y == c.anchorMax.y;
            bool edgeButton = c.GetComponent<Button>() != null
                && ((pointX && (c.anchorMin.x == 0f || c.anchorMin.x == 1f)) || (pointY && (c.anchorMin.y == 0f || c.anchorMin.y == 1f)));
            if (edgeButton && c.sizeDelta.y < Settings.minEdgeButtonHeight)
                c.sizeDelta = new Vector2(c.sizeDelta.x, Settings.minEdgeButtonHeight);

            // 창틀 안쪽보다 넓은 글자(제목 등): 폭을 줄이고 넘치면 글자 크기를 줄임
            TMP_Text label = c.GetComponent<TMP_Text>();
            float inner = panel.rect.width - 2f * b;
            if (label != null && pointX && inner > 0f && c.sizeDelta.x > inner)
            {
                Touch(label);
                c.sizeDelta = new Vector2(inner, c.sizeDelta.y);
                label.fontSizeMax = label.fontSize;
                label.fontSizeMin = label.fontSize * 0.6f;
                label.textWrappingMode = TextWrappingModes.NoWrap; // 두 줄로 꺾지 말고 크기를 줄임
                label.enableAutoSizing = true;
            }

            Vector2 pos = c.anchoredPosition, size = c.sizeDelta, piv = c.pivot;
            if (pointX && c.anchorMin.x == 0f) pos.x = Mathf.Max(pos.x, b + piv.x * size.x);
            if (pointX && c.anchorMin.x == 1f) pos.x = Mathf.Min(pos.x, -b - (1f - piv.x) * size.x);
            if (pointY && c.anchorMin.y == 0f) pos.y = Mathf.Max(pos.y, b + piv.y * size.y);
            if (pointY && c.anchorMin.y == 1f) pos.y = Mathf.Min(pos.y, -b - (1f - piv.y) * size.y);
            c.anchoredPosition = pos;
        }
    }

    // 도트 한 칸이 실제 화면에서 정수 픽셀이 되도록 (1080p에서 pixelScale칸, 캔버스 배율과 화면 높이에 맞춤)
    static float PixelMultiplier(Image img)
    {
        if (Baking) return BakeCanvasScale(img) / Mathf.Max(0.1f, Settings.pixelScale);
        Canvas root = img.canvas != null ? img.canvas.rootCanvas : null;
        float canvasScale = root != null && root.scaleFactor > 0f ? root.scaleFactor : 1f;
        float screenPx = Mathf.Max(1f, Mathf.Round(Settings.pixelScale * Screen.height / 1080f));
        return canvasScale / screenPx;
    }

    // 굽을 때는 화면 크기를 모르므로 1920x1080 기준으로 캔버스 배율을 계산 (CanvasScaler와 같은 식)
    static float BakeCanvasScale(Image img)
    {
        Canvas canvas = img.canvas != null ? img.canvas.rootCanvas : img.GetComponentInParent<Canvas>(true);
        CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        if (scaler == null) return 1f;
        if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) return scaler.scaleFactor;
        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return 1f;
        Vector2 refRes = scaler.referenceResolution;
        float logW = Mathf.Log(1920f / refRes.x, 2f), logH = Mathf.Log(1080f / refRes.y, 2f);
        return Mathf.Pow(2f, Mathf.Lerp(logW, logH, scaler.matchWidthOrHeight));
    }

    // 양피지(종이) 위의 흰·검은 글자 → 잉크색
    public static void ThemePaper(Image paper)
    {
        if (!Settings.enabled || paper == null || done.Contains(paper.GetInstanceID())) return;
        Sprite s = paper.sprite;
        if (s == null || s.texture == null || !s.texture.name.StartsWith("Paper0")) return;
        done.Add(paper.GetInstanceID());
        foreach (TMP_Text t in paper.GetComponentsInChildren<TMP_Text>(true))
        {
            if (done.Contains(t.GetInstanceID()) || t.GetComponentInParent<Button>(true) != null) continue;
            done.Add(t.GetInstanceID());
            if (IsNeutral(t.color)) { Touch(t); Color c = Settings.inkTextColor; c.a = t.color.a; t.color = c; }
        }
    }

    static bool FullScreen(RectTransform rt) => rt != null && rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;

    static bool Saturated(Color c)
    {
        Color.RGBToHSV(c, out _, out float s, out float v);
        return s > 0.45f && v > 0.35f;
    }

    // ─── 창 ───────────────────────────────────────────────────────
    public static void ThemePopup(Transform root)
    {
        if (!Settings.enabled || root == null) return;
        if (!Baking && root.GetComponent<UIThemeBaked>() != null) return; // 에디터에서 구운 창: 손으로 고친 것을 덮어쓰지 않음

        // 창 자체와 바로 아래 자식: 배경·창틀
        Image rootImage = root.GetComponent<Image>();
        bool rootIsDim = rootImage != null && FullScreen(root as RectTransform);
        if (rootImage != null && !Skip(rootImage) && IsPlain(rootImage.sprite))
        {
            if (rootIsDim) { Touch(rootImage); rootImage.sprite = null; rootImage.color = Settings.dimColor; }
            else
            {
                SetSprite(rootImage, "ui_panel", Color.white);
                FitInsideFrame(root as RectTransform);
            }
            done.Add(rootImage.GetInstanceID());
        }

        foreach (Transform child in root)
        {
            Image img = child.GetComponent<Image>();
            if (img == null || Skip(img) || child.GetComponent<Button>() != null) continue;
            done.Add(img.GetInstanceID());
            if (!IsPlain(img.sprite)) continue;

            Touch(img);
            if (child.name == "Outline") img.enabled = false; // 창틀 스프라이트에 테두리가 있음
            else if (FullScreen(child as RectTransform)) { img.sprite = null; img.color = Settings.dimColor; }
            else
            {
                SetSprite(img, "ui_panel", Color.white);
                FitInsideFrame(child as RectTransform);
            }
        }

        // 더 안쪽: 단색 칸 → 홈, 글자 색
        foreach (Image img in root.GetComponentsInChildren<Image>(true))
        {
            if (Skip(img) || img.GetComponent<Button>() != null) continue; // 버튼은 ThemeButton이 처리
            Transform t = img.transform;
            if (t == root || t.parent == root) continue; // 위에서 처리
            done.Add(img.GetInstanceID());
            ThemeInnerImage(img);
        }

        ThemeTexts(root, findTitle: true);
    }

    // 목록 칸 같은 UI 프리팹(굽기용): 단색 칸 → 홈, 버튼, 글자색 (제목 찾기 없음)
    public static void ThemeWidget(GameObject root)
    {
        if (!Settings.enabled || root == null) return;
        foreach (Button b in root.GetComponentsInChildren<Button>(true)) ThemeButton(b);
        foreach (Image img in root.GetComponentsInChildren<Image>(true))
        {
            if (Skip(img) || img.GetComponent<Button>() != null) continue;
            done.Add(img.GetInstanceID());
            ThemeInnerImage(img);
        }
        ThemeTexts(root.transform, findTitle: false);
    }

    static void ThemeInnerImage(Image img)
    {
        if (!IsPlain(img.sprite) || img.color.a < 0.05f) return;
        if (img.GetComponent<Button>() != null) return;                   // 버튼은 따로
        if (img.GetComponentInParent<Slider>(true) != null || img.GetComponentInParent<Toggle>(true) != null) return;
        Mask mask = img.GetComponent<Mask>();
        if (mask != null && !mask.showMaskGraphic) return;               // 스크롤 영역 가리개

        Scrollbar bar = img.GetComponentInParent<Scrollbar>(true);
        if (bar != null)
        {
            if (bar.handleRect == img.rectTransform) SetSprite(img, "ui_knob", Color.white);
            else if (bar.transform == img.transform) SetSprite(img, "ui_inset", Color.white);
            return;
        }

        Rect r = img.rectTransform.rect;
        if (r.width > 0f && r.height > 0f && (r.width < 6f || r.height < 6f)) return; // 구분선
        if (Saturated(img.color)) return; // 색으로 뜻을 나타내는 칸 (등급·상태 표시 등)

        SetSprite(img, "ui_inset", Color.white);
    }

    static void ThemeTexts(Transform root, bool findTitle)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text title = null;
        float biggest = 0f, second = 0f;
        foreach (TMP_Text t in texts)
        {
            if (t.GetComponentInParent<Button>(true) != null) continue;
            if (t.fontSize > biggest) { second = biggest; biggest = t.fontSize; title = t; }
            else if (t.fontSize > second) second = t.fontSize;
        }
        if (!findTitle || biggest < second * 1.15f) title = null; // 뚜렷하게 큰 글자가 없으면 제목 없음

        foreach (TMP_Text t in texts)
        {
            if (done.Contains(t.GetInstanceID()) || t.GetComponentInParent<Button>(true) != null) continue;
            done.Add(t.GetInstanceID());
            if (!IsNeutral(t.color)) continue;
            Touch(t);
            Color c = t == title ? Settings.titleColor : Settings.textColor;
            c.a = t.color.a;
            t.color = c;
        }
    }

    // 흰색·검은색 계열 (뜻 없는 기본 글자색). 회색(입력 칸 안내문 등)은 그대로
    static bool IsNeutral(Color c)
    {
        Color.RGBToHSV(c, out _, out float s, out float v);
        return s < 0.2f && (v > 0.8f || v < 0.25f);
    }

    // ─── 버튼 ─────────────────────────────────────────────────────
    public static void ThemeButton(Button button)
    {
        if (!Settings.enabled || button == null) return;
        Image img = button.targetGraphic as Image;
        if (img == null || Skip(img, allowMap: true)) return;
        done.Add(img.GetInstanceID());

        // 이미 구운 버튼: 눌린 모양만 연결 (실행 중)
        if (img.sprite != null && img.sprite.name.StartsWith("ui_button_") && !img.sprite.name.EndsWith("_pressed"))
        {
            UIButtonFeedback baked = button.GetComponent<UIButtonFeedback>();
            if (baked != null) baked.SetPressedSprite(Get(img.sprite.name + "_pressed"));
            return;
        }
        if (!IsPlain(img.sprite)) return;

        string variant = ButtonVariant(img.color * button.colors.normalColor); // 색을 버튼 색상표에 넣어 둔 버튼도 있음
        SetSprite(img, "ui_button_" + variant, Color.white);

        Touch(button);
        ColorBlock cb = button.colors;
        cb.normalColor      = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.highlightedColor = Color.white;
        cb.selectedColor    = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.pressedColor     = new Color(0.78f, 0.78f, 0.78f, 1f);
        cb.disabledColor    = new Color(0.5f, 0.48f, 0.46f, 0.85f);
        cb.colorMultiplier  = 1f;
        cb.fadeDuration     = 0.05f;
        button.colors = cb;
        if (button.transition == Selectable.Transition.SpriteSwap)
        {
            button.transition = Selectable.Transition.ColorTint;
        }

        // 눌렸을 때 턱이 사라진 모양 (색 변화와 함께 쓰려고 SpriteSwap 대신 UIButtonFeedback이 바꿈)
        UIButtonFeedback fb = button.GetComponent<UIButtonFeedback>();
        if (fb != null) fb.SetPressedSprite(Get("ui_button_" + variant + "_pressed"));

        Color textColor = variant == "gold" ? Settings.goldButtonTextColor : Settings.buttonTextColor;
        foreach (TMP_Text t in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (done.Contains(t.GetInstanceID())) continue;
            done.Add(t.GetInstanceID());
            if (IsNeutral(t.color)) { Touch(t); Color c = textColor; c.a = t.color.a; t.color = c; }
        }
    }

    // 원래 버튼 색 계열에 맞춰 고름
    static string ButtonVariant(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        float deg = h * 360f;
        if (s < 0.15f && v > 0.7f) return "wood";  // 유니티 기본 흰 버튼
        if (s < 0.3f) return "stone";              // 회색 (닫기·취소)
        if (deg >= 80f && deg <= 170f) return "green";
        if ((deg <= 12f || deg >= 340f) && s > 0.4f) return "red";
        if (deg >= 40f && deg <= 65f && v > 0.6f) return "gold";
        return "wood";
    }

    // ─── 툴팁 ─────────────────────────────────────────────────────
    public static void ThemeTooltip(Image img)
    {
        if (!Settings.enabled || img == null || Skip(img)) return;
        done.Add(img.GetInstanceID());
        if (!IsPlain(img.sprite)) return;
        SetSprite(img, "ui_panel_small", Color.white);
        foreach (TMP_Text t in img.GetComponentsInChildren<TMP_Text>(true))
            if (IsNeutral(t.color)) { Touch(t); Color c = Settings.textColor; c.a = t.color.a; t.color = c; }
    }
}
