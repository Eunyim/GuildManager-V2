using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 의뢰 게시판의 지역 카드 그림. 평소엔 흑백, 마우스를 올리면 컬러 애니메이션.
/// 잠긴 지역은 마우스를 올리면 쇠사슬 X + 자물쇠 애니메이션이 한 번 재생되고 마지막 장에 멈춤.
/// 그림: Resources/UI/Regions ({key}_gray, {key}_0~, lock_0~) — Tools/region_art_gen.py로 생성
/// </summary>
public class RegionCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Image art;          // 지역 그림
    public Image lockOverlay;  // 쇠사슬·자물쇠 (잠긴 지역만)
    public float fps = 8f;
    public float lockFps = 14f;

    Sprite gray;
    Sprite[] colorFrames;
    Sprite[] lockFrames;
    bool locked, hovered;
    float time;

    public void Setup(string key, bool isLocked)
    {
        locked = isLocked;
        gray = Resources.Load<Sprite>($"UI/Regions/{key}_gray");
        colorFrames = LoadFrames(key);
        lockFrames = LoadFrames("lock");
        ShowIdle();
    }

    static Sprite[] LoadFrames(string prefix)
    {
        var list = new System.Collections.Generic.List<Sprite>();
        for (int i = 0; ; i++)
        {
            Sprite s = Resources.Load<Sprite>($"UI/Regions/{prefix}_{i}");
            if (s == null) break;
            list.Add(s);
        }
        return list.ToArray();
    }

    void ShowIdle()
    {
        hovered = false;
        if (art != null && gray != null) art.sprite = gray;
        if (lockOverlay != null) lockOverlay.enabled = false;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        time = 0f;
        if (locked)
        {
            if (lockOverlay != null && lockFrames.Length > 0) { lockOverlay.enabled = true; lockOverlay.sprite = lockFrames[0]; }
        }
        else if (art != null && colorFrames.Length > 0) art.sprite = colorFrames[0];
    }

    public void OnPointerExit(PointerEventData e) => ShowIdle();
    void OnDisable() => ShowIdle();

    void Update()
    {
        if (!hovered) return;
        time += Time.unscaledDeltaTime;
        if (locked)
        {
            // 한 번 재생하고 마지막 장(자물쇠가 걸린 모습)에 멈춤
            if (lockOverlay != null && lockFrames.Length > 0)
                lockOverlay.sprite = lockFrames[Mathf.Min((int)(time * lockFps), lockFrames.Length - 1)];
        }
        else if (art != null && colorFrames.Length > 0)
            art.sprite = colorFrames[(int)(time * fps) % colorFrames.Length];
    }
}
