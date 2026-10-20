using System;

// 여관 방 증축 수치 (GameManager 인스펙터에서 조정)
// 방마다 레벨이 있고, 그 방에 묵는 파티의 최대 인원이 레벨마다 1명씩 늘어남 (방 레벨 = 파티 레벨)
[Serializable]
public class InnRoomSettings
{
    public int baseMembers = 4;                 // 방 Lv.1의 최대 인원
    public int maxMembers  = 6;                 // 증축해도 넘지 않는 최대 인원 (그 이상은 게임이 늘어짐)
    public int[] expandCosts = { 300, 600 };    // 증축 비용 (Lv.1→2, Lv.2→3 순서, 임시값)
}

// 여관 방 규칙 (순수 로직)
public static class InnRoomRules
{
    public static int MaxRoomLevel(InnRoomSettings s) =>
        s == null ? 1 : Math.Max(1, s.maxMembers - s.baseMembers + 1);

    // 방 레벨 → 최대 인원
    public static int Capacity(InnRoomSettings s, int roomLevel)
    {
        if (s == null) return 4;
        return Math.Min(s.baseMembers + Math.Max(roomLevel, 1) - 1, s.maxMembers);
    }

    // 지금 레벨에서 한 단계 증축하는 비용. 최대 레벨이면 -1
    public static int ExpandCost(InnRoomSettings s, int roomLevel)
    {
        if (s == null || roomLevel >= MaxRoomLevel(s)) return -1;
        int idx = Math.Max(roomLevel, 1) - 1;
        if (s.expandCosts == null || s.expandCosts.Length == 0) return 0;
        return s.expandCosts[Math.Min(idx, s.expandCosts.Length - 1)];
    }
}
