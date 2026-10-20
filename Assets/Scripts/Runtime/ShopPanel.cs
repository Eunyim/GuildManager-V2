using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 창. 길드 창고의 전리품을 골드로 팝니다.
/// 판매가는 아이템 데이터(MonsterDropData.price)를 따르고, 실제 거래는 GameManager.SellItem이 처리합니다.
/// 여기서는 목록 표시와 버튼 입력만 담당합니다. (Tools > 상점 세팅으로 생성)
/// </summary>
public class ShopPanel : MonoBehaviour
{
    [Header("매입 품목 (여기 있는 아이템만 팔 수 있음. 영혼석 등은 제외)")]
    public List<MonsterDropData> catalog = new List<MonsterDropData>();

    [Header("UI 연결")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI emptyText;     // 팔 물건이 없을 때 안내
    public Transform listContent;
    public GameObject slotPrefab;         // 텍스트 2개(이름/수량, 가격) + 버튼 2개(1개 판매, 모두 판매)
    public Button sellEverythingButton;
    public TextMeshProUGUI sellEverythingLabel;

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    // [닫기] 버튼
    public void Close()
    {
        gameObject.SetActive(false);
    }

    // [전부 판매] 버튼: 팔 수 있는 전리품을 모두 판매
    public void OnClickSellEverything()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        int earned = 0;
        foreach (MonsterDropData item in catalog)
        {
            if (item == null) continue;
            earned += gm.SellItem(item.itemName, gm.GetItemCount(item.itemName), item.price);
        }

        if (earned > 0 && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.ShowNotification($"전리품을 모두 팔았습니다. (+{earned:N0} G)");
            LobbyManager.Instance.RefreshUI();
        }
        Refresh();
    }

    void Sell(MonsterDropData item, int count)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || item == null) return;

        if (gm.SellItem(item.itemName, count, item.price) > 0 && LobbyManager.Instance != null)
            LobbyManager.Instance.RefreshUI();
        Refresh();
    }

    void Refresh()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (goldText != null) goldText.text = $"보유 골드: {gm.gold:N0} G";

        if (listContent != null)
            foreach (Transform child in listContent) Destroy(child.gameObject);

        int totalValue = 0;
        int rowCount = 0;
        foreach (MonsterDropData item in catalog)
        {
            if (item == null) continue;

            int count = gm.GetItemCount(item.itemName);
            if (count <= 0) continue;

            totalValue += count * item.price;
            rowCount++;
            if (listContent != null && slotPrefab != null) BuildRow(item, count);
        }

        if (emptyText != null) emptyText.gameObject.SetActive(rowCount == 0);
        if (sellEverythingButton != null) sellEverythingButton.interactable = totalValue > 0;
        if (sellEverythingLabel != null)
            sellEverythingLabel.text = totalValue > 0 ? $"전부 판매 (+{totalValue:N0} G)" : "전부 판매";
    }

    void BuildRow(MonsterDropData item, int count)
    {
        GameObject slot = Instantiate(slotPrefab, listContent);

        TextMeshProUGUI[] texts = slot.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length > 0) texts[0].text = $"{item.itemName}  x{count}";
        if (texts.Length > 1) texts[1].text = $"개당 {item.price:N0} G  /  합계 {count * item.price:N0} G";

        Button[] buttons = slot.GetComponentsInChildren<Button>();
        MonsterDropData captured = item;
        if (buttons.Length > 0) buttons[0].onClick.AddListener(() => Sell(captured, 1));
        if (buttons.Length > 1) buttons[1].onClick.AddListener(() => Sell(captured, GameManager.Instance.GetItemCount(captured.itemName)));
    }
}
