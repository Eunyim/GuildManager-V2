// 범위 공격의 거리별 피해 비율 (중심에서 멀수록 약해짐)
[System.Serializable]
public class SplashFalloffSettings
{
    [UnityEngine.Tooltip("중심에서 이 거리까지는 innerRatio")]
    public float innerRadius = 1f;
    public float innerRatio = 1f;
    [UnityEngine.Tooltip("innerRadius 초과 ~ 이 거리까지는 middleRatio")]
    public float middleRadius = 3f;
    public float middleRatio = 0.8f;
    [UnityEngine.Tooltip("middleRadius 초과 ~ 이 거리까지는 outerRatio, 그 밖은 피해 없음")]
    public float outerRadius = 5f;
    public float outerRatio = 0.5f;
}

public static class SplashFalloff
{
    // 중심에서 distance 떨어진 대상이 받는 피해 비율 (범위 밖이면 0)
    public static float GetRatio(SplashFalloffSettings s, float distance)
    {
        if (s == null || distance < 0f) return 0f;
        if (distance <= s.innerRadius) return s.innerRatio;
        if (distance <= s.middleRadius) return s.middleRatio;
        if (distance <= s.outerRadius) return s.outerRatio;
        return 0f;
    }
}
