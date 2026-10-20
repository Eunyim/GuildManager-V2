using System;
using System.Collections.Generic;

// 다쳐서 돌아온 모험가가 트라우마·결함을 얻는 상황
public enum AfflictionCause
{
    Severe,    // 중상으로 귀환 (체력 30% 이하)
    NearDeath, // 빈사로 귀환 (체력 10% 이하)
    Stranded   // 던전에 낙오
}

// 트라우마·결함 획득 확률 (GameManager 인스펙터에서 조정)
[Serializable]
public class AfflictionSettings
{
    [UnityEngine.Range(0f, 1f)] public float severeChance    = 0.3f; // 중상
    [UnityEngine.Range(0f, 1f)] public float nearDeathChance = 0.5f; // 빈사
    [UnityEngine.Range(0f, 1f)] public float strandedChance  = 0.8f; // 낙오
}

// 트라우마·결함 획득 규칙 (순수 로직)
public static class AfflictionRules
{
    public static float Chance(AfflictionSettings s, AfflictionCause cause)
    {
        if (s == null) return 0f;
        switch (cause)
        {
            case AfflictionCause.NearDeath: return s.nearDeathChance;
            case AfflictionCause.Stranded:  return s.strandedChance;
            default:                        return s.severeChance;
        }
    }

    // 트라우마·결함 후보(이미 가진 것은 뺀 목록) 중 하나를 고름. 얻지 않으면 null
    //  mentalMultiplier: 정신적 트라우마가 걸릴 확률 배율 (태평한 0.7). 트라우마가 뽑혔을 때 이 확률로만 실제로 걸림
    //  roll1·roll2·roll3: 0~1 난수 (획득 판정 / 후보 고르기 / 트라우마 경감 판정)
    public static T Pick<T>(IList<T> candidates, Func<T, bool> isMental, float chance, float mentalMultiplier,
                            float roll1, float roll2, float roll3) where T : class
    {
        if (candidates == null || candidates.Count == 0 || roll1 >= chance) return null;
        int index = Math.Min((int)(roll2 * candidates.Count), candidates.Count - 1);
        T picked = candidates[index];
        if (isMental(picked) && roll3 >= mentalMultiplier) return null;
        return picked;
    }
}
