using UnityEngine;

// 전투 화면 연출: 화면 흔들림, 히트스톱, 쓰러짐 잔상. 수치는 BattleManager.hitFeedback
// 모두 화면용이라 전투 판정에는 영향이 없음. 처음 쓸 때 스스로 생기고 씬이 바뀌면 같이 사라짐
public class CombatJuice : MonoBehaviour
{
    static CombatJuice instance;

    static CombatJuice Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("CombatJuice").AddComponent<CombatJuice>();
            return instance;
        }
    }

    static HitFeedbackSettings Settings => BattleManager.Instance != null ? BattleManager.Instance.hitFeedback : null;

    // ─── 화면 흔들림 ───────────────────────────────────────────────
    // 카메라 위치에 더했다가 다음 프레임에 빼는 방식 (던전 카메라는 움직이지 않음)
    Camera shakeCamera;
    Vector3 cameraOffset;
    float shakeLeft, shakeTotal, shakeAmplitude;

    public static void ShakeCamera(ShakeSpec spec)
    {
        HitFeedbackSettings s = Settings;
        if (s == null || !s.cameraShakeEnabled || spec == null || spec.amplitude <= 0f || spec.duration <= 0f) return;

        CombatJuice j = Instance;
        // 이미 더 센 흔들림이 남아 있으면 그쪽을 유지
        float current = j.shakeLeft > 0f ? j.shakeAmplitude * (j.shakeLeft / j.shakeTotal) : 0f;
        if (current >= spec.amplitude) return;
        j.shakeAmplitude = spec.amplitude;
        j.shakeTotal = j.shakeLeft = spec.duration;
    }

    // ─── 히트스톱 ─────────────────────────────────────────────────
    // 배속이 1일 때만 아주 짧게 거의 멈춤. 봇 배속이나 일시정지(0)는 건드리지 않음
    const float StopScale = 0.02f; // 일시정지(0)와 구분되도록 0이 아닌 값
    float stopUntil; // 실제 시간 기준

    public static void HitStop(float duration)
    {
        HitFeedbackSettings s = Settings;
        if (s == null || !s.hitStopEnabled || duration <= 0f) return;

        CombatJuice j = Instance;
        bool stopping = j.stopUntil > 0f && Mathf.Approximately(Time.timeScale, StopScale);
        if (!stopping && !Mathf.Approximately(Time.timeScale, 1f)) return;

        Time.timeScale = StopScale;
        j.stopUntil = Mathf.Max(j.stopUntil, Time.unscaledTime + duration);
    }

    // ─── 쓰러짐 잔상 ──────────────────────────────────────────────
    // 유닛은 바로 사라지고, 같은 모습의 잔상이 하얗게 번쩍였다가 납작해지며 흐려짐
    public static void SpawnDeathGhost(SpriteRenderer source)
    {
        HitFeedbackSettings s = Settings;
        if (s == null || !s.deathEffectEnabled || source == null || source.sprite == null) return;

        GameObject go = new GameObject(source.gameObject.name + "_DeathGhost");
        go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        go.transform.localScale = source.transform.lossyScale;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = source.sprite;
        sr.flipX = source.flipX;
        sr.flipY = source.flipY;
        sr.color = source.color;
        sr.sortingLayerID = source.sortingLayerID;
        sr.sortingOrder = source.sortingOrder;
        sr.sharedMaterial = HitFeedback.FlashMaterial != null ? HitFeedback.FlashMaterial : source.sharedMaterial;

        go.AddComponent<DeathGhost>().Play(sr, s.deathFlashTime, s.deathFadeTime);
    }

    void Update()
    {
        if (stopUntil > 0f && Time.unscaledTime >= stopUntil)
        {
            stopUntil = 0f;
            if (Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f; // 그사이 일시정지 등으로 바뀌었으면 그대로 둠
        }
    }

    void LateUpdate()
    {
        RemoveCameraOffset();
        if (shakeLeft <= 0f) return;

        shakeLeft -= Time.unscaledDeltaTime; // 히트스톱 중에도 흔들림은 흐름
        if (shakeLeft <= 0f) return;

        shakeCamera = Camera.main;
        if (shakeCamera == null) return;
        float strength = shakeAmplitude * (shakeLeft / shakeTotal);
        cameraOffset = new Vector3(Random.Range(-1f, 1f) * strength, Random.Range(-1f, 1f) * strength, 0f);
        shakeCamera.transform.position += cameraOffset;
    }

    void RemoveCameraOffset()
    {
        if (cameraOffset == Vector3.zero) return;
        if (shakeCamera != null) shakeCamera.transform.position -= cameraOffset;
        cameraOffset = Vector3.zero;
    }

    void OnDestroy()
    {
        RemoveCameraOffset();
        if (stopUntil > 0f && Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
        if (instance == this) instance = null;
    }
}

// 쓰러진 유닛의 잔상: 하얗게 번쩍임 → 위아래로 납작해지며 흐려짐 → 사라짐
public class DeathGhost : MonoBehaviour
{
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    SpriteRenderer sr;
    MaterialPropertyBlock block;
    Color baseColor;
    Vector3 baseScale;
    float baseBottom; // 발밑 높이 (납작해져도 발은 바닥에 붙어 있도록)
    float flashTime, fadeTime, elapsed;

    public void Play(SpriteRenderer renderer, float flashTime, float fadeTime)
    {
        sr = renderer;
        baseColor = sr.color;
        baseScale = transform.localScale;
        baseBottom = sr.bounds.min.y;
        this.flashTime = Mathf.Max(0f, flashTime);
        this.fadeTime = Mathf.Max(0.01f, fadeTime);
        block = new MaterialPropertyBlock();
        SetFlash(1f);
    }

    void Update()
    {
        if (sr == null) { Destroy(gameObject); return; }
        elapsed += Time.deltaTime;

        if (elapsed < flashTime) return;

        float t = Mathf.Clamp01((elapsed - flashTime) / fadeTime);
        SetFlash(1f - t);
        transform.localScale = new Vector3(baseScale.x * (1f + 0.3f * t), baseScale.y * (1f - 0.8f * t), baseScale.z);
        transform.position += Vector3.up * (baseBottom - sr.bounds.min.y);
        Color c = baseColor;
        c.a = baseColor.a * (1f - t);
        sr.color = c;

        if (t >= 1f) Destroy(gameObject);
    }

    void SetFlash(float amount)
    {
        sr.GetPropertyBlock(block);
        block.SetColor(FlashColorId, Color.white);
        block.SetFloat(FlashAmountId, amount);
        sr.SetPropertyBlock(block);
    }
}
