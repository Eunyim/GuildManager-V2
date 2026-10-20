using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모험가 도트 그림 조립기. 부위 그림(Resources/Adventurers, Tools/adventurer_art로 생성)을 겉모습(AdventurerLook)대로
/// 겹치고 색을 입혀 모험가 한 명의 애니메이션 묶음(MonsterSpriteSet)을 만듦. 같은 겉모습이면 다시 만들지 않음
///  - 층(뒤→먼 쪽→몸통→머리→가까운 쪽) 순서, 같은 층 안에서는 몸→하의→상의→헤어→무기 순서로 겹침
///  - 다시 칠할 픽셀(KEY: R=255, B=255, G=0/50/…/200)은 고른 색으로 만든 5단 색 띠로 바꿈
///  - 다 겹친 뒤 바깥 외곽선을 그음 (빛·효과 픽셀은 알파 254로 표시되어 외곽선 제외)
/// </summary>
public static class AdventurerSprites
{
    const string Dir = "Adventurers/";
    static readonly string[] JobNames = { "warrior", "mage", "archer", "healer", "rogue" }; // JobType 순서
    const int Subs = 5;

    // ─── manifest ───
    [System.Serializable] class Lists { public string[] body; public string[] hair; public string[] top; public string[] bottom; }
    [System.Serializable] class WeaponLists { public string[] warrior; public string[] rogue; public string[] archer; public string[] mage; public string[] healer; }
    [System.Serializable] class RowInfo { public string row; public string anim; public string dir; public string job; public int count; public float fps; public bool loop; }
    [System.Serializable] class Manifest
    {
        public int cellW, cellH, pivotX, pivotY, pxPerUnit;
        public Lists female, male;
        public WeaponLists weapons;
        public RowInfo[] rows;
    }
    [System.Serializable] class Tile { public string row; public int f, s, x, y, w, h, dx, dy; }
    [System.Serializable] class PartJson { public int atlasW, atlasH; public Tile[] tiles; }

    class Part
    {
        public int w, h;
        public Color32[] px;                       // 아틀라스 (Unity 순서: 아래 행부터)
        public Dictionary<string, Tile> tiles;     // "row|f|s"
    }

    static Manifest manifest;
    static readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();
    static readonly Dictionary<string, MonsterSpriteSet> sets = new Dictionary<string, MonsterSpriteSet>();

    static Manifest M
    {
        get
        {
            if (manifest == null)
            {
                TextAsset t = Resources.Load<TextAsset>(Dir + "manifest");
                manifest = t != null ? JsonUtility.FromJson<Manifest>(t.text) : null;
                if (manifest == null) Debug.LogError("[AdventurerSprites] Resources/Adventurers/manifest.json이 없습니다. Tools/adventurer_art/build.py를 실행하세요.");
            }
            return manifest;
        }
    }

    public static int PxPerUnit => M != null ? M.pxPerUnit : 48;

    static string[] Weapons(JobType job)
    {
        WeaponLists w = M.weapons;
        switch (job)
        {
            case JobType.Warrior: return w.warrior;
            case JobType.Mage: return w.mage;
            case JobType.Archer: return w.archer;
            case JobType.Healer: return w.healer;
            default: return w.rogue;
        }
    }

    // ─── 겉모습 만들기 ───────────────────────────────────────────
    static System.Random rng = new System.Random();

    public static AdventurerLook RandomLook(JobType job)
    {
        if (M == null) return null;
        AdventurerLookSettings st = AdventurerLookSettings.Get();
        var n = new AdventurerLookRules.Counts
        {
            femaleBody = M.female.body.Length, femaleHair = M.female.hair.Length, femaleTop = M.female.top.Length, femaleBottom = M.female.bottom.Length,
            maleBody = M.male.body.Length, maleHair = M.male.hair.Length, maleTop = M.male.top.Length, maleBottom = M.male.bottom.Length,
            weapons = Weapons(job).Length,
        };
        AdventurerLook look = AdventurerLookRules.Roll(rng, n, st.femaleChance, st.hairColors, st.clothColors);
        look.hairColor = Jitter(look.hairColor, st);
        look.topColor = Jitter(look.topColor, st);
        look.bottomColor = Jitter(look.bottomColor, st);
        return look;
    }

    static string Jitter(string hex, AdventurerLookSettings st)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color c)) return hex;
        Color.RGBToHSV(c, out float h, out float s, out float v);
        h = Mathf.Repeat(h + Random.Range(-st.hueJitter, st.hueJitter), 1f);
        v = Mathf.Clamp01(v * (1f + Random.Range(-st.valueJitter, st.valueJitter)));
        return "#" + ColorUtility.ToHtmlStringRGB(Color.HSVToRGB(h, s, v));
    }

    /// <summary>겉모습이 없거나 망가졌으면 새로 정함 (예전 저장 파일 등)</summary>
    public static AdventurerLook Ensure(Adventurer adv)
    {
        if (adv == null) return null;
        if (adv.look == null || !adv.look.IsValid) adv.look = RandomLook(adv.job);
        return adv.look;
    }

    // ─── 부위 읽기 ───────────────────────────────────────────────
    static Part LoadPart(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (parts.TryGetValue(id, out Part p)) return p;
        TextAsset bytes = Resources.Load<TextAsset>(Dir + id);
        TextAsset json = Resources.Load<TextAsset>(Dir + id + "_meta");
        if (bytes == null || json == null) { Debug.LogWarning($"[AdventurerSprites] 부위 '{id}' 없음"); parts[id] = null; return null; }
        PartJson pj = JsonUtility.FromJson<PartJson>(json.text);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(bytes.bytes);
        p = new Part { w = tex.width, h = tex.height, px = tex.GetPixels32(), tiles = new Dictionary<string, Tile>() };
        Object.Destroy(tex);
        foreach (Tile t in pj.tiles) p.tiles[$"{t.row}|{t.f}|{t.s}"] = t;
        Resources.UnloadAsset(bytes);
        parts[id] = p;
        return p;
    }

    // ─── 색 띠 (Tools/monster_art/core.py의 ramp와 같은 계산) ───
    static Color32[] Ramp(string hex)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color c)) c = Color.gray;
        Color.RGBToHSV(c, out float h, out float s, out float v);
        return new[]
        {
            HSV(Toward(h, 0.70f, 0.18f), s * 1.05f + 0.08f, v * 0.38f),
            HSV(Toward(h, 0.70f, 0.10f), s * 1.08f + 0.04f, v * 0.72f),
            (Color32)c,
            HSV(Toward(h, 0.14f, 0.08f), s * 0.85f, v * 1.18f + 0.04f),
            HSV(Toward(h, 0.14f, 0.14f), s * 0.45f, Mathf.Min(1f, v * 1.35f + 0.15f)),
        };
    }

    static float Toward(float h, float target, float amt)
    {
        float d = Mathf.Repeat(target - h + 0.5f, 1f) - 0.5f;
        return h + d * amt;
    }

    static Color32 HSV(float h, float s, float v) => (Color32)Color.HSVToRGB(Mathf.Repeat(h, 1f), Mathf.Clamp01(s), Mathf.Clamp01(v));

    // ─── 조립 ──────────────────────────────────────────────────
    /// <summary>
    /// 모험가 한 명의 그림 묶음. ppu: 1유닛당 텍스처 픽셀(프리팹 크기 반영), feetY: 발 위치(로컬 유닛, 0이면 기준점=발)
    /// </summary>
    public static MonsterSpriteSet Build(Adventurer adv, float ppu, float feetY = 0f)
    {
        AdventurerLook look = Ensure(adv);
        if (look == null || M == null) return null;
        string key = $"{look.Key(adv.job)}@{ppu:F2}/{feetY:F3}";
        if (sets.TryGetValue(key, out MonsterSpriteSet cachedSet) && cachedSet != null) return cachedSet;

        Lists L = look.female ? M.female : M.male;
        string[] ws = Weapons(adv.job);
        Part[] P =
        {
            LoadPart(Pick(L.body, look.body)), LoadPart(Pick(L.bottom, look.bottom)), LoadPart(Pick(L.top, look.top)),
            LoadPart(Pick(L.hair, look.hair)), LoadPart(Pick(ws, look.weapon)),
        };
        Color32[][] ramps = { null, Ramp(look.bottomColor), Ramp(look.topColor), Ramp(look.hairColor), null };
        string jobName = JobNames[(int)adv.job];

        int cw = M.cellW, ch = M.cellH;
        var frames = new List<(RowInfo row, int f, Color32[] px, RectInt box)>();
        foreach (RowInfo r in M.rows)
        {
            if (!string.IsNullOrEmpty(r.job) && r.job != jobName) continue;
            for (int f = 0; f < r.count; f++)
            {
                Color32[] canvas = ComposeFrame(P, ramps, r.row, f, cw, ch);
                frames.Add((r, f, canvas, Bounds(canvas, cw, ch)));
            }
        }
        MonsterSpriteSet set = Pack(frames, cw, ch, ppu, feetY, adv.name);
        sets[key] = set;
        return set;
    }

    static string Pick(string[] list, int i) => list != null && list.Length > 0 ? list[Mathf.Clamp(i, 0, list.Length - 1)] : null;

    static Color32[] ComposeFrame(Part[] P, Color32[][] ramps, string row, int f, int cw, int ch)
    {
        var px = new Color32[cw * ch];     // 위 행부터 (y=0이 위)
        var glow = new bool[cw * ch];
        for (int s = 0; s < Subs; s++)
        {
            for (int k = 0; k < P.Length; k++)
            {
                Part part = P[k];
                if (part == null || !part.tiles.TryGetValue($"{row}|{f}|{s}", out Tile t)) continue;
                Color32[] rmp = ramps[k];
                for (int ty = 0; ty < t.h; ty++)
                {
                    int srcRow = (part.h - 1 - (t.y + ty)) * part.w;
                    int dy = t.dy + ty;
                    if (dy < 0 || dy >= ch) continue;
                    for (int tx = 0; tx < t.w; tx++)
                    {
                        Color32 c = part.px[srcRow + t.x + tx];
                        if (c.a == 0) continue;
                        int dx = t.dx + tx;
                        if (dx < 0 || dx >= cw) continue;
                        if (rmp != null && c.r == 255 && c.b == 255 && c.g % 50 == 0 && c.g <= 200)
                        {
                            Color32 rc = rmp[c.g / 50];
                            c = new Color32(rc.r, rc.g, rc.b, c.a);
                        }
                        int i = dy * cw + dx;
                        glow[i] = c.a == 254;
                        c.a = 255;
                        px[i] = c;
                    }
                }
            }
        }
        // 바깥 외곽선 (옆 픽셀 색을 어둡게 섞음)
        var outPx = (Color32[])px.Clone();
        Color32 baseLine = new Color32(22, 12, 26, 255);
        for (int y = 0; y < ch; y++)
        {
            for (int x = 0; x < cw; x++)
            {
                int i = y * cw + x;
                if (px[i].a != 0) continue;
                int r = 0, g = 0, b = 0, n = 0;
                Acc(px, glow, cw, ch, x + 1, y, ref r, ref g, ref b, ref n);
                Acc(px, glow, cw, ch, x - 1, y, ref r, ref g, ref b, ref n);
                Acc(px, glow, cw, ch, x, y + 1, ref r, ref g, ref b, ref n);
                Acc(px, glow, cw, ch, x, y - 1, ref r, ref g, ref b, ref n);
                if (n == 0) continue;
                outPx[i] = new Color32((byte)(baseLine.r * 0.72f + r / n * 0.14f), (byte)(baseLine.g * 0.72f + g / n * 0.14f),
                                       (byte)(baseLine.b * 0.72f + b / n * 0.14f), 255);
            }
        }
        return outPx;
    }

    static void Acc(Color32[] px, bool[] glow, int cw, int ch, int x, int y, ref int r, ref int g, ref int b, ref int n)
    {
        if (x < 0 || y < 0 || x >= cw || y >= ch) return;
        int i = y * cw + x;
        if (px[i].a == 0 || glow[i]) return;
        r += px[i].r; g += px[i].g; b += px[i].b; n++;
    }

    static RectInt Bounds(Color32[] px, int cw, int ch)
    {
        int x0 = cw, y0 = ch, x1 = -1, y1 = -1;
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
                if (px[y * cw + x].a != 0)
                {
                    if (x < x0) x0 = x; if (x > x1) x1 = x;
                    if (y < y0) y0 = y; if (y > y1) y1 = y;
                }
        if (x1 < 0) return new RectInt(0, 0, 1, 1);
        return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    static MonsterSpriteSet Pack(List<(RowInfo row, int f, Color32[] px, RectInt box)> frames, int cw, int ch, float ppu, float feetY, string who)
    {
        // 프레임마다 그림이 있는 부분만 잘라 선반식으로 채움
        const int W = 1024;
        int x = 0, y = 0, rowH = 0;
        var pos = new Vector2Int[frames.Count];
        for (int i = 0; i < frames.Count; i++)
        {
            RectInt b = frames[i].box;
            if (x + b.width > W) { x = 0; y += rowH + 1; rowH = 0; }
            pos[i] = new Vector2Int(x, y);
            x += b.width + 1;
            rowH = Mathf.Max(rowH, b.height);
        }
        int H = Mathf.NextPowerOfTwo(y + rowH + 1);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Adv_" + who };
        var all = new Color32[W * H];
        for (int i = 0; i < frames.Count; i++)
        {
            RectInt b = frames[i].box;
            Color32[] px = frames[i].px;
            for (int yy = 0; yy < b.height; yy++)
            {
                int ty = H - 1 - (pos[i].y + yy);            // 텍스처는 아래 행부터
                int src = (b.y + yy) * cw + b.x;
                System.Array.Copy(px, src, all, ty * W + pos[i].x, b.width);
            }
        }
        tex.SetPixels32(all);
        tex.Apply(false, true);   // GPU로 올리고 CPU 사본은 버림

        MonsterSpriteSet set = ScriptableObject.CreateInstance<MonsterSpriteSet>();
        set.name = "Adv_" + who;
        set.key = who;
        var clips = new Dictionary<string, MonsterSpriteSet.Clip>();
        float feetUpPx = -feetY * ppu;
        for (int i = 0; i < frames.Count; i++)
        {
            var (row, f, _, b) = frames[i];
            Rect rect = new Rect(pos[i].x, H - pos[i].y - b.height, b.width, b.height);
            // 칸 기준점(발)을 잘라낸 사각형 기준으로 (위 행부터 → 아래부터)
            float pxv = M.pivotX - b.x;
            float pyv = (b.y + b.height) - M.pivotY + feetUpPx;
            Sprite sp = Sprite.Create(tex, rect, new Vector2(pxv / b.width, pyv / b.height), ppu, 0, SpriteMeshType.FullRect);
            sp.name = $"{who}_{row.row}_{f}";
            string ck = row.anim + "|" + row.dir;
            if (!clips.TryGetValue(ck, out MonsterSpriteSet.Clip clip))
            {
                clip = new MonsterSpriteSet.Clip { anim = ParseAnim(row.anim), dir = ParseDir(row.dir), fps = row.fps, loop = row.loop, frames = new Sprite[row.count] };
                clips[ck] = clip;
                set.clips.Add(clip);
            }
            clip.frames[f] = sp;
            if (row.anim == "idle" && row.dir == "front" && f == 0) set.bodyHeight = b.height / ppu;
        }
        set.feetY = feetY;
        return set;
    }

    static MonsterSpriteSet.Anim ParseAnim(string s) => s switch
    {
        "walk" => MonsterSpriteSet.Anim.Walk, "attack" => MonsterSpriteSet.Anim.Attack, "death" => MonsterSpriteSet.Anim.Death,
        "skill" => MonsterSpriteSet.Anim.Skill, "sit" => MonsterSpriteSet.Anim.Sit, _ => MonsterSpriteSet.Anim.Idle,
    };

    static MonsterSpriteSet.Dir ParseDir(string s) => s switch
    {
        "front" => MonsterSpriteSet.Dir.Front, "back" => MonsterSpriteSet.Dir.Back, _ => MonsterSpriteSet.Dir.Side,
    };

    // ─── 쓰는 곳 ────────────────────────────────────────────────
    /// <summary>전투 유닛: 직업 프리팹의 그림을 이 모험가의 조립 그림으로 바꾸고 애니메이션을 붙임</summary>
    public static void ApplyToUnit(GameObject unit, Adventurer adv)
    {
        if (unit == null || adv == null) return;
        SpriteRenderer sr = unit.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        // 발 위치는 예전 그림의 아랫변과 같게 (전투 위치·체력 바가 그대로이도록)
        float feetY = sr.sprite != null ? sr.sprite.bounds.min.y : 0f;
        float ppu = PxPerUnit * Mathf.Abs(unit.transform.localScale.y);
        MonsterSpriteSet set = Build(adv, ppu, feetY);
        if (set == null) return;

        foreach (SpriteRenderer child in unit.GetComponentsInChildren<SpriteRenderer>(true))
            if (child.gameObject != unit) child.gameObject.SetActive(false);   // 예전 무기 그림 (새 그림에 들어 있음)
        sr.color = Color.white;
        sr.flipX = false;
        sr.sprite = set.FirstSprite(MonsterSpriteSet.Anim.Idle, MonsterSpriteSet.Dir.Side);
        MonsterAnimator anim = unit.GetComponent<MonsterAnimator>();
        if (anim == null) anim = unit.AddComponent<MonsterAnimator>();
        anim.spriteSet = set;
    }

    /// <summary>초상화·UI용 (기준점 = 발, 1유닛 = 48픽셀)</summary>
    public static MonsterSpriteSet ForUI(Adventurer adv) => Build(adv, PxPerUnit, 0f);

    public static Sprite Portrait(Adventurer adv)
    {
        MonsterSpriteSet set = ForUI(adv);
        return set != null ? set.FirstSprite(MonsterSpriteSet.Anim.Idle, MonsterSpriteSet.Dir.Front) : null;
    }
}
