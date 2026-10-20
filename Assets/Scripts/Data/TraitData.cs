using UnityEngine;
using System.Collections.Generic;

// 1. 발동 조건 목록 (레고 블록 1)
public enum TraitConditionType
{
    Always,                 // 상시 발동 (예: 용사의 기운, 외팔이)
    NoAllyNearby,           // 반경 내 아군 없음
    SpecificEnemyExists,    // 특정 적 존재
    HpBelowPercentage,      // 체력이 일정 % 이하
    AllyCountBelow,         // 아군 수가 conditionValue 이하일 때
    AllyRatioAtLeast,       // 살아 있는 아군(자신 포함)이 출발 인원의 conditionValue 비율 이상일 때 (예: 외로운 늑대 - 0.5)
    TargetIsFamily          // 때리는 대상이 conditionTag 계열일 때 (괴수/인간/언데드/그린스킨/드래곤. EnemyFamilyRules). 피해를 줄 때만 판정
}

// 2. 결과 효과 목록 (레고 블록 2). 배율 효과는 곱해짐 (1.2 = 20% 증가, 0.7 = 30% 감소)
public enum TraitEffectType
{
    ModifyAttackPower,      // 공격력 배율
    ModifyMoveSpeed,        // 이동속도 배율
    ChangeAI_HideBehindAlly,// AI 변경: 아군 뒤로 숨기 (지금은 겁쟁이 '성격'이 같은 행동을 씀)
    ModifyAllStats,         // 공격력·방어력·이동속도 배율 (예: 용사의 기운 1.2)
    ModifyDefense,          // 방어력 배율
    ModifyEvasion,          // 회피율 더하기 (0.1 = +10%p, -0.3 = -30%p). 회피하면 그 공격의 피해가 없음
    ModifyDamageDealt,      // 가하는 피해 배율 (조건이 TargetIsFamily면 그 계열에게만)
    ModifyDamageTaken,      // 받는 피해 배율 (예: 약골 1.3)
    ModifyBleedTaken,       // 받는 출혈 피해 배율 (예: 단단한 피부 0.95)
    ModifyStunTaken,        // 받는 스턴 시간 배율 (예: 강인함 0.9)
    AttackByMissingHp,      // 잃은 체력 비율 × effectValue만큼 공격력 증가 (예: 0.5 → 빈사 직전 +50%)
    RevealNextRoom,         // 던전에서 지금 방과 이어진 방의 종류가 보임 (예: 육감)
    MultiplyEvasion         // 회피율 배율 (0.7 = 30% 감소). 더하기(ModifyEvasion)를 모두 더한 뒤에 곱함 (예: 외눈박이)
}

// 특성 분류: 긍정 특성 / 트라우마(정신, 신전에서 치료) / 결함(신체, 치유소에서 치료)
public enum TraitCategory
{
    Positive,
    Trauma,
    Defect
}

// 한 특성이 효과를 여러 개 가질 때 (같은 조건으로 함께 적용. 예: 빠른 발 = 이동속도 + 회피율)
[System.Serializable]
public class TraitExtraEffect
{
    public TraitEffectType effectType;
    public float effectValue = 1f;
}

// 이 속성 덕분에 유니티 우클릭 메뉴에서 특성을 붕어빵처럼 찍어낼 수 있습니다.
[CreateAssetMenu(fileName = "New Trait", menuName = "Guild/Trait Data")]
public class TraitData : ScriptableObject
{
    [Header("📝 특성 기본 정보")]
    public string traitName;        // 특성 이름 (예: "괴수 공포증")
    [TextArea(2, 3)]
    public string description;      // 설명
    public TraitCategory category;

    [Header("🔍 발동 조건 (Condition)")]
    public TraitConditionType conditionType;
    public string conditionTag;     // 찾을 태그 / 계열 이름 (예: "괴수")
    public float conditionValue;    // 조건 수치 (예: 반경 3.0f, 체력 0.3 등)

    [Header("💥 적용 효과 (Effect)")]
    public TraitEffectType effectType;
    public float effectValue;       // 효과 수치 (예: 1.2 = 20% 증가, 0.8 = 20% 감소)

    [Tooltip("켜면 조건을 만족하지 못할 때 elseEffectValue로 같은 효과를 적용 (예: 외로운 늑대 - 아군 50% 이상이면 0.9배, 미만이면 1.2배)")]
    public bool hasElseEffect;
    public float elseEffectValue = 1f;

    [Tooltip("같은 조건으로 함께 적용되는 효과 (예: 빠른 발의 회피율)")]
    public List<TraitExtraEffect> extraEffects = new List<TraitExtraEffect>();

    [Header("🏥 특성 분류")]
    [Tooltip("신체적 부정 특성(결함)은 신전에서 치유 불가 (예: 외팔이)")]
    public bool isPhysical;         // true = 신체적 특성 (신전 치유 불가)

    // 주 효과와 추가 효과를 모두 돌며 (효과 종류, 값)을 넘김. useElse면 주 효과는 elseEffectValue, 추가 효과는 적용하지 않음
    public void ForEachEffect(bool useElse, System.Action<TraitEffectType, float> apply)
    {
        apply(effectType, useElse ? elseEffectValue : effectValue);
        if (useElse || extraEffects == null) return;
        foreach (TraitExtraEffect e in extraEffects)
            if (e != null) apply(e.effectType, e.effectValue);
    }

    public bool HasEffect(TraitEffectType type)
    {
        if (effectType == type) return true;
        if (extraEffects != null)
            foreach (TraitExtraEffect e in extraEffects)
                if (e != null && e.effectType == type) return true;
        return false;
    }
}
