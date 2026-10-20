using UnityEngine;
using UnityEngine.EventSystems; //마우스 이벤트 감지
//마우스 올리고 내리는 것을 감지
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData) //마우스가 Hover일 때
    {
        if (LobbyManager.Instance != null) 
        {
            LobbyManager.Instance.ShowTraitTooltip(); //툴팁이 보여지도록 요청
        }
    }

    public void OnPointerExit(PointerEventData eventData) //마우스가 Exit일 때
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.HideTraitTooltip(); //툴팁이 꺼지도록 요청 
        }
    }
}
