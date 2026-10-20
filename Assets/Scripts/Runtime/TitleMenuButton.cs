using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 타이틀 메뉴 버튼 연출: 마우스를 올리면 커지고 글자가 금빛이 되며 왼쪽에 검 커서, 누르면 글자가 눌림.
/// 그림 바꾸기(평소·올림·누름·꺼짐)는 Button의 Sprite Swap이 처리
/// </summary>
[RequireComponent(typeof(Button))]
public class TitleMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public TMP_Text label;
    public Color normalColor = new Color(0.98f, 0.92f, 0.78f);
    public Color hoverColor = new Color(1f, 0.85f, 0.4f);
    public Color disabledColor = new Color(0.6f, 0.56f, 0.52f);
    public float hoverScale = 1.06f;
    public float pressOffset = 4f;

    Button button;
    RectTransform rect;
    bool hovered, pressed;
    Vector2 labelBase;
    float scale = 1f;

    void Awake()
    {
        button = GetComponent<Button>();
        rect = (RectTransform)transform;
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label != null) labelBase = label.rectTransform.anchoredPosition;
    }

    void OnDisable()
    {
        hovered = pressed = false;
        if (TitleScreen.Instance != null) TitleScreen.Instance.ClearCursorTarget(rect);
    }

    void Update()
    {
        bool usable = button.IsInteractable() && IsGroupInteractable();
        if (!usable && hovered) { hovered = false; if (TitleScreen.Instance != null) TitleScreen.Instance.ClearCursorTarget(rect); }

        float target = hovered && usable ? hoverScale : 1f;
        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        rect.localScale = new Vector3(scale, scale, 1f);

        if (label != null)
        {
            label.color = !button.IsInteractable() ? disabledColor : hovered ? hoverColor : normalColor;
            label.rectTransform.anchoredPosition = labelBase + (pressed && hovered ? new Vector2(0f, -pressOffset) : Vector2.zero);
        }
    }

    bool IsGroupInteractable()
    {
        CanvasGroup g = GetComponent<CanvasGroup>();
        return g == null || g.interactable;
    }

    public void OnPointerEnter(PointerEventData e) => Hover(true);
    public void OnPointerExit(PointerEventData e) { Hover(false); pressed = false; }
    public void OnSelect(BaseEventData e) => Hover(true);
    public void OnDeselect(BaseEventData e) => Hover(false);
    public void OnPointerDown(PointerEventData e) => pressed = true;
    public void OnPointerUp(PointerEventData e) => pressed = false;

    void Hover(bool on)
    {
        if (on && !(button.IsInteractable() && IsGroupInteractable())) return;
        hovered = on;
        if (TitleScreen.Instance == null) return;
        if (on) TitleScreen.Instance.SetCursorTarget(rect);
        else TitleScreen.Instance.ClearCursorTarget(rect);
    }
}
