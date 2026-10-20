using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건축 공방 창. 시설별 현재 레벨/효과, 다음 레벨 효과, 비용을 보여주고 업그레이드합니다.
/// 레벨/비용/효과 규칙은 GameManager와 LobbyManager가 정하고, 여기서는 표시와 버튼 입력만 담당합니다.
/// </summary>
public class FacilityUpgradePanel : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public string facilityType;          // "reception", "questboard", "inn", "temple", "pub"
        public TextMeshProUGUI infoText;     // 이름 / 레벨 / 현재 효과 / 다음 효과
        public Button button;
        public TextMeshProUGUI buttonLabel;  // "업그레이드 500 G"
    }

    public Row[] rows;
    public TextMeshProUGUI goldText;

    [Header("표시 색")]
    public Color nameColor       = new Color(1f, 0.85f, 0.4f);
    public Color affordableColor = Color.white;
    public Color tooExpensiveColor = new Color(1f, 0.45f, 0.4f);

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

    // 각 줄의 [업그레이드] 버튼 (facilityType은 버튼에 문자열 인자로 연결됨)
    public void OnClickUpgrade(string facilityType)
    {
        if (LobbyManager.Instance == null) return;

        LobbyManager.Instance.TryUpgrade(facilityType);
        Refresh();
    }

    void Refresh()
    {
        GameManager gm = GameManager.Instance;
        LobbyManager lobby = LobbyManager.Instance;
        if (gm == null || lobby == null) return;

        if (goldText != null) goldText.text = $"보유 골드: {gm.gold:N0} G";

        string nameHex = ColorUtility.ToHtmlStringRGB(nameColor);

        foreach (Row row in rows)
        {
            if (row == null || string.IsNullOrEmpty(row.facilityType)) continue;

            string type = row.facilityType;
            int level = gm.GetFacilityDisplayLevel(type);
            bool built = level > 0; // 레벨 0인 시설은 아직 짓지 않은 것으로 표시
            bool isMax = gm.GetFacilityLevel(type) >= gm.GetFacilityMaxLevel(type);
            int cost = gm.GetUpgradeCost(type);
            bool canAfford = gm.gold >= cost;

            if (row.infoText != null)
            {
                string levelText = built ? $"Lv.{level}" : "미건설";
                string nextText = isMax ? "최대 레벨입니다" : $"다음: {lobby.GetFacilityEffect(type, level + 1)}";
                row.infoText.text =
                    $"<color=#{nameHex}>{lobby.GetFacilityName(type)}</color>  {levelText}\n" +
                    $"현재: {lobby.GetFacilityEffect(type, level)}\n" +
                    nextText;
            }

            if (row.buttonLabel != null)
            {
                row.buttonLabel.text = isMax ? "최대 레벨" : $"{(built ? "업그레이드" : "건설")}\n{cost:N0} G";
                row.buttonLabel.color = (isMax || canAfford) ? affordableColor : tooExpensiveColor;
            }

            if (row.button != null) row.button.interactable = !isMax && canAfford;
        }
    }
}
