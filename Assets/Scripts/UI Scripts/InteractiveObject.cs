using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 로비 내 클릭 가능한 시설 오브젝트 (포인트 앤 클릭).
/// UI Image, 또는 월드에 놓인 SpriteRenderer + Collider2D 오브젝트에 붙여서 사용합니다.
/// (월드 오브젝트는 카메라의 Physics2DRaycaster가 클릭을 전달합니다)
/// </summary>
public class InteractiveObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("설정")]
    public string objectName; // 예: "접수처", "퀘스트 보드"

    [Header("클릭 시 실행할 행동")]
    public UnityEvent onClickAction;

    [Header("하이라이트")]
    [Tooltip("호버 시 밝기 증가량 (0~1)")]
    public float highlightBrightness = 0.3f;

    [Tooltip("호버 시 그림 외곽선을 이 색으로 칠함 (월드 SpriteRenderer 오브젝트만)")]
    public bool outlineOnHover = true;
    public Color outlineColor = new Color(1f, 0.86f, 0.25f, 1f);

    const string OUTLINE_SHADER_PATH = "Shaders/SpriteOutline";
    static Material outlineMaterial;
    static bool outlineLoaded;
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private Material spriteMaterial;   // 원래 재질 (호버가 끝나면 되돌림)
    private bool outlined;
    private MaterialPropertyBlock block;

    static Material OutlineMaterial
    {
        get
        {
            if (!outlineLoaded)
            {
                outlineLoaded = true;
                Shader shader = Resources.Load<Shader>(OUTLINE_SHADER_PATH);
                if (shader != null) outlineMaterial = new Material(shader) { name = "SpriteOutline (runtime)" };
                else Debug.LogWarning($"[InteractiveObject] Resources/{OUTLINE_SHADER_PATH} 셰이더가 없어 외곽선을 건너뜁니다.");
            }
            return outlineMaterial;
        }
    }

    // 외곽선 켜기/끄기: 호버 동안만 외곽선 재질로 바꿈
    void SetOutline(bool on)
    {
        if (!outlineOnHover || spriteRenderer == null || on == outlined) return;
        if (on)
        {
            if (OutlineMaterial == null) return;
            spriteMaterial = spriteRenderer.sharedMaterial;
            spriteRenderer.sharedMaterial = OutlineMaterial;
            if (block == null) block = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(block);
            block.SetColor(OutlineColorId, outlineColor);
            spriteRenderer.SetPropertyBlock(block);
        }
        else spriteRenderer.sharedMaterial = spriteMaterial;
        outlined = on;
    }

    private Image image;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isHovered;
    private bool initialized;

    void Start()
    {
        EnsureInitialized();
    }

    void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        image = GetComponent<Image>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (image != null) originalColor = image.color;
        else if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    // 평소 색을 바꿈 (상태 표시용). 호버 하이라이트는 이 색을 기준으로 적용됨
    public void SetBaseColor(Color color)
    {
        EnsureInitialized();
        originalColor = color;
        SetColor(isHovered ? Color.Lerp(originalColor, Color.white, highlightBrightness) : originalColor);
    }

    // 이름 라벨 변경. 마우스가 올라가 있으면 떠 있는 라벨도 바로 갱신
    public void SetObjectName(string newName)
    {
        objectName = newName;
        if (isHovered && LobbyManager.Instance != null)
            LobbyManager.Instance.ShowObjectLabel(objectName);
    }

    void SetColor(Color color)
    {
        if (image != null) image.color = color;
        else if (spriteRenderer != null) spriteRenderer.color = color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsBlocked()) return;

        isHovered = true;

        SetColor(Color.Lerp(originalColor, Color.white, highlightBrightness));
        EnsureInitialized();
        SetOutline(true);

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.ShowObjectLabel(objectName);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        SetColor(originalColor);
        SetOutline(false);

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.HideObjectLabel();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsBlocked()) return;

        onClickAction.Invoke();
    }

    bool IsBlocked()
    {
        if (LobbyManager.Instance != null && LobbyManager.Instance.IsAnyPopupOpen())
            return true;

        return false;
    }

    void OnDisable()
    {
        if (isHovered)
        {
            isHovered = false;
            SetColor(originalColor);
            SetOutline(false);
            if (LobbyManager.Instance != null) LobbyManager.Instance.HideObjectLabel();
        }
    }
}