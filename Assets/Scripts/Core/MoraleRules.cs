using System;

// 사기 수치 (GameManager 인스펙터에서 조정)
// 사기는 -50% ~ +20% 사이의 하나의 값. 음수면 하락(능력치 감소), 양수면 고조(능력치 증가). 다음 파견 때 HP·공격력에 곱해짐
[Serializable]
public class MoraleSettings
{
    public float minMorale = -0.5f; // 가장 낮은 사기 (능력치 -50%)
    public float maxMorale = 0.2f;  // 가장 높은 사기 (능력치 +20%)

    [UnityEngine.Header("던전 돌파 시 사기 상승")]
    [UnityEngine.Tooltip("끝까지 돌파하고 사망·빈사가 없을 때, 중상 인원 0명 / 1명 / 2명일 때 오르는 양. 표보다 중상이 많으면 오르지 않음")]
    public float[] victoryGainBySevere = { 0.10f, 0.06f, 0.03f };
    [UnityEngine.Tooltip("이 비율 이하로 돌아오면 빈사 (부상·낙오 판정과 같은 기준)")]
    public float nearDeathRatio = 0.1f;
    [UnityEngine.Tooltip("이 비율 이하로 돌아오면 중상 (부상 판정과 같은 기준)")]
    public float severeRatio = 0.3f;

    [UnityEngine.Header("시간 경과")]
    [UnityEngine.Tooltip("오른 사기가 매주 0을 향해 내려가는 양 (떨어진 사기는 주점이 회복)")]
    public float weeklyHighDecay = 0.05f;
}

// 사기 규칙 (순수 로직)
public static class MoraleRules
{
    const float HardMin = -0.5f, HardMax = 1f; // 설정이 없을 때의 안전 범위

    public static float Clamp(MoraleSettings s, float morale)
    {
        float min = s != null ? s.minMorale : HardMin, max = s != null ? s.maxMorale : HardMax;
        return Math.Min(Math.Max(morale, min), max);
    }

    // 능력치에 곱할 값 (사기 -15% → 0.85, +10% → 1.10)
    public static float Multiplier(float morale)
    {
        return 1f + Math.Min(Math.Max(morale, HardMin), HardMax);
    }

    // 사기가 적용된 정수 능력치 (화면 표시용)
    public static int Apply(int stat, float morale)
    {
        return (int)Math.Round(stat * Multiplier(morale), MidpointRounding.AwayFromZero);
    }

    // 화면에 보여 줄 크기 (부호 없이, 반올림)
    public static int Percent(float morale)
    {
        return (int)Math.Round(Math.Abs(Multiplier(morale) - 1f) * 100f, MidpointRounding.AwayFromZero);
    }

    // 던전 돌파 시 오르는 양: 사망·빈사가 있거나 중상이 표보다 많으면 0
    public static float VictoryGain(MoraleSettings s, int deaths, int nearDeath, int severe)
    {
        if (s == null || s.victoryGainBySevere == null || deaths > 0 || nearDeath > 0) return 0f;
        if (severe < 0 || severe >= s.victoryGainBySevere.Length) return 0f;
        return Math.Max(s.victoryGainBySevere[severe], 0f);
    }

    // 한 주가 지날 때: 오른 사기만 조금씩 식음 (0 아래로는 안 내려감)
    public static float WeeklyDecay(MoraleSettings s, float morale)
    {
        if (morale <= 0f || s == null) return morale;
        return Math.Max(0f, morale - Math.Max(s.weeklyHighDecay, 0f));
    }

    // 주점: 떨어진 사기만 회복 (0 위로는 안 올림)
    public static float PubRecover(float morale, float amount)
    {
        if (morale >= 0f) return morale;
        return Math.Min(0f, morale + Math.Max(amount, 0f));
    }
}
