using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

using UnityEngine.UI;

/// <summary>
/// 튜토리얼 화면 표시 (설명 창, 목표 상자, 강조 테두리, 도움말 버튼·목록).
/// 씬에 미리 놓지 않고 TutorialManager가 실행 중에 만듭니다. 무엇을 보여줄지는 TutorialManager가 정합니다.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    // 색 (UITheme와 비슷한 갈색 톤)
    static readonly Color DimColor      = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color WindowColor   = new Color(0.16f, 0.09f, 0.05f, 0.97f);
    static readonly Color BorderColor   = new Color(0.85f, 0.66f, 0.3f, 1f);
    static readonly Color TitleColor    = new Color(1f, 0.85f, 0.4f, 1f);
    static readonly Color ButtonColor   = new Color(0.55f, 0.36f, 0.16f, 1f);
    static readonly Color SubButtonColor = new Color(0.32f, 0.27f, 0.24f, 1f);
    static readonly Color HighlightColor = new Color(1f, 0.86f, 0.25f, 1f);
    static readonly Color ObjectiveColor = new Color(0.1f, 0.06f, 0.03f, 0.88f);

    const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;

    TMP_FontAsset font;
    RectTransform root;

    // 설명 창
    GameObject dialogRoot;
    TextMeshProUGUI dialogTitle, dialogBody, dialogPageText;
    Button dialogNextButton, dialogPrevButton, dialogSkipButton;
    TextMeshProUGUI dialogNextLabel;
    TutorialPage[] pages;
    int pageIndex;
    Action onDialogClosed;

    // 목표 상자
    GameObject objectiveRoot;
    TextMeshProUGUI objectiveText;

    // 강조 테두리
    RectTransform highlight;
    Image[] highlightEdges;

    // 도움말
    GameObject helpButton;
    GameObject helpPanel;
    RectTransform helpListContent;
    TextMeshProUGUI tipsToggleLabel;

    public event Action SkipRequested;
    public event Action TipsToggled;

    public bool IsDialogOpen => dialogRoot != null && dialogRoot.activeSelf;
    public bool IsHelpOpen => helpPanel != null && helpPanel.activeSelf;

    // ─── 만들기 ──────────────────────────────────────────

    public void Build(TMP_FontAsset fontAsset)
    {
        font = fontAsset;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // 게임의 모든 팝업 위
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;

        BuildHighlight();
        BuildObjective();
        BuildHelp();
        BuildDialog();
    }

    public void SetFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null || fontAsset == font) return;
        font = fontAsset;
        foreach (TextMeshProUGUI t in GetComponentsInChildren<TextMeshProUGUI>(true)) t.font = font;
    }

    void BuildDialog()
    {
        dialogRoot = NewRect("Tutorial_Dialog", root);
        Stretch(dialogRoot.GetComponent<RectTransform>());
        Image dim = dialogRoot.AddComponent<Image>();
        dim.color = DimColor; // 설명을 읽는 동안 뒤의 화면을 누르지 못하게 막음

        RectTransform window = MakeWindow("Window", dialogRoot.transform, new Vector2(980f, 520f));
        window.anchoredPosition = new Vector2(0f, -40f);

        dialogTitle = MakeText("Txt_Title", window, "", 40, TitleColor, TextAlignmentOptions.Center);
        Place(dialogTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(-80f, 64f));
        dialogTitle.fontStyle = FontStyles.Bold;

        dialogBody = MakeText("Txt_Body", window, "", 27, Color.white, TextAlignmentOptions.TopLeft);
        Place(dialogBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 18f), new Vector2(-100f, -220f));
        dialogBody.lineSpacing = 12f;
        dialogBody.textWrappingMode = TextWrappingModes.Normal;

        dialogPageText = MakeText("Txt_Page", window, "", 22, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
        Place(dialogPageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 105f), new Vector2(200f, 30f));

        dialogPrevButton = MakeButton("Btn_Prev", window, "이전", SubButtonColor, () => ShowPage(pageIndex - 1));
        Place((RectTransform)dialogPrevButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 52f), new Vector2(220f, 64f));

        dialogNextButton = MakeButton("Btn_Next", window, "다음", ButtonColor, OnClickNext);
        Place((RectTransform)dialogNextButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 52f), new Vector2(220f, 64f));
        dialogNextLabel = dialogNextButton.GetComponentInChildren<TextMeshProUGUI>();

        dialogSkipButton = MakeButton("Btn_Skip", window, "튜토리얼 건너뛰기", SubButtonColor, () => SkipRequested?.Invoke());
        Place((RectTransform)dialogSkipButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-130f, 52f), new Vector2(220f, 50f));
        dialogSkipButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 20;

        dialogRoot.SetActive(false);
    }

    void BuildObjective()
    {
        objectiveRoot = NewRect("Tutorial_Objective", root);
        RectTransform rt = objectiveRoot.GetComponent<RectTransform>();
        Place(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1000f, 84f));
        Image bg = objectiveRoot.AddComponent<Image>();
        bg.color = ObjectiveColor;
        bg.raycastTarget = false; // 목표 상자는 클릭을 막지 않음
        Outline outline = objectiveRoot.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(3f, -3f);

        objectiveText = MakeText("Txt_Objective", rt, "", 26, Color.white, TextAlignmentOptions.Center);
        Stretch(objectiveText.rectTransform, 24f, 8f);
        objectiveText.raycastTarget = false;
        objectiveText.textWrappingMode = TextWrappingModes.Normal;

        objectiveRoot.SetActive(false);
    }

    void BuildHighlight()
    {
        GameObject go = NewRect("Tutorial_Highlight", root);
        highlight = go.GetComponent<RectTransform>();
        highlight.anchorMin = highlight.anchorMax = new Vector2(0.5f, 0.5f);
        highlight.pivot = new Vector2(0.5f, 0.5f);

        // 테두리 네 변 (가운데는 비워 두어 대상이 그대로 보이고 클릭됨)
        highlightEdges = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject edge = NewRect($"Edge{i}", highlight);
            Image img = edge.AddComponent<Image>();
            img.color = HighlightColor;
            img.raycastTarget = false;
            highlightEdges[i] = img;
            RectTransform e = (RectTransform)edge.transform;
            const float t = 6f;
            switch (i)
            {
                case 0: e.anchorMin = new Vector2(0f, 1f); e.anchorMax = new Vector2(1f, 1f); e.sizeDelta = new Vector2(2f * t, t); e.pivot = new Vector2(0.5f, 0f); break;
                case 1: e.anchorMin = new Vector2(0f, 0f); e.anchorMax = new Vector2(1f, 0f); e.sizeDelta = new Vector2(2f * t, t); e.pivot = new Vector2(0.5f, 1f); break;
                case 2: e.anchorMin = new Vector2(0f, 0f); e.anchorMax = new Vector2(0f, 1f); e.sizeDelta = new Vector2(t, 0f); e.pivot = new Vector2(1f, 0.5f); break;
                case 3: e.anchorMin = new Vector2(1f, 0f); e.anchorMax = new Vector2(1f, 1f); e.sizeDelta = new Vector2(t, 0f); e.pivot = new Vector2(0f, 0.5f); break;
            }
            e.anchoredPosition = Vector2.zero;
        }
        go.SetActive(false);
    }

    void BuildHelp()
    {
        Button btn = MakeButton("Btn_Help", root, "도움말", ButtonColor, ToggleHelp);
        helpButton = btn.gameObject;
        Place((RectTransform)btn.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 50f), new Vector2(160f, 56f));
        helpButton.SetActive(false);

        helpPanel = NewRect("Tutorial_Help", root);
        Stretch(helpPanel.GetComponent<RectTransform>());
        helpPanel.AddComponent<Image>().color = DimColor;

        RectTransform window = MakeWindow("Window", helpPanel.transform, new Vector2(760f, 820f));
        TextMeshProUGUI title = MakeText("Txt_Title", window, "도움말", 40, TitleColor, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(-80f, 64f));
        title.fontStyle = FontStyles.Bold;

        // 스크롤 목록
        GameObject scrollGO = NewRect("Scroll", window);
        RectTransform scrollRT = (RectTransform)scrollGO.transform;
        Place(scrollRT, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 10f), new Vector2(-80f, -250f));
        scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        scrollGO.AddComponent<RectMask2D>();
        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.scrollSensitivity = 30f;

        GameObject contentGO = NewRect("Content", scrollRT);
        helpListContent = (RectTransform)contentGO.transform;
        helpListContent.anchorMin = new Vector2(0f, 1f);
        helpListContent.anchorMax = new Vector2(1f, 1f);
        helpListContent.pivot = new Vector2(0.5f, 1f);
        helpListContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = contentGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = helpListContent;
        scroll.viewport = scrollRT;

        Button toggle = MakeButton("Btn_TipsToggle", window, "", SubButtonColor, () => TipsToggled?.Invoke());
        Place((RectTransform)toggle.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 60f), new Vector2(270f, 60f));
        tipsToggleLabel = toggle.GetComponentInChildren<TextMeshProUGUI>();
        tipsToggleLabel.fontSize = 22;

        Button close = MakeButton("Btn_Close", window, "닫기", ButtonColor, () => helpPanel.SetActive(false));
        Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(150f, 60f), new Vector2(220f, 60f));

        helpPanel.SetActive(false);
    }

    // ─── 설명 창 ──────────────────────────────────────────

    // pages를 차례로 보여주고, 마지막 장에서 확인을 누르면 onClosed 호출
    public void ShowDialog(TutorialPage[] newPages, bool allowSkip, Action onClosed)
    {
        if (newPages == null || newPages.Length == 0) { onClosed?.Invoke(); return; }
        pages = newPages;
        onDialogClosed = onClosed;
        dialogSkipButton.gameObject.SetActive(allowSkip);
        if (helpPanel != null) helpPanel.SetActive(false);
        dialogRoot.SetActive(true);
        dialogRoot.transform.SetAsLastSibling();
        ShowPage(0);
    }

    public void CloseDialog()
    {
        if (dialogRoot != null) dialogRoot.SetActive(false);
        pages = null;
        onDialogClosed = null;
    }

    void ShowPage(int index)
    {
        if (pages == null) return;
        pageIndex = Mathf.Clamp(index, 0, pages.Length - 1);
        dialogTitle.text = pages[pageIndex].title;
        dialogBody.text = pages[pageIndex].body;
        dialogPageText.text = pages.Length > 1 ? $"{pageIndex + 1} / {pages.Length}" : "";
        dialogPrevButton.gameObject.SetActive(pageIndex > 0);
        dialogNextLabel.text = pageIndex < pages.Length - 1 ? "다음" : "확인";
    }

    void OnClickNext()
    {
        if (pages == null) return;
        if (pageIndex < pages.Length - 1) { ShowPage(pageIndex + 1); return; }

        Action callback = onDialogClosed;
        CloseDialog();
        callback?.Invoke();
    }

    // ─── 목표 상자 ──────────────────────────────────────────

    public void SetObjective(string text)
    {
        bool show = !string.IsNullOrEmpty(text);
        if (objectiveRoot.activeSelf != show) objectiveRoot.SetActive(show);
        if (show && objectiveText.text != text) objectiveText.text = text;
    }

    // ─── 강조 테두리 ──────────────────────────────────────────

    // screenRect: 화면 픽셀 좌표의 사각형. null이면 숨김
    public void SetHighlight(Rect? screenRect)
    {
        if (screenRect == null)
        {
            if (highlight.gameObject.activeSelf) highlight.gameObject.SetActive(false);
            return;
        }

        Rect r = screenRect.Value;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, r.min, null, out Vector2 min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, r.max, null, out Vector2 max);
        const float pad = 10f;
        highlight.anchoredPosition = (min + max) * 0.5f;
        highlight.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x) + pad * 2f, Mathf.Abs(max.y - min.y) + pad * 2f);
        if (!highlight.gameObject.activeSelf) highlight.gameObject.SetActive(true);
        highlight.SetAsFirstSibling(); // 설명 창·도움말보다 뒤

        // 깜빡임 (일시정지 중에도 움직이도록 실제 시간 사용)
        float a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
        foreach (Image edge in highlightEdges)
            edge.color = new Color(HighlightColor.r, HighlightColor.g, HighlightColor.b, a);
    }

    // ─── 도움말 ──────────────────────────────────────────

    public void SetHelpButtonVisible(bool visible)
    {
        if (helpButton.activeSelf != visible) helpButton.SetActive(visible);
        if (!visible && helpPanel.activeSelf) helpPanel.SetActive(false);
    }

    void ToggleHelp()
    {
        if (helpPanel.activeSelf) { helpPanel.SetActive(false); return; }
        HelpOpened?.Invoke();
    }

    public event Action HelpOpened;

    // entries: (제목, 설명 창 내용)
    public void OpenHelp(List<KeyValuePair<string, TutorialPage[]>> entries, bool tipsEnabled)
    {
        foreach (Transform child in helpListContent) Destroy(child.gameObject);
        foreach (var entry in entries)
        {
            TutorialPage[] entryPages = entry.Value;
            Button b = MakeButton("Entry", helpListContent, entry.Key, SubButtonColor, () => ShowDialog(entryPages, false, null));
            LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 58f;
            TextMeshProUGUI label = b.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(20f, 0f, 10f, 0f);
            label.fontSize = 24;
        }
        SetTipsToggleLabel(tipsEnabled);
        helpPanel.SetActive(true);
        helpPanel.transform.SetAsLastSibling();
    }

    public void SetTipsToggleLabel(bool tipsEnabled)
    {
        if (tipsToggleLabel != null) tipsToggleLabel.text = tipsEnabled ? "상황별 도움말: 켜짐" : "상황별 도움말: 꺼짐";
    }

    // ─── UI 도우미 ──────────────────────────────────────────

    static GameObject NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(-padX * 2f, -padY * 2f);
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    RectTransform MakeWindow(string name, Transform parent, Vector2 size)
    {
        GameObject go = NewRect(name, parent);
        RectTransform rt = (RectTransform)go.transform;
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        go.AddComponent<Image>().color = WindowColor;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(4f, -4f);
        return rt;
    }

    TextMeshProUGUI MakeText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = NewRect(name, parent);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.richText = true;
        t.raycastTarget = false;
        return t;
    }

    Button MakeButton(string name, Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = NewRect(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = color;
        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;
        if (onClick != null) b.onClick.AddListener(onClick);

        TextMeshProUGUI t = MakeText("Label", go.transform, label, 26, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        return b;
    }
}
