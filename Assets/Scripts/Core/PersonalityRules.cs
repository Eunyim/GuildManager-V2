using System;
using System.Collections.Generic;

// 성격 효과 수치 (GameManager 인스펙터에서 조정)
// 성격은 스탯을 직접 바꾸지 않고 AI 행동·케미·보상 같은 비전투 요인에 관여함
[Serializable]
public class PersonalitySettings
{
    [UnityEngine.Header("겁쟁이: 중상이거나 아군이 절반 이상 죽으면 힐을 받을 때까지 아군 뒤로 숨음")]
    public float cowardHpRatio = 0.3f;

    [UnityEngine.Header("다혈질: 대상을 고를 때 이 확률로 어그로를 무시하고 가장 가까운 적에게 돌진")]
    [UnityEngine.Range(0f, 1f)] public float hotTemperedChargeChance = 0.1f;

    [UnityEngine.Header("게으른: 공격하려 할 때 이 확률로 행동을 멈춤")]
    [UnityEngine.Range(0f, 1f)] public float lazyChance = 0.1f;
    public float lazyDuration = 1.5f;

    [UnityEngine.Header("덜렁거리는: 파티가 얻는 골드 배율 (한 명마다 곱함)")]
    public float clumsyGoldMultiplier = 0.9f;

    [UnityEngine.Header("운 좋은: 희귀 드랍(기본 확률이 아래 값 이하인 아이템)의 확률 배율 (한 명마다 곱함)")]
    public float luckyRareDropMultiplier = 1.1f;
    public float rareDropMaxRate = 10f; // %

    [UnityEngine.Header("외향적인 / 내향적인: 케미 적응 속도(관계가 쌓이는 속도), 관계에서 받는 스트레스(동료를 잃었을 때 우울함 확률)")]
    public float extrovertBondGainMultiplier = 1.1f;
    public float extrovertStressMultiplier   = 1.1f;
    public float introvertBondGainMultiplier = 0.9f;
    public float introvertStressMultiplier   = 0.9f;

    [UnityEngine.Header("태평한: 정신적 트라우마(공포증·우울함)를 얻을 확률 배율")]
    public float carefreeTraumaMultiplier = 0.7f;

    [UnityEngine.Header("신중한: 보물상자가 미믹이면 이 확률로 알아챔 → 미믹이 기절한 채로 나와 먼저 칠 수 있음")]
    [UnityEngine.Range(0f, 1f)] public float carefulMimicDetectChance = 0.5f;
    public float carefulMimicStun = 2f;

    [UnityEngine.Header("나르시시스트: 막타를 칠 수 있는 적(체력이 내 공격 이 횟수 이하로 남은 적)을 먼저 노림, 사기 변동 배율")]
    public float narcissistFinishHits = 2f;
    public float narcissistMoraleMultiplier = 1.5f;

    [UnityEngine.Header("겸손한: 월급 배율, 같은 파티원이 받는 스트레스(우울함 확률) 배율 (겸손한 동료 한 명마다 곱함)")]
    public float humbleSalaryMultiplier = 0.9f;
    public float humbleTeammateStressMultiplier = 0.9f;
}

// 성격 이름·설명과 효과 계산 (순수 로직)
public static class PersonalityRules
{
    // 직업 앞에 붙는 수식어 형태 (예: "겁쟁이 궁수", "운 좋은 마법사")
    public static string GetName(TraitType type)
    {
        switch (type)
        {
            case TraitType.Normal:      return "평범한";
            case TraitType.Coward:      return "겁쟁이";
            case TraitType.HotTempered: return "다혈질";
            case TraitType.Clumsy:      return "덜렁거리는";
            case TraitType.Careful:     return "신중한";
            case TraitType.Lazy:        return "게으른";
            case TraitType.Narcissist:  return "나르시시스트";
            case TraitType.Lucky:       return "운 좋은";
            case TraitType.Altruistic:  return "이타적인";
            case TraitType.Extrovert:   return "외향적인";
            case TraitType.Humble:      return "겸손한";
            case TraitType.Carefree:    return "태평한";
            case TraitType.Introvert:   return "내향적인";
            default:                    return "알 수 없음";
        }
    }

    public static string GetDescription(TraitType type, PersonalitySettings s)
    {
        if (s == null) s = new PersonalitySettings();
        switch (type)
        {
            case TraitType.Normal:
                return "특별한 버릇이 없다.";
            case TraitType.Coward:
                return $"중상(체력 {Pct(s.cowardHpRatio)}% 이하)을 입거나 아군이 절반 이상 쓰러지면, 힐을 받을 때까지 아군의 뒤로 숨으려고 한다.";
            case TraitType.HotTempered:
                return $"적을 향해 돌진하려 한다. {Pct(s.hotTemperedChargeChance)}% 확률로 어그로를 무시하고 가장 가까운 적에게 돌진한다.";
            case TraitType.Clumsy:
                return $"파티가 얻는 골드가 {Pct(1f - s.clumsyGoldMultiplier)}% 줄어든다.";
            case TraitType.Careful:
                return $"보물상자가 미믹이면 {Pct(s.carefulMimicDetectChance)}% 확률로 미리 알아챈다. 알아챈 미믹은 {s.carefulMimicStun:0.#}초 동안 기절한 채로 나온다.";
            case TraitType.Lazy:
                return $"전투 중 {Pct(s.lazyChance)}% 확률로 {s.lazyDuration:0.#}초 동안 행동을 멈춘다.";
            case TraitType.Narcissist:
                return $"막타를 칠 수 있는 적을 먼저 노린다. 사기가 오르고 내리는 폭이 {Pct(s.narcissistMoraleMultiplier - 1f)}% 크다.";
            case TraitType.Lucky:
                return $"희귀 드랍 아이템(기본 확률 {s.rareDropMaxRate:0.#}% 이하)을 얻을 확률이 {Pct(s.luckyRareDropMultiplier - 1f)}% 늘어난다.";
            case TraitType.Altruistic:
                return "아군에게 붙은 적을 먼저 처리하려고 한다.";
            case TraitType.Extrovert:
                return $"케미 적응 속도 +{Pct(s.extrovertBondGainMultiplier - 1f)}%, 관계에서 받는 스트레스 +{Pct(s.extrovertStressMultiplier - 1f)}%.";
            case TraitType.Humble:
                return $"월급을 {Pct(1f - s.humbleSalaryMultiplier)}% 적게 받는다. 같은 파티원이 관계에서 받는 스트레스를 {Pct(1f - s.humbleTeammateStressMultiplier)}% 줄여 준다.";
            case TraitType.Carefree:
                return $"정신적 트라우마를 얻을 확률이 {Pct(1f - s.carefreeTraumaMultiplier)}% 줄어든다.";
            case TraitType.Introvert:
                return $"케미 적응 속도 -{Pct(1f - s.introvertBondGainMultiplier)}%, 관계에서 받는 스트레스 -{Pct(1f - s.introvertStressMultiplier)}%.";
            default:
                return "설명이 없습니다.";
        }
    }

    static int Pct(float ratio) => (int)Math.Round(ratio * 100f);

    // 같은 성격을 가진 인원 수만큼 배율을 곱함 (예: 덜렁거리는 2명 → 0.9 × 0.9)
    public static float StackedMultiplier(IEnumerable<TraitType> personalities, TraitType type, float multiplier)
    {
        float result = 1f;
        if (personalities == null) return result;
        foreach (TraitType p in personalities) if (p == type) result *= multiplier;
        return result;
    }

    // 관계 한 번 쌓일 때의 배율 (두 사람 성격을 곱함). 외향적인 둘 = 1.21, 외향적인 + 내향적인 = 0.99
    public static float BondGainMultiplier(PersonalitySettings s, TraitType a, TraitType b) =>
        BondGainOf(s, a) * BondGainOf(s, b);

    static float BondGainOf(PersonalitySettings s, TraitType t) =>
        t == TraitType.Extrovert ? s.extrovertBondGainMultiplier :
        t == TraitType.Introvert ? s.introvertBondGainMultiplier : 1f;

    public static float StressMultiplier(PersonalitySettings s, TraitType t) =>
        t == TraitType.Extrovert ? s.extrovertStressMultiplier :
        t == TraitType.Introvert ? s.introvertStressMultiplier : 1f;

    // 소수 배율을 정수 증가로 바꿈: 1.21 → 1 (21% 확률로 2), 0.9 → 90% 확률로 1, 아니면 0
    public static int RoundRandomly(float value, float roll)
    {
        int whole = (int)Math.Floor(value);
        return whole + (roll < value - whole ? 1 : 0);
    }
}
