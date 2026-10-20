// 화면에 보여줄 한글 이름 (순수 로직)
public static class DisplayNames
{
    public static string JobToKorean(JobType job)
    {
        switch (job)
        {
            case JobType.Warrior: return "전사";
            case JobType.Mage:    return "마법사";
            case JobType.Archer:  return "궁수";
            case JobType.Healer:  return "사제";
            case JobType.Rogue:   return "도적";
            default:              return job.ToString();
        }
    }

    // 적 프리팹(또는 소환된 오브젝트) 이름으로 적의 종류를 알아냄. 모르는 이름이면 그대로 돌려줌
    public static string EnemyToKorean(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return "";

        string lower = prefabName.ToLower();
        if (lower.Contains("kingslime")) return "킹 슬라임";
        if (lower.Contains("slime"))    return "슬라임";
        if (lower.Contains("goblinshamanking")) return "샤먼킹";
        if (lower.Contains("goblinwarrior"))  return "고블린 전사";
        if (lower.Contains("goblinhunter"))   return "고블린 사냥꾼";
        if (lower.Contains("goblinpriest"))   return "고블린 사제";
        if (lower.Contains("goblin"))   return "고블린";
        if (lower.Contains("skeletonnecromancer")) return "스켈레톤 네크로맨서";
        if (lower.Contains("skeletonwarrior")) return "스켈레톤 전사";
        if (lower.Contains("skeletonarcher"))  return "스켈레톤 궁수";
        if (lower.Contains("skeletontank"))    return "스켈레톤 탱커";
        if (lower.Contains("skeleton")) return "스켈레톤";
        if (lower.Contains("orccenturion")) return "오크 백부장";
        if (lower.Contains("orcgrunt")) return "오크 하급 전사";
        if (lower.Contains("orcrider")) return "오크 기수";
        if (lower.Contains("banditwarrior")) return "도적단 전사";
        if (lower.Contains("banditarcher"))  return "도적단 궁수";
        if (lower.Contains("bandit"))   return "도적단 암살자";
        if (lower.Contains("orc"))      return "오크";
        if (lower.Contains("dragonhatchling")) return "해츨링";
        if (lower.Contains("babydragon")) return "베이비 드래곤";
        if (lower.Contains("dragon"))   return "드래곤";
        if (lower.Contains("mimic"))    return "미믹";
        if (lower.Contains("alphawolf")) return "알파";
        if (lower.Contains("wolf"))     return "초원 늑대";
        return prefabName;
    }
}
