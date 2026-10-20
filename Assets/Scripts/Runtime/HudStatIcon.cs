using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상단 정보줄: 글자 왼쪽에 도트 아이콘을 붙이고, 아이콘·글자에 마우스를 올리면 이름(툴팁)을 보여줍니다.
/// 아이콘은 Resources/UI/Theme/icon_*.png (Tools/ui_icon_gen.py로 생성). 글자 내용은 LobbyManager가 채움
/// Tools > UI 테마 굽기로 씬에 미리 붙여 두면 에디터에서도 보이고(아이콘 위치는 에디터에서도 글자를 따라감), 실행 중에는 그 아이콘을 그대로 씀
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TextMeshProUGUI))]
public class HudStatIcon : MonoBehaviour
{
    [Tooltip("Resources/UI/Theme/icon_{이름}.png")]
    public string iconName;
    public float gap = 8f; // 아이콘과 글자 사이

    private TextMeshProUGUI text;
    private RectTransform icon;
    private string tip;
    private static readonly System.Collections.Generic.List<RaycastResult> hits = new System.Collections.Generic.List<RaycastResult>();

    // 여러 번 불러도 됨 (툴팁 문구만 바뀜). 씬에 이미 붙어 있으면 그것을 씀
    public static HudStatIcon Attach(TextMeshProUGUI target, string iconName, string tooltip)
    {
        if (target == null) return null;
        HudStatIcon hud = target.GetComponent<HudStatIcon>();
        if (hud == null)
        {
            hud = target.gameObject.AddComponent<HudStatIcon>();
            hud.iconName = iconName;
        }
        hud.EnsureIcon();
        hud.tip = tooltip;
        return hud;
    }

    void OnEnable() => EnsureIcon();

    // 자식 "Icon"을 찾고, 없으면 만듦
    public RectTransform EnsureIcon()
    {
        if (text == null) text = GetComponent<TextMeshProUGUI>();
        if (Application.isPlaying) text.raycastTarget = true; // 글자에 마우스를 올려도 툴팁
        if (icon != null) return icon;

        Transform found = transform.Find("Icon");
        if (found != null) { icon = (RectTransform)found; return icon; }
        if (string.IsNullOrEmpty(iconName)) return null;

        Sprite sprite = Resources.Load<Sprite>("UI/Theme/icon_" + iconName);
        if (sprite == null) { Debug.LogWarning($"[HudStatIcon] Resources/UI/Theme/icon_{iconName} 아이콘이 없습니다."); return null; }

        GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon = (RectTransform)go.transform;
        icon.SetParent(transform, false);
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);

        // 도트 한 칸이 정수 픽셀이 되도록: 큰 글자 옆은 3배(48), 작은 글자 옆은 2배(32)
        int scale = text.fontSize >= 30f ? 3 : 2;
        icon.sizeDelta = new Vector2(16f * scale, 16f * scale);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        return icon;
    }

    void LateUpdate()
    {
        if (icon == null || text == null) return;

        // 글자가 실제로 그려진 영역의 왼쪽에 붙임 (가운데 정렬이라 글자 길이에 따라 움직임)
        if (!Application.isPlaying) text.ForceMeshUpdate();
        Bounds b = text.textBounds;
        bool empty = string.IsNullOrEmpty(text.text) || b.size.x <= 0f;
        if (Application.isPlaying && icon.gameObject.activeSelf == empty) icon.gameObject.SetActive(!empty);
        if (empty) return;

        Rect r = text.rectTransform.rect;
        Vector2 center = new Vector2((0.5f - text.rectTransform.pivot.x) * r.width, (0.5f - text.rectTransform.pivot.y) * r.height);
        Vector2 pos = new Vector2(b.min.x - gap - icon.sizeDelta.x * 0.5f, b.center.y) - center;
        if ((icon.anchoredPosition - pos).sqrMagnitude > 0.01f) icon.anchoredPosition = pos;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null || string.IsNullOrEmpty(tip)) { TextLinkTooltip.HideShared(this); return; }

        Vector2 pos = pointer.position.ReadValue();
        if (IsHovered(pos)) TextLinkTooltip.ShowShared(this, tip, pos, text);
        else TextLinkTooltip.HideShared(this);
    }

    // 아이콘이나 글자 위에 있고, 다른 창에 가려지지 않았을 때
    bool IsHovered(Vector2 screenPos)
    {
        Canvas root = text.canvas != null ? text.canvas.rootCanvas : null;
        Camera cam = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        bool over = (icon != null && icon.gameObject.activeSelf && RectTransformUtility.RectangleContainsScreenPoint(icon, screenPos, cam))
                    || TextAreaContains(screenPos, cam);
        if (!over || EventSystem.current == null) return over;

        hits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPos }, hits);
        return hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(transform);
    }

    // 글자 칸 전체가 아니라 실제 글자가 그려진 부분만
    bool TextAreaContains(Vector2 screenPos, Camera cam)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(text.rectTransform, screenPos, cam, out Vector2 local)) return false;
        Bounds b = text.textBounds;
        return b.size.x > 0f && local.x >= b.min.x && local.x <= b.max.x && local.y >= b.min.y - 4f && local.y <= b.max.y + 4f;
    }

    void OnDisable()
    {
        if (Application.isPlaying) TextLinkTooltip.HideShared(this);
    }
}
