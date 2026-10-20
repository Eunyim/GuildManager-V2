using System;

// 방 종류별 수치 (BattleManager 인스펙터에서 조정)
[Serializable]
public class DungeonRoomSettings
{
    // 휴식 공간에서 회복하는 양 (최대 체력 대비)
    public float restHealRatio = 0.5f;

    // 웨이브 방에서 한 무리를 정리한 뒤 다음 무리가 나오기까지의 시간(초)
    public float waveInterval = 1.5f;

    // 보물 상자에서 나오는 골드 범위 (의뢰 랭크 D, C, B, A, S 순서)
    public int[] treasureGoldMin = { 40, 70, 120, 200, 350 };
    public int[] treasureGoldMax = { 80, 130, 220, 350, 600 };

    // 웨이브를 모두 막아냈을 때 받는 골드 범위 (의뢰 랭크 D, C, B, A, S 순서)
    public int[] waveGoldMin = { 60, 100, 180, 300, 500 };
    public int[] waveGoldMax = { 100, 180, 320, 500, 800 };
}

// 방 종류별 보상 규칙 (순수 로직)
public static class DungeonRoomRules
{
    // roll01: 0~1 사이의 난수
    public static int RollTreasureGold(DungeonRoomSettings settings, RankType rank, float roll01)
    {
        return Roll(settings.treasureGoldMin, settings.treasureGoldMax, rank, roll01);
    }

    public static int RollWaveGold(DungeonRoomSettings settings, RankType rank, float roll01)
    {
        return Roll(settings.waveGoldMin, settings.waveGoldMax, rank, roll01);
    }

    static int Roll(int[] minTable, int[] maxTable, RankType rank, float roll01)
    {
        int min = GetByRank(minTable, rank);
        int max = Math.Max(min, GetByRank(maxTable, rank));
        float t = Math.Min(Math.Max(roll01, 0f), 1f);
        return (int)Math.Round(min + (max - min) * t);
    }

    static int GetByRank(int[] table, RankType rank)
    {
        if (table == null || table.Length == 0) return 0;
        return Math.Max(0, table[Math.Min(Math.Max((int)rank, 0), table.Length - 1)]);
    }
}
