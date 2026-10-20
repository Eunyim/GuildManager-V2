using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 적 프리팹의 드랍 테이블에서 특정 아이템의 드랍률을 찾음 (재료 수급 의뢰의 필요 개수·막판 보장 계산용)
// 적 스크립트마다 dropTable 필드를 따로 갖고 있어서, 'dropTable'이라는 List<DropItem> 필드를 찾아 읽음
public static class DropTableLookup
{
    static readonly Dictionary<(GameObject, MonsterDropData), float> cache = new Dictionary<(GameObject, MonsterDropData), float>();

    // 이 프리팹이 item을 떨어뜨릴 원래 확률(%). 없으면 0
    public static float GetRate(GameObject prefab, MonsterDropData item)
    {
        if (prefab == null || item == null) return 0f;
        if (cache.TryGetValue((prefab, item), out float cached)) return cached;

        float rate = 0f;
        foreach (MonoBehaviour mb in prefab.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            FieldInfo field = mb.GetType().GetField("dropTable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || !(field.GetValue(mb) is List<DropItem> table)) continue;
            foreach (DropItem loot in table)
                if (loot.item == item) rate = Mathf.Max(rate, loot.dropRate);
        }
        cache[(prefab, item)] = rate;
        return rate;
    }

    // 던전에 반드시 나오는 재료 몬스터(보물 방 미믹 후보 제외)의 수와 드랍률 합
    public static int CountSources(List<StageData> stages, DungeonLayout layout, MonsterDropData item, out float sumRates)
    {
        sumRates = 0f;
        int count = 0;
        if (stages == null || item == null) return 0;
        for (int i = 0; i < stages.Count; i++)
        {
            StageData stage = stages[i];
            if (stage == null || stage.enemyPrefabs == null) continue;
            if (layout != null && i < layout.rooms.Count && layout.rooms[i].type == DungeonRoomType.Treasure) continue;
            foreach (GameObject prefab in stage.enemyPrefabs)
            {
                float rate = GetRate(prefab, item);
                if (rate <= 0f) continue;
                count++;
                sumRates += rate;
            }
        }
        return count;
    }
}
