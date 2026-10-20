using System;

// 던전 방 모양 무작위 생성 (아이소메트릭, 순수 규칙)
// 결과는 MapGenerator와 같은 방향의 mask[x, y] (true = 바닥). n x n, 바깥 한 줄은 항상 비움
//
// 계산은 "땅 좌표"로 함: gx = 화면 가로(왼쪽 아군 → 오른쪽 적), gy = 화면 세로를 땅 기준으로 편 값.
// 땅에서 원이면 화면에서는 2:1 타원으로 보임. 판(마름모) 경계는 |gx| + |gy| <= A
//
// 항상 지키는 것:
//  - 왼쪽 끝(아군 출발)과 오른쪽 끝(적 출발) 근처, 그리고 둘을 잇는 가운데 통로는 바닥
//  - 바닥은 한 덩어리로 이어짐 (떨어진 조각은 지움)
public static class RoomShapeRules
{
    public static readonly string[] ShapeNames =
        { "마름모", "타원", "긴 홀", "십자", "쌍둥이 방", "팔각 투기장", "동굴", "비스듬한 홀", "기둥 방" };

    public struct Settings
    {
        public float edgeRoughness;   // 가장자리 칸을 깎을 확률 (0~0.5)
        public int maxPits;           // 안쪽 구덩이 최대 개수
        public int spineHalfWidth;    // 가운데 통로 반폭 (gy 기준)

        public static Settings Default => new Settings { edgeRoughness = 0.3f, maxPits = 3, spineHalfWidth = 1 };
    }

    public static bool[,] Generate(Random rng, int n, Settings st, out string shapeName)
    {
        int shape = rng.Next(ShapeNames.Length);
        shapeName = ShapeNames[shape];
        double A = (n - 1) / 2.0;
        var grid = new bool[n, n];   // [c, r] 템플릿 좌표 (r = 0 이 화면 왼쪽 위 변)

        // 모양별 수치
        double a = A * Lerp(0.85, 1.0, rng.NextDouble());
        double b = A * Lerp(0.35, 0.6, rng.NextDouble());
        double w = A * Lerp(0.18, 0.3, rng.NextDouble());
        double slope = (rng.NextDouble() < 0.5 ? -1 : 1) * Lerp(0.25, 0.45, rng.NextDouble());
        int noiseSeed = rng.Next();

        for (int c = 0; c < n; c++)
        {
            for (int r = 0; r < n; r++)
            {
                double gx = (c + r) / 2.0 - A;
                double gy = (c - r) / 2.0;
                bool inside;
                switch (shape)
                {
                    case 1: inside = Sq(gx / (a + 0.5)) + Sq(gy / (b + 1.5)) <= 1.0; break;
                    case 2: inside = Math.Abs(gy) <= b * 0.7; break;
                    case 3: inside = Math.Abs(gy) <= b * 0.55 || Math.Abs(gx) <= w + 0.5; break;
                    case 4:
                    {
                        double rr = A * 0.48;
                        inside = Sq(gx + A * 0.5) + Sq(gy) <= rr * rr || Sq(gx - A * 0.5) + Sq(gy) <= rr * rr || Math.Abs(gy) <= 1.6;
                        break;
                    }
                    case 5: inside = Math.Abs(gy) <= b + 1 && Math.Abs(gx) + Math.Abs(gy) <= a * 0.95; break;
                    case 6:
                    {
                        double fall = 1.0 - (Sq(gx / (A + 0.5)) + Sq(gy / (A * 0.62)));
                        inside = Noise(gx * 0.45, gy * 0.45, noiseSeed) * 0.55 + fall * 0.75 > 0.42;
                        break;
                    }
                    case 7: inside = Math.Abs(gy - slope * gx) <= b * 0.6; break;
                    default: inside = true; break;   // 0 마름모, 8 기둥 방(구덩이로 기둥 자리를 냄)
                }
                grid[c, r] = inside && c > 0 && r > 0 && c < n - 1 && r < n - 1;
            }
        }

        // 가장자리 울퉁불퉁하게 깎고 다듬기
        Roughen(grid, n, rng, st.edgeRoughness);
        for (int k = 0; k < 2; k++) Smooth(grid, n);

        // 기둥 방: 네 군데에 대칭 구덩이 / 나머지: 구덩이 0~maxPits개
        if (shape == 8) foreach (var (px, py) in new[] { (-0.45, 0.45), (-0.45, -0.45), (0.45, 0.45), (0.45, -0.45) }) Pit(grid, n, A, px * A, py * A, 1);
        else
        {
            int pits = rng.Next(st.maxPits + 1);
            for (int i = 0; i < pits; i++)
            {
                double gx = Lerp(-A * 0.6, A * 0.6, rng.NextDouble());
                double gy = (rng.NextDouble() < 0.5 ? -1 : 1) * Lerp(2.5, A * 0.5, rng.NextDouble());
                Pit(grid, n, A, gx, gy, rng.Next(2));
            }
        }

        // 꼭 남겨야 할 바닥: 가운데 통로 + 양 끝 출발 자리
        for (int c = 1; c < n - 1; c++)
        {
            for (int r = 1; r < n - 1; r++)
            {
                double gx = (c + r) / 2.0 - A;
                double gy = (c - r) / 2.0;
                bool spine = Math.Abs(gy) <= st.spineHalfWidth + 0.01 && Math.Abs(gx) <= A - 1.0;
                bool ends = Math.Abs(gx) >= A - 5.5 && Math.Abs(gy) <= 2.01 && Math.Abs(gx) + Math.Abs(gy) <= A - 0.5;
                if (spine || ends) grid[c, r] = true;
            }
        }
        KeepConnected(grid, n);

        // 템플릿 좌표 → MapGenerator mask[x, y] (y 뒤집음)
        var mask = new bool[n, n];
        for (int c = 0; c < n; c++)
            for (int r = 0; r < n; r++)
                mask[c, n - 1 - r] = grid[c, r];
        return mask;
    }

    static double Sq(double v) => v * v;
    static double Lerp(double a, double b, double t) => a + (b - a) * t;

    static void Roughen(bool[,] g, int n, Random rng, float p)
    {
        var cut = new bool[n, n];
        for (int c = 1; c < n - 1; c++)
            for (int r = 1; r < n - 1; r++)
                if (g[c, r] && (!g[c - 1, r] || !g[c + 1, r] || !g[c, r - 1] || !g[c, r + 1]) && rng.NextDouble() < p) cut[c, r] = true;
        for (int c = 0; c < n; c++)
            for (int r = 0; r < n; r++)
                if (cut[c, r]) g[c, r] = false;
    }

    // 주변 8칸 중 5칸 이상이 바닥이면 바닥, 3칸 이하면 빈칸 (삐죽한 조각과 좁은 틈 정리)
    static void Smooth(bool[,] g, int n)
    {
        var next = (bool[,])g.Clone();
        for (int c = 1; c < n - 1; c++)
        {
            for (int r = 1; r < n - 1; r++)
            {
                int k = 0;
                for (int dc = -1; dc <= 1; dc++)
                    for (int dr = -1; dr <= 1; dr++)
                        if ((dc != 0 || dr != 0) && g[c + dc, r + dr]) k++;
                if (k >= 5) next[c, r] = true;
                else if (k <= 3) next[c, r] = false;
            }
        }
        for (int c = 0; c < n; c++)
            for (int r = 0; r < n; r++)
                g[c, r] = next[c, r] && c > 0 && r > 0 && c < n - 1 && r < n - 1;
    }

    // 땅 좌표 (gx, gy)에 구덩이 (size 0 = 한 칸, 1 = 십자 다섯 칸)
    static void Pit(bool[,] g, int n, double A, double gx, double gy, int size)
    {
        int c = (int)Math.Round(gx + A + gy);
        int r = (int)Math.Round(gx + A - gy);
        for (int dc = -size; dc <= size; dc++)
            for (int dr = -size; dr <= size; dr++)
            {
                if (Math.Abs(dc) + Math.Abs(dr) > size) continue;
                int cc = c + dc, rr = r + dr;
                if (cc > 1 && rr > 1 && cc < n - 2 && rr < n - 2) g[cc, rr] = false;
            }
    }

    // 가운데에서 이어진 바닥만 남김
    static void KeepConnected(bool[,] g, int n)
    {
        int sc = (n - 1) / 2;
        var seen = new bool[n, n];
        var stack = new System.Collections.Generic.Stack<(int, int)>();
        if (!g[sc, sc]) return;
        stack.Push((sc, sc));
        seen[sc, sc] = true;
        while (stack.Count > 0)
        {
            var (c, r) = stack.Pop();
            foreach (var (dc, dr) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int cc = c + dc, rr = r + dr;
                if (cc < 0 || rr < 0 || cc >= n || rr >= n || seen[cc, rr] || !g[cc, rr]) continue;
                seen[cc, rr] = true;
                stack.Push((cc, rr));
            }
        }
        for (int c = 0; c < n; c++)
            for (int r = 0; r < n; r++)
                if (!seen[c, r]) g[c, r] = false;
    }

    // 부드러운 값 노이즈 (동굴 모양용)
    static double Noise(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        double a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed), c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
        return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
    }

    static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xffff) / 65535.0;
        }
    }
}
