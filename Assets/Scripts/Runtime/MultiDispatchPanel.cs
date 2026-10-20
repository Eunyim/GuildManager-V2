using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동시 파견 창. 의뢰마다 보낼 파티를 하나씩 짝지은 뒤 한꺼번에 보내고, 전투 없이 결과만 확인합니다.
/// 최소 파티 수 / 최대 동시 파견 수 / 승패 계산은 GameManager가 정하고, 여기서는 선택과 표시만 담당합니다.
/// </summary>
public class MultiDispatchPanel : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public GameObject root;
        public TextMeshProUGUI infoText;    // 의뢰 이름(등급 포함) / 보상 / 권장 레벨 / 예상 승률
        public TextMeshProUGUI buttonLabel; // 배정된 파티 (없으면 "파티 선택")
    }

    [Header("의뢰 생성")]
    public QuestGenerator questGenerator;

    [Header("UI 연결")]
    public Row[] rows;                  // 줄 수 = 한 번에 보여줄 의뢰 수
    public TextMeshProUGUI statusText;
    public Button startButton;

    [Header("표시 색")]
    public Color questNameColor = new Color(1f, 0.85f, 0.4f);

    private readonly List<QuestData> quests = new List<QuestData>();
    private Party[] assigned; // assigned[i] = i번째 의뢰에 보낼 파티 (없으면 null)

    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        GenerateQuests();
        assigned = new Party[rows.Length];
        Refresh();
    }

    // [닫기] 버튼
    public void Close()
    {
        gameObject.SetActive(false);
    }

    void OnDisable()
    {
        DestroyQuests();
    }

    // ─── 의뢰 목록 ───────────────────────────────────────────────────

    // 명성에 맞는 등급의 일반 의뢰를 줄 수만큼 생성 (구출/유해 수습은 직접 지휘해야 하므로 제외)
    void GenerateQuests()
    {
        DestroyQuests();
        if (questGenerator == null || GameManager.Instance == null) return;

        // 지역이 있으면 의뢰마다 갈 수 있는(명성 레벨로 열린) 지역 중 하나를 무작위로 고름. 랭크는 그 지역의 확률대로
        var unlocked = new List<int>();
        int level = GameManager.Instance.GetReputationLevel();
        if (questGenerator.regions != null)
            for (int r = 0; r < questGenerator.regions.Count; r++)
                if (questGenerator.IsRegionUnlocked(r, level)) unlocked.Add(r);

        for (int i = 0; i < rows.Length; i++)
        {
            QuestData quest = unlocked.Count > 0
                ? questGenerator.GenerateInRegion(unlocked[Random.Range(0, unlocked.Count)], QuestType.BossHunt) // 결과만 계산하므로 보스 토벌만
                : questGenerator.Generate(questGenerator.DetermineQuestRank(GameManager.Instance.reputation));
            if (quest != null) quests.Add(quest);
        }
    }

    // 이 창이 만든 의뢰 인스턴스 정리
    void DestroyQuests()
    {
        foreach (QuestData quest in quests)
        {
            if (quest == null) continue;
            if (quest.stages != null)
                foreach (StageData stage in quest.stages)
                    if (stage != null) Destroy(stage);
            Destroy(quest);
        }
        quests.Clear();
    }

    // ─── 파티 배정 ───────────────────────────────────────────────────

    // 의뢰 줄의 파티 버튼: 누를 때마다 다음 파견 가능 파티 -> ... -> 선택 해제 순으로 바뀜
    public void OnClickRowParty(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= quests.Count) return;

        GameManager gm = GameManager.Instance;
        Party current = assigned[rowIndex];

        // 새로 파티를 배정하려는데 이미 한도만큼 골랐으면 막음
        if (current == null && CountAssigned() >= gm.GetMaxDispatchSlots())
        {
            LobbyManager.Instance.ShowNotification($"동시 파견 한도 초과! (최대 {gm.GetMaxDispatchSlots()}팀) 퀘스트 보드를 업그레이드하세요.");
            return;
        }

        // 이 줄에서 고를 수 있는 파티: 파견 가능하고 다른 의뢰에 배정되지 않은 파티
        List<Party> candidates = new List<Party>();
        foreach (Party party in gm.partyList)
            if (gm.CanDispatch(party) && (party == current || !IsAssignedElsewhere(party, rowIndex)))
                candidates.Add(party);

        if (candidates.Count == 0)
        {
            LobbyManager.Instance.ShowNotification("보낼 수 있는 파티가 없습니다. (여관 방 배정과 멤버 편성이 필요합니다)");
            return;
        }

        // 현재 파티의 다음 후보로, 마지막 후보 다음은 선택 해제
        int next = current == null ? 0 : candidates.IndexOf(current) + 1;
        assigned[rowIndex] = next < candidates.Count ? candidates[next] : null;

        Refresh();
    }

    bool IsAssignedElsewhere(Party party, int exceptRow)
    {
        for (int i = 0; i < assigned.Length; i++)
            if (i != exceptRow && assigned[i] == party) return true;
        return false;
    }

    int CountAssigned()
    {
        int count = 0;
        foreach (Party party in assigned)
            if (party != null) count++;
        return count;
    }

    // ─── 출발 ────────────────────────────────────────────────────────

    // [출발] 버튼: 배정된 파티들을 한꺼번에 보내고 결과창으로
    public void OnClickStart()
    {
        GameManager gm = GameManager.Instance;
        if (CountAssigned() < gm.minMultiDispatchParties)
        {
            LobbyManager.Instance.ShowNotification($"동시 파견은 최소 {gm.minMultiDispatchParties}개 파티를 선택해야 합니다.");
            return;
        }

        List<Party> parties = new List<Party>();
        List<QuestData> targets = new List<QuestData>();
        for (int i = 0; i < quests.Count; i++)
        {
            if (assigned[i] == null) continue;
            parties.Add(assigned[i]);
            targets.Add(quests[i]);
        }

        gm.RunMultiDispatch(parties, targets);

        Close(); // 의뢰 인스턴스는 결과 계산이 끝났으므로 여기서 정리됨
        LobbyManager.Instance.RefreshUI(); // 예약된 결과창 표시
        LobbyManager.Instance.UpdateLobbyCharacters();
    }

    // ─── 표시 ────────────────────────────────────────────────────────

    void Refresh()
    {
        GameManager gm = GameManager.Instance;
        string nameHex = ColorUtility.ToHtmlStringRGB(questNameColor);

        for (int i = 0; i < rows.Length; i++)
        {
            Row row = rows[i];
            bool hasQuest = i < quests.Count;
            if (row.root != null) row.root.SetActive(hasQuest);
            if (!hasQuest) continue;

            QuestData quest = quests[i];
            Party party = assigned[i];

            if (row.infoText != null)
            {
                int autoReward = Mathf.RoundToInt(quest.rewardGold * gm.autoResolve.rewardMultiplier); // 직접 지휘보다 적게 받음
                string detail = $"보상 {autoReward:N0} G <size=80%><color=#B8A890>(직접 지휘의 {gm.autoResolve.rewardMultiplier * 100f:F0}%, 전리품 없음)</color></size>  |  권장 Lv.{quest.recommendedLevel}";
                if (party != null)
                    detail += $"  |  예상 승률 {gm.GetAutoResolveWinChance(party, quest) * 100f:F0}%";
                row.infoText.text = $"<color=#{nameHex}>{quest.questName}</color>\n{detail}";
            }

            if (row.buttonLabel != null)
                row.buttonLabel.text = party != null
                    ? $"{party.partyName} (파티 Lv.{GameManager.Instance.GetPartyLevel(party)})\n평균 Lv.{party.GetAverageMemberLevel()} / {party.members.Count}명"
                    : "파티 선택";
        }

        int assignedCount = CountAssigned();
        int minParties = gm.minMultiDispatchParties;

        if (statusText != null)
        {
            int available = 0;
            foreach (Party party in gm.partyList)
                if (gm.CanDispatch(party)) available++;

            statusText.text = available < minParties
                ? $"<color=#FF7366>보낼 수 있는 파티가 {available}팀뿐입니다. 최소 {minParties}팀이 필요합니다.</color>\n(파티에 멤버를 넣고 2층 여관에서 방을 배정하세요)"
                : $"선택한 파티: {assignedCount}팀  (최소 {minParties}팀 / 최대 {gm.GetMaxDispatchSlots()}팀)";
        }

        if (startButton != null) startButton.interactable = assignedCount >= minParties;
    }
}
