using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그림 몇 장을 돌려 가며 보여 주는 간단한 프레임 애니메이션 (촛불·횃불·벽난로·굴뚝 연기 등).
/// 월드 오브젝트(SpriteRenderer)와 UI(Image) 둘 다 됨. 에디터에서는 첫 장만 보임
/// </summary>
public class SpriteFrameAnimator : MonoBehaviour
{
    public Sprite[] frames;
    [Tooltip("초당 프레임 수")]
    public float fps = 8f;
    [Tooltip("여러 개가 똑같이 깜빡이지 않도록 시작 프레임을 무작위로")]
    public bool randomStart = true;

    SpriteRenderer spriteRenderer;
    Image image;
    float time;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        image = GetComponent<Image>();
        if (randomStart && frames != null && frames.Length > 0) time = Random.value * frames.Length / Mathf.Max(0.01f, fps);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || fps <= 0f) return;
        time += Time.deltaTime;
        Sprite s = frames[(int)(time * fps) % frames.Length];
        if (spriteRenderer != null && spriteRenderer.sprite != s) spriteRenderer.sprite = s;
        else if (image != null && image.sprite != s) image.sprite = s;
    }
}
