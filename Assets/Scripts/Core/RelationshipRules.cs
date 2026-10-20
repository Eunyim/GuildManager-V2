using System;

// 모험가 두 사람 사이의 관계 (케미와 별개). 한 쌍에 하나만. 사건으로 드물게 생기고, 수치보다 행동·무드에 관여
public enum RelationType
{
    None,
    Friend,       // 친우: 같은 파티면 무드 안정, 친우가 쓰러지면 그 적에게 분노
    Lover,        // 연인 (한 명만): 연인을 노리는 적을 먼저 공격, 떨어져 있으면 무드 하락, 잃으면 우울함 확정
    SwornSibling, // 의형제 (최대 3명 묶음): 도주 때 서로 낙오시키지 않음, 무드를 함께 느낌, 잃으면 복수심(사냥꾼 특성)
    Rival,        // 라이벌: 같은 파티면 공격력 상승, 대신 무드가 조금씩 깎임
    Feud          // 불화: 같은 파티면 케미가 쌓이지 않고 무드 하락
}

// 관계 수치 (GameManager 인스펙터에서 조정)
[Serializable]
public class RelationshipSettings
{
    [UnityEngine.Header("생기는 확률 (던전이 끝날 때 조건을 채운 한 쌍마다)")]
    [UnityEngine.Tooltip("친우: 함께 완수, 케미 관계 Lv.2 이상")]
    public float friendChance = 0.1f;
    public int friendBondLevel = 2;
    [UnityEngine.Tooltip("연인: 함께 완수, 케미 관계 Lv.3 이상, 둘 중 한 명이 중상 이하로 돌아옴 (생사를 함께함). 둘 다 연인이 없어야 함")]
    public float loverChance = 0.25f;
    public int loverBondLevel = 3;
    [UnityEngine.Tooltip("의형제: 함께 완수, 케미 관계 Lv.2 이상, 위기를 넘김 (사망자가 나왔거나 누군가 중상 이하). 한 사람당 의형제 최대 2명")]
    public float swornChance = 0.15f;
    public int swornBondLevel = 2;
    public int maxSwornSiblings = 2;
    [UnityEngine.Tooltip("라이벌: 둘 중 나르시시스트가 있으면 (둘 다면 2배)")]
    public float rivalChance = 0.1f;
    [UnityEngine.Tooltip("불화: 도주·실패로 끝난 던전 (둘 중 다혈질이 있으면 2배)")]
    public float feudChance = 0.05f;
    [UnityEngine.Tooltip("좋은 관계가 생기는 확률 배율: 외향적인 / 내향적인 / 겸손한(친우) / 무드가 좋은 둘")]
    public float extrovertMultiplier = 1.3f;
    public float introvertMultiplier = 0.7f;
    public float humbleFriendMultiplier = 1.3f;
    public float goodMoodMultiplier = 1.5f;

    [UnityEngine.Header("효과")]
    public float rivalAttackMultiplier = 1.1f;    // 라이벌이 같은 파티일 때 공격력
    public float swornStrandMultiplier = 0.5f;    // 의형제가 같은 파티에 살아 있으면 도주 때 낙오 확률 배율
    public float swornRevengeTraitChance = 0.5f;  // 의형제를 잃으면 그 적 계열 사냥꾼 특성을 얻을 확률
    public float rageDuration = 8f;               // 친우·연인·의형제가 쓰러지면 쓰러뜨린 적에게 분노 (대상 고정, 초)
}

// 무드: 길드 생활의 기분 (-1 ~ +1, 0이 보통). 사기(던전 결과 → 능력치)와 별개로 행동에 관여
[Serializable]
public class MoodSettings
{
    [UnityEngine.Header("사건별 무드 변화")]
    public float dungeonCleared = 0.1f;
    public float dungeonFailed = -0.15f;   // 도주·전멸·동시 파견 실패
    public float partyMemberDied = -0.15f; // 같은 파티원이 죽음 (관계가 있으면 아래 값이 대신)
    public float friendDied = -0.4f;       // 친우·의형제·라이벌
    public float loverDied = -0.7f;
    public float relationFormed = 0.3f;    // 친우·연인·의형제가 생김
    public float feudFormed = -0.2f;
    public float seriouslyInjured = -0.1f; // 중상 이하로 돌아옴
    public float levelUp = 0.1f;

    [UnityEngine.Header("매주")]
    public float weeklyDrift = 0.05f;           // 0 쪽으로 돌아가는 양
    public float loverTogether = 0.05f;         // 연인과 같은 파티
    public float loverApart = -0.1f;            // 연인과 다른 파티이거나 연인이 낙오 중
    public float friendTogether = 0.03f;        // 친우·의형제가 같은 파티 (한 명당)
    public float feudTogether = -0.1f;          // 불화 상대가 같은 파티
    public float rivalTogether = -0.03f;        // 라이벌이 같은 파티
    [UnityEngine.Range(0f, 1f)] public float swornShare = 0.3f; // 의형제끼리 무드가 평균 쪽으로 이만큼 닮아감

    [UnityEngine.Header("효과")]
    public float lowMood = -0.5f;             // 이 값 이하면 '우울한 기분'
    public float highMood = 0.5f;             // 이 값 이상이면 '좋은 기분'
    [UnityEngine.Range(0f, 1f)] public float lowMoodIdleChance = 0.1f; // 기분이 나쁘면 공격하려 할 때 이 확률로 멈춤 (게으른과 같은 방식)
    public float lowMoodIdleDuration = 1.5f;

    [UnityEngine.Header("표시")]
    public float emoteThreshold = 0.05f; // 이만큼 이상 바뀌면 말풍선 (:) / :()
}

// 관계·무드 규칙 (순수 로직)
public static class RelationshipRules
{
    public static string GetName(RelationType t)
    {
        switch (t)
        {
            case RelationType.Friend:       return "친우";
            case RelationType.Lover:        return "연인";
            case RelationType.SwornSibling: return "의형제";
            case RelationType.Rival:        return "라이벌";
            case RelationType.Feud:         return "불화";
            default:                        return "";
        }
    }

    public static string GetDescription(RelationType t, RelationshipSettings s)
    {
        if (s == null) s = new RelationshipSettings();
        switch (t)
        {
            case RelationType.Friend:
                return "같은 파티면 기분이 좋아진다. 친우가 쓰러지면 쓰러뜨린 적에게 분노한다.";
            case RelationType.Lover:
                return "같은 파티면 연인을 노리는 적을 먼저 공격하고 기분이 좋아진다. 떨어져 있으면 기분이 나빠지고, 연인을 잃으면 반드시 우울해진다.";
            case RelationType.SwornSibling:
                return $"같은 파티면 도주할 때 낙오 확률 -{Pct(1f - s.swornStrandMultiplier)}%, 기분을 함께 느낀다. 의형제를 잃으면 {Pct(s.swornRevengeTraitChance)}% 확률로 그 적을 노리는 사냥꾼 특성을 얻는다.";
            case RelationType.Rival:
                return $"같은 파티면 서로 지지 않으려 공격력 +{Pct(s.rivalAttackMultiplier - 1f)}%, 대신 기분이 조금씩 나빠진다.";
            case RelationType.Feud:
                return "같은 파티면 케미가 쌓이지 않고 기분이 나빠진다.";
            default:
                return "";
        }
    }

    public static bool IsPositive(RelationType t) => t == RelationType.Friend || t == RelationType.Lover || t == RelationType.SwornSibling;

    // 이미 있는 관계를 새 관계로 바꿀 수 있는가 (연인 > 의형제 > 친우. 라이벌·불화는 관계가 없을 때만)
    public static bool CanReplace(RelationType current, RelationType next)
    {
        if (current == RelationType.None) return next != RelationType.None;
        return Rank(next) > Rank(current) && IsPositive(next) && IsPositive(current);
    }

    static int Rank(RelationType t) =>
        t == RelationType.Lover ? 3 : t == RelationType.SwornSibling ? 2 : t == RelationType.Friend ? 1 : 0;

    public static float ClampMood(float mood) => Math.Max(-1f, Math.Min(1f, mood));

    // 0 쪽으로 drift만큼 돌아감
    public static float Drift(float mood, float drift)
    {
        if (mood > 0f) return Math.Max(0f, mood - drift);
        if (mood < 0f) return Math.Min(0f, mood + drift);
        return 0f;
    }

    public static string MoodName(MoodSettings s, float mood)
    {
        if (s == null) s = new MoodSettings();
        if (mood >= s.highMood) return "아주 좋음";
        if (mood >= 0.15f)      return "좋음";
        if (mood > -0.15f)      return "보통";
        if (mood > s.lowMood)   return "나쁨";
        return "아주 나쁨";
    }

    static int Pct(float ratio) => (int)Math.Round(ratio * 100f);
}
