using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TMP 텍스트 안의 <link="id"> 구간에 마우스를 올리면 Tips[id] 문구를 말풍선으로 보여줍니다.
/// 말풍선은 처음 쓸 때 최상위 캔버스에 하나 만들어 모든 텍스트가 같이 씁니다.
/// (성격·특성 문구는 Core/TraitTooltipFormat이 만듦)
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextLinkTooltip : MonoBehaviour
{
    [Header("표시 설정")]
    public Vector2 cursorOffset = new Vector2(16f, -20f); // 마우스에서 말풍선 왼쪽-위 모서리까지
    public float maxWidth = 360f;                         // 이보다 길면 줄바꿈
    [Tooltip("말풍선 글자 크기 = 원래 텍스트 글자 크기 × 이 값")]
    public float fontScale = 0.9f;
    public Vector2 padding = new Vector2(12f, 9f);

    // 링크 id → 툴팁 문구. 텍스트를 바꿀 때 같이 채움
    public readonly Dictionary<string, string> Tips = new Dictionary<string, string>();

    private TextMeshProUGUI text;

    // 모든 TextLinkTooltip이 함께 쓰는 말풍선
    private static RectTransform panel;
    private static TextMeshProUGUI label;
    private static Vector2 panelPadding;
    private static object owner; // 지금 말풍선을 띄운 쪽 (TextLinkTooltip 또는 ShowShared를 부른 컴포넌트)
    private static readonly List<RaycastResult> raycastHits = new List<RaycastResult>();

    public static TextLinkTooltip Attach(TextMeshProUGUI target)
    {
        TextLinkTooltip tooltip = target.GetComponent<TextLinkTooltip>();
        return tooltip != null ? tooltip : target.gameObject.AddComponent<TextLinkTooltip>();
    }

    void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        text.raycastTarget = true; // 다른 창에 가려졌는지 확인하려면 레이캐스트에 걸려야 함
    }

    void OnDisable() => Hide();

    void Update()
    {
        // 프로젝트가 새 Input System 전용이라 Input.mousePosition은 예외를 던짐
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null || Tips.Count == 0) { Hide(); return; }

        Vector2 screenPos = pointer.position.ReadValue();
        Canvas root = text.canvas != null ? text.canvas.rootCanvas : null;
        if (root == null) { Hide(); return; }
        Camera uiCamera = root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;

        string tip = null;
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(text, screenPos, uiCamera);
        if (linkIndex >= 0 && linkIndex < text.textInfo.linkCount && IsTopmost(screenPos))
            Tips.TryGetValue(text.textInfo.linkInfo[linkIndex].GetLinkID(), out tip);

        if (string.IsNullOrEmpty(tip)) { Hide(); return; }
        Show(tip, screenPos, root, uiCamera);
    }

    // 다른 팝업이 이 텍스트 위를 덮고 있으면 툴팁을 띄우지 않음
    bool IsTopmost(Vector2 screenPos)
    {
        if (EventSystem.current == null) return true;

        PointerEventData data = new PointerEventData(EventSystem.current) { position = screenPos };
        raycastHits.Clear();
        EventSystem.current.RaycastAll(data, raycastHits);
        return raycastHits.Count > 0 && raycastHits[0].gameObject.transform.IsChildOf(transform);
    }

    void Show(string tip, Vector2 screenPos, Canvas root, Camera uiCamera) =>
        ShowBubble(this, tip, screenPos, root, uiCamera, text, text.fontSize * fontScale, cursorOffset, maxWidth, padding);

    // 링크가 아닌 곳(아이콘 등)에서 같은 말풍선을 띄울 때. style 텍스트의 폰트·크기를 따름
    public static void ShowShared(object who, string tip, Vector2 screenPos, TMP_Text style)
    {
        Canvas root = style != null && style.canvas != null ? style.canvas.rootCanvas : null;
        if (root == null || string.IsNullOrEmpty(tip)) { HideShared(who); return; }
        Camera uiCamera = root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        ShowBubble(who, tip, screenPos, root, uiCamera, style, 24f, new Vector2(16f, -20f), 360f, new Vector2(18f, 15f));
    }

    public static void HideShared(object who)
    {
        if (owner != who) return;
        owner = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    static void ShowBubble(object who, string tip, Vector2 screenPos, Canvas root, Camera uiCamera, TMP_Text style,
                           float fontSize, Vector2 cursorOffset, float maxWidth, Vector2 padding)
    {
        EnsurePanel(root);
        owner = who;

        label.font = style.font;
        bool resized = !Mathf.Approximately(label.fontSize, fontSize) || panelPadding != padding;
        label.fontSize = fontSize;
        panelPadding = padding;
        if (label.text != tip || resized)
        {
            label.text = tip;
            float width = Mathf.Min(label.GetPreferredValues(tip).x + 1f, maxWidth);
            float height = label.GetPreferredValues(tip, width, 0f).y;
            label.rectTransform.sizeDelta = new Vector2(width, height);
            panel.sizeDelta = new Vector2(width, height) + padding * 2f;
        }

        if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();

        // 마우스 오른쪽 아래에 두되 화면 밖으로 나가지 않게 가둠
        RectTransform parent = (RectTransform)root.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, uiCamera, out Vector2 local)) return;

        Vector2 size = panel.sizeDelta;
        Vector2 corner = local + cursorOffset;
        corner.x = Mathf.Clamp(corner.x, parent.rect.xMin, parent.rect.xMax - size.x);
        corner.y = Mathf.Clamp(corner.y, parent.rect.yMin + size.y, parent.rect.yMax);
        panel.localPosition = corner;
    }

    void Hide() => HideShared(this);

    static void EnsurePanel(Canvas root)
    {
        if (panel == null)
        {
            GameObject go = new GameObject("LinkTooltip", typeof(RectTransform), typeof(Image));
            panel = (RectTransform)go.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0f, 1f);

            Image bg = go.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.11f, 0.95f);
            bg.raycastTarget = false;

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(panel, false);
            label = textGo.GetComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.richText = true;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
        }

        if (panel.parent != root.transform) panel.SetParent(root.transform, false);

        // 다른 캔버스(상단 정보줄 등)보다 항상 위에 그려지도록 자체 정렬 순서를 줌
        Canvas own = panel.GetComponent<Canvas>();
        if (own == null) own = panel.gameObject.AddComponent<Canvas>();
        own.overrideSorting = true;
        own.sortingOrder = 30000;
    }
}
