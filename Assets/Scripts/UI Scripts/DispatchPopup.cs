using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class DispatchPopup : MonoBehaviour
{
    [Header("UI 연결")]
    public GameObject partySlotPrefab; // 파티 슬롯 프리팹
    public Transform contentArea;      // Scroll View의 Content
    public GameObject popupObject;     // 이 팝업창 자체 (자기 자신)

    private QuestData currentTargetQuest; // 지금 하려는 퀘스트가 뭔지 기억

    // 외부에서 이 창을 열 때 호출하는 함수
    public void OpenDispatchWindow(QuestData quest)
    {
        currentTargetQuest = quest; // 퀘스트 정보 저장
        popupObject.SetActive(true);
        popupObject.transform.SetAsLastSibling(); // 맨 앞으로

        RefreshPartyList();
    }

    void RefreshPartyList()
    {
        // 1. 기존 슬롯 청소
        foreach (Transform child in contentArea) Destroy(child.gameObject);

        // 2. 내 파티 목록 불러오기
        List<Party> parties = GameManager.Instance.partyList;

        for (int i = 0; i < parties.Count; i++)
        {
            Party party = parties[i];
            GameObject slot = Instantiate(partySlotPrefab, contentArea);

            // 텍스트 설정 (프리팹 구조에 따라 수정 필요)
            TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
            
            // 파티 이름과 레벨 표시
            int avgLevel = party.GetAverageMemberLevel();
            if(texts.Length > 0) texts[0].text = $"파티 {i + 1}";
            if(texts.Length > 1) texts[1].text = $"평균 Lv.{avgLevel} / {party.members.Count}명";

            // 버튼 설정
            Button btn = slot.GetComponentInChildren<Button>();
            TextMeshProUGUI btnText = btn.GetComponentInChildren<TextMeshProUGUI>();

            // 조건 검사: 파티원이 1명이라도 있어야 함 + (나중엔 '이미 파견중'인지도 체크)
            if (party.members.Count == 0)
            {
                btn.interactable = false;
                btnText.text = "빈 파티";
            }
            else if (avgLevel < currentTargetQuest.recommendedLevel)
            {
                // 레벨 부족해도 보낼 순 있게 하거나, 막거나 (일단 경고색 표시)
                btnText.text = "<color=red>위험</color>";
                btn.onClick.AddListener(() => OnSelectParty(party));
            }
            else
            {
                btnText.text = "출동 가능";
                btn.onClick.AddListener(() => OnSelectParty(party));
            }
        }
    }

    // 파티 선택 시 실행 (실제 파견 로직)
    void OnSelectParty(Party party)
    {
        Debug.Log($"[파견 시작] 파티 {party.members.Count}명이 '{currentTargetQuest.questName}' 의뢰를 수행하러 떠납니다!");
        
        // TODO: 여기서 실제로 파티 상태를 '파견중'으로 바꾸고 타이머 시작해야 함
        
        popupObject.SetActive(false); // 창 닫기
    }
    
    // 닫기 버튼용
    public void ClosePopup()
    {
        popupObject.SetActive(false);
    }
}