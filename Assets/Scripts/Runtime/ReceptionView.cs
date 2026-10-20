using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 접수처에서 바라보는 1인칭 고용 화면.
/// 길드를 찾아온 모험가를 한 명씩 보여주고, 카드를 오른쪽으로 끌면 고용 / 왼쪽으로 끌면 돌려보냅니다.
/// 고용 판정과 비용은 GameManager가 처리하고, 여기서는 표시와 입력만 담당합니다.
/// </summary>
public class ReceptionView : MonoBehaviour
{
    [Header("모험가 카드 (드래그 대상)")]
    public SwipeCard card;
    public Image portraitImage;

    [Header("모험가 정보 (우상단)")]
    public GameObject infoPanel;
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI jobText;
    public TextMeshProUGUI statsText;
    public TextMeshProUGUI costText;

    [Header("상태 표시")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI remainingText;
    public GameObject emptyMessage;      // 방문 모험가가 더 없을 때
    public TextMeshProUGUI messageText;  // 고용 실패 사유 등
    public float messageDuration = 2f;

    [Header("드래그 방향 안내")]
    public CanvasGroup hireHint;    // 오른쪽
    public CanvasGroup rejectHint;  // 왼쪽
    [Range(0f, 1f)] public float hintIdleAlpha = 0.35f;

    private Adventurer current;
    private Coroutine messageRoutine;

    void Awake()
    {
        if (card != null)
        {
            card.Dragged  += OnCardDragged;
            card.Released += OnCardReleased;
        }
    }

    void OnDestroy()
    {
        if (card != null)
        {
            card.Dragged  -= OnCardDragged;
            card.Released -= OnCardReleased;
        }
    }

    public void Open()
    {
        if (GameManager.Instance == null) return;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (LobbyManager.Instance != null) LobbyManager.Instance.HideObjectLabel();

        if (messageText != null) messageText.gameObject.SetActive(false);
        OnCardDragged(0f);

        GameManager.Instance.EnsureVisitors();
        ShowNextVisitor();
    }

    // [로비로] 버튼
    public void Close()
    {
        gameObject.SetActive(false);
    }

    void ShowNextVisitor()
    {
        GameManager gm = GameManager.Instance;
        current = gm.visitors.Count > 0 ? gm.visitors[0] : null;

        bool hasVisitor = current != null;
        if (card != null) card.gameObject.SetActive(hasVisitor);
        if (infoPanel != null) infoPanel.SetActive(hasVisitor);
        if (emptyMessage != null) emptyMessage.SetActive(!hasVisitor);

        RefreshStatus();
        if (!hasVisitor) return;

        if (portraitImage != null)
        {
            // 이 모험가의 조립 그림 (정면). 그림이 없으면 예전처럼 직업 프리팹 그림
            Sprite look = AdventurerSprites.Portrait(current);
            SpriteRenderer model = look == null ? GetJobModel(current.job) : null;
            portraitImage.sprite = look != null ? look : (model != null ? model.sprite : null);
            portraitImage.color = look != null || model == null ? Color.white : model.color;
            portraitImage.preserveAspect = true;
            portraitImage.enabled = portraitImage.sprite != null;
        }

        if (rankText != null)
        {
            rankText.text = $"{current.rank} 랭크";
            rankText.color = GetRankColor(current.rank);
        }
        if (nameText != null) nameText.text = current.name;
        if (jobText != null) jobText.text = $"{GetJobName(current.job)}  Lv.{current.level}";
        if (statsText != null)
        {
            TextLinkTooltip tooltip = TextLinkTooltip.Attach(statsText);
            tooltip.Tips.Clear();
            statsText.text = BuildStatsText(current, gm.receptionLevel, tooltip.Tips);
        }
        if (costText != null)
        {
            int cost = gm.GetHireCost(current.rank);
            costText.text = $"고용 비용: {cost:N0} G";
            costText.color = gm.gold >= cost ? Color.white : Color.red;
        }

        if (card != null) card.PlayEnter();
    }

    void RefreshStatus()
    {
        GameManager gm = GameManager.Instance;
        if (goldText != null) goldText.text = $"{gm.gold:N0} G";
        if (remainingText != null)
            remainingText.text = $"대기 중인 모험가: {gm.visitors.Count}명   |   소속: {gm.adventurers.Count}/{gm.GetMaxAdventurers()}";
    }

    // 접수처 레벨에 따라 공개되는 정보가 달라짐 (Lv2: 성격/특성, Lv3: 잠재력)
    // 성격·특성 이름은 링크로 감싸 마우스를 올리면 설명이 나옴 (tips에 툴팁 문구를 채움)
    string BuildStatsText(Adventurer adv, int receptionLevel, Dictionary<string, string> tips)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"HP  {adv.hp}");
        sb.AppendLine($"ATK {adv.atk}");
        sb.AppendLine($"DEF {adv.def}");
        sb.AppendLine($"SPD {adv.speed:0.0}");
        CombatStatSettings combat = GameManager.Instance.combatStats;
        sb.AppendLine($"치명 {CombatStatRules.CritChance(combat, adv.job) * 100f:0}%  회피 {CombatStatRules.Evasion(combat, adv.job) * 100f:0}%");

        if (receptionLevel >= 2)
        {
            GameManager gm = GameManager.Instance;
            sb.AppendLine($"성격: {TraitTooltipFormat.PersonalityLink(adv, tips)}");

            string sections = TraitTooltipFormat.TraitSections(adv, gm.GetTraitCategory, t => gm.GetTraitNote(adv, t), tips, "\n");
            if (sections.Length > 0) sb.AppendLine(sections);
        }
        else
        {
            sb.AppendLine("성격: ???  (접수처 Lv2 필요)");
        }

        sb.Append(receptionLevel >= 3
            ? $"잠재력: {adv.potential} / 5"
            : "잠재력: ???  (접수처 Lv3 필요)");

        return sb.ToString();
    }

    // ─── 드래그 결과 처리 ────────────────────────────────────────────

    void OnCardDragged(float progress)
    {
        // 끄는 방향의 안내 문구가 점점 선명해짐
        if (hireHint != null)   hireHint.alpha   = Mathf.Lerp(hintIdleAlpha, 1f, Mathf.Clamp01(progress));
        if (rejectHint != null) rejectHint.alpha = Mathf.Lerp(hintIdleAlpha, 1f, Mathf.Clamp01(-progress));
    }

    void OnCardReleased(bool toRight)
    {
        if (current == null) { card.SnapBack(); return; }

        GameManager gm = GameManager.Instance;
        if (toRight)
        {
            if (!gm.TryHireVisitor(current, out string failMessage))
            {
                ShowMessage(failMessage);
                card.SnapBack();
                return;
            }

            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.RefreshUI();
                LobbyManager.Instance.UpdateLobbyCharacters();
            }
        }
        else
        {
            gm.DismissVisitor(current);
        }

        current = null;
        RefreshStatus();
        card.FlyOut(toRight, ShowNextVisitor);
    }

    void ShowMessage(string message)
    {
        if (messageText == null || string.IsNullOrEmpty(message)) return;

        messageText.text = message;
        messageText.gameObject.SetActive(true);

        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(HideMessageAfterDelay());
    }

    IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSecondsRealtime(messageDuration);
        messageText.gameObject.SetActive(false);
        messageRoutine = null;
    }

    // ─── 표시용 도우미 ───────────────────────────────────────────────

    // 전투에서 쓰는 직업별 유닛 프리팹의 스프라이트를 모험가 모델로 사용
    SpriteRenderer GetJobModel(JobType job)
    {
        GameManager gm = GameManager.Instance;
        if (gm.unitPrefabs == null || gm.unitPrefabs.Length == 0) return null;

        GameObject prefab = gm.GetUnitPrefab(job);
        return prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>(true) : null;
    }

    static string GetJobName(JobType job) => DisplayNames.JobToKorean(job);

    static Color GetRankColor(RankType rank)
    {
        switch (rank)
        {
            case RankType.S: return Color.yellow;
            case RankType.A: return Color.red;
            case RankType.B: return Color.cyan;
            case RankType.C: return Color.green;
            default:         return Color.white;
        }
    }
}
