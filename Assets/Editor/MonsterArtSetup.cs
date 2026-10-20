using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 몬스터 도트 시트 적용: Assets/Art/Monsters/{key}.png + {key}.json (Tools/monster_art/build.py로 생성)
//  1. 시트를 칸마다 잘라 스프라이트로 (이름 {key}_{동작}_{방향}_{번호}, 발 밑 기준점)
//  2. MonsterSpriteSet 에셋 (Assets/Art/Monsters/Sets/{key}.asset)
//  3. 적 프리팹: 몸 그림 교체 + 예전 무기·기수 그림 끄기 + MonsterAnimator 연결
// PPU = 48 × 프리팹 크기 → 프리팹마다 크기가 달라도 화면에서 1유닛 = 48도트로 같음
// 발 위치는 예전 그림의 아랫변과 같게 맞춤 (전투 위치·체력 바 위치가 그대로이도록). 여러 번 실행해도 결과는 같음
public static class MonsterArtSetup
{
    const string ArtDir = "Assets/Art/Monsters";
    const string SetDir = "Assets/Art/Monsters/Sets";
    const string PrefabDir = "Assets/Prefabs/Characters";
    const string BossSetForSlime = "king_slime"; // 보스방 슬라임이 킹 슬라임이 될 때 쓰는 그림

    [System.Serializable] class Row { public string anim; public string dir; public int count; public float fps; public bool loop; }
    [System.Serializable] class Meta
    {
        public string key; public string prefab; public int cellW; public int cellH; public int pivotX; public int pivotY;
        public int pxPerUnit; public int bodyPx; public Row[] rows;
    }

    [MenuItem("Tools/몬스터 도트 스프라이트 적용")]
    static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[MonsterArtSetup] 플레이 중에는 실행할 수 없습니다."); return; }
        if (!AssetDatabase.IsValidFolder(SetDir)) AssetDatabase.CreateFolder(ArtDir, "Sets");

        var sets = new Dictionary<string, MonsterSpriteSet>();
        var metas = new Dictionary<string, Meta>();
        foreach (string json in Directory.GetFiles(ArtDir, "*.json"))
        {
            Meta meta = JsonUtility.FromJson<Meta>(File.ReadAllText(json));
            if (meta == null || string.IsNullOrEmpty(meta.key)) continue;
            MonsterSpriteSet set = BuildSet(meta);
            if (set != null) { sets[meta.key] = set; metas[meta.key] = meta; }
        }
        AssetDatabase.SaveAssets();

        int done = 0;
        foreach (var kv in metas)
        {
            if (string.IsNullOrEmpty(kv.Value.prefab)) continue;
            MonsterSpriteSet boss = null;
            if (kv.Key == "slime") sets.TryGetValue(BossSetForSlime, out boss);
            if (ApplyPrefab(kv.Value.prefab, sets[kv.Key], boss)) done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[MonsterArtSetup] 몬스터 그림 {sets.Count}종, 프리팹 {done}개 적용 완료");
    }

    static string PrefabPath(string name) => $"{PrefabDir}/{name}.prefab";

    // 프리팹 원래 그림의 아랫변 (로컬 유닛, 크기 적용 전). 이미 바꾼 프리팹이면 세트에 저장해 둔 값
    static float FeetY(Meta meta, MonsterSpriteSet existing)
    {
        if (existing != null && existing.feetY != 0f) return existing.feetY;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(meta.prefab));
        SpriteRenderer sr = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null || prefab.GetComponent<MonsterAnimator>() != null) return -0.5f;
        return sr.sprite.bounds.min.y;
    }

    static float PrefabScale(string prefabName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prefabName));
        return prefab != null ? Mathf.Max(0.01f, Mathf.Abs(prefab.transform.localScale.y)) : 1f;
    }

    static MonsterSpriteSet BuildSet(Meta meta)
    {
        string png = $"{ArtDir}/{meta.key}.png";
        TextureImporter ti = AssetImporter.GetAtPath(png) as TextureImporter;
        if (ti == null) { Debug.LogWarning($"[MonsterArtSetup] {png} 없음"); return null; }

        string setPath = $"{SetDir}/{meta.key}.asset";
        MonsterSpriteSet set = AssetDatabase.LoadAssetAtPath<MonsterSpriteSet>(setPath);
        float feetY = FeetY(meta, set);
        float ppu = meta.pxPerUnit * (string.IsNullOrEmpty(meta.prefab) ? 1f : PrefabScale(meta.prefab));

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.spritePixelsPerUnit = ppu;
        ti.maxTextureSize = 4096;
        ti.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();
        var oldIds = new Dictionary<string, GUID>();
        foreach (SpriteRect r in dp.GetSpriteRects()) oldIds[r.name] = r.spriteID;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        int texH = tex.height;
        // 기준점: 발 밑에서 예전 그림 아랫변만큼 위 (월드 위치 = 예전과 같은 발 위치)
        float pivotUpPx = (meta.cellH - meta.pivotY) + (-feetY) * ppu;
        Vector2 pivot = new Vector2((float)meta.pivotX / meta.cellW, pivotUpPx / meta.cellH);

        var rects = new List<SpriteRect>();
        for (int r = 0; r < meta.rows.Length; r++)
        {
            Row row = meta.rows[r];
            for (int i = 0; i < row.count; i++)
            {
                string name = $"{meta.key}_{row.anim}_{row.dir}_{i}";
                rects.Add(new SpriteRect
                {
                    name = name,
                    rect = new Rect(i * meta.cellW, texH - (r + 1) * meta.cellH, meta.cellW, meta.cellH),
                    alignment = SpriteAlignment.Custom,
                    pivot = pivot,
                    spriteID = oldIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                });
            }
        }
        dp.SetSpriteRects(rects.ToArray());
        var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null) names.SetNameFileIdPairs(rects.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)));
        dp.Apply();
        ti.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(png).OfType<Sprite>().ToDictionary(s => s.name);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<MonsterSpriteSet>();
            AssetDatabase.CreateAsset(set, setPath);
        }
        set.key = meta.key;
        set.feetY = feetY;
        set.bodyHeight = meta.bodyPx / ppu;
        set.clips = new List<MonsterSpriteSet.Clip>();
        foreach (Row row in meta.rows)
        {
            var frames = new Sprite[row.count];
            for (int i = 0; i < row.count; i++) sprites.TryGetValue($"{meta.key}_{row.anim}_{row.dir}_{i}", out frames[i]);
            set.clips.Add(new MonsterSpriteSet.Clip
            {
                anim = ParseAnim(row.anim), dir = ParseDir(row.dir), fps = row.fps, loop = row.loop, frames = frames,
            });
        }
        EditorUtility.SetDirty(set);
        return set;
    }

    static MonsterSpriteSet.Anim ParseAnim(string s) => s switch
    {
        "walk" => MonsterSpriteSet.Anim.Walk, "attack" => MonsterSpriteSet.Anim.Attack, "death" => MonsterSpriteSet.Anim.Death,
        _ => MonsterSpriteSet.Anim.Idle,
    };

    static MonsterSpriteSet.Dir ParseDir(string s) => s switch
    {
        "front" => MonsterSpriteSet.Dir.Front, "back" => MonsterSpriteSet.Dir.Back, _ => MonsterSpriteSet.Dir.Side,
    };

    static bool ApplyPrefab(string prefabName, MonsterSpriteSet set, MonsterSpriteSet bossSet)
    {
        string path = PrefabPath(prefabName);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) { Debug.LogWarning($"[MonsterArtSetup] {path} 없음"); return false; }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
            if (sr == null) { Debug.LogWarning($"[MonsterArtSetup] {prefabName}에 SpriteRenderer가 없습니다."); return false; }
            sr.sprite = set.FirstSprite();
            sr.color = Color.white;   // 예전에는 같은 그림에 색을 입혀 구분했지만 이제 그림마다 색이 있음
            sr.flipX = false;

            // 예전 무기·기수 그림은 새 그림에 들어 있으므로 끔
            foreach (SpriteRenderer child in root.GetComponentsInChildren<SpriteRenderer>(true))
                if (child.gameObject != root) child.gameObject.SetActive(false);

            MonsterAnimator anim = root.GetComponent<MonsterAnimator>();
            if (anim == null) anim = root.AddComponent<MonsterAnimator>();
            anim.spriteSet = set;
            anim.bossSet = bossSet;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
