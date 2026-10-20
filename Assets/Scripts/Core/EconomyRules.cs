using System;

// 길드 경제 수치 (GameManager 인스펙터에서 조정). 랭크별 배열은 D, C, B, A, S 순서
[Serializable]
public class EconomySettings
{
    [UnityEngine.Header("월급 = 랭크 기본급 + 레벨 × 레벨당 월급")]
    public int[] salaryBasePerRank = { 50, 100, 200, 350, 500 };
    public int salaryPerLevel = 10;

    [UnityEngine.Header("경험치")]
    [UnityEngine.Tooltip("의뢰 성공 시 생환자가 받는 경험치 (의뢰 랭크별)")]
    public int[] expPerQuestRank = { 40, 70, 110, 160, 210 };
    [UnityEngine.Tooltip("다음 레벨까지 필요한 경험치 = 현재 레벨 × 이 값")]
    public int expPerLevelStep = 100;

    [UnityEngine.Header("신전 비용 (모험가 랭크별)")]
    public int[] reviveCostPerRank = { 200, 300, 500, 800, 1200 };
    public int[] traumaCureCostPerRank = { 100, 200, 400, 700, 1200 };

    [UnityEngine.Header("명성")]
    [UnityEngine.Tooltip("의뢰 성공 명성 = 의뢰비 × 이 값 (0.1이면 의뢰비의 10%)")]
    public float reputationPerRewardGold = 0.1f;
    [UnityEngine.Tooltip("결과만 계산하는 파견이 실패했을 때 명성 변화")]
    public int autoDispatchFailReputation = -10;

    [UnityEngine.Header("여관 수용 인원 = 기본 + (여관 레벨 - 1) × 레벨당 증가")]
    public int baseMaxAdventurers = 8;
    public int maxAdventurersPerInnLevel = 4;

    [UnityEngine.Header("시설 최대 레벨")]
    public int receptionMaxLevel = 3;
    public int templeMaxLevel = 3;
    public int defaultFacilityMaxLevel = 5;
}

// 길드 경제 계산 (순수 로직)
public static class EconomyRules
{
    // 랭크 배열에서 값 꺼내기 (배열이 짧으면 마지막 값, 비었으면 fallback)
    public static int ByRank(int[] table, RankType rank, int fallback)
    {
        if (table == null || table.Length == 0) return fallback;
        int i = Math.Max(0, Math.Min((int)rank, table.Length - 1));
        return table[i];
    }

    public static int Salary(EconomySettings s, RankType rank, int level)
    {
        s = s ?? new EconomySettings();
        return ByRank(s.salaryBasePerRank, rank, 50) + level * s.salaryPerLevel;
    }

    // 의뢰 랭크 문자열("D"~"S")로 경험치. 모르는 값은 D랭크
    public static int QuestExp(EconomySettings s, string questRank)
    {
        s = s ?? new EconomySettings();
        RankType rank = Enum.TryParse(questRank, out RankType r) ? r : RankType.D;
        return ByRank(s.expPerQuestRank, rank, 40);
    }

    public static int ExpToNextLevel(EconomySettings s, int level) => level * (s ?? new EconomySettings()).expPerLevelStep;

    public static int ReviveCost(EconomySettings s, RankType rank) => ByRank((s ?? new EconomySettings()).reviveCostPerRank, rank, 200);

    public static int TraumaCureCost(EconomySettings s, RankType rank) => ByRank((s ?? new EconomySettings()).traumaCureCostPerRank, rank, 100);

    // 의뢰 성공 명성 (정수 나눗셈과 같도록 내림)
    public static int SuccessReputation(EconomySettings s, int rewardGold)
    {
        s = s ?? new EconomySettings();
        return (int)Math.Floor(rewardGold * s.reputationPerRewardGold + 1e-4);
    }

    public static int MaxAdventurers(EconomySettings s, int innLevel)
    {
        s = s ?? new EconomySettings();
        return s.baseMaxAdventurers + (innLevel - 1) * s.maxAdventurersPerInnLevel;
    }

    public static int FacilityMaxLevel(EconomySettings s, string facilityType)
    {
        s = s ?? new EconomySettings();
        switch (facilityType)
        {
            case "reception": return s.receptionMaxLevel;
            case "temple":    return s.templeMaxLevel;
            default:          return s.defaultFacilityMaxLevel;
        }
    }
}
