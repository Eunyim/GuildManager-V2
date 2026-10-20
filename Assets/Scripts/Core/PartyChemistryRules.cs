using System;
using System.Collections.Generic;

// 케미 수치 (GameManager 인스펙터에서 조정)
// 케미는 파티가 아니라 모험가 두 명 사이의 '관계'에 쌓임: 같은 파티로 던전을 완수(승리 또는 모닥불 귀환)할 때마다 그 둘의 관계 +1.
// 모험가 한 명의 케미 = 지금 파티 동료들과의 관계 중 가장 높은 레벨 (신입이 와도 기존 멤버는 그대로, 신입은 관계가 쌓이는 만큼 따라 올라감)
[Serializable]
public class PartyChemistrySettings
{
    public int minMembers = 2;           // 이 인원 이상이어야 케미가 생김 (혼자인 파티는 케미 없음)
    public int completionsPerLevel = 5;  // 관계가 다음 레벨로 오르기까지 함께 완수해야 하는 횟수
    [UnityEngine.Tooltip("죽은 동료와의 관계가 이 레벨 이상이어야 영향(우울함·케미 하락)을 받음. 표시에서 '적응 중'을 가르는 기준도 같음")]
    public int countedBondLevel = 2;
    // 레벨별 이름과 HP·공격력 보너스 (Lv.1 ~ Lv.4 순서)
    public string[] levelNames  = { "그럭저럭", "약간 좋음", "좋음", "매우 좋음" };
    public float[] statBonus    = { 0.03f, 0.05f, 0.07f, 0.10f };
    // 파티원이 사망했을 때, 그와의 관계 레벨에 따라 남은 파티원이 '우울함'을 얻을 확률 (Lv.1 ~ Lv.4 순서)
    // 우울함에 걸리지 않은 사람은 대신 케미가 한 단계 떨어짐 (Lv.1이면 더 떨어지지 않음)
    public float[] depressionChance = { 0f, 0f, 0.2f, 0.3f };
    public float depressionAttackMultiplier = 0.85f; // 우울함 효과: 공격력에 곱하는 값 (0.85 = 15% 감소)
    public int depressionMonths = 2;     // 우울함이 저절로 사라지기까지 걸리는 기간 (달)
    public int depressionClearLevel = 2; // 우울한 모험가의 개인 케미가 이 레벨에 오르면 우울함이 사라짐
}

// 케미 규칙 (순수 로직)
public static class PartyChemistryRules
{
    // ─── 관계 (모험가 두 명 사이) ───

    // 함께 완수한 횟수 → 관계 레벨 (1부터. 0번이어도 Lv.1)
    public static int BondLevel(PartyChemistrySettings s, int completions)
    {
        int per = Math.Max(s != null ? s.completionsPerLevel : 5, 1);
        return Math.Min(1 + Math.Max(completions, 0) / per, MaxLevel(s));
    }

    // 관계를 한 단계 낮춘 완수 횟수 (그 단계의 시작 값). Lv.1이면 그대로
    public static int DropOneLevel(PartyChemistrySettings s, int completions)
    {
        int level = BondLevel(s, completions);
        if (level <= 1) return completions;
        int per = Math.Max(s != null ? s.completionsPerLevel : 5, 1);
        return (level - 2) * per;
    }

    // 개인 케미 레벨: 동료들과의 관계 중 가장 높은 레벨 (평균을 내지 않음 → 신입이 와도 기존 멤버는 떨어지지 않음). 동료가 없으면 Lv.1
    // partySize가 minMembers보다 작으면 0 (혼자인 파티는 케미 없음)
    public static int IndividualLevel(PartyChemistrySettings s, int partySize, IEnumerable<int> bondCompletionsWithTeammates)
    {
        if (s == null || partySize < Math.Max(s.minMembers, 1)) return 0;
        int best = 1;
        foreach (int c in bondCompletionsWithTeammates) best = Math.Max(best, BondLevel(s, c));
        return best;
    }

    // 두 모험가 이름으로 만든 관계 키 (순서 상관없이 같은 값)
    public static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";

    // ─── 레벨 → 수치 ───
    public static int MaxLevel(PartyChemistrySettings s) => s != null && s.statBonus != null && s.statBonus.Length > 0 ? s.statBonus.Length : 4;

    // 완수 횟수 → 케미 레벨 (인원이 모자라면 0 = 케미 없음)
    public static int GetLevel(PartyChemistrySettings s, int memberCount, int completions)
    {
        if (s == null || memberCount < Math.Max(s.minMembers, 1)) return 0;
        int per = Math.Max(s.completionsPerLevel, 1);
        return Math.Min(1 + Math.Max(completions, 0) / per, MaxLevel(s));
    }

    // 다음 레벨까지 남은 완수 횟수 (최대 레벨이거나 케미가 없으면 0)
    public static int CompletionsToNext(PartyChemistrySettings s, int memberCount, int completions)
    {
        int level = GetLevel(s, memberCount, completions);
        if (level <= 0 || level >= MaxLevel(s)) return 0;
        int per = Math.Max(s.completionsPerLevel, 1);
        return level * per - Math.Max(completions, 0);
    }

    // HP·공격력에 곱할 값 (케미 없으면 1)
    public static float GetStatMultiplier(PartyChemistrySettings s, int level) => 1f + PickByLevel(s?.statBonus, level, 0f);

    public static float GetDepressionChance(PartyChemistrySettings s, int level) => PickByLevel(s?.depressionChance, level, 0f);

    public static string GetLevelName(PartyChemistrySettings s, int level)
    {
        if (level <= 0) return "없음";
        string[] names = s?.levelNames;
        if (names == null || names.Length == 0) return $"Lv.{level}";
        return names[Math.Min(level, names.Length) - 1];
    }

    // 구성원 이름을 정렬해 붙인 값. 저장해 둔 값과 다르면 구성원이 바뀐 것
    public static string MakeMemberKey(IEnumerable<string> memberNames)
    {
        List<string> names = new List<string>(memberNames);
        names.Sort(StringComparer.Ordinal);
        return string.Join("|", names);
    }

    // 레벨(1부터)에 맞는 표 값. 표가 짧으면 마지막 값, 레벨 0이면 기본값
    static float PickByLevel(float[] table, int level, float fallback)
    {
        if (table == null || table.Length == 0 || level <= 0) return fallback;
        return table[Math.Min(level, table.Length) - 1];
    }
}
