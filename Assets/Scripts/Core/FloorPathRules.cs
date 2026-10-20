using System;
using System.Collections.Generic;

// 던전 바닥 길찾기 (순수 규칙). walk[x, y] = 걸을 수 있는 칸 (바닥). 구멍·바닥 밖은 false
// 이웃 8칸으로 움직이되, 대각선은 맞닿은 두 칸이 모두 바닥일 때만 (구멍 모서리를 가로지르지 않게)
public static class FloorPathRules
{
    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
    const float Diagonal = 1.41421356f;

    public static bool IsWalkable(bool[,] walk, int x, int y) =>
        x >= 0 && y >= 0 && x < walk.GetLength(0) && y < walk.GetLength(1) && walk[x, y];

    /// <summary>start → goal 칸 경로 (start·goal 포함). 길이 없으면 false. 두 칸 모두 바닥이어야 함</summary>
    public static bool FindPath(bool[,] walk, int sx, int sy, int gx, int gy, List<(int x, int y)> result)
    {
        result.Clear();
        if (!IsWalkable(walk, sx, sy) || !IsWalkable(walk, gx, gy)) return false;
        if (sx == gx && sy == gy) { result.Add((sx, sy)); return true; }

        int w = walk.GetLength(0), h = walk.GetLength(1);
        var gScore = new float[w, h];
        var from = new int[w, h];
        var closed = new bool[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++) { gScore[x, y] = float.MaxValue; from[x, y] = -1; }

        var open = new MinHeap();
        gScore[sx, sy] = 0f;
        open.Push(Heuristic(sx, sy, gx, gy), sx + sy * w);

        while (open.Count > 0)
        {
            int cur = open.Pop();
            int cx = cur % w, cy = cur / w;
            if (closed[cx, cy]) continue;
            closed[cx, cy] = true;
            if (cx == gx && cy == gy) break;

            for (int i = 0; i < 8; i++)
            {
                int nx = cx + DX[i], ny = cy + DY[i];
                if (!IsWalkable(walk, nx, ny) || closed[nx, ny]) continue;
                bool diag = i >= 4;
                if (diag && (!IsWalkable(walk, cx + DX[i], cy) || !IsWalkable(walk, cx, cy + DY[i]))) continue;
                float g = gScore[cx, cy] + (diag ? Diagonal : 1f);
                if (g >= gScore[nx, ny]) continue;
                gScore[nx, ny] = g;
                from[nx, ny] = cur;
                open.Push(g + Heuristic(nx, ny, gx, gy), nx + ny * w);
            }
        }

        if (from[gx, gy] < 0) return false;
        int startIndex = sx + sy * w;
        for (int c = gx + gy * w; ; c = from[c % w, c / w])
        {
            result.Add((c % w, c / w));
            if (c == startIndex) break;
        }
        result.Reverse();
        return true;
    }

    // 대각선 이동을 허용한 거리 (옥타일)
    static float Heuristic(int x, int y, int gx, int gy)
    {
        int dx = Math.Abs(x - gx), dy = Math.Abs(y - gy);
        return Math.Max(dx, dy) + (Diagonal - 1f) * Math.Min(dx, dy);
    }

    /// <summary>(x, y)에서 가장 가까운 바닥 칸 (자기가 바닥이면 그대로). 바닥이 하나도 없으면 false</summary>
    public static bool NearestWalkable(bool[,] walk, int x, int y, out int nx, out int ny)
    {
        int w = walk.GetLength(0), h = walk.GetLength(1);
        nx = Math.Max(0, Math.Min(w - 1, x));
        ny = Math.Max(0, Math.Min(h - 1, y));
        if (walk[nx, ny]) return true;

        var seen = new bool[w, h];
        var queue = new Queue<(int, int)>();
        queue.Enqueue((nx, ny));
        seen[nx, ny] = true;
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            if (walk[cx, cy]) { nx = cx; ny = cy; return true; }
            for (int i = 0; i < 4; i++)
            {
                int ax = cx + DX[i], ay = cy + DY[i];
                if (ax < 0 || ay < 0 || ax >= w || ay >= h || seen[ax, ay]) continue;
                seen[ax, ay] = true;
                queue.Enqueue((ax, ay));
            }
        }
        return false;
    }

    // 작은 이진 힙 (우선순위가 낮은 것부터)
    class MinHeap
    {
        readonly List<(float p, int v)> items = new List<(float, int)>();
        public int Count => items.Count;

        public void Push(float p, int v)
        {
            items.Add((p, v));
            int i = items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (items[parent].p <= items[i].p) break;
                (items[parent], items[i]) = (items[i], items[parent]);
                i = parent;
            }
        }

        public int Pop()
        {
            int top = items[0].v;
            int last = items.Count - 1;
            items[0] = items[last];
            items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < items.Count && items[l].p < items[m].p) m = l;
                if (r < items.Count && items[r].p < items[m].p) m = r;
                if (m == i) break;
                (items[m], items[i]) = (items[i], items[m]);
                i = m;
            }
            return top;
        }
    }
}
