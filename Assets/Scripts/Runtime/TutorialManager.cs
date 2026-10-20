using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 진행. 게임이 시작되면 스스로 만들어져(씬에 놓을 필요 없음) 씬을 넘나들며 남습니다.
/// 매 프레임 게임 상태(열린 창, 파티, 던전 진행)를 보고 단계를 넘기고, 화면에서 눌러야 할 곳을 강조합니다.
/// 진행 상태는 GameManager.tutorial에 있고 저장 파일에 함께 들어갑니다. 문구는 TutorialRules(Core).
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>자동 플레이 봇·검증 도구가 켜면 튜토리얼과 도움말을 전혀 띄우지 않음 (설명 창이 전투를 멈추므로)</summary>
    public static bool Suppressed;

    const string LobbySceneName   = "LobbyScene";
    const string DungeonSceneName = "DungeonScene";

    [Tooltip("로비에 들어와서 결과창이 뜨기를 기다리는 시간(초). 이 안에 결과창이 없으면 결과 설명 단계를 넘김")]
    public float resultWaitSeconds = 1.5f;
    [Tooltip("모험가 체력이 이 비율 아래로 떨어지면 '위험' 도움말")]
    public float lowHpTipRatio = 0.3f;

    TutorialOverlay overlay;
    readonly Queue<TutorialTip> tipQueue = new Queue<TutorialTip>();
    bool pausedByTutorial;     // 던전에서 설명 창 때문에 시간을 멈췄는가
    float sceneTime;           // 지금 씬에 들어온 뒤 지난 시간 (실제 시간)
    int lastReputationLevel = -1;
    int lastRoomSeen = -1;

    // 캐시 (씬이 바뀌면 비움)
    LobbyFloorSwitcher floorSwitcher;
    QuestBoardPopup questBoard;

    TutorialProgress Progress => GameManager.Instance != null ? GameManager.Instance.tutorial : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        GameObject go = new GameObject("TutorialManager");
        DontDestroyOnLoad(go);
        go.AddComponent<TutorialManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        GameObject overlayGO = new GameObject("TutorialOverlay", typeof(RectTransform));
        overlayGO.layer = LayerMask.NameToLayer("UI");
        overlayGO.transform.SetParent(transform, false);
        overlay = overlayGO.AddComponent<TutorialOverlay>();
        overlay.Build(FindFont());
        overlay.SkipRequested += OnSkipRequested;
        overlay.HelpOpened += OpenHelp;
        overlay.TipsToggled += OnTipsToggled;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        sceneTime = 0f;
        lastRoomSeen = -1;
        floorSwitcher = null;
        questBoard = null;
        overlay.CloseDialog();
        ResumeTime();
        overlay.SetFont(FindFont());
    }

    static TMP_FontAsset FindFont()
    {
        // 씬에 있는 한글 글꼴을 그대로 씀 (도트 글꼴과 맞추기 위해)
        if (LobbyManager.Instance != null && LobbyManager.Instance.notificationText != null)
            return LobbyManager.Instance.notificationText.font;
        TextMeshProUGUI any = Object.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        if (any != null && any.font != null) return any.font;
        return TMP_Settings.defaultFontAsset;
    }

    static string SceneName => SceneManager.GetActiveScene().name;
    static bool InLobby => SceneName == LobbySceneName;
    static bool InDungeon => SceneName == DungeonSceneName;

    // ─── 매 프레임 ──────────────────────────────────────────

    void Update()
    {
        sceneTime += Time.unscaledDeltaTime;
        TutorialProgress p = Progress;

        bool playing = p != null && (InLobby || InDungeon) && !IsGameOver() && !Suppressed;
        if (Suppressed && overlay.IsDialogOpen) { overlay.CloseDialog(); ResumeTime(); }
        overlay.SetHelpButtonVisible(playing && InLobby && !overlay.IsDialogOpen);
        if (!playing)
        {
            overlay.SetObjective(null);
            overlay.SetHighlight(null);
            return;
        }

        if (overlay.IsDialogOpen || overlay.IsHelpOpen)
        {
            overlay.SetObjective(null);
            overlay.SetHighlight(DialogHighlight(p));
            return;
        }
        ResumeTime();

        if (p.active) UpdateMainTutorial(p);
        else
        {
            overlay.SetObjective(null);
            overlay.SetHighlight(null);
        }

        if (!overlay.IsDialogOpen) UpdateTips(p);
    }

    static bool IsGameOver()
    {
        GameManager gm = GameManager.Instance;
        return gm == null || gm.IsGameOver;
    }

    // ─── 기본 튜토리얼 ──────────────────────────────────────

    void UpdateMainTutorial(TutorialProgress p)
    {
        // 던전 단계인데 로비에 있으면(도주·전멸 등으로 일찍 돌아옴) 결과 단계로
        if (InLobby && TutorialRules.IsDungeonStep(p.Step)) p.Step = TutorialStep.Result;

        TutorialStepInfo info = TutorialRules.GetStep(p.Step);
        if (info == null || p.Step >= TutorialStep.Done)
        {
            p.Skip();
            return;
        }

        if (IsStepDone(p.Step))
        {
            Advance(p);
            return;
        }

        if (!IsStepReady(p.Step))
        {
            overlay.SetObjective(null);
            overlay.SetHighlight(null);
            return;
        }

        if (!p.introShown && info.intro != null && info.intro.Length > 0)
        {
            ShowDialog(info.intro, true, () =>
            {
                p.introShown = true;
                if (info.objective == null) Advance(p); // 설명만 있는 단계
            });
            return;
        }
        p.introShown = true;
        if (info.objective == null) { Advance(p); return; }

        string hint = SubHint(p.Step);
        string header = $"<size=70%><color=#FFD966>튜토리얼 {TutorialRules.StepNumber(p.Step)}/{TutorialRules.StepTotal}</color></size>\n";
        overlay.SetObjective(header + (string.IsNullOrEmpty(hint) ? info.objective : hint));
        overlay.SetHighlight(ResolveTarget(p.Step));
    }

    void Advance(TutorialProgress p)
    {
        TutorialStep next = TutorialRules.Next(p.Step);
        if (next >= TutorialStep.Done)
        {
            p.Skip();
            overlay.SetObjective(null);
            overlay.SetHighlight(null);
            return;
        }
        p.Step = next;
    }

    // 이 단계의 설명 창을 띄울 상황인가 (예: 모닥불 설명은 모닥불 화면에서)
    bool IsStepReady(TutorialStep step)
    {
        LobbyManager lm = LobbyManager.Instance;
        BattleManager bm = BattleManager.Instance;
        switch (step)
        {
            case TutorialStep.Welcome:
            case TutorialStep.VisitReception:
            case TutorialStep.MakeParty:
            case TutorialStep.AssignRoom:
            case TutorialStep.AcceptQuest:
            case TutorialStep.Town:
                return InLobby && lm != null && !IsResultOpen();
            case TutorialStep.ReceptionGuide:
                return InLobby && lm != null && lm.receptionView != null && lm.receptionView.gameObject.activeSelf;
            case TutorialStep.Battle:
            case TutorialStep.FinishDungeon:
                return InDungeon && bm != null && sceneTime > 0.5f;
            case TutorialStep.Campfire:
                return InDungeon && bm != null && bm.IsResting;
            case TutorialStep.Result:
                return InLobby && lm != null && (IsResultOpen() || sceneTime > resultWaitSeconds);
            case TutorialStep.PassWeek:
                return InLobby && lm != null;
            default:
                return false;
        }
    }

    // 이 단계의 목표를 이뤘는가
    bool IsStepDone(TutorialStep step)
    {
        GameManager gm = GameManager.Instance;
        LobbyManager lm = LobbyManager.Instance;
        BattleManager bm = BattleManager.Instance;
        switch (step)
        {
            case TutorialStep.VisitReception:
                return lm != null && lm.receptionView != null && lm.receptionView.gameObject.activeSelf;
            case TutorialStep.ReceptionGuide:
                return Progress.introShown && lm != null && (lm.receptionView == null || !lm.receptionView.gameObject.activeSelf);
            case TutorialStep.MakeParty:
                // 파티를 만들고 모험가를 넣은 뒤 파티 창들을 닫았을 때 (창을 닫기 전에 다음 설명이 끼어들지 않게)
                return FindPartyWithMembers() != null && !AnyPartyPopupOpen(lm);
            case TutorialStep.AssignRoom:
                return FindDispatchableParty() != null && (lm == null || !lm.partyListPopup.activeSelf);
            case TutorialStep.AcceptQuest:
                return InDungeon;
            case TutorialStep.Battle:
                return Progress.introShown && bm != null && bm.IsResting;
            case TutorialStep.Campfire:
                return Progress.introShown && bm != null && !bm.IsResting;
            case TutorialStep.FinishDungeon:
                return InLobby;
            case TutorialStep.Result:
                return Progress.introShown && !IsResultOpen();
            case TutorialStep.Town:
                return lm != null && lm.townView != null && lm.townView.activeSelf;
            default:
                return false;
        }
    }

    // 목표 문구를 지금 상황에 맞게 더 구체적으로 (없으면 단계의 기본 목표)
    string SubHint(TutorialStep step)
    {
        LobbyManager lm = LobbyManager.Instance;
        if (lm == null) return null;
        bool upstairs = IsUpstairs();

        switch (step)
        {
            case TutorialStep.VisitReception:
                if (upstairs) return "<b>계단</b>을 눌러 1층 길드 홀로 내려가세요.";
                return null;

            case TutorialStep.MakeParty:
                if (lm.memberSelectPopup.activeSelf) return "파티에 넣을 모험가를 눌러 고른 뒤 확인하세요. (앞줄 1~2명 + 뒷줄 2~3명)";
                if (lm.inputNamePopup.activeSelf) return "파티 이름을 적고 <b>[완료]</b>를 누르세요.";
                if (lm.partyDetailPopup.activeSelf)
                    return FindPartyWithMembers() != null ? "모험가를 넣었습니다! 창을 닫아 주세요." : "<b>[추가]</b> 버튼을 눌러 모험가를 파티에 넣으세요.";
                if (lm.partyListPopup.activeSelf)
                {
                    if (FindPartyWithMembers() != null) return "파티 편성이 끝났습니다. 창을 닫아 주세요.";
                    return GameManager.Instance.partyList.Count == 0 ? "<b>[파티 생성]</b>을 눌러 새 파티를 만드세요." : "만든 파티를 눌러 상세 창을 여세요.";
                }
                if (lm.adventurerMenuPopup != null && lm.adventurerMenuPopup.activeSelf) return "<b>[파티 편성]</b>을 누르세요.";
                if (lm.managePopup.activeSelf || lm.detailPopup.activeSelf) return "명단 창을 닫고 <b>모험가 관리 → 파티 편성</b>으로 가세요.";
                if (upstairs) return "<b>계단</b>을 눌러 1층으로 내려가세요.";
                return "1층의 <b>모험가 관리</b>를 클릭하세요.";

            case TutorialStep.AssignRoom:
                if (lm.partyListPopup.activeSelf) return "방에 묵을 파티를 누르세요.";
                if (AnyPartyPopupOpen(lm)) return "열린 창을 닫고 계단으로 2층 여관에 올라가세요.";
                if (!upstairs) return "<b>계단</b>을 눌러 2층 여관으로 올라가세요.";
                return "<b>빈 방</b>을 클릭해 파티를 배정하세요.";

            case TutorialStep.AcceptQuest:
                if (lm.partyListPopup.activeSelf) return "보낼 파티를 누르면 출발합니다. (<color=#FF7366>위험</color> 표시가 있으면 한 번 더 눌러야 출발)";
                if (lm.questPopup != null && lm.questPopup.activeSelf)
                {
                    QuestBoardPopup board = GetQuestBoard();
                    if (board != null && board.SelectedRegion < 0) return "의뢰를 받을 <b>지역</b>을 고르세요. 처음에는 첫 지역부터!";
                    return "<b>D랭크</b> 의뢰를 하나 골라 누르세요.";
                }
                if (lm.questBoardMenuPopup != null && lm.questBoardMenuPopup.activeSelf) return "<b>[의뢰 수행]</b>을 누르세요. 직접 지휘하며 던전을 진행합니다.";
                if (upstairs) return "<b>계단</b>을 눌러 1층으로 내려가세요.";
                return null;

            case TutorialStep.Town:
                if (upstairs) return "<b>계단</b>을 눌러 1층으로 내려가세요.";
                return null;
        }
        return null;
    }

    // ─── 강조할 곳 찾기 ──────────────────────────────────────

    Rect? ResolveTarget(TutorialStep step)
    {
        LobbyManager lm = LobbyManager.Instance;
        BattleManager bm = BattleManager.Instance;
        bool upstairs = IsUpstairs();

        switch (step)
        {
            case TutorialStep.VisitReception:
                if (lm == null || lm.IsAnyPopupOpen()) return null;
                return upstairs ? InteractiveRect("계단") : InteractiveRect("접수처");

            case TutorialStep.MakeParty:
                if (lm == null) return null;
                if (lm.memberSelectPopup.activeSelf || lm.inputNamePopup.activeSelf) return null;
                if (lm.partyDetailPopup.activeSelf)
                    return FindPartyWithMembers() != null ? null : ButtonRect("OnClickAddMemberButton", "추가");
                if (lm.partyListPopup.activeSelf)
                {
                    if (FindPartyWithMembers() != null) return null;
                    return GameManager.Instance.partyList.Count == 0 ? ButtonRect("OnClickCreatePartyButton", "파티 생성") : FirstChildRect(lm.partyListContent);
                }
                if (lm.adventurerMenuPopup != null && lm.adventurerMenuPopup.activeSelf) return ButtonRect("OnClickMenuParty", "파티 편성");
                if (lm.IsAnyPopupOpen()) return null;
                return upstairs ? InteractiveRect("계단") : InteractiveRect("모험가 관리");

            case TutorialStep.AssignRoom:
                if (lm == null) return null;
                if (lm.partyListPopup.activeSelf) return FirstChildRect(lm.partyListContent);
                if (lm.IsAnyPopupOpen()) return null;
                return upstairs ? InteractiveRect("빈 방") : InteractiveRect("계단");

            case TutorialStep.AcceptQuest:
                if (lm == null) return null;
                if (lm.partyListPopup.activeSelf) return FirstChildRect(lm.partyListContent);
                if (lm.questBoardMenuPopup != null && lm.questBoardMenuPopup.activeSelf) return ButtonRect("OnClickMenuDirectQuest", "의뢰 수행");
                if (lm.IsAnyPopupOpen()) return null;
                return upstairs ? InteractiveRect("계단") : InteractiveRect("퀘스트 보드");

            case TutorialStep.Campfire:
                return null; // 모닥불 창 자체가 화면 가운데에 크게 떠 있어 따로 가리키지 않음

            case TutorialStep.Town:
                if (lm == null || lm.IsAnyPopupOpen()) return null;
                return upstairs ? InteractiveRect("계단") : InteractiveRect("길드 문");
        }
        return null;
    }

    // 설명 창이 떠 있는 동안 가리킬 곳 (설명하는 대상)
    Rect? DialogHighlight(TutorialProgress p)
    {
        if (!p.active) return null;
        switch (p.Step)
        {
            case TutorialStep.Battle:   return ButtonRect("OnClickFleeButton", "도주");
            case TutorialStep.PassWeek:
                LobbyManager lm = LobbyManager.Instance;
                if (lm != null && lm.townView != null && lm.townView.activeSelf) return null; // 길드 밖 화면에 가려져 있음
                return ButtonRect("OnClickPassWeek", "한 주 보내기");
        }
        return null;
    }

    bool IsUpstairs()
    {
        if (floorSwitcher == null) floorSwitcher = Object.FindFirstObjectByType<LobbyFloorSwitcher>();
        return floorSwitcher != null && floorSwitcher.IsUpstairs;
    }

    QuestBoardPopup GetQuestBoard()
    {
        if (questBoard == null) questBoard = Object.FindFirstObjectByType<QuestBoardPopup>(FindObjectsInactive.Include);
        return questBoard;
    }

    // 로비 시설 오브젝트(포인트 앤 클릭) 중 이름에 keyword가 들어간 것의 화면 사각형
    static Rect? InteractiveRect(string keyword)
    {
        foreach (InteractiveObject obj in Object.FindObjectsByType<InteractiveObject>(FindObjectsSortMode.None))
        {
            if (!obj.isActiveAndEnabled || string.IsNullOrEmpty(obj.objectName) || !obj.objectName.Contains(keyword)) continue;
            Rect? r = ScreenRectOf(obj.gameObject);
            if (r != null) return r;
        }
        return null;
    }

    // onClick에 methodName이 연결된 버튼(없으면 글자에 labelKeyword가 들어간 버튼)의 화면 사각형
    static Rect? ButtonRect(string methodName, string labelKeyword)
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (Button b in buttons)
        {
            if (!b.isActiveAndEnabled) continue;
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == methodName) return ScreenRectOf(b.gameObject);
        }
        if (string.IsNullOrEmpty(labelKeyword)) return null;
        foreach (Button b in buttons)
        {
            if (!b.isActiveAndEnabled || (Instance != null && b.transform.IsChildOf(Instance.transform))) continue;
            TMP_Text label = b.GetComponentInChildren<TMP_Text>();
            if (label != null && label.text.Contains(labelKeyword)) return ScreenRectOf(b.gameObject);
        }
        return null;
    }

    static Rect? FirstChildRect(Transform parent)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
            if (child.gameObject.activeInHierarchy) return ScreenRectOf(child.gameObject);
        return null;
    }

    // UI(RectTransform)든 월드 그림(Renderer/Collider2D)이든 화면 픽셀 사각형으로
    static Rect? ScreenRectOf(GameObject go)
    {
        if (go == null) return null;
        Vector3[] corners = new Vector3[4];

        if (go.transform is RectTransform rt && go.GetComponentInParent<Canvas>() is Canvas canvas)
        {
            rt.GetWorldCorners(corners);
            Camera cam = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            return RectFromPoints(corners, cam, true);
        }

        Bounds bounds;
        Renderer rend = go.GetComponent<Renderer>();
        Collider2D col = go.GetComponent<Collider2D>();
        if (rend != null) bounds = rend.bounds;
        else if (col != null) bounds = col.bounds;
        else return null;

        Camera main = Camera.main;
        if (main == null) return null;
        corners[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.center.z);
        corners[1] = new Vector3(bounds.min.x, bounds.max.y, bounds.center.z);
        corners[2] = new Vector3(bounds.max.x, bounds.max.y, bounds.center.z);
        corners[3] = new Vector3(bounds.max.x, bounds.min.y, bounds.center.z);
        return RectFromPoints(corners, main, false);
    }

    static Rect? RectFromPoints(Vector3[] worldCorners, Camera cam, bool ui)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (Vector3 c in worldCorners)
        {
            Vector2 s = ui ? RectTransformUtility.WorldToScreenPoint(cam, c) : (Vector2)cam.WorldToScreenPoint(c);
            min = Vector2.Min(min, s);
            max = Vector2.Max(max, s);
        }
        if (max.x < 0f || max.y < 0f || min.x > Screen.width || min.y > Screen.height) return null; // 화면 밖
        if (max.x - min.x < 1f || max.y - min.y < 1f) return null;
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // ─── 상태 판정 도우미 ──────────────────────────────────

    static Party FindPartyWithMembers()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return null;
        foreach (Party party in gm.partyList)
            if (party.members.Count > 0) return party;
        return null;
    }

    static Party FindDispatchableParty()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return null;
        foreach (Party party in gm.partyList)
            if (party.members.Count > 0 && party.roomIndex >= 0) return party;
        return null;
    }

    static bool AnyPartyPopupOpen(LobbyManager lm)
    {
        if (lm == null) return false;
        return lm.partyListPopup.activeSelf || lm.partyDetailPopup.activeSelf || lm.memberSelectPopup.activeSelf ||
               lm.inputNamePopup.activeSelf || (lm.adventurerMenuPopup != null && lm.adventurerMenuPopup.activeSelf);
    }

    static bool IsResultOpen()
    {
        LobbyManager lm = LobbyManager.Instance;
        return lm != null && lm.resultPopup != null && lm.resultPopup.activeSelf;
    }

    // ─── 상황별 도움말 ──────────────────────────────────────

    void UpdateTips(TutorialProgress p)
    {
        if (!p.tipsEnabled) { tipQueue.Clear(); return; }

        if (InLobby) CheckLobbyTips(p);
        else if (InDungeon) CheckDungeonTips(p);

        // 기본 튜토리얼이 설명 창을 띄울 차례면 그쪽이 먼저
        if (tipQueue.Count > 0 && !overlay.IsDialogOpen)
        {
            TutorialTip tip = tipQueue.Dequeue();
            if (p.HasSeen(tip.id)) return;
            p.MarkSeen(tip.id);
            ShowDialog(tip.pages, false, null);
        }
    }

    void Offer(TutorialProgress p, string id, bool condition)
    {
        if (!condition || p.HasSeen(id)) return;
        foreach (TutorialTip queued in tipQueue) if (queued.id == id) return;
        TutorialTip tip = TutorialRules.GetTip(id);
        if (tip != null) tipQueue.Enqueue(tip);
    }

    void CheckLobbyTips(TutorialProgress p)
    {
        GameManager gm = GameManager.Instance;
        LobbyManager lm = LobbyManager.Instance;
        if (gm == null || lm == null || sceneTime < 0.5f) return;

        // 열어 본 창에 대한 설명 (기본 튜토리얼 진행 중에도 뜸)
        Offer(p, TutorialRules.TipAdventurerDetail, lm.detailPopup != null && lm.detailPopup.activeSelf);
        Offer(p, TutorialRules.TipMultiDispatch, lm.multiDispatchPanel != null && lm.multiDispatchPanel.gameObject.activeSelf);
        Offer(p, TutorialRules.TipShop,     lm.shopPanel != null && lm.shopPanel.gameObject.activeSelf);
        Offer(p, TutorialRules.TipFacility, lm.facilityUpgradePanel != null && lm.facilityUpgradePanel.gameObject.activeSelf);
        Offer(p, TutorialRules.TipTemple,   lm.spiritStonePopup != null && lm.spiritStonePopup.gameObject.activeSelf);
        Offer(p, TutorialRules.TipClinic,   lm.clinicPanel != null && lm.clinicPanel.gameObject.activeSelf);

        // 길드 상황에 대한 설명은 결과창을 닫은 뒤, 기본 튜토리얼이 결과 단계를 지났을 때만 (흐름을 끊지 않게)
        if (IsResultOpen() || (p.active && p.Step <= TutorialStep.Result)) return;

        bool injured = false, affliction = false, stranded = false, lowMorale = false;
        foreach (Adventurer adv in gm.adventurers)
        {
            if (adv.recoveryWeeks > 0) injured = true;
            if (adv.isStranded) stranded = true;
            if (adv.morale < 0f) lowMorale = true;
            foreach (TraitData t in adv.traits)
                if (t != null && t.category != TraitCategory.Positive) affliction = true;
        }
        Offer(p, TutorialRules.TipInjured, injured);
        Offer(p, TutorialRules.TipAffliction, affliction);
        Offer(p, TutorialRules.TipStranded, stranded);
        Offer(p, TutorialRules.TipDead, gm.deadAdventurers.Count > 0);
        Offer(p, TutorialRules.TipLowMorale, lowMorale);
        Offer(p, TutorialRules.TipPayday, gm.IsNextWeekPayday() && gm.adventurers.Count > 0);

        bool hasRelation = false;
        foreach (Adventurer adv in gm.adventurers)
            if (gm.GetRelationsOf(adv).Count > 0) { hasRelation = true; break; }
        Offer(p, TutorialRules.TipRelation, hasRelation);

        int repLevel = gm.GetReputationLevel();
        if (lastReputationLevel >= 0 && repLevel > lastReputationLevel) Offer(p, TutorialRules.TipReputationUp, true);
        lastReputationLevel = repLevel;
    }

    void CheckDungeonTips(TutorialProgress p)
    {
        BattleManager bm = BattleManager.Instance;
        GameManager gm = GameManager.Instance;
        if (bm == null || bm.Layout == null || sceneTime < 0.5f) return;
        // 기본 튜토리얼의 전투·모닥불 설명이 먼저
        if (p.active && p.Step <= TutorialStep.Campfire) return;

        QuestData quest = gm != null ? gm.currentQuest : null;
        if (quest != null)
        {
            Offer(p, TutorialRules.TipGatherQuest, quest.questType == QuestType.Gather);
            Offer(p, TutorialRules.TipRescueQuest, quest.questType == QuestType.Rescue);
        }

        int room = bm.currentStageIndex;
        if (room != lastRoomSeen)
        {
            lastRoomSeen = room;
            DungeonRoomType type = bm.GetRoomType(room);
            Offer(p, TutorialRules.TipBossRoom, type == DungeonRoomType.Boss);
            Offer(p, TutorialRules.TipTreasureRoom, type == DungeonRoomType.Treasure);
            Offer(p, TutorialRules.TipWaveRoom, type == DungeonRoomType.Wave);
        }

        if (bm.isBattleActive && !p.HasSeen(TutorialRules.TipLowHp))
        {
            foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Player"))
            {
                BattleUnit unit = obj.GetComponent<BattleUnit>();
                if (unit != null && !unit.isDead && unit.maxHp > 0f && unit.currentHp / unit.maxHp < lowHpTipRatio)
                {
                    Offer(p, TutorialRules.TipLowHp, true);
                    break;
                }
            }
        }
    }

    // ─── 설명 창 / 일시정지 ──────────────────────────────────

    void ShowDialog(TutorialPage[] pages, bool allowSkip, System.Action onClosed)
    {
        // 던전에서는 읽는 동안 전투를 멈춤
        if (InDungeon && Mathf.Approximately(Time.timeScale, 1f))
        {
            Time.timeScale = 0f;
            pausedByTutorial = true;
        }
        overlay.SetObjective(null);
        overlay.ShowDialog(pages, allowSkip && Progress != null && Progress.active, () =>
        {
            ResumeTime();
            onClosed?.Invoke();
        });
    }

    void ResumeTime()
    {
        if (!pausedByTutorial) return;
        pausedByTutorial = false;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
    }

    void OnSkipRequested()
    {
        TutorialProgress p = Progress;
        if (p != null) p.Skip();
        overlay.CloseDialog();
        ResumeTime();
        overlay.SetObjective(null);
        overlay.SetHighlight(null);
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.ShowNotification("튜토리얼을 건너뛰었습니다.\n왼쪽 아래 [도움말]에서 설명을 다시 볼 수 있습니다.");
    }

    void OnTipsToggled()
    {
        TutorialProgress p = Progress;
        if (p == null) return;
        p.tipsEnabled = !p.tipsEnabled;
        overlay.SetTipsToggleLabel(p.tipsEnabled);
    }

    void OpenHelp()
    {
        TutorialProgress p = Progress;
        if (p == null) return;

        var entries = new List<KeyValuePair<string, TutorialPage[]>>();
        foreach (TutorialStepInfo info in TutorialRules.StepsWithIntro())
            entries.Add(new KeyValuePair<string, TutorialPage[]>(info.intro[0].title, info.intro));
        foreach (TutorialTip tip in TutorialRules.AllTips)
            entries.Add(new KeyValuePair<string, TutorialPage[]>(tip.Title, tip.pages));
        overlay.OpenHelp(entries, p.tipsEnabled);
    }

    // ─── 테스트용 ──────────────────────────────────────────

    // 기본 튜토리얼을 처음부터 다시 (에디터 메뉴 'Tools > 튜토리얼 처음부터'에서 호출)
    public void RestartTutorial()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.tutorial = TutorialProgress.NewGame();
        tipQueue.Clear();
        overlay.CloseDialog();
        ResumeTime();
    }
}
