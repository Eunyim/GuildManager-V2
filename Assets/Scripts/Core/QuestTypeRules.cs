using System;
using System.Collections.Generic;

// 의뢰 종류
public enum QuestType
{
    BossHunt, // 보스 토벌: 정해진 보스를 쓰러뜨림
    Gather,   // 재료 수급: 정해진 재료를 정해진 개수만큼 모아 돌아옴
    Rescue,   // 구출: 던전 어딘가의 방에 갇힌 인물을 구함
}

// 의뢰 종류별 수치 (QuestGenerator 인스펙터에서 조정)
[Serializable]
public class QuestTypeSettings
{
    [UnityEngine.Header("의뢰 종류가 나올 비율")]
    public float bossHuntWeight = 1f;
    public float gatherWeight   = 1f;
    public float rescueWeight   = 1f;

    [UnityEngine.Header("재료 수급")]
    [UnityEngine.Tooltip("의뢰비 = 그 랭크 평균 의뢰비 x 이 배율 (평균보다 20% 높음)")]
    public float gatherFeeMultiplier = 1.2f;
    [UnityEngine.Tooltip("넘긴 재료 값 = 시세 x 이 비율 (재료는 의뢰 완료와 함께 없어짐)")]
    public float gatherMaterialPriceRatio = 0.8f;
    [UnityEngine.Tooltip("필요 개수 = 던전에 나오는 재료 몬스터들의 원래 드랍률 합 ÷ 100 (반올림, 최소 1). 의뢰 중에는 그 재료 드랍률에 이 배율을 곱함 (최대 100%)")]
    public float gatherDropRateMultiplier = 1.5f;
    [UnityEngine.Tooltip("막판 보장: 남은 재료 몬스터 수가 아직 못 모은 개수 이하가 되면 그 재료가 100% 떨어짐 (재료 몬스터를 다 잡으면 반드시 성공)")]
    public bool gatherGuaranteeAtEnd = true;
    [UnityEngine.Tooltip("재료 몬스터 드랍률 합이 이 값(%)보다 작으면 던전을 다시 만듦 (필요 개수 1개 이상을 기대할 수 있도록)")]
    public float gatherMinSourceRate = 100f;
    [UnityEngine.Tooltip("재료 수급 의뢰에서 방마다 적을 뽑을 때 그 재료를 주는 종족이 뽑힐 확률 (0 = 지역 몬스터 중 고르게, 다른 의뢰와 같음)")]
    [UnityEngine.Range(0f, 1f)] public float gatherRaceBias = 0f;

    [UnityEngine.Header("구출")]
    [UnityEngine.Tooltip("갇힌 인물은 입구에서 이 칸 수 이상 떨어진 방에 있음")]
    public int rescueMinDistance = 2;
    [UnityEngine.Tooltip("구출한 뒤 더 나아가 보스까지 쓰러뜨리면 의뢰비에 더해 주는 비율")]
    public float rescueDeepBonusRatio = 0.5f;
}

// 의뢰 종류 규칙 (순수 로직)
public static class QuestTypeRules
{
    // 비율대로 의뢰 종류 하나 (roll 0~1). 고를 수 없는 종류는 allowed가 false
    public static QuestType PickType(QuestTypeSettings s, float roll, bool gatherAllowed = true, bool rescueAllowed = true)
    {
        float boss = Math.Max(0f, s.bossHuntWeight);
        float gather = gatherAllowed ? Math.Max(0f, s.gatherWeight) : 0f;
        float rescue = rescueAllowed ? Math.Max(0f, s.rescueWeight) : 0f;
        float total = boss + gather + rescue;
        if (total <= 0f) return QuestType.BossHunt;
        float r = roll * total;
        if (r < boss) return QuestType.BossHunt;
        if (r < boss + gather) return QuestType.Gather;
        return QuestType.Rescue;
    }

    public static int GatherFee(QuestTypeSettings s, int averageReward) =>
        (int)Math.Round(averageReward * s.gatherFeeMultiplier);

    public static int GatherMaterialPayment(QuestTypeSettings s, int price, int count) =>
        (int)Math.Round(price * count * s.gatherMaterialPriceRatio);

    public static int RescueDeepBonus(QuestTypeSettings s, int reward) =>
        (int)Math.Round(reward * s.rescueDeepBonusRatio);

    // 갇힌 인물이 있을 방: 입구에서 minDistance 칸 이상, 입구·보스방 제외. 그런 방이 없으면 -1 (던전 구조를 다시 뽑아야 함)
    public static int PickCaptiveRoom(DungeonLayout layout, int minDistance, Random rng)
    {
        if (layout == null || !layout.IsValid) return -1;
        int[] dist = DungeonLayoutRules.GetDistances(layout, layout.startRoom);
        var candidates = new List<int>();
        for (int i = 0; i < layout.rooms.Count; i++)
        {
            if (i == layout.startRoom || i == layout.bossRoom || dist[i] < 0) continue;
            if (dist[i] >= minDistance) candidates.Add(i);
        }
        return candidates.Count > 0 ? candidates[rng.Next(candidates.Count)] : -1;
    }

    // 조건에 맞는 방이 끝내 없을 때의 대비: 보스방이 아닌 가장 먼 방
    public static int FarthestNonBossRoom(DungeonLayout layout)
    {
        if (layout == null || !layout.IsValid) return -1;
        int[] dist = DungeonLayoutRules.GetDistances(layout, layout.startRoom);
        int farthest = -1;
        for (int i = 0; i < layout.rooms.Count; i++)
        {
            if (i == layout.startRoom || i == layout.bossRoom || dist[i] < 0) continue;
            if (farthest < 0 || dist[i] > dist[farthest]) farthest = i;
        }
        return farthest;
    }

    public static string TypeName(QuestType type)
    {
        switch (type)
        {
            case QuestType.Gather: return "재료 수급";
            case QuestType.Rescue: return "구출";
            default:               return "보스 토벌";
        }
    }
}
