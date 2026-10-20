using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// 신전 팝업: 탭 1 = 부활, 탭 2 = 특성 제거
// 사용 방법:
//   1. Hierarchy에 신전 팝업 Panel 생성 후 이 스크립트를 추가
//   2. 인스펙터에서 아래 필드를 연결
//   3. LobbyManager.spiritStonePopup에 이 컴포넌트 연결
//   4. 신전 버튼 onClick → LobbyManager.OnClickOpenSpiritStonePopup()
public class SpiritStonePopup : MonoBehaviour
{
    [Header("상단 공통 정보")]
    public TextMeshProUGUI stoneCountText;
    public TextMeshProUGUI goldText;

    [Header("탭 패널 (각각 자식 오브젝트 연결)")]
    public GameObject revivePanel;       // 부활 탭 내용 전체
    public GameObject traitRemovePanel;  // 특성 제거 탭 내용 전체

    [Header("탭 버튼 (색 변경용, 없으면 null)")]
    public Button reviveTabButton;
    public Button traitRemoveTabButton;

    // ── 부활 탭 ──────────────────────────────────────────────────────
    [Header("부활 탭")]
    public TextMeshProUGUI emptyText;    // "부활 가능한 모험가가 없습니다"
    public Transform listContent;        // Scroll View Content
    public GameObject slotPrefab;        // 슬롯 프리팹

    // ── 특성 제거 탭 ─────────────────────────────────────────────────
    [Header("특성 제거 탭")]
    public TextMeshProUGUI traitEmptyText; // "특성을 보유한 모험가가 없습니다"
    public Transform traitListContent;     // 특성 목록 Content (slotPrefab 재사용)

    // -----------------------------------------------------------

    public void Refresh()
    {
        UpdateHeader();
        BuildReviveList();
        BuildTraitRemoveList();
        ShowReviveTab(); // 열릴 때 항상 부활 탭 먼저
    }

    // ── 탭 전환 ──────────────────────────────────────────────────────

    public void ShowReviveTab()
    {
        if (revivePanel      != null) revivePanel.SetActive(true);
        if (traitRemovePanel != null) traitRemovePanel.SetActive(false);
        SetTabButtonColors(isRevive: true);
    }

    public void ShowTraitRemoveTab()
    {
        if (revivePanel      != null) revivePanel.SetActive(false);
        if (traitRemovePanel != null) traitRemovePanel.SetActive(true);
        SetTabButtonColors(isRevive: false);
        BuildTraitRemoveList(); // 탭 전환 시 최신 데이터 반영
    }

    void SetTabButtonColors(bool isRevive)
    {
        Color activeColor   = Color.white;
        Color inactiveColor = new Color(0.6f, 0.6f, 0.6f);

        if (reviveTabButton != null)
        {
            ColorBlock cb = reviveTabButton.colors;
            cb.normalColor = isRevive ? activeColor : inactiveColor;
            reviveTabButton.colors = cb;
        }
        if (traitRemoveTabButton != null)
        {
            ColorBlock cb = traitRemoveTabButton.colors;
            cb.normalColor = isRevive ? inactiveColor : activeColor;
            traitRemoveTabButton.colors = cb;
        }
    }

    // ── 공통 헤더 ────────────────────────────────────────────────────

    void UpdateHeader()
    {
        if (GameManager.Instance == null) return;
        if (stoneCountText != null) stoneCountText.text = $"보유 영혼석: {GameManager.Instance.GetSpiritStoneCount()}개";
        if (goldText       != null) goldText.text       = $"보유 골드: {GameManager.Instance.gold:N0} G";
    }

    // ── 부활 탭 ──────────────────────────────────────────────────────

    void BuildReviveList()
    {
        if (listContent == null || slotPrefab == null) return;

        foreach (Transform child in listContent) Destroy(child.gameObject);

        List<Adventurer> dead = GameManager.Instance != null
            ? GameManager.Instance.deadAdventurers
            : new List<Adventurer>();

        if (emptyText != null) emptyText.gameObject.SetActive(dead.Count == 0);

        foreach (Adventurer deceased in dead)
        {
            GameObject slot = Instantiate(slotPrefab, listContent);
            SetupReviveSlot(slot, deceased);
        }
    }

    void SetupReviveSlot(GameObject slot, Adventurer deceased)
    {
        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
        int goldCost    = GameManager.Instance.GetReviveCost(deceased.rank);
        bool hasStone   = GameManager.Instance.HasSpiritStone(deceased);
        bool canAfford  = hasStone && GameManager.Instance.gold >= goldCost;
        string stoneKey = $"{deceased.name}의 영혼석";
        string diedAt   = string.IsNullOrEmpty(deceased.diedAtQuestName)
            ? "알 수 없는 던전" : deceased.diedAtQuestName;

        if (texts.Length > 0) texts[0].text = deceased.name;
        if (texts.Length > 1) texts[1].text = $"{deceased.job} | Rank {deceased.rank} | 전사: {diedAt}";
        if (texts.Length > 2)
        {
            texts[2].text  = hasStone ? $"[{stoneKey} 보유]" : $"[{stoneKey} 없음 — 유해 수습 필요]";
            texts[2].color = hasStone ? Color.cyan : Color.grey;
        }
        if (texts.Length > 3)
        {
            texts[3].text  = $"부활 비용: {goldCost:N0} G";
            texts[3].color = canAfford ? Color.white : Color.red;
        }

        Button btn = slot.GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.interactable = canAfford;
            Adventurer captured = deceased;
            btn.onClick.AddListener(() => OnClickRevive(captured));
        }
    }

    void OnClickRevive(Adventurer deceased)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.ReviveWithSpiritStone(deceased))
        {
            if (LobbyManager.Instance != null) LobbyManager.Instance.RefreshUI();
            Refresh();
        }
    }

    // ── 특성 제거 탭 ─────────────────────────────────────────────────

    void BuildTraitRemoveList()
    {
        if (traitListContent == null || slotPrefab == null) return;

        foreach (Transform child in traitListContent) Destroy(child.gameObject);

        if (GameManager.Instance == null) return;

        // 특성(TraitData)을 1개 이상 가진 현역 모험가만 표시
        int slotCount = 0;
        foreach (Adventurer adv in GameManager.Instance.adventurers)
        {
            if (adv.traits == null || adv.traits.Count == 0) continue;

            foreach (TraitData t in new List<TraitData>(adv.traits))
            {
                if (t == null || GameManager.Instance.GetTraitCategory(t) != TraitCategory.Trauma) continue; // 신전은 트라우마만 치료 (결함은 치유소, 긍정 특성은 대상 아님)
                GameObject slot = Instantiate(slotPrefab, traitListContent);
                SetupTraitRemoveSlot(slot, adv, t);
                slotCount++;
            }
        }

        if (traitEmptyText != null) traitEmptyText.gameObject.SetActive(slotCount == 0);
    }

    void SetupTraitRemoveSlot(GameObject slot, Adventurer adv, TraitData trait)
    {
        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
        int cost       = GameManager.Instance.GetTraitRemoveCost_Public(adv.rank);
        bool canAfford = GameManager.Instance.gold >= cost;

        if (texts.Length > 0) texts[0].text = adv.name;
        if (texts.Length > 1) texts[1].text = $"{adv.job} | Rank {adv.rank}";
        if (texts.Length > 2)
        {
            texts[2].text  = $"[{trait.traitName}] {trait.description}";
            texts[2].color = Color.yellow;
        }
        if (texts.Length > 3)
        {
            texts[3].text  = $"제거 비용: {cost:N0} G";
            texts[3].color = canAfford ? Color.white : Color.red;
        }

        Button btn = slot.GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.interactable = canAfford;
            Adventurer capturedAdv   = adv;
            TraitData  capturedTrait = trait;
            btn.onClick.AddListener(() => OnClickRemoveTrait(capturedAdv, capturedTrait));
        }
    }

    void OnClickRemoveTrait(Adventurer adv, TraitData trait)
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.RemoveTrait(adv, trait))
        {
            if (LobbyManager.Instance != null) LobbyManager.Instance.RefreshUI();
            UpdateHeader();
            BuildTraitRemoveList();
        }
    }

    public void OnClickClose()
    {
        gameObject.SetActive(false);
    }
}
