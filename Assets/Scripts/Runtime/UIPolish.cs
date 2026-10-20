using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// UI 반응 수치 (GameManager 인스펙터에서 조정. GameManager가 없는 씬에서는 기본값)
[System.Serializable]
public class UIPolishSettings
{
    [Header("창 열기 (Canvas 바로 아래 Popup_* / Panel_*)")]
    public bool popupEnabled = true;
    public float popupDuration = 0.16f;   // 나타나는 데 걸리는 시간(실제 시간, 초)
    public float popupStartScale = 0.9f;  // 상자가 이 크기에서 시작해 1로 커짐

    [Header("버튼")]
    public bool buttonEnabled = true;
    public float buttonHoverScale = 1.04f; // 마우스를 올렸을 때
    public float buttonPressScale = 0.94f; // 누르고 있을 때
    public float buttonScaleSpeed = 18f;   // 목표 크기로 따라가는 빠르기 (클수록 빠름)
}

// 씬의 창·버튼에 반응 컴포넌트와 테마(UITheme)를 자동으로 입힘. 씬 파일은 건드리지 않음
// 목록 칸처럼 나중에 생기는 버튼도 붙도록 주기적으로 다시 훑음
public class UIPolish : MonoBehaviour
{
    const float RescanInterval = 0.5f; // 실제 시간(초)
    float nextScan;

    static readonly UIPolishSettings defaultSettings = new UIPolishSettings();
    public static UIPolishSettings Settings =>
        GameManager.Instance != null && GameManager.Instance.uiPolish != null ? GameManager.Instance.uiPolish : defaultSettings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        GameObject go = new GameObject("UIPolish");
        DontDestroyOnLoad(go);
        go.AddComponent<UIPolish>();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => nextScan = 0f;

    void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + RescanInterval;
        Scan();
    }

    static void Scan()
    {
        // 창: 최상위 Canvas 바로 아래의 Popup_* / Panel_* (꺼져 있는 것 포함)
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!canvas.isRootCanvas) continue;
            foreach (Transform child in canvas.transform)
            {
                string n = child.name;
                if (!n.StartsWith("Popup_") && !n.StartsWith("Panel_")) continue;
                if (child.GetComponent<UIPopupAppear>() == null) child.gameObject.AddComponent<UIPopupAppear>();
                UITheme.ThemePopup(child); // 목록 칸처럼 새로 생긴 것도 입히도록 매번 (이미 입힌 것은 건너뜀)
            }
        }

        // 버튼: 드래그로 움직이는 카드 등 스스로 크기·위치를 바꾸는 것은 제외
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool movesItself = button.GetComponent<SwipeCard>() != null || button.GetComponent<UI_Draggable>() != null;
            if (!movesItself && button.GetComponent<UIButtonFeedback>() == null) button.gameObject.AddComponent<UIButtonFeedback>();
            UITheme.ThemeButton(button);
        }

        // 툴팁 상자 (이름에 Tooltip이 들어간 이미지), 양피지 위 글자
        if (UITheme.Enabled)
            foreach (Image image in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image.name.Contains("Tooltip")) UITheme.ThemeTooltip(image);
                else UITheme.ThemePaper(image);
            }
    }
}

// 창이 켜질 때: 전체를 페이드인하고, 가운데 상자(화면 전체를 덮지 않는 자식)는 살짝 커지며 나타남
// 닫기는 각 스크립트가 SetActive(false)로 바로 끄므로 연출 없음
public class UIPopupAppear : MonoBehaviour
{
    CanvasGroup group;
    readonly List<RectTransform> boxes = new List<RectTransform>();
    readonly List<Vector3> boxScales = new List<Vector3>();
    float elapsed = -1f; // 음수면 연출 중이 아님

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        foreach (Transform child in transform)
        {
            RectTransform rt = child as RectTransform;
            if (rt == null) continue;
            bool fullScreen = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;
            if (fullScreen) continue; // 어두운 배경 등은 페이드만
            boxes.Add(rt);
            boxScales.Add(rt.localScale);
        }
        // 상자로 볼 자식이 없으면(작은 패널) 자기 자신을 키움
        if (boxes.Count == 0 && transform is RectTransform self)
        {
            boxes.Add(self);
            boxScales.Add(self.localScale);
        }
    }

    void OnEnable()
    {
        if (!UIPolish.Settings.popupEnabled || UIPolish.Settings.popupDuration <= 0f) return;
        elapsed = 0f;
        Apply(0f);
    }

    void OnDisable()
    {
        if (elapsed >= 0f) Apply(1f); // 연출 도중에 닫혀도 다음에 열 때 원래 모습에서 시작
        elapsed = -1f;
    }

    void Update()
    {
        if (elapsed < 0f) return;
        elapsed += Time.unscaledDeltaTime; // 일시정지(게임오버 등) 중에도 동작
        float t = Mathf.Clamp01(elapsed / UIPolish.Settings.popupDuration);
        Apply(t);
        if (t >= 1f) elapsed = -1f;
    }

    void Apply(float t)
    {
        group.alpha = t;
        float eased = 1f - (1f - t) * (1f - t) * (1f - t); // 처음 빠르고 끝에서 부드럽게
        float scale = Mathf.LerpUnclamped(UIPolish.Settings.popupStartScale, 1f, eased);
        for (int i = 0; i < boxes.Count; i++)
            if (boxes[i] != null) boxes[i].localScale = boxScales[i] * scale;
    }
}

// 버튼: 마우스를 올리면 살짝 커지고, 누르면 작아짐. 누를 수 없는 버튼은 반응하지 않음
// 테마가 눌린 모양 스프라이트를 넘겨주면 누르는 동안 그 모양으로 바꾸고 글자를 도트 한 칸 내림
public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    Button button;
    Vector3 baseScale;
    bool hovered, pressed;

    Image image;
    Sprite normalSprite, pressedSprite;
    bool showingPressed;
    TMP_Text[] labels;
    Vector2[] labelPositions;

    public void SetPressedSprite(Sprite sprite)
    {
        image = button != null ? button.targetGraphic as Image : GetComponent<Image>();
        if (image == null || sprite == null) return;
        normalSprite = image.sprite;
        pressedSprite = sprite;
    }

    void ShowPressed(bool on)
    {
        if (showingPressed == on || image == null || pressedSprite == null) return;
        showingPressed = on;
        image.sprite = on ? pressedSprite : normalSprite;

        if (on)
        {
            labels = GetComponentsInChildren<TMP_Text>();
            labelPositions = new Vector2[labels.Length];
            float drop = image.pixelsPerUnitMultiplier > 0f ? 1f / image.pixelsPerUnitMultiplier : 3f; // 도트 한 칸
            for (int i = 0; i < labels.Length; i++)
            {
                labelPositions[i] = labels[i].rectTransform.anchoredPosition;
                labels[i].rectTransform.anchoredPosition = labelPositions[i] + Vector2.down * drop;
            }
        }
        else if (labels != null)
        {
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] != null) labels[i].rectTransform.anchoredPosition = labelPositions[i];
            labels = null;
        }
    }

    void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
    }

    void OnDisable()
    {
        hovered = pressed = false;
        ShowPressed(false);
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) pressed = true; }
    public void OnPointerUp(PointerEventData e) => pressed = false;

    void Update()
    {
        UIPolishSettings s = UIPolish.Settings;
        bool active = s.buttonEnabled && button != null && button.IsInteractable();
        ShowPressed(active && pressed);
        float target = !active ? 1f : pressed ? s.buttonPressScale : hovered ? s.buttonHoverScale : 1f;

        Vector3 goal = baseScale * target;
        if (transform.localScale == goal) return;
        transform.localScale = Vector3.Lerp(transform.localScale, goal, 1f - Mathf.Exp(-s.buttonScaleSpeed * Time.unscaledDeltaTime));
        if ((transform.localScale - goal).sqrMagnitude < 0.000001f) transform.localScale = goal;
    }
}
