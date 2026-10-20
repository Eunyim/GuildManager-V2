// 적 계열 (사냥꾼·공포증 특성이 대상으로 삼는 묶음). 프리팹 이름으로 판별 (순수 로직)
public static class EnemyFamilyRules
{
    public const string Beast     = "괴수";     // 늑대, 슬라임
    public const string Human     = "인간";     // 도적단
    public const string Undead    = "언데드";   // 스켈레톤
    public const string Greenskin = "그린스킨"; // 고블린, 오크
    public const string Dragon    = "드래곤";   // 드래곤, 베이비 드래곤, 해츨링

    // 계열에 속하지 않으면 빈 문자열 (예: 미믹)
    public static string FromPrefabName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return "";
        string lower = prefabName.ToLower();
        if (lower.Contains("slime") || lower.Contains("wolf")) return Beast;
        if (lower.Contains("bandit"))   return Human;
        if (lower.Contains("skeleton")) return Undead;
        if (lower.Contains("goblin") || lower.Contains("orc")) return Greenskin;
        if (lower.Contains("dragon") || lower.Contains("hatchling")) return Dragon;
        return "";
    }

    // 드래곤슬레이어를 주는 '진짜 드래곤'인가 (베이비 드래곤·해츨링은 제외)
    public static bool IsTrueDragon(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return false;
        string lower = prefabName.ToLower();
        return lower.Contains("dragon") && !lower.Contains("baby") && !lower.Contains("hatchling");
    }
}
