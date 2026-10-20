using System;

// 동시 파견(전투 없이 결과만 계산)의 승률 수치 (GameManager 인스펙터에서 조정)
[Serializable]
public class AutoResolveSettings
{
    // 멤버 한 명의 전투력 = (공격력 + 체력 * hpWeight) * 남은 체력 비율 * 사기
    public float hpWeight = 0.1f;

    // 의뢰 랭크별 난이도 (D, C, B, A, S 순서). 파티 전투력이 이 값과 같으면 기준 승률(전투력 비 1)
    public float[] difficultyPerRank = { 75f, 150f, 240f, 390f, 590f };

    // 승률 = (파티 전투력 / 난이도) ^ steepness. 클수록 전투력이 모자란 파티의 승률이 가파르게 떨어짐
    public float steepness = 2f;

    public float minChance = 0.02f;
    public float maxChance = 0.95f;

    [UnityEngine.Header("직접 지휘와의 차별화 (손이 덜 가는 대신 덜 벌고 더 위험)")]
    [UnityEngine.Tooltip("성공 시 의뢰비·명성 배율 (직접 지휘 = 1)")]
    public float rewardMultiplier = 0.8f;
    [UnityEngine.Tooltip("성공 시 경험치 배율 (직접 지휘 = 1)")]
    public float expMultiplier = 0.7f;
    [UnityEngine.Tooltip("성공해도 멤버마다 경상을 입을 확률")]
    public float winInjuryChance = 0.2f;

    [UnityEngine.Header("실패 시 멤버마다 결과 확률 (나머지는 무사, 사기만 하락)")]
    public float failDeathChance = 0.1f;
    public float failStrandChance = 0.2f;
    [UnityEngine.Tooltip("예전 30% → 40%: 무사 확률을 40%에서 30%로 낮춤")]
    public float failInjuryChance = 0.4f;
}

// 동시 파견 승률 규칙 (순수 로직)
public static class AutoResolveRules
{
    // 멤버 한 명의 전투력. hpRatio: 남은 체력 비율(건강하면 1), moraleMultiplier: 사기를 반영한 능력치 배율(평소 1, 하락 <1, 고조 >1)
    public static float GetMemberPower(AutoResolveSettings settings, float atk, float hp, float hpRatio, float moraleMultiplier)
    {
        float hpWeight = settings != null ? settings.hpWeight : 0f;
        float ratio = Math.Min(Math.Max(hpRatio, 0f), 1f);
        float morale = Math.Min(Math.Max(moraleMultiplier, 0f), 2f); // 사기 하락(<1)과 고조(>1) 모두 반영
        return (atk + hp * hpWeight) * ratio * morale;
    }

    public static float GetDifficulty(AutoResolveSettings settings, RankType rank)
    {
        if (settings == null || settings.difficultyPerRank == null || settings.difficultyPerRank.Length == 0) return 1f;

        float[] table = settings.difficultyPerRank;
        int index = Math.Min(Math.Max((int)rank, 0), table.Length - 1);
        return Math.Max(table[index], 1f);
    }

    // 파티 전투력(멤버 전투력의 합)과 의뢰 랭크로 승률 계산
    public static float GetWinChance(AutoResolveSettings settings, float partyPower, RankType rank)
    {
        if (settings == null) return 0f;

        float ratio = Math.Max(partyPower, 0f) / GetDifficulty(settings, rank);
        float chance = (float)Math.Pow(ratio, Math.Max(settings.steepness, 0.01f));

        float min = Math.Min(settings.minChance, settings.maxChance);
        return Math.Min(Math.Max(chance, min), settings.maxChance);
    }
}
