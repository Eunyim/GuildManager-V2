using UnityEngine;

// 맞았을 때의 반응 수치 (BattleManager 인스펙터에서 조정)
[System.Serializable]
public class HitFeedbackSettings
{
    [Header("번쩍임")]
    public bool flashEnabled = true;
    public Color flashColor = Color.white;
    public float flashDuration = 0.1f;    // 단색에서 원래 색으로 돌아오기까지(초)

    [Header("흔들림")]
    public bool shakeEnabled = true;
    public float shakeDuration = 0.12f;   // 흔들리는 시간(초)
    public float shakeAmplitude = 0.08f;  // 처음 흔들림 폭 (월드 유닛, 시간이 지나며 줄어듦)

    [Header("치명타")]
    public Color critFlashColor = new Color(1f, 0.85f, 0.3f);
    public float critShakeMultiplier = 1.6f; // 치명타로 맞으면 몸 흔들림 폭에 곱함

    [Header("공격 동작 (대상 쪽으로 살짝 내딛었다 돌아옴, 화면용)")]
    public bool lungeEnabled = true;
    public float lungeDistance = 0.12f;   // 내딛는 거리 (월드 유닛)
    public float lungeDuration = 0.14f;   // 내딛었다 돌아오기까지(초)

    [Header("히트스톱 (배속 1일 때만, 봇 배속·일시정지 중에는 건너뜀)")]
    public bool hitStopEnabled = true;
    public float critHitStop = 0.04f;     // 치명타 (실제 시간, 초)
    public float killHitStop = 0.06f;     // 적을 쓰러뜨렸을 때
    public float bossKillHitStop = 0.25f; // 보스를 쓰러뜨렸을 때

    [Header("화면 흔들림")]
    public bool cameraShakeEnabled = true;
    public ShakeSpec critCameraShake      = new ShakeSpec(0.03f, 0.08f);
    public ShakeSpec allyDeathCameraShake = new ShakeSpec(0.10f, 0.25f);
    public ShakeSpec bossDeathCameraShake = new ShakeSpec(0.25f, 0.5f);

    [Header("쓰러짐 (하얗게 번쩍였다가 납작해지며 사라짐)")]
    public bool deathEffectEnabled = true;
    public float deathFlashTime = 0.08f;  // 하얗게 번쩍이는 시간(초)
    public float deathFadeTime = 0.35f;   // 그 뒤 사라지기까지(초)

    [Header("체력바 잔상 (깎인 만큼 잠깐 남았다가 줄어듦)")]
    public bool hpTrailEnabled = true;
    public Color hpTrailColor = new Color(1f, 1f, 1f, 0.85f);
    public float hpTrailDelay = 0.3f;     // 맞은 뒤 줄어들기 시작할 때까지(초)
    public float hpTrailSpeed = 0.8f;     // 초당 줄어드는 양 (바 전체 = 1)
}

[System.Serializable]
public class ShakeSpec
{
    public float amplitude; // 흔들림 폭 (월드 유닛)
    public float duration;  // 흔들리는 시간(초)
    public ShakeSpec(float amplitude, float duration) { this.amplitude = amplitude; this.duration = duration; }
}

// 유닛이 맞았을 때 스프라이트를 번쩍이고 몸을 흔듦, 공격할 때 대상 쪽으로 살짝 내딛음. BattleUnit이 처음 필요할 때 스스로 붙임
// 흔들림·내딛기는 위치에 더했다가 다음 Update 전에 빼는 방식이라 이동/공격 판정에는 영향이 없음 (BattleUnit.Update 첫 줄에서 RemoveShakeOffset 호출)
[DisallowMultipleComponent]
public class HitFeedback : MonoBehaviour
{
    const string FLASH_SHADER_PATH = "Shaders/SpriteHitFlash";
    static Material flashMaterial;
    static bool flashMaterialLoaded;
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    SpriteRenderer spriteRenderer;
    Material originalMaterial;
    MaterialPropertyBlock block;
    Color flashColor;
    float flashLeft, flashTotal;

    float shakeLeft, shakeTotal, shakeAmplitude;
    float lungeLeft, lungeTotal;
    Vector3 lungeOffset; // 내딛는 방향 × 거리
    Vector3 appliedOffset;

    // 번쩍임 재질 (쓰러짐 연출도 같이 씀)
    public static Material FlashMaterial
    {
        get
        {
            if (!flashMaterialLoaded)
            {
                flashMaterialLoaded = true;
                Shader shader = Resources.Load<Shader>(FLASH_SHADER_PATH);
                if (shader != null) flashMaterial = new Material(shader) { name = "SpriteHitFlash (runtime)" };
                else Debug.LogWarning($"[HitFeedback] Resources/{FLASH_SHADER_PATH} 셰이더가 없어 번쩍임을 건너뜁니다.");
            }
            return flashMaterial;
        }
    }

    public void Play(HitFeedbackSettings settings, bool crit = false)
    {
        if (settings == null) return;

        if (settings.flashEnabled && settings.flashDuration > 0f && FlashMaterial != null)
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                if (flashLeft <= 0f) originalMaterial = spriteRenderer.sharedMaterial; // 번쩍이는 중에 또 맞으면 원래 재질을 덮어쓰지 않음
                spriteRenderer.sharedMaterial = FlashMaterial;
                flashColor = crit ? settings.critFlashColor : settings.flashColor;
                flashTotal = flashLeft = settings.flashDuration;
                ApplyFlash(1f);
            }
        }

        if (settings.shakeEnabled && settings.shakeDuration > 0f && settings.shakeAmplitude > 0f)
        {
            shakeTotal = shakeLeft = settings.shakeDuration;
            shakeAmplitude = settings.shakeAmplitude * (crit ? settings.critShakeMultiplier : 1f);
        }
    }

    // 공격할 때 대상 쪽으로 살짝 내딛었다 돌아옴
    public void PlayLunge(HitFeedbackSettings settings, Vector3 toward)
    {
        if (settings == null || !settings.lungeEnabled || settings.lungeDuration <= 0f) return;
        toward.z = 0f;
        if (toward.sqrMagnitude < 0.0001f) return;
        lungeOffset = toward.normalized * settings.lungeDistance;
        lungeTotal = lungeLeft = settings.lungeDuration;
    }

    // 지난 프레임에 더한 흔들림을 빼서 실제 위치로 되돌림
    public void RemoveShakeOffset()
    {
        if (appliedOffset == Vector3.zero) return;
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
    }

    void LateUpdate()
    {
        if (flashLeft > 0f)
        {
            flashLeft -= Time.deltaTime;
            if (flashLeft <= 0f) EndFlash();
            else ApplyFlash(flashLeft / flashTotal);
        }

        RemoveShakeOffset();
        Vector3 offset = Vector3.zero;
        if (shakeLeft > 0f)
        {
            shakeLeft -= Time.deltaTime;
            if (shakeLeft > 0f)
            {
                float strength = shakeAmplitude * (shakeLeft / shakeTotal);
                offset += new Vector3(Random.Range(-1f, 1f) * strength, Random.Range(-0.5f, 0.5f) * strength, 0f);
            }
        }
        if (lungeLeft > 0f)
        {
            lungeLeft -= Time.deltaTime;
            if (lungeLeft > 0f) offset += lungeOffset * Mathf.Sin(Mathf.PI * (1f - lungeLeft / lungeTotal)); // 나갔다가 돌아옴
        }
        if (offset != Vector3.zero)
        {
            appliedOffset = offset;
            transform.position += appliedOffset;
        }
    }

    void ApplyFlash(float amount)
    {
        if (block == null) block = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(block);
        block.SetColor(FlashColorId, flashColor);
        block.SetFloat(FlashAmountId, amount);
        spriteRenderer.SetPropertyBlock(block);
    }

    void EndFlash()
    {
        flashLeft = 0f;
        if (spriteRenderer == null) return;
        spriteRenderer.sharedMaterial = originalMaterial;
        if (block != null)
        {
            spriteRenderer.GetPropertyBlock(block);
            block.SetFloat(FlashAmountId, 0f);
            spriteRenderer.SetPropertyBlock(block);
        }
    }

    void OnDisable()
    {
        if (flashLeft > 0f) EndFlash();
        RemoveShakeOffset();
        shakeLeft = 0f;
        lungeLeft = 0f;
    }
}
