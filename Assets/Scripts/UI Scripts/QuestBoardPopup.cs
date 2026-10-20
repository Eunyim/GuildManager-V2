using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// 의뢰 게시판
//  1. 창을 열면 "어느 지역의 의뢰를 확인하시겠습니까?"와 함께 지역 목록 (초원·숲·지하 묘지·오크 부락·드래곤 둥지)
//  2. 지역을 누르면 그 지역에서 생긴 의뢰 목록 (구출·유해 수습은 그 지역 목록 맨 위). 맨 위 줄은 [지역 선택으로]
//  3. 의뢰를 누르면 파견할 파티 선택 (LobbyManager.OpenDispatchPartyPopup)
// 지역 선택은 가로로 늘어선 그림 카드(RegionCard: 평소 흑백, 마우스를 올리면 컬러 애니메이션 / 잠긴 지역은 쇠사슬·자물쇠)
// 의뢰 목록은 줄 프리팹(questSlotPrefab). 지역이 하나도 없으면 예전처럼 바로 의뢰 목록
public class QuestBoardPopup : MonoBehaviour
{
    [Header("퀘스트 생성기 (연결 필수)")]
    public QuestGenerator questGenerator;
    public int questSlotCount = 5; // 지역마다 표시할 일반 의뢰 수

    [Header("(레거시) 수동 퀘스트 목록 — Generator 미연결 시 사용")]
    public List<QuestData> availableQuests;

    [Header("UI 연결")]
    public GameObject questSlotPrefab;
    public Transform contentArea;
    [Tooltip("창 제목. 비워 두면 Window/Title을 찾아 씀")]
    public TextMeshProUGUI titleText;

    const string RegionPrompt = "어느 지역의 의뢰를 확인하시겠습니까?";

    // 지금 화면에 보이는 의뢰 목록 (지역 선택 화면에서는 비어 있음)
    private List<QuestData> _activeQuests = new List<QuestData>();
    public IReadOnlyList<QuestData> ActiveQuests => _activeQuests;

    // 이번에 창을 연 동안 지역마다 만든 일반 의뢰 (지역을 오가도 같은 의뢰가 보이도록)
    private readonly Dictionary<int, List<QuestData>> _regionQuests = new Dictionary<int, List<QuestData>>();
    private string _defaultTitle;

    public int SelectedRegion { get; private set; } = -1;

    bool UsesRegions => questGenerator != null && questGenerator.regions != null && questGenerator.regions.Count > 0;

    void OnEnable()
    {
        if (titleText == null)
        {
            Transform t = transform.Find("Window/Title");
            if (t != null) titleText = t.GetComponent<TextMeshProUGUI>();
        }
        if (titleText != null && _defaultTitle == null) _defaultTitle = titleText.text;

        CleanupQuests();
        CreateUrgentQuests();
        if (UsesRegions) ShowRegionSelect();
        else ShowLegacyBoard();
    }

    // 창이 닫힐 때 생성된 의뢰 인스턴스 정리 (파견 선택된 것만 보존)
    void OnDisable()
    {
        CleanupQuests();
    }

    // [새로고침] 버튼용: 보고 있던 지역의 의뢰를 새로 만듦
    public void OnClickRefresh()
    {
        if (!UsesRegions) { ShowLegacyBoard(); return; }
        if (SelectedRegion >= 0)
        {
            DestroyRegionQuests(SelectedRegion);
            SelectRegion(SelectedRegion);
        }
        else ShowRegionSelect();
    }

    // ─── 1. 지역 선택 ────────────────────────────────────────────────

    public void ShowRegionSelect()
    {
        SelectedRegion = -1;
        _activeQuests.Clear();
        SetTitle(RegionPrompt);
        ClearSlots();
        if (ShowRegionCards()) return;

        int level = ReputationLevel();
        for (int i = 0; i < questGenerator.regions.Count; i++)
        {
            DungeonRegion region = questGenerator.regions[i];
            if (region == null) continue;

            int captured = i;
            if (!questGenerator.IsRegionUnlocked(i, level))
            {
                // 잠긴 지역: 회색으로, 누르면 필요한 명성 레벨 안내
                int required = region.requiredReputationLevel;
                AddSlot($"<color=grey>{region.name}</color>",
                        $"<color=grey>{RegionRules.RangeText(region.minRank, region.maxRank)}</color>",
                        $"<color=grey>명성 Lv.{required} 필요</color>",
                        $"<color=grey>{MonsterList(region)}</color>",
                        () => ShowLockedNotice(captured));
                continue;
            }

            int urgent = UrgentQuestsIn(region.name).Count;
            AddSlot(region.name,
                    RegionRules.RangeText(region.minRank, region.maxRank),
                    urgent > 0 ? $"<color=red>! 긴급 {urgent}건</color>" : "",
                    MonsterList(region),
                    () => SelectRegion(captured));
        }
    }

    int ReputationLevel() => GameManager.Instance != null ? GameManager.Instance.GetReputationLevel() : 1;

    void ShowLockedNotice(int regionIndex)
    {
        DungeonRegion region = questGenerator.GetRegion(regionIndex);
        if (region == null || LobbyManager.Instance == null) return;
        int need = GameManager.Instance != null
            ? ReputationRules.GetRequiredReputation(GameManager.Instance.reputationLevel, region.requiredReputationLevel) : 0;
        int now = GameManager.Instance != null ? GameManager.Instance.reputation : 0;
        LobbyManager.Instance.ShowNotification($"{region.name}은(는) 명성 Lv.{region.requiredReputationLevel}부터 갈 수 있습니다.\n(명성 {need} 필요, 지금 {now})");
    }

    // ─── 2. 지역의 의뢰 목록 ──────────────────────────────────────────

    public void SelectRegion(int regionIndex)
    {
        DungeonRegion region = questGenerator != null ? questGenerator.GetRegion(regionIndex) : null;
        if (region == null) { ShowRegionSelect(); return; }
        if (!questGenerator.IsRegionUnlocked(regionIndex, ReputationLevel())) { ShowLockedNotice(regionIndex); return; }

        SelectedRegion = regionIndex;
        if (!_regionQuests.ContainsKey(regionIndex))
        {
            var list = new List<QuestData>();
            for (int i = 0; i < questSlotCount; i++)
            {
                QuestData quest = questGenerator.GenerateInRegion(regionIndex);
                if (quest != null) list.Add(quest);
            }
            _regionQuests[regionIndex] = list;
        }

        _activeQuests.Clear();
        _activeQuests.AddRange(UrgentQuestsIn(region.name)); // 구출·유해 수습이 맨 위
        _activeQuests.AddRange(_regionQuests[regionIndex]);

        SetTitle($"{region.name}의 의뢰 ({RegionRules.RangeText(region.minRank, region.maxRank)})");
        ShowListMode();
        ClearSlots();
        AddSlot("← 지역 선택으로", "", "", "", ShowRegionSelect);
        foreach (QuestData quest in _activeQuests) AddQuestSlot(quest);
    }

    // ─── 예전 방식 (지역이 없을 때) ────────────────────────────────────

    void ShowLegacyBoard()
    {
        CleanupQuests();
        CreateUrgentQuests();
        SetTitle(_defaultTitle);

        if (GameManager.Instance != null)
        {
            foreach (QuestData rq in GameManager.Instance.rescueQuests) if (rq != null) _activeQuests.Add(rq);
            foreach (QuestData cq in GameManager.Instance.corpseQuests) if (cq != null) _activeQuests.Add(cq);
        }

        if (questGenerator != null)
        {
            int rep = GameManager.Instance != null ? GameManager.Instance.reputation : 0;
            for (int i = 0; i < questSlotCount; i++)
            {
                QuestData quest = questGenerator.Generate(questGenerator.DetermineQuestRank(rep));
                if (quest != null) _activeQuests.Add(quest);
            }
        }
        else if (availableQuests != null)
        {
            _activeQuests.AddRange(availableQuests);
        }

        ShowListMode();
        ClearSlots();
        foreach (QuestData quest in _activeQuests) AddQuestSlot(quest);
    }

    // ─── 1-1. 지역 카드 ─────────────────────────────────────────────

    [Header("지역 카드 (왼쪽부터 이 순서, 지역 이름 → Resources/UI/Regions 그림 이름)")]
    public string[] regionCardOrder = { "초원", "숲", "지하 묘지", "오크 부락", "드래곤 둥지" };
    public string[] regionCardArt   = { "prairie", "forest", "crypt", "orc", "dragon" };
    public float regionCardSpacing = 20f;
    public Vector2 regionArtSize = new Vector2(288f, 360f); // 그림 144x180의 2배

    RectTransform cardRow;

    ScrollRect ListScroll => contentArea != null ? contentArea.GetComponentInParent<ScrollRect>(true) : null;

    void ShowListMode()
    {
        if (cardRow != null) cardRow.gameObject.SetActive(false);
        if (ListScroll != null) ListScroll.gameObject.SetActive(true);
    }

    // 카드로 지역 선택을 보여줌. 카드를 만들 수 없으면 false (예전 줄 목록으로)
    bool ShowRegionCards()
    {
        ScrollRect scroll = ListScroll;
        if (scroll == null || regionCardOrder == null) return false;
        Transform window = scroll.transform.parent;

        if (cardRow == null)
        {
            GameObject row = new GameObject("RegionCards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardRow = (RectTransform)row.transform;
            cardRow.SetParent(window, false);
            cardRow.anchorMin = Vector2.zero; cardRow.anchorMax = Vector2.one;
            cardRow.offsetMin = new Vector2(44f, 104f); // 아래쪽은 [닫기] 버튼 자리
            cardRow.offsetMax = new Vector2(-44f, -130f);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = regionCardSpacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }
        foreach (Transform child in cardRow) Destroy(child.gameObject);

        scroll.gameObject.SetActive(false);
        cardRow.gameObject.SetActive(true);

        int level = ReputationLevel();
        for (int k = 0; k < regionCardOrder.Length; k++)
        {
            int index = questGenerator.FindRegionIndex(regionCardOrder[k]);
            DungeonRegion region = questGenerator.GetRegion(index);
            if (region == null) continue;
            string art = regionCardArt != null && k < regionCardArt.Length ? regionCardArt[k] : "";
            AddRegionCard(region, index, art, !questGenerator.IsRegionUnlocked(index, level));
        }
        return true;
    }

    void AddRegionCard(DungeonRegion region, int index, string art, bool locked)
    {
        TMP_FontAsset font = titleText != null ? titleText.font : null;
        GameObject card = new GameObject($"Card_{art}", typeof(RectTransform), typeof(Image), typeof(Button));
        card.transform.SetParent(cardRow, false);
        Image frame = card.GetComponent<Image>();
        Sprite panel = Resources.Load<Sprite>("UI/Theme/ui_panel_small");
        if (panel != null)
        {
            frame.sprite = panel;
            frame.type = Image.Type.Sliced;
            frame.pixelsPerUnitMultiplier = 1f / 3f;
        }
        Button button = card.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (locked) button.onClick.AddListener(() => ShowLockedNotice(index));
        else button.onClick.AddListener(() => SelectRegion(index));

        // 그림 (위쪽)
        Image artImage = NewImage("Art", card.transform);
        RectTransform ar = artImage.rectTransform;
        ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 1f);
        ar.anchoredPosition = new Vector2(0f, -16f);
        ar.sizeDelta = regionArtSize;
        artImage.preserveAspect = true;
        Image lockImage = NewImage("Lock", artImage.transform);
        RectTransform lr = lockImage.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        lockImage.preserveAspect = true;
        lockImage.enabled = false;

        RegionCard rc = card.AddComponent<RegionCard>();
        rc.art = artImage;
        rc.lockOverlay = lockImage;
        rc.Setup(art, locked);

        // 글자 (아래쪽): 이름 / 등급 / 상태
        int urgent = UrgentQuestsIn(region.name).Count;
        string status = locked ? $"<color=#9a9488>명성 Lv.{region.requiredReputationLevel} 필요</color>"
                      : urgent > 0 ? $"<color=#ff6a5a>! 긴급 {urgent}건</color>" : "";
        NewText("Txt_Name", card.transform, region.name, 34f, new Color32(0xF2, 0xCD, 0x6A, 0xFF), 118f, font);
        NewText("Txt_Rank", card.transform, RegionRules.RangeText(region.minRank, region.maxRank), 26f, new Color32(0xEF, 0xE0, 0xBF, 0xFF), 78f, font);
        NewText("Txt_Status", card.transform, status, 24f, Color.white, 40f, font);
    }

    static Image NewImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    static void NewText(string name, Transform parent, string text, float size, Color color, float y, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;
        t.fontSizeMax = size;
        t.fontSizeMin = size * 0.6f;
        t.raycastTarget = false;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(12f, y - 20f);
        rt.offsetMax = new Vector2(-12f, y + 20f);
    }

    // ─── 긴급 의뢰 (구출·유해 수습) ────────────────────────────────────

    // 낙오자·전사자에게 아직 의뢰가 없으면 새로 만듦 (GameManager가 수명 관리)
    void CreateUrgentQuests()
    {
        if (GameManager.Instance == null || questGenerator == null) return;

        foreach (Adventurer adv in GameManager.Instance.adventurers)
        {
            if (!adv.isStranded) continue;
            if (GameManager.Instance.rescueQuests.Exists(q => q.rescueTargetName == adv.name)) continue;

            QuestData rq = questGenerator.GenerateRescueQuest(adv);
            if (rq != null)
            {
                GameManager.Instance.rescueQuests.Add(rq);
                Debug.Log($"🆘 [구출 퀘스트 생성] {adv.name} 구출 작전 의뢰가 게시됐습니다. ({rq.region})");
            }
        }

        foreach (Adventurer deceased in GameManager.Instance.deadAdventurers)
        {
            // 이미 유해를 수습해 영혼석을 얻은 전사자는 신전에서 부활만 기다림
            if (deceased.corpseRecovered) continue;
            if (GameManager.Instance.corpseQuests.Exists(q => q.corpseTargetName == deceased.name)) continue;

            QuestData cq = questGenerator.GenerateCorpseQuest(deceased);
            if (cq != null)
            {
                GameManager.Instance.corpseQuests.Add(cq);
                Debug.Log($"⚰️ [유해 수습 퀘스트 생성] {deceased.name} 유해 수습 의뢰가 게시됐습니다. ({cq.region})");
            }
        }
    }

    // 이 지역의 구출 → 유해 수습 의뢰 (지역 기록이 없는 의뢰는 첫 지역에 표시)
    List<QuestData> UrgentQuestsIn(string regionName)
    {
        var list = new List<QuestData>();
        if (GameManager.Instance == null) return list;
        string firstRegion = questGenerator != null && questGenerator.regions.Count > 0 && questGenerator.regions[0] != null
            ? questGenerator.regions[0].name : "";

        bool InRegion(QuestData q) =>
            q != null && (q.region == regionName || (string.IsNullOrEmpty(q.region) || questGenerator.FindRegionIndex(q.region) < 0) && regionName == firstRegion);

        foreach (QuestData rq in GameManager.Instance.rescueQuests) if (InRegion(rq)) list.Add(rq);
        foreach (QuestData cq in GameManager.Instance.corpseQuests) if (InRegion(cq)) list.Add(cq);
        return list;
    }

    // ─── 정리 ──────────────────────────────────────────────────────

    void CleanupQuests()
    {
        foreach (int region in new List<int>(_regionQuests.Keys)) DestroyRegionQuests(region);
        _regionQuests.Clear();

        // 예전 방식 목록에 있던 일반 의뢰
        foreach (QuestData q in _activeQuests) DestroyQuest(q);
        _activeQuests.Clear();
        SelectedRegion = -1;
    }

    void DestroyRegionQuests(int region)
    {
        if (!_regionQuests.TryGetValue(region, out List<QuestData> list)) return;
        foreach (QuestData q in list) DestroyQuest(q);
        _regionQuests.Remove(region);
    }

    void DestroyQuest(QuestData q)
    {
        if (q == null) return;
        QuestData selected = GameManager.Instance != null ? GameManager.Instance.currentQuest : null;
        if (q == selected) return;                      // 파견에 쓰는 의뢰는 보존
        if (q.isRescueQuest || q.isCorpseQuest) return; // GameManager가 수명 관리
        if (questGenerator == null) return;             // 수동 Asset은 파괴하지 않음
        if (availableQuests != null && availableQuests.Contains(q)) return;

        if (q.stages != null)
            foreach (StageData s in q.stages)
                if (s != null) Destroy(s);
        Destroy(q);
    }

    // ─── 줄 만들기 ─────────────────────────────────────────────────

    void ClearSlots()
    {
        foreach (Transform child in contentArea) Destroy(child.gameObject);
    }

    void SetTitle(string text)
    {
        if (titleText != null && text != null) titleText.text = text;
    }

    // 줄 프리팹의 글자 칸 4개: [0] 이름, [1] 보상/랭크 범위, [2] 표시, [3] 상세
    void AddSlot(string t0, string t1, string t2, string t3, UnityEngine.Events.UnityAction onClick)
    {
        GameObject slot = Instantiate(questSlotPrefab, contentArea);
        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0) texts[0].text = t0;
        if (texts.Length > 1) texts[1].text = t1;
        if (texts.Length > 2) texts[2].text = t2;
        if (texts.Length > 3) texts[3].text = t3;

        Button btn = slot.GetComponentInChildren<Button>();
        if (btn != null && onClick != null) btn.onClick.AddListener(onClick);
    }

    void AddQuestSlot(QuestData quest)
    {
        if (quest == null) return;
        string detail = $"[{quest.rank}] 권장 Lv.{quest.recommendedLevel}";

        if (quest.isRescueQuest)
        {
            // 구출 퀘스트: 남은 주 수를 강조 표시
            Adventurer target = GameManager.Instance?.adventurers.Find(a => a.name == quest.rescueTargetName);
            int weeksLeft     = target != null ? target.strandedWeeksRemaining : 0;
            string urgency    = weeksLeft <= 1 ? "<color=red>" : "<color=yellow>";
            AddSlot(quest.questName, $"{quest.rewardGold:N0} G", $"{urgency}! {weeksLeft}주 남음</color>", detail, () => OnClickQuest(quest));
        }
        else if (quest.isCorpseQuest)
        {
            AddSlot(quest.questName, $"{quest.rewardGold:N0} G", "<color=grey>† 유해 수습</color>", detail, () => OnClickQuest(quest));
        }
        else
        {
            // 의뢰 종류 표시: [보스 토벌] / [재료 수급] 가죽 3개 / [구출]
            string kind = $"[{QuestTypeRules.TypeName(quest.questType)}]";
            if (quest.questType == QuestType.Gather && quest.gatherItem != null) kind += $" {quest.gatherItem.itemName} {quest.gatherCount}개";
            string reward = quest.questType == QuestType.Rescue && quest.rescueDeepBonus > 0
                ? $"{quest.rewardGold:N0} G (+{quest.rescueDeepBonus:N0})" : $"{quest.rewardGold:N0} G";
            AddSlot(quest.questName, reward, kind, detail, () => OnClickQuest(quest));
        }
    }

    // 지역에 나오는 몬스터 이름 (중복 제거)
    static string MonsterList(DungeonRegion region)
    {
        var names = new List<string>();
        if (region.enemies != null)
            foreach (GameObject enemy in region.enemies)
            {
                if (enemy == null) continue;
                string n = MonsterFamilyName(enemy.name);
                if (!names.Contains(n)) names.Add(n);
            }
        return string.Join(", ", names);
    }

    // 무리 대표 프리팹은 무리 이름으로 (Enemy_Bandit → 도적단, Enemy_Goblin → 고블린 무리 등)
    static string MonsterFamilyName(string prefabName)
    {
        string lower = prefabName.ToLower();
        if (lower == "enemy_bandit") return "도적단";
        return DisplayNames.EnemyToKorean(prefabName);
    }

    void OnClickQuest(QuestData quest)
    {
        Debug.Log($"의뢰 선택: {quest.questName}");
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OpenDispatchPartyPopup(quest);
    }
}
