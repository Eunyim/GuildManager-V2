using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 치유소 창: 결함(외팔이·절름발이·외눈박이·약골)을 가진 모험가 목록과 [치료] 버튼.
/// 치료 판정과 비용은 GameManager.CureDefect가 처리하고, 여기서는 표시와 입력만 담당합니다.
/// (Tools > 치유소 세팅으로 생성. 길드 밖 화면의 [치유소] 건물을 누르면 열림)
/// </summary>
public class ClinicPanel : MonoBehaviour
{
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI emptyText;  // "치료할 결함을 가진 모험가가 없습니다."
    public TextMeshProUGUI messageText;
    public Transform listContent;
    public GameObject slotPrefab;      // 텍스트 4줄 + 버튼 (신전 슬롯과 같은 모양)

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (messageText != null) messageText.text = "";
        Refresh();
    }

    public void Close() => gameObject.SetActive(false);

    void Refresh()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        if (goldText != null) goldText.text = $"보유 골드: {gm.gold:N0} G";

        foreach (Transform child in listContent) Destroy(child.gameObject);

        int count = 0;
        foreach (Adventurer adv in gm.adventurers)
        {
            if (adv.traits == null) continue;
            foreach (TraitData trait in adv.traits.ToArray())
            {
                if (trait == null || gm.GetTraitCategory(trait) != TraitCategory.Defect) continue;
                CreateSlot(gm, adv, trait);
                count++;
            }
        }
        if (emptyText != null) emptyText.gameObject.SetActive(count == 0);
    }

    void CreateSlot(GameManager gm, Adventurer adv, TraitData trait)
    {
        GameObject slot = Instantiate(slotPrefab, listContent);
        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
        int cost = gm.GetDefectCureCost(adv.rank);
        bool canAfford = gm.gold >= cost;

        if (texts.Length > 0) texts[0].text = adv.name;
        if (texts.Length > 1) texts[1].text = $"{adv.GetJobWithPersonality()} | Rank {adv.rank}";
        if (texts.Length > 2)
        {
            texts[2].text = $"[{trait.traitName}] {trait.description}";
            texts[2].color = new Color(1f, 0.69f, 0.38f); // 결함 색 (TraitTooltipFormat.DefectColor)
        }
        if (texts.Length > 3)
        {
            texts[3].text = $"치료 비용: {cost:N0} G";
            texts[3].color = canAfford ? Color.white : Color.red;
        }

        Button button = slot.GetComponentInChildren<Button>();
        if (button == null) return;
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = "치료";
        button.interactable = canAfford;
        button.onClick.AddListener(() => OnClickCure(adv, trait));
    }

    void OnClickCure(Adventurer adv, TraitData trait)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        gm.CureDefect(adv, trait, out string message);
        if (messageText != null) messageText.text = message;
        if (LobbyManager.Instance != null) LobbyManager.Instance.RefreshUI();
        Refresh();
    }
}
