using UnityEngine;
using UnityEngine.EventSystems; // 이게 있어야 UI 이벤트를 받습니다.

public class UI_Draggable : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    [Header("움직일 대상 (팝업 Window)")]
    public RectTransform targetWindow;

    [Header("캔버스 (드래그 감도 조절용)")]
    public Canvas canvas;

    private RectTransform dragRect;

    void Awake()
    {
        // 1. 드래그 핸들(이 스크립트가 붙은 오브젝트)의 RectTransform 가져오기
        dragRect = GetComponent<RectTransform>();

        // 2. targetWindow를 안 넣었으면, 그냥 내 부모를 움직이도록 자동 설정 (편의성)
        if (targetWindow == null)
        {
            targetWindow = transform.parent.GetComponent<RectTransform>();
        }

        // 3. Canvas를 안 넣었으면, 자동으로 찾기 (이게 없으면 해상도마다 속도가 다름)
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }
    }

    // 클릭하는 순간 실행 (맨 앞으로 가져오기 기능)
    public void OnPointerDown(PointerEventData eventData)
    {
        // 클릭하자마자 이 창을 최상단으로 올림
        if (targetWindow != null)
        {
            // 팝업 전체(targetWindow의 부모)를 맨 앞으로 보내야 함
            // 구조가 Popup(전체) -> Window(창) 이라면, Popup을 앞으로 보내야 함
            if(targetWindow.parent != null)
                targetWindow.parent.SetAsLastSibling();
            else
                targetWindow.SetAsLastSibling();
        }
    }

    // 드래그 중일 때 실행 (위치 이동)
    public void OnDrag(PointerEventData eventData)
    {
        if (targetWindow == null || canvas == null) return;

        // 마우스가 움직인 거리(delta)만큼 창 위치를 더해줌
        // canvas.scaleFactor로 나눠줘야 화면 크기가 바껴도 마우스랑 1:1로 따라옴
        targetWindow.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }
}