using System;
using System.Collections.Generic;
using UnityEngine;

// 전투 성향: 유닛이 "어디에 서고 싶은가"를 정하는 방식 (아군·몬스터 공용)
public enum CombatRole
{
    Charger,  // 돌격: 대상에게 곧장 달려감 (기본)
    Guardian, // 수호: 우리 후열을 노리는 적을 먼저 막음
    Ranged,   // 원거리: 적과 거리를 유지하며 쏨
    Support,  // 지원: 적이 다가오면 도망침
    Flanker,  // 우회: 상대 후열을 먼저 노리고, 후열이 없으면 대상의 등 뒤로 돌아감
}

// 전투 이동 수치 (BattleManager 인스펙터에서 조정)
[Serializable]
public class CombatMovementSettings
{
    [Tooltip("끄면 모든 유닛이 예전처럼 대상에게 곧장 달려감 (비교용)")]
    public bool useRoles = true;
    [Tooltip("성향 판단(대상 다시 고르기 등) 주기(초). 이동 자체는 매 프레임")]
    public float decisionInterval = 0.25f;

    [Header("원거리")]
    [Tooltip("가장 가까운 적이 (사거리 x 이 값)보다 가까우면 물러나기 시작")]
    public float rangedRetreatStart = 0.45f;
    [Tooltip("물러나다가 가장 가까운 적이 (사거리 x 이 값)보다 멀어지면 멈춤 (떨림 방지로 시작 값보다 크게)")]
    public float rangedRetreatStop = 0.7f;
    [Tooltip("물러날 때 이동속도 배율 (물러나는 동안에는 공격하지 않음)")]
    public float rangedRetreatSpeed = 0.8f;

    [Header("지원")]
    [Tooltip("적이 이 거리 안에 들어오면 도망치기 시작")]
    public float supportFleeStart = 2.5f;
    [Tooltip("도망치다가 가장 가까운 적이 이 거리보다 멀어지면 멈춤")]
    public float supportFleeStop = 3.5f;
    [Tooltip("도망칠 때 아군 전열(수호 우선) 뒤로 피함. 그 전열 유닛에서 적 반대쪽으로 이만큼 떨어진 곳")]
    public float supportShelterOffset = 1.0f;
    [Tooltip("피할 곳 방향이 적 방향과 이 값(코사인)보다 비슷하면 그쪽으로 가지 않고 그냥 적 반대쪽으로 도망")]
    public float supportShelterMaxAlign = 0.5f;

    [Header("우회")]
    [Tooltip("대상의 등 뒤 어느 정도 거리로 돌아갈지")]
    public float flankOffset = 1.0f;
    [Tooltip("이 시간(초) 안에 등 뒤로 못 가면 그냥 정면에서 공격")]
    public float flankTimeout = 3.0f;

    [Header("후열 방어")]
    [Tooltip("켜면 수호는 우리 후열을 노리는 적(후열을 대상으로 잡았거나 후열 가까이 붙은 적)을 막으러 가고, 원거리는 그 적을 먼저 쏨")]
    public bool backlineDefense = true;

    [Header("수호")]
    [Tooltip("우리 후열에서 이 거리 안에 들어온 적을 먼저 막으러 감")]
    public float guardRadius = 3.0f;

    [Header("겹침 방지")]
    public float separationRadius = 0.7f;
    public float separationStrength = 1.0f;
}

// 전투 이동 계산 (순수 로직). 위치들을 받아 목적지·판단을 돌려줌
public static class CombatPositioning
{
    public static bool IsBackline(CombatRole role) => role == CombatRole.Ranged || role == CombatRole.Support;

    // 원거리: 물러날지 (지금 물러나는 중이면 멈추는 기준을 더 멀리 잡아 떨림 방지)
    public static bool ShouldRetreat(CombatMovementSettings s, float nearestEnemyDistance, float attackRange, bool retreating)
    {
        float threshold = attackRange * (retreating ? s.rangedRetreatStop : s.rangedRetreatStart);
        return nearestEnemyDistance < threshold;
    }

    // 지원: 도망칠지
    public static bool ShouldFlee(CombatMovementSettings s, float nearestEnemyDistance, bool fleeing)
    {
        return nearestEnemyDistance < (fleeing ? s.supportFleeStop : s.supportFleeStart);
    }

    // threat에서 멀어지는 방향의 한 걸음 앞 지점. 바닥 밖이면 돌려서 시도할 방향들을 차례로 돌려줌
    public static IEnumerable<Vector2> AwayCandidates(Vector2 me, Vector2 threat, float step)
    {
        Vector2 away = me - threat;
        if (away.sqrMagnitude < 0.0001f) away = Vector2.left;
        away.Normalize();
        float[] angles = { 0f, 35f, -35f, 70f, -70f, 105f, -105f };
        foreach (float a in angles) yield return me + Rotate(away, a) * step;
    }

    // 대상의 "등 뒤" 방향: 대상이 바라보는 상대(없으면 우리 편 중심) 반대쪽
    public static Vector2 BackDirection(Vector2 target, Vector2 targetFacingPoint)
    {
        Vector2 back = target - targetFacingPoint;
        return back.sqrMagnitude < 0.0001f ? Vector2.right : back.normalized;
    }

    // 등 뒤에 있는지 (대상 기준으로 뒤쪽 반원 안)
    public static bool IsBehind(Vector2 me, Vector2 target, Vector2 backDir)
    {
        Vector2 toMe = me - target;
        return toMe.sqrMagnitude > 0.0001f && Vector2.Dot(toMe.normalized, backDir) > 0.3f;
    }

    // 우회 경로: 대상 앞쪽에 있으면 옆으로 먼저 돌고, 옆·뒤에 있으면 등 뒤 지점으로
    public static Vector2 FlankPoint(Vector2 me, Vector2 target, Vector2 backDir, float offset)
    {
        Vector2 toMe = me - target;
        float along = Vector2.Dot(toMe, backDir);
        if (along > -offset * 0.3f) return target + backDir * offset; // 이미 옆이나 뒤: 등 뒤로 붙음

        Vector2 side = new Vector2(-backDir.y, backDir.x);
        if (Vector2.Dot(toMe, side) < 0f) side = -side; // 내가 있는 쪽 옆으로 돎
        return target + side * (offset * 1.5f);
    }

    // 지원: 전열 아군 뒤(위협 반대쪽)의 피할 곳
    public static Vector2 ShelterPoint(Vector2 protector, Vector2 threat, float offset)
    {
        Vector2 away = protector - threat;
        if (away.sqrMagnitude < 0.0001f) away = Vector2.left;
        return protector + away.normalized * offset;
    }

    // 피할 곳으로 가는 길이 위협 쪽으로 다가가는 길인지 (그렇다면 그냥 반대쪽으로 도망)
    public static bool ShelterTowardThreat(Vector2 me, Vector2 shelter, Vector2 threat, float maxAlign)
    {
        Vector2 toShelter = shelter - me, toThreat = threat - me;
        if (toShelter.sqrMagnitude < 0.0001f || toThreat.sqrMagnitude < 0.0001f) return false;
        return Vector2.Dot(toShelter.normalized, toThreat.normalized) > maxAlign;
    }

    // 같은 편끼리 겹치지 않게 밀어내는 힘
    public static Vector2 Separation(Vector2 me, IList<Vector2> others, float radius)
    {
        Vector2 push = Vector2.zero;
        foreach (Vector2 o in others)
        {
            Vector2 d = me - o;
            float dist = d.magnitude;
            if (dist <= 0.0001f || dist >= radius) continue;
            push += d / dist * (1f - dist / radius);
        }
        return push;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
