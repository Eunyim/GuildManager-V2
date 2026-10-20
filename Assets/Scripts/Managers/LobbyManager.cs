using UnityEngine;
using UnityEngine.UI;
using TMPro; // 텍스트 매시 프로 필수
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance; //싱글톤 추가 (어디서든 LobbyManager.Instance로 호출)

    void Awake()
    {
        Instance = this;
    }

    [Header("툴팁 UI")]
    public GameObject tooltipPanel;         
    public TextMeshProUGUI tooltipText;

    [Header("상단 정보 UI")]
    public TextMeshProUGUI guildNameText;
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI spiritStoneText; // 영혼석 보유 수 표시 (없으면 null 무시)
    public TextMeshProUGUI reputationText;  // 명성 표시 (없으면 null 무시)

    [Header("시설 레벨 표시 텍스트 (없으면 null 무시)")]
    public TextMeshProUGUI innLevelText;
    public TextMeshProUGUI pubLevelText;
    public TextMeshProUGUI templeLevelText;
    public TextMeshProUGUI receptionLevelText;
    public TextMeshProUGUI questBoardLevelText;

    [Header("알림 메시지 UI (없으면 Debug.Log만)")]
    public GameObject notificationPanel;        // 화면 상단/중앙 알림 패널
    public TextMeshProUGUI notificationText;    // 알림 텍스트

    [Header("오브젝트 라벨 UI (포인트 앤 클릭)")]
    public GameObject objectLabelPanel;      // 마우스 근처에 뜨는 이름 라벨
    public TextMeshProUGUI objectLabelText;  // 라벨 텍스트

    [Header("팝업창")]
    public ReceptionView receptionView; // 접수처 1인칭 고용 화면
    public GameObject adventurerMenuPopup; // 모험가 관리 선택창 (명단 확인 / 파티 편성)
    public GameObject roomMenuPopup;       // 여관 방 선택창 (파티 관리 / 방 비우기)
    public FacilityUpgradePanel facilityUpgradePanel; // 건축 공방(시설 업그레이드) 창
    public ShopPanel shopPanel;            // 상점(전리품 판매) 창
    public ClinicPanel clinicPanel;        // 치유소(결함 치료) 창
    public GameObject townView;            // 길드 밖 화면 (건축 공방 / 신전 / 상점 선택)
    public GameObject questBoardMenuPopup; // 퀘스트 보드 선택창 (직접 지휘 / 동시 파견)
    public MultiDispatchPanel multiDispatchPanel; // 동시 파견 창
    public GameObject passWeekPopup;       // 한 주 보내기 확인창
    public TextMeshProUGUI passWeekMessageText;
    public TextMeshProUGUI roomMenuTitleText;
    public GameObject resultPopup; // ★ 추가: 전투 결과 팝업
    public SpiritStonePopup spiritStonePopup; // 신전(영혼석) 팝업

    [Header("전투 결과 팝업 UI 요소")]
    public TextMeshProUGUI resultTitleText;
    public TextMeshProUGUI resultGoldText;
    public TextMeshProUGUI resultReputationText;
    public Transform resultItemContent;
    public GameObject resultItemSlotPrefab;
    public TextMeshProUGUI resultLevelUpText; // 레벨업 내역 (없으면 null 무시)
    public TextMeshProUGUI resultItemsText;   // 전리품 목록을 텍스트 하나로 표시 (슬롯 프리팹이 없을 때 사용)

    [Header("파산(게임오버) 팝업")]
    public GameObject gameOverPopup;

    [Header("관리(명단) UI")]
    public GameObject managePopup;    // 관리 팝업창
    public Transform listContent;     // Scroll View 안의 Content (슬롯이 붙을 부모)
    public GameObject slotPrefab;     // Slot 프리팹

    [Header("상세 정보 팝업 연결")]
    public GameObject detailPopup;        // 팝업 전체 오브젝트
    public TextMeshProUGUI detailName;    // 이름 텍스트
    public TextMeshProUGUI detailInfo;    // 직업+등급
    public TextMeshProUGUI detailTrait;   // 성격
    public TextMeshProUGUI detailParty;   // 소속 파티 상태
    public TextMeshProUGUI detailStats;   // 스탯 정보
    public TextMeshProUGUI detailRelations; // 관계 (오른쪽 칸)
    public Image detailPortrait;          // 가운데 모험가 모습 (조립 그림, 대기 애니메이션)
    [Tooltip("신상 창 가운데 모험가 그림의 몸 키 (UI 단위)")]
    public float detailPortraitHeight = 400f;



    [Header("파티 목록 팝업")]
    public GameObject partyListPopup;
    public Transform partyListContent; // 파티 목록이 뜰 곳
    public GameObject partyListSlotPrefab; // 파티 이름이 적힌 긴 버튼 프리팹

    [Header("이름 입력 팝업")]
    public GameObject inputNamePopup;
    public TMP_InputField nameInputField;

    [Header("파티 상세 팝업")]
    public GameObject partyDetailPopup;
    public TextMeshProUGUI detailTitleText; // "OOO 파티" 제목
    public Transform detailMemberContent;   // 멤버 슬롯이 뜰 곳
    public GameObject memberSlotPrefab;     // 멤버 슬롯 프리팹
    public TextMeshProUGUI emptyMessageText; // "소속된 모험가가 없습니다"

    [Header("멤버 선택(추가) 팝업")]
    public GameObject memberSelectPopup;
    public Transform selectListContent;
    public TextMeshProUGUI selectCountText; // "1/4" 표시
    public GameObject highlightFramePrefab; // 선택 시 씌울 노란 테두리 이미지 (옵션)

    [Header("색상 설정")]
    //명단 배경 갈색 (RGB: 60, 25, 12)
    public Color normalColor = new Color(60f / 255f, 25f / 255f, 12f / 255f); 
    public Color selectedColor = Color.yellow; // 선택 시 노란색

    [Header("로비 캐릭터 설정")]
    public GameObject lobbyChibiPrefab; // 프리팹
    public RectTransform lobbyFloor;    // 활동 구역(Lobby_Floor)

    [Header("로비 치비 겉모습 (전투용 모험가 프리팹, 'Tools > 로비 치비 외형 연결'이 넣음)")]
    public GameObject chibiWarriorPrefab;
    public GameObject chibiMagePrefab;
    public GameObject chibiArcherPrefab;
    public GameObject chibiHealerPrefab;
    public GameObject chibiRoguePrefab;
    [Tooltip("로비 치비 몸통의 화면 높이 (UI 단위)")]
    public float chibiHeight = 100f;

    GameObject GetChibiLookPrefab(JobType job)
    {
        switch (job)
        {
            case JobType.Warrior: return chibiWarriorPrefab;
            case JobType.Mage:    return chibiMagePrefab;
            case JobType.Archer:  return chibiArcherPrefab;
            case JobType.Healer:  return chibiHealerPrefab;
            case JobType.Rogue:   return chibiRoguePrefab;
            default:              return null;
        }
    }

    private Party currentViewingParty; // 지금 상세창 파티
    
    // 현재 보고 있는 모험가를 기억하는 변수 (해고할 때 필요)
    private Adventurer currentTarget;

    public GameObject questPopup;

// 파견을 위한 임시 저장 변수
    private QuestData pendingQuest; // 지금 수락하려는 퀘스트
    private bool isDispatchMode = false; // 지금 파티 상태 구분

    // 여관 방 배정을 위한 임시 저장 변수
    private bool isRoomAssignMode = false; // 파티 목록을 '방에 배정할 파티 고르기'로 쓰는 중
    private int pendingRoomIndex = -1;     // 배정하려는 방
    private Party roomMenuParty;           // 방 선택창이 가리키는 파티

    

    // 시작할 때 UI 갱신
    void Start()
    {
        RefreshUI(); //UI 새로고침
        
        UpdateLobbyCharacters(); //게임 시작하자마자 로비에 캐릭터 소환
    }

    // UI 새로고침 함수 (돈 쓰거나 날짜 바뀔 때 호출)
    public void RefreshUI()
    {
       if (GameManager.Instance == null) return;

        GameManager gm = GameManager.Instance;
        if (guildNameText != null) guildNameText.text = gm.guildName;

        // 상단 정보줄: 이름 대신 도트 아이콘 (마우스를 올리면 이름이 뜸)
        if (goldText != null) goldText.text = $"{gm.gold:N0}";
        if (dayText != null) dayText.text = $"{gm.month}월 {gm.week}주차";
        if (spiritStoneText  != null) spiritStoneText.text  = $"{gm.GetSpiritStoneCount()}";
        if (reputationText   != null) reputationText.text   = $"{gm.reputation} (Lv.{gm.GetReputationLevel()})";
        if (innLevelText       != null) innLevelText.text       = $"Lv.{gm.innLevel}";
        if (pubLevelText       != null) pubLevelText.text       = gm.pubLevel > 0 ? $"Lv.{gm.pubLevel}" : "미건설";
        if (templeLevelText    != null) templeLevelText.text    = $"Lv.{gm.templeLevel}";
        if (receptionLevelText != null) receptionLevelText.text = $"Lv.{gm.receptionLevel}";
        if (questBoardLevelText != null) questBoardLevelText.text = $"Lv.{gm.questBoardLevel}";

        HudStatIcon.Attach(goldText, "gold", "골드");
        HudStatIcon.Attach(spiritStoneText, "spirit", "영혼석");
        HudStatIcon.Attach(reputationText, "reputation", $"명성 (Lv.{gm.GetReputationLevel()})\n명성 레벨이 오르면 새 지역의 의뢰를 받을 수 있습니다.");
        HudStatIcon.Attach(dayText, "day", "날짜\n매달 1주차에 월급을 정산합니다.");
        HudStatIcon.Attach(receptionLevelText, "reception", "접수처");
        HudStatIcon.Attach(questBoardLevelText, "questboard", $"퀘스트 보드\n동시 파견 최대 {gm.GetMaxDispatchSlots()}팀");
        HudStatIcon.Attach(innLevelText, "inn", "여관");
        HudStatIcon.Attach(pubLevelText, "pub", gm.pubLevel > 0 ? "주점" : "주점 (미건설)");
        HudStatIcon.Attach(templeLevelText, "temple", "신전");

        // ★ [추가] 전투 결과가 있다면 결과 팝업 띄우기
        if (GameManager.Instance.hasPendingResult)
        {
            if (resultPopup != null) 
            {
                resultPopup.SetActive(true);

                // 1. 승리/패배 메시지 표시
                if (resultTitleText != null)
                {
                    if (GameManager.Instance.lastResultIsMultiDispatch)
                    {
                        resultTitleText.text = "동시 파견 결과";
                        resultTitleText.color = Color.white;
                    }
                    else
                    {
                        resultTitleText.text = GameManager.Instance.lastBattleWon ? "의뢰 성공!" : "의뢰 실패...";
                        resultTitleText.color = GameManager.Instance.lastBattleWon ? Color.green : Color.red;
                    }
                }

                // 2. 이번 파견에서 얻은 골드/명성 (괄호 안은 현재 보유량)
                int earnedGold = GameManager.Instance.lastEarnedGold;
                int earnedRep  = GameManager.Instance.lastEarnedReputation;
                if (resultGoldText != null)
                    resultGoldText.text = $"골드: {(earnedGold >= 0 ? "+" : "")}{earnedGold:N0} G  (보유 {GameManager.Instance.gold:N0} G)";
                if (resultReputationText != null)
                    resultReputationText.text = $"명성: {(earnedRep >= 0 ? "+" : "")}{earnedRep}  (현재 {GameManager.Instance.reputation})";

                // 3. 획득 아이템 목록 표시
                if (resultItemContent != null && resultItemSlotPrefab != null)
                {
                    // 기존 아이템 슬롯 제거
                    foreach (Transform child in resultItemContent) Destroy(child.gameObject);

                    foreach (var itemPair in GameManager.Instance.lastEarnedLoot)
                    {
                        GameObject itemSlot = Instantiate(resultItemSlotPrefab, resultItemContent);
                        TextMeshProUGUI itemText = itemSlot.GetComponentInChildren<TextMeshProUGUI>();
                        if (itemText != null) itemText.text = $"{itemPair.Key} x {itemPair.Value}";
                    }
                }
                else if (resultItemsText != null && GameManager.Instance.lastResultIsMultiDispatch)
                {
                    // 동시 파견은 전리품 대신 파티별 결과를 보여줌
                    resultItemsText.text = string.Join("\n", GameManager.Instance.lastDispatchReport);
                }
                else if (resultItemsText != null)
                {
                    List<string> lines = new List<string>();
                    foreach (var itemPair in GameManager.Instance.lastEarnedLoot)
                        lines.Add($"{itemPair.Key} x {itemPair.Value}");
                    resultItemsText.text = lines.Count > 0
                        ? "전리품\n" + string.Join("\n", lines)
                        : "전리품 없음";
                }

                resultPopup.transform.SetAsLastSibling();

                // 레벨업 내역 표시
                if (resultLevelUpText != null)
                {
                    if (GameManager.Instance.levelUpLog.Count > 0)
                    {
                        resultLevelUpText.gameObject.SetActive(true);
                        resultLevelUpText.text = string.Join("\n", GameManager.Instance.levelUpLog);
                    }
                    else
                    {
                        resultLevelUpText.gameObject.SetActive(false);
                    }
                }
            }
            GameManager.Instance.hasPendingResult = false; // 플래그 초기화 (한 번만 띄우기 위함)
            RefreshMemberList(); // 멤버 목록 갱신 (부상자/사망자 반영)
        }
    }

    // --- 버튼 연결용 함수들 (나중에 내용 채움) ---
    
    // 접수처 클릭 -> 접수처에서 바라보는 1인칭 고용 화면으로 전환
    public void OnClickReception()
    {
        if (receptionView == null)
        {
            Debug.LogWarning("receptionView가 연결되지 않았습니다. Tools > 접수처 고용 화면 세팅을 실행하세요.");
            return;
        }
        receptionView.Open();
    }

    // 모험가 관리 오브젝트 클릭 -> 명단을 볼지, 파티를 편성할지 고르는 선택창
    public void OnClickAdventurerMenu()
    {
        if (adventurerMenuPopup == null)
        {
            Debug.LogWarning("adventurerMenuPopup이 연결되지 않았습니다. Tools > 로비 시설 오브젝트 배치를 실행하세요.");
            return;
        }
        HideObjectLabel();
        adventurerMenuPopup.SetActive(true);
        adventurerMenuPopup.transform.SetAsLastSibling();
    }

    public void CloseAdventurerMenu()
    {
        if (adventurerMenuPopup != null) adventurerMenuPopup.SetActive(false);
    }

    // 선택창의 [모험가 명단] 버튼
    public void OnClickMenuRoster()
    {
        CloseAdventurerMenu();
        OnClickManage();
    }

    // 선택창의 [파티 편성] 버튼
    public void OnClickMenuParty()
    {
        CloseAdventurerMenu();
        OnClickOpenPartyManage();
    }

    public void OnClickManage()
    {
        managePopup.SetActive(true); // 팝업 열기
        managePopup.transform.SetAsLastSibling(); // 맨 앞으로
        RefreshMemberList();         // 명단 갱신
    }

    // [닫기] 버튼
    public void OnClickCloseManage()
    {
        managePopup.SetActive(false);
    }

    public void OnClickOpenPartyManage() //파티 관리 버튼
    {
        isRoomAssignMode = false;
        pendingRoomIndex = -1;
        partyListPopup.SetActive(true);
        partyListPopup.transform.SetAsLastSibling(); //맨 앞으로
        RefreshPartyList();
    }

    void RefreshPartyList()
    {
        foreach (Transform child in partyListContent) Destroy(child.gameObject);

        // 생성된 파티들을 버튼으로 만들기
        foreach (Party party in GameManager.Instance.partyList)
        {
            // 방 배정 중에는 아직 방이 없는 파티만 보여줌
            if (isRoomAssignMode && party.roomIndex >= 0) continue;

            GameObject newSlot = Instantiate(partyListSlotPrefab, partyListContent);
            // 버튼 텍스트에 파티 이름 + 묵고 있는 방 표시
            string roomInfo = party.roomIndex >= 0 ? $"{party.roomIndex + 1}번 방" : "방 없음";
            string label = $"{party.partyName} ({roomInfo}, 파티 Lv.{GameManager.Instance.GetPartyLevel(party)} {party.members.Count}/{GameManager.Instance.GetPartyCapacity(party)}명)  <color=#FFB6D9>{GameManager.Instance.GetChemistryShortText(party)}</color>";
            // 파견할 파티를 고르는 중이면, 의뢰에 비해 약한 파티에 경고 표시
            if (isDispatchMode && IsRiskyDispatch(party, pendingQuest))
                label += $"  <color=#FF7366>위험 ({GetDispatchRiskReason(party, pendingQuest)})</color>";
            newSlot.GetComponentInChildren<TextMeshProUGUI>().text = label;

            // 클릭 시 -> 상세 창 열기
            newSlot.GetComponent<Button>().onClick.AddListener(() => OnClickPartyItem(party));
        }
    }

    public void OnClickCreatePartyButton() // [파티 생성] 버튼
    {
        inputNamePopup.SetActive(true);

        inputNamePopup.transform.SetAsLastSibling();
        nameInputField.text = ""; // 초기화
    }

    public void OnClickConfirmCreateName() // 입력창 [완료] 버튼
    {
        string pName = nameInputField.text;
        if (string.IsNullOrEmpty(pName)) return; // 빈 이름이면 아무것도 안 함

        // 1. 파티 생성 및 저장
        Party newParty = new Party(pName);
        GameManager.Instance.partyList.Add(newParty);

        // 2. UI 정리 (순서 중요!)
        nameInputField.text = "";        // 텍스트 비우기 (다음 번을 위해)
        inputNamePopup.SetActive(false); // ★ 팝업 끄기 (확실하게!)
        
        // 3. 목록 새로고침
        RefreshPartyList();
    }

    void OnClickPartyItem(Party party)
    {
        // 0. 방 배정 모드일 때 (2층 여관의 빈 방을 눌러서 온 경우)
        if (isRoomAssignMode)
        {
            int roomIndex = pendingRoomIndex;
            if (GameManager.Instance.AssignPartyToRoom(party, roomIndex))
                ShowNotification($"'{party.partyName}' 파티를 {roomIndex + 1}번 방에 배정했습니다.");
            else
                ShowNotification("이 방에는 배정할 수 없습니다.");

            ClosePartyListPopup();
            return;
        }

        // A. 파견 모드일 때 (퀘스트 수락하러 온 경우)
        if (isDispatchMode)
        {
            // 0. 여관 방이 없는 파티는 길드에 수용되지 않은 상태라 파견 불가
            if (party.roomIndex < 0)
            {
                ShowNotification("여관 방에 배정되지 않은 파티는 파견할 수 없습니다! (2층 여관에서 방을 배정하세요)");
                return;
            }

            // 1. 파티가 텅 비었으면?
            if (party.members.Count == 0)
            {
                Debug.Log("⚠️ 멤버가 없는 파티는 보낼 수 없습니다!");
                ShowNotification("멤버가 없는 파티는 보낼 수 없습니다!");
                return;
            }

            // 2. 이미 파견 나간 파티라면?
            if (party.state != PartyState.Idle)
            {
                Debug.Log("⚠️ 이미 파견 중인 파티입니다!");
                ShowNotification("이미 파견 중인 파티입니다!");
                return;
            }

            // 3. 파견 슬롯 초과 체크
            int maxSlots = GameManager.Instance.GetMaxDispatchSlots();
            int usedSlots = 0;
            foreach (Party p in GameManager.Instance.partyList)
                if (p.state == PartyState.OnQuest) usedSlots++;
            if (usedSlots >= maxSlots)
            {
                Debug.Log($"⚠️ 동시 파견 한도 초과! (최대 {maxSlots}팀) 퀘스트 보드를 업그레이드하세요.");
                ShowNotification($"동시 파견 한도 초과! (최대 {maxSlots}팀)");
                return;
            }

            // 4. 의뢰에 비해 약한 파티면 한 번 경고하고, 같은 파티를 다시 눌러야 출발 (막지는 않음)
            if (riskConfirmParty != party && IsRiskyDispatch(party, pendingQuest))
            {
                riskConfirmParty = party;
                ShowNotification($"<color=#FF7366>위험한 파견입니다! ({GetDispatchRiskReason(party, pendingQuest)})</color>\n그래도 보내려면 파티를 한 번 더 누르세요.");
                return;
            }

            // 5. 출동! (GameManager에게 명령)
            GameManager.Instance.StartQuest(party, pendingQuest);

            // 6. 정리 (모드 끄기)
            isDispatchMode = false;
            pendingQuest = null;
            riskConfirmParty = null;
        }
        // B. 일반 관리 모드일 때 (그냥 상세정보 보러 온 경우)
        else
        {
            OpenPartyDetail(party);
        }
    }

    // ─── 파견 경고 ──────────────────────────
    private Party riskConfirmParty; // 위험 경고를 이미 본 파티 (다시 누르면 출발)

    static float AverageLevel(Party party)
    {
        float sum = 0f;
        foreach (Adventurer member in party.members) sum += member.level;
        return party.members.Count > 0 ? sum / party.members.Count : 0f;
    }

    static float AverageRank(Party party)
    {
        float sum = 0f;
        foreach (Adventurer member in party.members) sum += (int)member.rank;
        return party.members.Count > 0 ? sum / party.members.Count : 0f;
    }

    // 이 의뢰에 보내기에 파티가 약한가 (기준은 GameManager.dispatchRisk)
    bool IsRiskyDispatch(Party party, QuestData quest)
    {
        if (party == null || quest == null || party.members.Count == 0) return false;
        if (!EnemyScalingRules.TryParseRank(quest.rank, out RankType questRank)) return false;

        return DispatchRiskRules.IsRisky(GameManager.Instance.dispatchRisk,
            AverageLevel(party), AverageRank(party), quest.recommendedLevel, questRank);
    }

    string GetDispatchRiskReason(Party party, QuestData quest)
    {
        return $"{quest.rank}랭크 의뢰, 권장 Lv.{quest.recommendedLevel} / 파티 평균 Lv.{AverageLevel(party):0.#}, 평균 랭크가 의뢰보다 낮음";
    }

    // 파티 상세 창 열기 (파티 목록, 2층 여관 방에서 호출)
    public void OpenPartyDetail(Party party)
    {
        if (party == null) return;

        currentViewingParty = party;
        partyDetailPopup.SetActive(true);
        partyDetailPopup.transform.SetAsLastSibling();
        RefreshPartyDetail();
    }

    void RefreshPartyDetail()
    {
        if (currentViewingParty == null) return;

        // 파티 레벨(묵는 방의 레벨)·인원과 케미 평균. 구성원을 바꾸면 바로 보이도록 매번 다시 씀
        GameManager gm = GameManager.Instance;
        detailTitleText.text = $"{currentViewingParty.partyName}  <size=70%>파티 Lv.{gm.GetPartyLevel(currentViewingParty)} ({currentViewingParty.members.Count}/{gm.GetPartyCapacity(currentViewingParty)}명)  " +
                               $"<color=#FFB6D9>{gm.GetChemistryText(currentViewingParty)}</color></size>";
        RefreshExpandRoomButton();

        // 멤버 리스트 청소
        foreach (Transform child in detailMemberContent) Destroy(child.gameObject);

        // 멤버가 없으면 문구 띄우기
        if (currentViewingParty.members.Count == 0)
        {
            emptyMessageText.gameObject.SetActive(true);
        }
        else
        {
            emptyMessageText.gameObject.SetActive(false);
            // 멤버 슬롯 생성
            foreach (Adventurer member in currentViewingParty.members)
            {
                GameObject slot = Instantiate(memberSlotPrefab, detailMemberContent);
                SetupSlotBasic(slot, member); // 이름/직업 표시

                // 개인 케미 (동료들과의 관계). 직업 칸 아래에 덧붙임
                string chem = GameManager.Instance.GetMemberChemistryText(member, currentViewingParty);
                Transform jobText = slot.transform.Find("Txt_Job");
                TextMeshProUGUI job = jobText != null ? jobText.GetComponent<TextMeshProUGUI>() : null;
                if (job != null && chem != "") job.text += $"\n<size=70%><color=#FFB6D9>{chem}</color></size>";
                
                // 상세창에서의 클릭 기능: (추후 추방 기능을 넣는다면 여기)
            }
        }
    }
    
    // ─── 여관 방 증축 (파티 상세 창 아래 가운데 버튼, 처음 열 때 [멤버 추가] 버튼을 본떠 만듦) ───
    private Button expandRoomButton;
    private TextMeshProUGUI expandRoomLabel;

    void RefreshExpandRoomButton()
    {
        if (expandRoomButton == null)
        {
            Transform addButton = partyDetailPopup != null ? partyDetailPopup.transform.Find("Window/Btn_Add") : null;
            if (addButton == null) return;
            GameObject clone = Instantiate(addButton.gameObject, addButton.parent);
            clone.name = "Btn_ExpandRoom";
            RectTransform rt = clone.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 15f);
            rt.sizeDelta = new Vector2(320f, rt.sizeDelta.y);
            expandRoomButton = clone.GetComponent<Button>();
            expandRoomButton.onClick = new Button.ButtonClickedEvent(); // 복사해 온 [멤버 추가] 동작 지움
            expandRoomButton.onClick.AddListener(OnClickExpandRoom);
            expandRoomLabel = clone.GetComponentInChildren<TextMeshProUGUI>();
        }

        GameManager gm = GameManager.Instance;
        int room = currentViewingParty != null ? currentViewingParty.roomIndex : -1;
        int cost = room >= 0 ? gm.GetRoomExpandCost(room) : -1;
        if (room < 0)
        {
            expandRoomLabel.text = "방 증축 (방 없음)";
            expandRoomButton.interactable = false;
        }
        else if (cost < 0)
        {
            expandRoomLabel.text = $"방 증축 완료 (최대 {gm.innRooms.maxMembers}명)";
            expandRoomButton.interactable = false;
        }
        else
        {
            int now = gm.GetPartyCapacity(currentViewingParty);
            bool afford = gm.gold >= cost;
            string price = afford ? $"{cost:N0} G" : $"<color=#FF7366>{cost:N0} G</color>";
            expandRoomLabel.text = $"방 증축 {now}→{now + 1}명 ({price})";
            expandRoomButton.interactable = true;
        }
    }

    public void OnClickExpandRoom()
    {
        if (currentViewingParty == null) return;
        GameManager.Instance.ExpandRoom(currentViewingParty.roomIndex, out string message);
        ShowNotification(message);
        RefreshPartyDetail();
        RefreshUI();
    }

    // [해체] 버튼
    public void OnClickDisbandParty()
    {
        if (currentViewingParty == null) return;

        // 멤버들을 대기 상태로 돌리고 리스트에서 삭제 (다른 파티 멤버들의 소속 번호도 함께 정리됨)
        GameManager.Instance.DisbandParty(currentViewingParty);
        currentViewingParty = null;

        // 창 닫고 목록 갱신
        partyDetailPopup.SetActive(false);
        RefreshPartyList();
    }

    public void OnClickAddMemberButton() // [추가] 버튼
    {
        if (currentViewingParty == null) return;

        memberSelectPopup.SetActive(true); // 멤버 선택 창을 맨 앞으로 가져오기
        memberSelectPopup.transform.SetAsLastSibling(); 
        
        RefreshSelectPopup();
    }

    void RefreshSelectPopup()
    {
        // 안전장치
        if (currentViewingParty == null || selectListContent == null) return;

        // 1. 기존 목록 청소
        foreach (Transform child in selectListContent) Destroy(child.gameObject);

        // 2. 인원수 텍스트 갱신
        if (selectCountText != null)
        {
            int currentCount = currentViewingParty.members.Count;
            int capacity = GameManager.Instance.GetPartyCapacity(currentViewingParty);
            selectCountText.text = $"현재 인원: {currentCount} / {capacity}";
            selectCountText.color = (currentCount >= capacity) ? Color.red : Color.white;
        }

        // 3. 리스트 생성 루프
        foreach (Adventurer member in GameManager.Instance.adventurers)
        {
            // 다른 파티 소속이면 패스
            if (member.assignedPartyIndex != -1 && !currentViewingParty.members.Contains(member))
                continue;

            // 던전에 낙오한 모험가는 구출하기 전까지 편성할 수 없음
            if (member.isStranded) continue;

            GameObject slot = Instantiate(memberSlotPrefab, selectListContent);
            SetupSlotBasic(slot, member);

            // 이미지 & 버튼 찾기
            Image bgImage = slot.GetComponent<Image>();
            if (bgImage == null) bgImage = slot.GetComponentInChildren<Image>();

            Button btn = slot.GetComponent<Button>();
            if (btn == null) btn = slot.GetComponentInChildren<Button>();

            // 텍스트 색상 변경을 위해 찾기
            TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();

            // ★ [핵심] 색상 적용 로직
            if (bgImage != null)
            {
                if (currentViewingParty.members.Contains(member)) 
                {
                    // A. 선택된 상태 (노란 배경 + 검은 글씨)
                    bgImage.color = selectedColor; 
                    foreach(var txt in texts) txt.color = Color.black; 
                }
                else
                {
                    // B. 선택 안 된 상태 (지정하신 갈색 + 흰 글씨)
                    bgImage.color = normalColor; 
                    foreach(var txt in texts) txt.color = Color.white; 
                }
            }

            // 버튼 기능 연결
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnToggleMember(member, bgImage, texts));
            }
        }
    }

    // 8-1. 클릭 시 넣었다 뺐다 (토글)
   void OnToggleMember(Adventurer member, Image slotImage, TextMeshProUGUI[] texts)
    {
        // A. 이미 멤버라면 -> 뺀다 (원상복구)
        if (currentViewingParty.members.Contains(member))
        {
            currentViewingParty.members.Remove(member);
            member.assignedPartyIndex = -1; // -1은 '소속 없음'
            
            // 색상 원상복구
            slotImage.color = normalColor;
            foreach(var txt in texts) txt.color = Color.white;
        }
        // B. 멤버가 아니라면 -> 넣는다 (하이라이트)
        else
        {
            int capacity = GameManager.Instance.GetPartyCapacity(currentViewingParty);
            if (currentViewingParty.members.Count >= capacity)
            {
                ShowNotification(capacity < GameManager.Instance.innRooms.maxMembers
                    ? $"파티가 꽉 찼습니다! ({capacity}명) 여관 방을 증축하면 더 넣을 수 있습니다."
                    : $"파티가 꽉 찼습니다! (최대 {capacity}명)");
                return;
            }

            // ★ [추가된 부분] 다친 모험가인지 체크 (현재 체력이 최대 체력보다 낮다면 무조건 부상!)
            if (member.currentHp < member.hp) 
            {
                // 막지 않고 경고 문구만 출력합니다! (진정한 악덕 매니저의 길 😈)
                Debug.LogWarning($"⚠️ [출전 경고] {member.name}님은 부상을 입은 상태입니다! (현재 HP: {member.currentHp}/{member.hp}) 그래도 파티에 강제 편성합니다.");
                
                // TODO: 나중에 화면 중앙에 팝업 띄우기 추가 가능
            }

            currentViewingParty.members.Add(member);

            // GameManager의 파티 리스트에서 '지금 보고 있는 파티(currentViewingParty)'가 몇 번째인지 찾습니다.
            int realPartyIndex = GameManager.Instance.partyList.IndexOf(currentViewingParty);
            member.assignedPartyIndex = realPartyIndex; 
            
            // 하이라이트
            slotImage.color = selectedColor;
            foreach(var txt in texts) txt.color = Color.black;
        }

        // 인원수 텍스트 갱신
        int currentCount = currentViewingParty.members.Count;
        if(selectCountText != null) 
        {
            int capacity = GameManager.Instance.GetPartyCapacity(currentViewingParty);
            selectCountText.text = $"현재 인원: {currentCount} / {capacity}";
            selectCountText.color = (currentCount >= capacity) ? Color.red : Color.white;
        }

        // 상세 창도 실시간으로 갱신
        RefreshPartyDetail();
    }

    // 팝업 닫기들
    public void CloseInputPopup() => inputNamePopup.SetActive(false);
    public void ClosePartyDetailPopup() => partyDetailPopup.SetActive(false);
    public void CloseSelectPopup() => memberSelectPopup.SetActive(false);
    public void CloseQuestPopup() => questPopup.SetActive(false); // 의뢰 게시판 [닫기]
    public void ClosePartyListPopup()
    {
        partyListPopup.SetActive(false);
        isDispatchMode = false;
        pendingQuest = null;
        isRoomAssignMode = false;
        pendingRoomIndex = -1;
    }

    // ─── 2층 여관: 방 배정 ───────────────────────────────────────────

    // 빈 방 클릭 -> 아직 방이 없는 파티 목록에서 배정할 파티를 고름
    public void OpenRoomAssignPopup(int roomIndex)
    {
        isDispatchMode = false;
        pendingQuest = null;
        isRoomAssignMode = true;
        pendingRoomIndex = roomIndex;

        partyListPopup.SetActive(true);
        partyListPopup.transform.SetAsLastSibling();
        RefreshPartyList();

        bool hasCandidate = GameManager.Instance.partyList.Exists(p => p.roomIndex < 0);
        ShowNotification(hasCandidate
            ? $"{roomIndex + 1}번 방에 배정할 파티를 선택하세요."
            : $"{roomIndex + 1}번 방에 배정할 수 있는 파티가 없습니다. [파티 생성]으로 새 파티를 만드세요.");
    }

    // 파티가 묵고 있는 방 클릭 -> 파티를 관리할지, 방을 비울지 고르는 선택창
    public void OpenRoomMenu(Party party)
    {
        if (party == null) return;
        if (roomMenuPopup == null)
        {
            OpenPartyDetail(party); // 선택창이 없으면 바로 파티 상세로
            return;
        }

        roomMenuParty = party;
        if (roomMenuTitleText != null)
            roomMenuTitleText.text = $"{party.roomIndex + 1}번 방 - {party.partyName}";

        HideObjectLabel();
        roomMenuPopup.SetActive(true);
        roomMenuPopup.transform.SetAsLastSibling();
    }

    public void CloseRoomMenu()
    {
        if (roomMenuPopup != null) roomMenuPopup.SetActive(false);
        roomMenuParty = null;
    }

    // 선택창의 [파티 관리] 버튼
    public void OnClickRoomMenuDetail()
    {
        Party party = roomMenuParty;
        CloseRoomMenu();
        OpenPartyDetail(party);
    }

    // 선택창의 [방 비우기] 버튼
    public void OnClickRoomMenuVacate()
    {
        Party party = roomMenuParty;
        CloseRoomMenu();
        if (party == null) return;

        int roomNumber = party.roomIndex + 1;
        GameManager.Instance.VacateRoom(party);
        ShowNotification($"'{party.partyName}' 파티가 {roomNumber}번 방을 비웠습니다.");
    }


    void RefreshMemberList()
    {
        // 1. 기존에 떠있던 슬롯들 싹 지우기 (초기화)
        foreach (Transform child in listContent)
        {
            Destroy(child.gameObject);
        }

        // 2. GameManager에 있는 모험가 수만큼 반복
        foreach (Adventurer member in GameManager.Instance.adventurers)
        {
            // 3. 슬롯 생성 (Instantiate)
            GameObject newSlot = Instantiate(slotPrefab, listContent);

           // 텍스트 컴포넌트들을 가져옴
            TextMeshProUGUI[] texts = newSlot.GetComponentsInChildren<TextMeshProUGUI>();
            
            // 텍스트 개수에 맞춰 데이터 할당
            // (순서: 이름, 직업, 등급, 성격) <- 프리팹 상의 순서(Hierarchy 위에서 아래)에 따라 다릅니다!
            // 안전하게 하려면 나중에 별도 스크립트를 파는 게 좋지만, 지금은 순서대로 넣습니다.
            
            // 접수처 레벨에 따른 정보 가림은 고용 전(접수처 화면)에만 적용. 고용한 모험가는 모두 공개
            if (texts.Length >= 4)
            {
                texts[0].text = member.name + MoraleBadge(member);
                texts[1].text = DisplayNames.JobToKorean(member.job);
                texts[2].text = member.rank.ToString();
                texts[3].text = member.GetTraitName();
            }
            else if (texts.Length >= 3)
            {
                texts[0].text = $"{member.name} ({member.GetTraitName()}){MoraleBadge(member)}";
                texts[1].text = DisplayNames.JobToKorean(member.job);
                texts[2].text = member.rank.ToString();
            }

            Button slotButton = newSlot.GetComponent<Button>();
            
            if (slotButton != null)
            {
                // 클릭 이벤트 연결 (람다식 사용)
                // "이 버튼을 누르면 -> OpenDetailPopup(member)를 실행해라"
                slotButton.onClick.AddListener(() => OpenDetailPopup(member));
            }
        }
    }

    // --- 1. 슬롯 클릭 시 실행될 함수 (팝업 열기) ---
    public void OpenDetailPopup(Adventurer adventurer)
    {
        currentTarget = adventurer; // 누구를 클릭했는지 기억
        GameManager gm = GameManager.Instance;

        // ── 가운데: 모험가 모습 ──
        if (detailPortrait != null)
        {
            MonsterSpriteSet look = AdventurerSprites.ForUI(adventurer);
            UISpriteAnimator anim = detailPortrait.GetComponent<UISpriteAnimator>();
            if (anim == null) anim = detailPortrait.gameObject.AddComponent<UISpriteAnimator>();
            detailPortrait.enabled = look != null;
            if (look != null)
            {
                detailPortrait.color = Color.white;
                detailPortrait.preserveAspect = false;
                anim.spriteSet = look;
                anim.uiPerPixel = detailPortraitHeight / Mathf.Max(1f, look.bodyHeight * AdventurerSprites.PxPerUnit);
                anim.Show(MonsterSpriteSet.Anim.Idle, MonsterSpriteSet.Dir.Front, 0f);
            }
        }

        // ── 왼쪽: 이름 · 직업 · 능력치 ──
        detailName.text = adventurer.name;
        detailInfo.text = $"{adventurer.GetJobWithPersonality()}\n<size=80%><color=#E8C870>{adventurer.rank} 랭크</color>  Lv.{adventurer.level}" +
                          $"  <color=#A89C88>EXP {adventurer.exp} / {adventurer.expToNextLevel}</color></size>"; // 성격이 직업 앞 수식어로 (예: 겁쟁이 궁수)

        float morale = adventurer.morale;
        string hp, atk;
        if (morale != 0f)
        {
            // 사기가 떨어져 있으면 다음 파견 때 실제로 쓰일 능력치를 보여 주고, 원래 값은 취소선으로
            int maxEff = MoraleRules.Apply(adventurer.hp, morale);
            int curEff = adventurer.hp > 0 ? Mathf.RoundToInt(maxEff * (float)adventurer.currentHp / adventurer.hp) : maxEff;
            hp = $"<color={MoraleColorOf(morale)}>{curEff} / {maxEff}{MoraleArrow(morale)}</color> <size=75%><color={OriginalStatColor}><s>{adventurer.currentHp} / {adventurer.hp}</s></color></size>";
            atk = MoraleStat(adventurer.atk, morale);
        }
        else
        {
            hp = $"{adventurer.currentHp} / {adventurer.hp}";
            atk = adventurer.atk.ToString();
        }
        CombatStatSettings cs = gm != null ? gm.combatStats : null;
        // 장비가 더해 주는 값은 초록 "+N"으로 (전투에서는 기본 + 장비에 사기가 곱해짐)
        int eqHp = gm != null ? gm.EquipHp(adventurer) : 0, eqAtk = gm != null ? gm.EquipAtk(adventurer) : 0, eqDef = gm != null ? gm.EquipDef(adventurer) : 0;
        detailStats.text =
            $"{StatLabel("체력")}{hp}{EquipBonus(eqHp)}\n" +
            $"{StatLabel("공격력")}{atk}{EquipBonus(eqAtk)}\n" +
            $"{StatLabel("방어력")}{adventurer.def}{EquipBonus(eqDef)}\n" +
            $"{StatLabel("치명타")}{CombatStatRules.CritChance(cs, adventurer.job) * 100f:0}%\n" +
            $"{StatLabel("회피")}{CombatStatRules.Evasion(cs, adventurer.job) * 100f:0}%\n" +
            $"{StatLabel("잠재력")}{adventurer.potential}\n" +
            $"{StatLabel("무기")}{EquipName(adventurer.weapon)}\n" +
            $"{StatLabel("방어구")}{EquipName(adventurer.armor)}";
        EnsureEquipmentButton();

        // ── 오른쪽: 상태(파티·무드·사기) · 성격과 특성 · 관계 ──
        string status;
        if (adventurer.isStranded) status = $"<color=orange>던전에 낙오 (구출 기한 {adventurer.strandedWeeksRemaining}주)</color>";
        else if (adventurer.recoveryWeeks > 0) status = $"<color=#FF7366>요양 중 (완치까지 {adventurer.recoveryWeeks}주)</color>";
        else if (adventurer.assignedPartyIndex == -1) status = "<color=#7CFC7C>대기 중</color>";
        else
        {
            string partyName = gm != null && adventurer.assignedPartyIndex < gm.partyList.Count ? gm.partyList[adventurer.assignedPartyIndex].partyName : null;
            status = $"<color=#F2CD6A>{(string.IsNullOrEmpty(partyName) ? $"파티 {adventurer.assignedPartyIndex + 1}" : partyName)}</color> 소속";
        }
        string text = $"{StatLabel("파티")}{status}\n{StatLabel("무드")}<color={MoodColor(adventurer.mood)}>{RelationshipRules.MoodName(gm.moodSettings, adventurer.mood)}</color>";
        if (morale != 0f)
            text += morale < 0f
                ? $"\n{StatLabel("사기")}<color={MoraleLowColor}>↓ {MoraleRules.Percent(morale)}%</color> <size=80%>(주점에서 매주 회복)</size>"
                : $"\n{StatLabel("사기")}<color={MoraleHighColor}>↑ {MoraleRules.Percent(morale)}%</color> <size=80%>(매주 조금씩 식음)</size>";
        detailParty.text = text;

        // 고용한 모험가는 접수처 레벨과 상관없이 성격/특성/잠재력을 모두 보여줌 (가림은 고용 전에만)
        // 성격·특성·트라우마·결함·관계 이름에 마우스를 올리면 설명 툴팁
        TooltipTrigger oldTooltip = detailTrait.GetComponent<TooltipTrigger>();
        if (oldTooltip != null) Destroy(oldTooltip);
        TextLinkTooltip traitTooltip = TextLinkTooltip.Attach(detailTrait);
        traitTooltip.Tips.Clear();
        string traitText = $"{StatLabel("성격")}{TraitTooltipFormat.PersonalityLink(adventurer, traitTooltip.Tips)}";
        string sections = TraitTooltipFormat.TraitSections(adventurer, gm.GetTraitCategory, t => gm.GetTraitNote(adventurer, t), traitTooltip.Tips, "\n");
        if (sections.Length > 0) traitText += "\n" + sections;
        detailTrait.text = traitText;

        TextMeshProUGUI relationTarget = detailRelations != null ? detailRelations : detailTrait;
        TextLinkTooltip relationTooltip = relationTarget == detailTrait ? traitTooltip : TextLinkTooltip.Attach(relationTarget);
        if (relationTarget != detailTrait) relationTooltip.Tips.Clear();
        string relationText = RelationLinks(adventurer, relationTooltip.Tips, "\n");
        if (relationTarget == detailTrait)
        {
            if (relationText.Length > 0) detailTrait.text += "\n관계: " + relationText;
        }
        else relationTarget.text = relationText.Length > 0 ? relationText : "<color=#A89C88>아직 특별한 관계가 없습니다</color>";

        // 팝업 켜기
        detailPopup.SetActive(true);
        detailPopup.transform.SetAsLastSibling(); // 맨 앞으로 가져오기
    }

    static string EquipBonus(int v) => v > 0 ? $" <color=#7CFC7C>+{v}</color>" : "";

    static string EquipName(EquipmentItem e)
    {
        GameManager gm = GameManager.Instance;
        return EquipmentRules.IsEmpty(e) ? "<color=#A89C88>없음</color>" : EquipmentRules.Name(gm != null ? gm.equipment : null, e);
    }

    // 신상 창 아래 가운데의 [장비] 버튼 (해고 버튼을 복제해 모양을 맞춤, 처음 한 번만 만듦)
    void EnsureEquipmentButton()
    {
        if (detailPopup == null) return;
        Transform window = detailPopup.transform.Find("Window");
        if (window == null || window.Find("Btn_Equipment") != null) return;
        Transform fire = window.Find("Btn_Fire");
        if (fire == null) return;
        GameObject go = Instantiate(fire.gameObject, window);
        go.name = "Btn_Equipment";
        Button b = go.GetComponent<Button>();
        b.onClick = new Button.ButtonClickedEvent(); // 해고 연결(씬에 저장된 호출)을 끊음
        b.onClick.AddListener(() =>
        {
            if (currentTarget == null) return;
            TMP_FontAsset f = detailName != null ? detailName.font : null;
            EquipmentPanel.Open(currentTarget, f, () =>
            {
                if (goldText != null && GameManager.Instance != null) goldText.text = $"{GameManager.Instance.gold:N0}"; // 구입·강화·판매로 바뀐 골드
                if (currentTarget != null && detailPopup.activeSelf) OpenDetailPopup(currentTarget);
            });
        });
        TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = "장비";
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, ((RectTransform)fire).anchoredPosition.y);
    }

    // "체력  " 같은 칸 이름 (흐린 금색, 폭을 맞춤)
    static string StatLabel(string label) => $"<color=#C8A860>{label}</color>  ";

    // 관계 목록 (이름에 마우스를 올리면 관계 설명)
    static string RelationLinks(Adventurer adv, Dictionary<string, string> tips, string separator = ", ")
    {
        GameManager gm = GameManager.Instance;
        var parts = new List<string>();
        int i = 0;
        foreach (var pair in gm.GetRelationsOf(adv))
        {
            string id = "relation" + i++;
            string name = RelationshipRules.GetName(pair.Value);
            tips[id] = $"<b>{name} · {pair.Key.name}</b>\n{RelationshipRules.GetDescription(pair.Value, gm.relationship)}";
            string color = RelationshipRules.IsPositive(pair.Value) ? "#FF9EC7" : "#C0A0FF";
            parts.Add($"<link=\"{id}\"><u><color={color}>{name}</color> {pair.Key.name}</u></link>");
        }
        return string.Join(separator, parts);
    }

    static string MoodColor(float mood) => mood >= 0.15f ? "#7CFC7C" : mood <= -0.15f ? "#FF7366" : "#DDDDDD";

    // "치명 10% | 회피 10%" (직업별 기본값, 특성 보정 전)
    static string CritEvasionText(Adventurer adv)
    {
        CombatStatSettings s = GameManager.Instance != null ? GameManager.Instance.combatStats : null;
        return $"치명 {CombatStatRules.CritChance(s, adv.job) * 100f:0}% | 회피 {CombatStatRules.Evasion(s, adv.job) * 100f:0}%";
    }

    // --- 2. [해고하기] 버튼 기능 ---
    public void OnClickFire()
    {
        if (currentTarget == null) return;

        // 파티에 속해있으면 해고 불가 (안전장치)
        if (currentTarget.assignedPartyIndex != -1)
        {
            Debug.Log("파티 중인 멤버는 해고할 수 없습니다!");
            return;
        }

        // 명단에서 삭제
        GameManager.Instance.ReturnEquipmentToStorage(currentTarget); // 해고해도 장비는 길드에 남음
        GameManager.Instance.adventurers.Remove(currentTarget);
        Debug.Log($"{currentTarget.name} 해고 완료.");

        // UI 갱신 (리스트 다시 그리기)
        RefreshMemberList();

        // 팝업 닫기
        CloseDetailPopup();
    }

    // --- 3. [닫기] 버튼 기능 ---
    public void CloseDetailPopup()
    {
        detailPopup.SetActive(false);
        currentTarget = null;

        HideTraitTooltip();
    }

    public void OnClickquestPopup()
    {
        questPopup.SetActive(true);
    }

    // ─── 퀘스트 보드: 직접 지휘 / 동시 파견 선택 ──────────────────────────

    // 퀘스트 보드 오브젝트 클릭 -> 어떻게 파견할지 고르는 선택창
    public void OnClickQuestBoardMenu()
    {
        if (questBoardMenuPopup == null)
        {
            OnClickquestPopup(); // 선택창이 없으면 예전처럼 의뢰 목록으로
            return;
        }
        HideObjectLabel();
        questBoardMenuPopup.SetActive(true);
        questBoardMenuPopup.transform.SetAsLastSibling();
    }

    public void CloseQuestBoardMenu()
    {
        if (questBoardMenuPopup != null) questBoardMenuPopup.SetActive(false);
    }

    // 선택창의 [의뢰 수행] 버튼: 의뢰 하나를 골라 던전에서 직접 지휘
    public void OnClickMenuDirectQuest()
    {
        CloseQuestBoardMenu();
        OnClickquestPopup();
    }

    // 선택창의 [동시 파견] 버튼: 여러 파티를 보내고 결과만 확인
    public void OnClickMenuMultiDispatch()
    {
        CloseQuestBoardMenu();
        if (multiDispatchPanel == null)
        {
            Debug.LogWarning("multiDispatchPanel이 연결되지 않았습니다. Tools > 동시 파견 UI 세팅을 실행하세요.");
            return;
        }
        multiDispatchPanel.Open();
    }

    public void OnClickOpenSpiritStonePopup()
    {
        if (spiritStonePopup == null) return;
        spiritStonePopup.gameObject.SetActive(true);
        spiritStonePopup.gameObject.transform.SetAsLastSibling();
        spiritStonePopup.Refresh();
    }

    public void ShowTraitTooltip()// 툴팁 켜기 함수
    {
        if (currentTarget == null) return; //현재 선택된 모험가가 없다면 무시

        tooltipText.text = currentTarget.GetTraitDescription(); // 텍스트 설정

        tooltipPanel.SetActive(true);
    }

    public void HideTraitTooltip()// 툴팁 끄기 함수
    {
        tooltipPanel.SetActive(false);
    }

     // - 로비 캐릭터 관리 -

    // 로비 내의 캐릭터들을 기록하는 출석부
    private Dictionary<Adventurer, GameObject> spawnedChibiMap = new Dictionary<Adventurer, GameObject>();

    public void UpdateLobbyCharacters()//로비에 캐릭터 업데이트
    {
        // 1. [제거 로직] 데이터에는 없는데 로비에만 있는 유령 캐릭터 삭제 (해고/사망 시 처리)
        // (Dictionary를 돌면서 삭제하기 위해 리스트로 키만 먼저 복사함)
        List<Adventurer> toRemove = new List<Adventurer>();

        foreach (var pair in spawnedChibiMap)
        {
            // 게임매니저 명단에 이 모험가가 없거나, 던전에 낙오해 길드에 없으면?
            if (!GameManager.Instance.adventurers.Contains(pair.Key) || pair.Key.isStranded)
            {
                Destroy(pair.Value); // 로비에서 삭제
                toRemove.Add(pair.Key); // 출석부 지울 명단에 추가
            }
        }
        // 출석부에서 진짜 지우기
        foreach (var key in toRemove) spawnedChibiMap.Remove(key);


        // 2. [생성 로직] 데이터에는 있는데 로비엔 없는 신입 캐릭터 소환
        foreach (Adventurer adv in GameManager.Instance.adventurers)
        {
            // 이미 출석부에 있으면? -> 패스! (하던 산책 계속 하세요)
            if (spawnedChibiMap.ContainsKey(adv))
                continue;

            // 낙오자는 던전에 남아 있으므로 로비에 나타나지 않음 (구출되면 다시 소환)
            if (adv.isStranded) continue;

            // 없다면? -> 새로 소환!
            GameObject chibi = Instantiate(lobbyChibiPrefab, lobbyFloor);
            if (!LobbyChibiLook.Apply(chibi, adv, chibiHeight))                     // 이 모험가의 조립 그림 + 걷기 애니메이션
                LobbyChibiLook.Apply(chibi, GetChibiLookPrefab(adv.job), chibiHeight); // (그림이 없으면) 직업별 프리팹 겉모습
            
            // 위치 랜덤 조정 (안 그러면 다 정중앙에서 겹쳐서 태어남)
            RectTransform rect = chibi.GetComponent<RectTransform>();
            if (LobbyMapGenerator.Instance != null
                && LobbyMapGenerator.Instance.TryGetRandomFloorAnchoredPosition(rect, out Vector2 floorPos))
            {
                // 타일맵 바닥 위에서 태어나게 함
                rect.anchoredPosition = floorPos;
            }
            else
            {
                float rangeX = lobbyFloor.rect.width / 2f - 50f;
                float rangeY = lobbyFloor.rect.height / 2f - 50f;
                rect.anchoredPosition = new Vector2(Random.Range(-rangeX, rangeX), Random.Range(-rangeY, rangeY));
            }

            // 출석부에 등록
            spawnedChibiMap.Add(adv, chibi);
        }

        ShowPendingMoodEmotes();
    }

    // 지난번 표시 뒤 무드가 오르거나 내린 모험가 옆에 말풍선 (:) / :()
    void ShowPendingMoodEmotes()
    {
        float threshold = GameManager.Instance.moodSettings.emoteThreshold;
        foreach (var pair in spawnedChibiMap)
        {
            Adventurer adv = pair.Key;
            if (pair.Value == null || Mathf.Abs(adv.moodShownDelta) < threshold) continue;
            MoodEmote.ShowUI(pair.Value.GetComponent<RectTransform>(), adv.moodShownDelta > 0f, 3f);
            adv.moodShownDelta = 0f;
        }
    }

    // 퀘스트 보드에서 호출할 함수 (파티 선택)
    public void OpenDispatchPartyPopup(QuestData quest)
    {
        pendingQuest = quest; // 퀘스트 기억하기
        isDispatchMode = true; // 파티 선택 중
        riskConfirmParty = null;
        isRoomAssignMode = false;
        pendingRoomIndex = -1;

        //파티 목록 팝업 열기
        partyListPopup.SetActive(true);
        partyListPopup.transform.SetAsLastSibling();

        RefreshPartyList(); //목록 갱신
    }

    // 슬롯 텍스트 채우는 도우미 함수 (부활!)
    // ─── 사기 표시: 하락은 푸른 보라 + ↓, 고조는 주황 + ↑ (부상=빨강, 소속=노랑, 대기=초록과 겹치지 않게) ───
    const string MoraleLowColor  = "#8FA6FF";
    const string MoraleHighColor = "#FFB347";
    const string OriginalStatColor = "#8C8C8C";

    static string MoraleColorOf(float morale) => morale < 0f ? MoraleLowColor : MoraleHighColor;
    static string MoraleArrow(float morale)   => morale < 0f ? "↓" : "↑";

    // 이름 옆에 붙는 작은 표시: "↓사기 15%" / "↑사기 10%" (사기가 0이면 빈 문자열)
    string MoraleBadge(Adventurer adv)
    {
        if (adv == null || adv.morale == 0f) return "";
        return $" <size=80%><color={MoraleColorOf(adv.morale)}>{MoraleArrow(adv.morale)}사기 {MoraleRules.Percent(adv.morale)}%</color></size>";
    }

    // 사기가 적용된 능력치: 적용된 값은 사기 색 + 화살표, 원래 값은 옆에 작은 회색 취소선
    string MoraleStat(int baseValue, float morale)
    {
        if (morale == 0f) return baseValue.ToString();
        return $"<color={MoraleColorOf(morale)}>{MoraleRules.Apply(baseValue, morale)}{MoraleArrow(morale)}</color> <size=75%><color={OriginalStatColor}><s>{baseValue}</s></color></size>";
    }

    void SetupSlotBasic(GameObject slot, Adventurer member)
    {
        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 3)
        {
            // ★ [추가] 부상 중이면 이름 옆에 빨간색으로 몇 주 남았는지 표시!
            if (member.recoveryWeeks > 0)
            {
                texts[0].text = $"<color=red>{member.name} (부상:{member.recoveryWeeks}주)</color>{MoraleBadge(member)}";
            }
            else
            {
                texts[0].text = member.name + MoraleBadge(member); // 건강하면 이름 (사기가 떨어졌으면 옆에 표시)
            }
            
            texts[1].text = DisplayNames.JobToKorean(member.job);
            texts[2].text = member.rank.ToString();
        }
    }

    // ─── 시설 업그레이드 버튼 (Inspector에서 각 버튼 onClick에 연결) ──────────
    public void OnClickUpgradeInn()       => TryUpgrade("inn");
    public void OnClickUpgradePub()       => TryUpgrade("pub");
    public void OnClickUpgradeTemple()    => TryUpgrade("temple");
    public void OnClickUpgradeReception() => TryUpgrade("reception");
    public void OnClickUpgradeQuestBoard()=> TryUpgrade("questboard");

    // ─── 길드 밖 (건축 공방 / 신전) ──────────────────────────

    // 길드 문 클릭 -> 길드 밖으로 나가 갈 곳을 고르는 화면
    public void OnClickGuildDoor()
    {
        if (townView == null)
        {
            Debug.LogWarning("townView가 연결되지 않았습니다. Tools > 길드 밖 화면 세팅을 실행하세요.");
            return;
        }
        HideObjectLabel();
        townView.SetActive(true);
        townView.transform.SetAsLastSibling();
    }

    // 길드 밖 화면의 [길드로 돌아가기] 버튼
    public void CloseTown()
    {
        if (townView != null) townView.SetActive(false);
    }

    // 길드 밖 화면의 [상점] -> 전리품을 골드로 파는 창
    public void OnClickShop()
    {
        if (shopPanel == null)
        {
            Debug.LogWarning("shopPanel이 연결되지 않았습니다. Tools > 상점 세팅을 실행하세요.");
            return;
        }
        HideObjectLabel();
        shopPanel.Open();
    }

    // 길드 밖 화면의 [신전]
    public void OnClickTemple()
    {
        if (spiritStonePopup == null)
        {
            ShowNotification("신전은 아직 준비 중입니다.");
            return;
        }
        OnClickOpenSpiritStonePopup();
    }

    // 길드 밖 화면의 [치유소] -> 결함 치료 창
    public void OnClickClinic()
    {
        if (clinicPanel == null)
        {
            ShowNotification("치유소는 아직 준비 중입니다.");
            return;
        }
        HideObjectLabel();
        clinicPanel.Open();
    }

    // 1층 주점 오브젝트 클릭 -> 현재 효과 안내 (업그레이드는 건축 공방에서)
    public void OnClickPub()
    {
        int level = GameManager.Instance.GetFacilityDisplayLevel("pub");
        ShowNotification($"주점 Lv.{level}\n{GetFacilityEffect("pub", level)}\n(업그레이드는 길드 밖 건축 공방에서)");
    }

    // 길드 밖 화면의 [건축 공방] -> 모든 시설의 레벨/효과/비용을 보고 업그레이드하는 창
    public void OnClickFacilityUpgrade()
    {
        if (facilityUpgradePanel == null)
        {
            Debug.LogWarning("facilityUpgradePanel이 연결되지 않았습니다. Tools > 시설 업그레이드 UI 세팅을 실행하세요.");
            return;
        }
        HideObjectLabel();
        facilityUpgradePanel.Open();
    }

    // 시설 업그레이드 시도. 결과는 알림으로 표시하고 성공 여부를 반환
    public bool TryUpgrade(string facilityType)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return false;

        string facilityName = GetFacilityName(facilityType);

        if (gm.GetFacilityLevel(facilityType) >= gm.GetFacilityMaxLevel(facilityType))
        {
            ShowNotification($"{facilityName}: 이미 최대 레벨입니다!");
            return false;
        }

        int cost = gm.GetUpgradeCost(facilityType);
        if (gm.gold < cost)
        {
            ShowNotification($"{facilityName} 업그레이드 비용 부족! (필요: {cost:N0}G)");
            return false;
        }

        if (!gm.UpgradeFacility(facilityType)) return false;

        int newLevel = gm.GetFacilityDisplayLevel(facilityType);
        ShowNotification($"{facilityName} Lv.{newLevel} 업그레이드 완료! (-{cost:N0}G)\n효과: {GetFacilityEffect(facilityType, newLevel)}");
        RefreshUI();
        return true;
    }

    public string GetFacilityName(string facilityType)
    {
        switch (facilityType)
        {
            case "reception":  return "접수처";
            case "inn":        return "여관";
            case "pub":        return "주점";
            case "temple":     return "신전";
            case "questboard": return "퀘스트 보드";
            default: return facilityType;
        }
    }

    // 해당 레벨(화면 표시 레벨 기준)에서의 시설 효과 설명
    public string GetFacilityEffect(string facilityType, int level)
    {
        GameManager gm = GameManager.Instance;
        switch (facilityType)
        {
            case "reception":
                string visitorInfo = $"방문 모험가 {RecruitmentRules.GetVisitorCount(gm.recruitment, level)}명";
                if (level >= 3) return $"{visitorInfo}, 성격/특성과 잠재력 확인";
                if (level == 2) return $"{visitorInfo}, 성격/특성 확인";
                return visitorInfo;
            case "inn":
                return $"방 {gm.GetMaxParties(level)}개 (수용 파티 수), 최대 모험가 {gm.GetMaxAdventurers(level)}명";
            case "pub":
                return level <= 0 ? "없음" : $"길드 모험가 사기 하락 매주 {level * 5}% 회복";
            case "temple":
                return level <= 1 ? "특성 치유 (할인 없음)" : $"특성 치유 비용 {(level - 1) * 10}% 할인";
            case "questboard":
                return $"동시 파견 {level + 1}팀";
            default: return "";
        }
    }

    // 2. ★ [결산 확인] 버튼을 눌렀을 때 실행될 함수 추가!
    public void OnClickConfirmBattleResult()
    {
        // 1. 결과창 닫기
        if (resultPopup != null) resultPopup.SetActive(false);

        // 2. 비로소 시간이 1주 흐릅니다! (정산 페이즈 종료 -> 다음 정비 페이즈 시작)
        AdvanceWeek();

        Debug.Log("✅ 결산 완료! 다음 주차 정비 페이즈를 시작합니다.");
    }

    // 한 주를 넘기고 로비 화면에 반영 (결산 확인, 한 주 보내기 버튼 공용)
    void AdvanceWeek()
    {
        int goldBefore = GameManager.Instance.gold;
        GameManager.Instance.PassWeek();

        // 깎인 월급이나 바뀐 주차, 사망/회복한 모험가를 로비에 즉시 반영
        RefreshUI();
        UpdateLobbyCharacters();

        // PassWeek에서 골드가 줄어드는 건 월급 정산뿐
        int salaryPaid = goldBefore - GameManager.Instance.gold;
        string dateText = $"{GameManager.Instance.month}월 {GameManager.Instance.week}주차가 되었습니다.";
        string message = salaryPaid > 0 ? $"{dateText}\n모험가 월급 -{salaryPaid:N0} G" : dateText;
        foreach (string name in GameManager.Instance.lastExpiredStrandedNames)
            message += $"\n<color=#FF7366>{name}이(가) 구출되지 못하고 던전에서 사망했습니다.</color>";
        ShowNotification(message);
    }

    // ─── 한 주 보내기 (파견 없이 시간만 넘기기) ──────────────────────────

    // 로비의 [한 주 보내기] 버튼 -> 확인창
    public void OnClickPassWeek()
    {
        if (IsAnyPopupOpen()) return;
        if (passWeekPopup == null)
        {
            Debug.LogWarning("passWeekPopup이 연결되지 않았습니다. Tools > 한 주 보내기 버튼 세팅을 실행하세요.");
            return;
        }

        GameManager gm = GameManager.Instance;
        if (passWeekMessageText != null)
        {
            string message = "다음 주로 넘어갑니다.";
            if (gm.visitors.Count > 0)
                message += $"\n고용하지 않은 방문 모험가 {gm.visitors.Count}명은 떠납니다.";
            if (gm.IsNextWeekPayday())
                message += $"\n<color=#FF7366>다음 주는 월급날입니다. (지출 {gm.GetTotalSalary():N0} G / 보유 {gm.gold:N0} G)</color>";
            passWeekMessageText.text = message;
        }

        HideObjectLabel();
        passWeekPopup.SetActive(true);
        passWeekPopup.transform.SetAsLastSibling();
    }

    // 확인창의 [보내기] 버튼
    public void OnClickConfirmPassWeek()
    {
        ClosePassWeekPopup();
        AdvanceWeek();
    }

    public void ClosePassWeekPopup()
    {
        if (passWeekPopup != null) passWeekPopup.SetActive(false);
    }

    // ─── 파산(게임오버) ──────────────────────────
    public void ShowGameOver()
    {
        if (gameOverPopup == null) return;
        gameOverPopup.SetActive(true);
        gameOverPopup.transform.SetAsLastSibling();
    }

    // 파산 팝업의 [타이틀로] 버튼
    public void OnClickGameOverConfirm()
    {
        GameManager.Instance.OnClickGameOverConfirm();
    }

    // ─── 알림 메시지 표시 ──────────────────────────
    private Coroutine _notificationCoroutine;
    public void ShowNotification(string message)
    {
        Debug.Log($"[알림] {message}");
        if (notificationPanel == null || notificationText == null) return;

        notificationText.text = message;
        notificationPanel.SetActive(true);
        notificationPanel.transform.SetAsLastSibling(); // 열려 있는 팝업 위에 보이도록

        if (_notificationCoroutine != null) StopCoroutine(_notificationCoroutine);
        _notificationCoroutine = StartCoroutine(HideNotificationAfterDelay(3f));
    }

    System.Collections.IEnumerator HideNotificationAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (notificationPanel != null) notificationPanel.SetActive(false);
    }

    // ─── 포인트 앤 클릭: 팝업 열림 체크 ──────────
    public bool IsAnyPopupOpen()
    {
        if (receptionView    != null && receptionView.gameObject.activeSelf) return true;
        if (adventurerMenuPopup != null && adventurerMenuPopup.activeSelf)   return true;
        if (roomMenuPopup    != null && roomMenuPopup.activeSelf)    return true;
        if (gameOverPopup    != null && gameOverPopup.activeSelf)    return true;
        if (facilityUpgradePanel != null && facilityUpgradePanel.gameObject.activeSelf) return true;
        if (shopPanel        != null && shopPanel.gameObject.activeSelf) return true;
        if (townView         != null && townView.activeSelf)         return true;
        if (passWeekPopup    != null && passWeekPopup.activeSelf)    return true;
        if (questBoardMenuPopup != null && questBoardMenuPopup.activeSelf)  return true;
        if (multiDispatchPanel  != null && multiDispatchPanel.gameObject.activeSelf) return true;
        if (resultPopup      != null && resultPopup.activeSelf)      return true;
        if (managePopup      != null && managePopup.activeSelf)      return true;
        if (detailPopup      != null && detailPopup.activeSelf)      return true;
        if (partyListPopup   != null && partyListPopup.activeSelf)   return true;
        if (inputNamePopup   != null && inputNamePopup.activeSelf)   return true;
        if (partyDetailPopup != null && partyDetailPopup.activeSelf) return true;
        if (memberSelectPopup!= null && memberSelectPopup.activeSelf)return true;
        if (questPopup       != null && questPopup.activeSelf)       return true;
        if (spiritStonePopup != null && spiritStonePopup.gameObject.activeSelf) return true;
        return false;
    }

    // ─── 포인트 앤 클릭: 오브젝트 이름 라벨 ──────────
    public void ShowObjectLabel(string label)
    {
        if (objectLabelPanel == null || objectLabelText == null) return;

        objectLabelText.text = label;
        objectLabelPanel.SetActive(true);
    }

    public void HideObjectLabel()
    {
        if (objectLabelPanel != null)
            objectLabelPanel.SetActive(false);
    }

    void Update()
    {
        // 라벨이 마우스를 따라다니도록
        if (objectLabelPanel != null && objectLabelPanel.activeSelf)
        {
            // 프로젝트가 새 Input System 전용이라 Input.mousePosition은 예외를 던짐
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null) return;

            RectTransform parent = objectLabelPanel.transform.parent as RectTransform;
            RectTransform label = objectLabelPanel.GetComponent<RectTransform>();

            Vector2 mousePos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                pointer.position.ReadValue(),
                null,
                out mousePos
            );

            // mousePos는 부모의 피벗 기준 좌표라서 anchoredPosition(앵커 기준)에 그대로 넣으면 어긋남.
            // 라벨의 왼쪽-아래 모서리 위치를 부모 로컬 좌표로 계산하고, 화면 밖으로 나가지 않게 가둔다.
            Vector2 size = label.rect.size;
            Vector2 corner = mousePos + new Vector2(15f, 20f);
            corner.x = Mathf.Clamp(corner.x, parent.rect.xMin, parent.rect.xMax - size.x);
            corner.y = Mathf.Clamp(corner.y, parent.rect.yMin, parent.rect.yMax - size.y);

            label.localPosition = corner + Vector2.Scale(size, label.pivot);
        }
    }
}