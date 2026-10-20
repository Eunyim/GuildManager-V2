using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 이미지(길드 밖 거리의 건물 등)에 마우스를 올리면 그림 외곽선을 노란색으로 칠합니다.
/// 로비 월드 오브젝트의 InteractiveObject 외곽선과 같은 모양 (셰이더: Resources/Shaders/UIOutline)
/// </summary>
[RequireComponent(typeof(Image))]
public class UIHoverOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Color outlineColor = new Color(1f, 0.86f, 0.25f, 1f);

    const string SHADER_PATH = "Shaders/UIOutline";
    static Shader shader;
    static bool loaded;

    Image image;
    Material material;   // 이 이미지 전용 (색을 따로 줄 수 있도록)
    Material original;
    bool outlined;

    void Awake() => image = GetComponent<Image>();

    public void OnPointerEnter(PointerEventData e) => SetOutline(true);
    public void OnPointerExit(PointerEventData e) => SetOutline(false);
    void OnDisable() => SetOutline(false);

    void SetOutline(bool on)
    {
        if (image == null || on == outlined) return;
        if (on)
        {
            if (!loaded)
            {
                loaded = true;
                shader = Resources.Load<Shader>(SHADER_PATH);
                if (shader == null) Debug.LogWarning($"[UIHoverOutline] Resources/{SHADER_PATH} 셰이더가 없어 외곽선을 건너뜁니다.");
            }
            if (shader == null) return;
            if (material == null) material = new Material(shader) { name = "UIOutline (runtime)" };
            material.SetColor("_OutlineColor", outlineColor);
            original = image.material == image.defaultMaterial ? null : image.material;
            image.material = material;
        }
        else image.material = original;
        outlined = on;
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
