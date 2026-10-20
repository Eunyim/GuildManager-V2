using System;
using System.Collections.Generic;

// 던전 구조(개미굴): 방들이 격자 위에 놓이고 동서남북으로 이어짐. 어떻게 이어질지는 던전마다 무작위

// 방의 종류
public enum DungeonRoomType
{
    Normal,   // 몬스터가 나오는 기본 방
    Rest,     // 휴식 공간: 전투 없이 크게 회복
    Treasure, // 보물 방: 전투 없이 상자 (미믹일 수 있음)
    Boss,     // 보스 방: 정리하면 던전 클리어
    Wave,     // 몬스터 웨이브: 적이 여러 차례 몰려오고 보상이 큼
}

// 방 하나. 이웃은 방 번호(rooms의 인덱스)이고, 그 방향에 길이 없으면 -1
[Serializable]
public class DungeonRoom
{
    public DungeonRoomType type = DungeonRoomType.Normal;
    public int x, y;                                // 격자 위치 (x: 동쪽이 +, y: 북쪽이 +)
    public int seed;                                // 방 모양을 정하는 값 (되돌아와도 같은 모양이 나오도록)
    public int[] neighbors = { -1, -1, -1, -1 };    // 북, 동, 남, 서 (DungeonLayoutRules.North ~ West)
}

[Serializable]
public class DungeonLayout
{
    public List<DungeonRoom> rooms = new List<DungeonRoom>();
    public int startRoom = 0;
    public int bossRoom = 0; // 입구에서 가장 먼 방. 여기를 정리하면 던전 클리어

    public bool IsValid => rooms != null && rooms.Count > 0
                           && startRoom >= 0 && startRoom < rooms.Count
                           && bossRoom >= 0 && bossRoom < rooms.Count;

    // 그 방향의 이웃 방 번호 (없으면 -1)
    public int GetNeighbor(int room, int direction)
    {
        if (room < 0 || room >= rooms.Count || direction < 0 || direction >= DungeonLayoutRules.DirectionCount) return -1;
        return rooms[room].neighbors[direction];
    }
}

// 던전 구조 생성 수치 (QuestGenerator 인스펙터에서 조정)
[Serializable]
public class DungeonLayoutSettings
{
    // 의뢰 랭크별 방 개수 범위 (D, C, B, A, S 순서. 입구와 보스방 포함)
    public int[] minRoomsPerRank = { 4, 5, 6, 7, 8 };
    public int[] maxRoomsPerRank = { 5, 6, 7, 8, 10 };

    // 새 방이 가장 최근에 만든 방이 아니라 아무 방에서나 뻗어 나올 확률 (높을수록 갈림길이 많음)
    public float branchChance = 0.35f;

    // 입구에서 보스방까지 가장 짧은 길에 놓이는 방의 최소 비율 (전체 방 개수 대비. 보스가 입구 바로 옆에 붙는 것을 막음)
    public float minMainPathRatio = 0.5f;

    // 입구와 보스방을 뺀 나머지 방의 종류 비중 (입구는 항상 기본 방)
    public float normalRoomWeight   = 50f;
    public float restRoomWeight     = 15f;
    public float treasureRoomWeight = 15f;
    public float waveRoomWeight     = 20f;

    // 던전 하나에 나올 수 있는 최대 개수
    public int maxRestRooms     = 2;
    public int maxTreasureRooms = 2;
    public int maxWaveRooms     = 2;

    // 웨이브 방에서 적이 몰려오는 횟수 (한 번의 규모는 기본 방과 같음)
    public int waveCount = 2;
}

// 던전 구조 생성 규칙 (순수 로직)
public static class DungeonLayoutRules
{
    public const int North = 0, East = 1, South = 2, West = 3;
    public const int DirectionCount = 4;

    static readonly int[] StepX = { 0, 1, 0, -1 };
    static readonly int[] StepY = { 1, 0, -1, 0 };
    static readonly string[] Names = { "북쪽", "동쪽", "남쪽", "서쪽" };

    const int MaxAttempts = 30; // 보스까지의 최소 거리 조건을 만족할 때까지 다시 만드는 횟수

    public static int Opposite(int direction) => (direction + 2) % DirectionCount;
    public static string GetDirectionName(int direction) => direction >= 0 && direction < DirectionCount ? Names[direction] : "";

    public static int RollRoomCount(DungeonLayoutSettings settings, RankType rank, Random rng)
    {
        int min = Math.Max(2, GetByRank(settings.minRoomsPerRank, rank, 3));
        int max = Math.Max(min, GetByRank(settings.maxRoomsPerRank, rank, min));
        return rng.Next(min, max + 1);
    }

    public static DungeonLayout Generate(DungeonLayoutSettings settings, RankType rank, Random rng)
    {
        int roomCount = RollRoomCount(settings, rank, rng);
        int minPathRooms = Math.Min(roomCount, Math.Max(2, (int)Math.Ceiling(roomCount * settings.minMainPathRatio)));

        DungeonLayout best = null;
        int bestPathRooms = 0;
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            DungeonLayout layout = Build(roomCount, settings.branchChance, rng);
            int pathRooms = GetDistances(layout, layout.startRoom)[layout.bossRoom] + 1;
            if (pathRooms > bestPathRooms) { best = layout; bestPathRooms = pathRooms; }
            if (pathRooms >= minPathRooms) break;
        }
        AssignRoomTypes(best, settings, rng);
        return best;
    }

    // 입구는 기본 방, 보스방은 보스, 나머지는 비중에 따라 무작위 (종류별 최대 개수를 넘지 않게)
    static void AssignRoomTypes(DungeonLayout layout, DungeonLayoutSettings settings, Random rng)
    {
        int rest = 0, treasure = 0, wave = 0;
        for (int i = 0; i < layout.rooms.Count; i++)
        {
            DungeonRoom room = layout.rooms[i];
            if (i == layout.bossRoom) { room.type = DungeonRoomType.Boss; continue; }
            if (i == layout.startRoom) { room.type = DungeonRoomType.Normal; continue; }

            float normalWeight   = Math.Max(0f, settings.normalRoomWeight);
            float restWeight     = rest < settings.maxRestRooms ? Math.Max(0f, settings.restRoomWeight) : 0f;
            float treasureWeight = treasure < settings.maxTreasureRooms ? Math.Max(0f, settings.treasureRoomWeight) : 0f;
            float waveWeight     = wave < settings.maxWaveRooms ? Math.Max(0f, settings.waveRoomWeight) : 0f;

            float total = normalWeight + restWeight + treasureWeight + waveWeight;
            double roll = rng.NextDouble() * total;
            if (total <= 0f || roll < normalWeight) room.type = DungeonRoomType.Normal;
            else if (roll < normalWeight + restWeight) { room.type = DungeonRoomType.Rest; rest++; }
            else if (roll < normalWeight + restWeight + treasureWeight) { room.type = DungeonRoomType.Treasure; treasure++; }
            else { room.type = DungeonRoomType.Wave; wave++; }
        }
    }

    public static string GetRoomTypeName(DungeonRoomType type)
    {
        switch (type)
        {
            case DungeonRoomType.Rest:     return "휴식";
            case DungeonRoomType.Treasure: return "보물";
            case DungeonRoomType.Boss:     return "보스";
            case DungeonRoomType.Wave:     return "웨이브";
            default:                       return "전투";
        }
    }

    // 방이 한 줄로 이어진 구조 (구조 정보 없이 방 목록만 있는 의뢰용)
    public static DungeonLayout Linear(int roomCount)
    {
        DungeonLayout layout = new DungeonLayout();
        for (int i = 0; i < roomCount; i++)
        {
            layout.rooms.Add(new DungeonRoom { x = i, y = 0, seed = i + 1 });
            if (i > 0) Link(layout, i - 1, i, East);
        }
        layout.startRoom = 0;
        layout.bossRoom = Math.Max(0, roomCount - 1);
        if (roomCount > 0) layout.rooms[layout.bossRoom].type = DungeonRoomType.Boss;
        return layout;
    }

    // 입구에서 시작해 방을 하나씩 붙여 나감. 보스방은 입구에서 가장 먼 방
    static DungeonLayout Build(int roomCount, float branchChance, Random rng)
    {
        DungeonLayout layout = new DungeonLayout();
        Dictionary<long, int> occupied = new Dictionary<long, int>();

        layout.rooms.Add(new DungeonRoom { x = 0, y = 0, seed = rng.Next() });
        occupied[Key(0, 0)] = 0;
        int tip = 0; // 가장 최근에 만든 방

        while (layout.rooms.Count < roomCount)
        {
            int parent = rng.NextDouble() < branchChance ? rng.Next(layout.rooms.Count) : tip;
            int direction = PickFreeDirection(layout, occupied, parent, rng);
            if (direction < 0)
            {
                // 사방이 막힌 방이면 빈 자리가 있는 다른 방에서 뻗음 (가장자리 방은 항상 빈 자리가 있음)
                List<int> candidates = new List<int>();
                for (int i = 0; i < layout.rooms.Count; i++)
                    if (HasFreeDirection(layout, occupied, i)) candidates.Add(i);
                parent = candidates[rng.Next(candidates.Count)];
                direction = PickFreeDirection(layout, occupied, parent, rng);
            }

            DungeonRoom from = layout.rooms[parent];
            DungeonRoom room = new DungeonRoom { x = from.x + StepX[direction], y = from.y + StepY[direction], seed = rng.Next() };
            layout.rooms.Add(room);
            int index = layout.rooms.Count - 1;
            occupied[Key(room.x, room.y)] = index;
            Link(layout, parent, index, direction);
            tip = index;
        }

        layout.startRoom = 0;
        int[] distances = GetDistances(layout, 0);
        int farthest = 0;
        for (int i = 1; i < distances.Length; i++)
            if (distances[i] > distances[farthest]) farthest = i;
        layout.bossRoom = farthest;
        return layout;
    }

    // from에서 각 방까지 거쳐야 하는 길의 수 (닿지 않으면 -1)
    public static int[] GetDistances(DungeonLayout layout, int from)
    {
        int[] distances = new int[layout.rooms.Count];
        for (int i = 0; i < distances.Length; i++) distances[i] = -1;
        if (from < 0 || from >= distances.Length) return distances;

        Queue<int> queue = new Queue<int>();
        distances[from] = 0;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int room = queue.Dequeue();
            foreach (int next in layout.rooms[room].neighbors)
            {
                if (next < 0 || distances[next] >= 0) continue;
                distances[next] = distances[room] + 1;
                queue.Enqueue(next);
            }
        }
        return distances;
    }

    static void Link(DungeonLayout layout, int a, int b, int directionFromA)
    {
        layout.rooms[a].neighbors[directionFromA] = b;
        layout.rooms[b].neighbors[Opposite(directionFromA)] = a;
    }

    static bool IsFree(DungeonLayout layout, Dictionary<long, int> occupied, int room, int direction)
    {
        DungeonRoom r = layout.rooms[room];
        return !occupied.ContainsKey(Key(r.x + StepX[direction], r.y + StepY[direction]));
    }

    static bool HasFreeDirection(DungeonLayout layout, Dictionary<long, int> occupied, int room)
    {
        for (int d = 0; d < DirectionCount; d++)
            if (IsFree(layout, occupied, room, d)) return true;
        return false;
    }

    static int PickFreeDirection(DungeonLayout layout, Dictionary<long, int> occupied, int room, Random rng)
    {
        List<int> free = new List<int>();
        for (int d = 0; d < DirectionCount; d++)
            if (IsFree(layout, occupied, room, d)) free.Add(d);
        return free.Count > 0 ? free[rng.Next(free.Count)] : -1;
    }

    static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

    static int GetByRank(int[] table, RankType rank, int fallback)
    {
        if (table == null || table.Length == 0) return fallback;
        return table[Math.Min(Math.Max((int)rank, 0), table.Length - 1)];
    }
}
