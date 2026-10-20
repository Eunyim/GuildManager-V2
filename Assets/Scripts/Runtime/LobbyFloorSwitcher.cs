using UnityEngine;

/// <summary>
/// 로비의 층 전환 (1층 길드 홀 ↔ 2층 여관).
/// 계단 오브젝트를 클릭하면 해당 층의 오브젝트 묶음만 켜서 보여줍니다.
/// </summary>
public class LobbyFloorSwitcher : MonoBehaviour
{
    [Header("층별 오브젝트 묶음")]
    public GameObject firstFloorRoot;   // 1층 시설 (Lobby_Facilities)
    public GameObject secondFloorRoot;  // 2층 여관 (Lobby_Floor2)

    [Header("1층을 돌아다니는 모험가 치비 (2층에서는 숨김)")]
    public RectTransform chibiLayer;

    [Header("층 이동 시 알림 문구")]
    public string firstFloorName  = "1층 - 길드 홀";
    public string secondFloorName = "2층 - 여관";

    public bool IsUpstairs { get; private set; }

    private CanvasGroup chibiGroup;

    void Start()
    {
        Show(false, false);
    }

    // 1층 계단 클릭
    public void GoUpstairs()
    {
        Show(true, true);
    }

    // 2층 계단 클릭
    public void GoDownstairs()
    {
        Show(false, true);
    }

    void Show(bool upstairs, bool notify)
    {
        IsUpstairs = upstairs;

        if (firstFloorRoot != null)  firstFloorRoot.SetActive(!upstairs);
        if (secondFloorRoot != null) secondFloorRoot.SetActive(upstairs);

        // 치비는 꺼버리면 이동 코루틴이 끊기므로, 투명하게만 만들어 숨김
        if (chibiLayer != null)
        {
            if (chibiGroup == null)
            {
                chibiGroup = chibiLayer.GetComponent<CanvasGroup>();
                if (chibiGroup == null) chibiGroup = chibiLayer.gameObject.AddComponent<CanvasGroup>();
            }
            chibiGroup.alpha = upstairs ? 0f : 1f;
        }

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.HideObjectLabel();
            if (notify) LobbyManager.Instance.ShowNotification(upstairs ? secondFloorName : firstFloorName);
        }
    }
}
