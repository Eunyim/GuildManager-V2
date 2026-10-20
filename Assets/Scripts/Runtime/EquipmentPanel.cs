using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장비 창 (실행 중에 만듦). 모험가 상세 창의 [장비] 버튼으로 열림
///  왼쪽: 끼고 있는 무기·방어구 (강화·해제)   오른쪽: 길드 창고 (이 모험가가 낄 수 있는 것: 장착·판매)   아래: 상점 (D·C 등급 구입)
/// 수치·비용은 GameManager.equipment (EquipmentRules)
/// </summary>
public class EquipmentPanel : MonoBehaviour
{
    const float RefW = 1920f, RefH = 1080f;
    static readonly Color Gold      = new Color(0.95f, 0.8f, 0.42f);
    static readonly Color Body      = new Color(0.93f, 0.88f, 0.78f);
    static readonly Color Dim       = new Color(0.66f, 0.6f, 0.52f);
    static readonly Color Good      = new Color(0.49f, 0.99f, 0.49f);
    static readonly Color Bad       = new Color(1f, 0.45f, 0.4f);
    static readonly Color WindowCol = new Color(0.13f, 0.09f, 0.08f, 0.97f);
    static readonly Color CardCol   = new Color(0.22f, 0.16f, 0.13f, 1f);
    static readonly Color BorderCol = new Color(0.75f, 0.55f, 0.25f, 1f);
    static readonly Color BtnCol    = new Color(0.45f, 0.28f, 0.12f, 1f);
    static readonly Color BtnSubCol = new Color(0.28f, 0.22f, 0.2f, 1f);

    static EquipmentPanel instance;

    TMP_FontAsset font;
    Adventurer target;
    RectTransform window, slotArea, storageContent, shopArea;
    TextMeshProUGUI titleText, messageText, goldText;
    System.Action onChanged;

    public static void Open(Adventurer adv, TMP_FontAsset font, System.Action onChanged)
    {
        if (adv == null || GameManager.Instance == null) return;
        if (instance == null)
        {
            GameObject go = new GameObject("EquipmentPanel", typeof(RectTransform));
            instance = go.AddComponent<EquipmentPanel>();
            instance.font = font;
            instance.Build();
        }
        instance.gameObject.SetActive(true);
        instance.target = adv;
        instance.onChanged = onChanged;
        instance.messageText.text = "";
        instance.Refresh();
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // ─── 만들기 ──────────────────────────────────────────

    void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500; // 로비 팝업 위, 튜토리얼(5000) 아래
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)transform;

        Image dim = NewImage("Dim", root, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim.rectTransform);

        window = Box("Window", root, new Vector2(1400f, 860f), WindowCol, 4f);
        titleText = Text("Title", window, "", 40, Gold, TextAlignmentOptions.Center);
        Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f), new Vector2(1200f, 60f));
        goldText = Text("Gold", window, "", 26, Body, TextAlignmentOptions.Right);
        Place(goldText.rectTransform, new Vector2(1f, 1f), new Vector2(-190f, -48f), new Vector2(300f, 40f));

        Button close = MakeButton("Btn_Close", window, "닫기", BtnSubCol, () => { gameObject.SetActive(false); onChanged?.Invoke(); });
        Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(220f, 58f));

        // 왼쪽: 낀 장비
        TextMeshProUGUI h1 = Text("Hdr_Slots", window, "장착 중", 28, Gold, TextAlignmentOptions.Left);
        Place(h1.rectTransform, new Vector2(0f, 1f), new Vector2(330f, -110f), new Vector2(560f, 40f));
        slotArea = Empty("Slots", window);
        Place(slotArea, new Vector2(0f, 1f), new Vector2(330f, -300f), new Vector2(580f, 330f));

        // 오른쪽: 창고
        TextMeshProUGUI h2 = Text("Hdr_Storage", window, "길드 창고 (낄 수 있는 장비)", 28, Gold, TextAlignmentOptions.Left);
        Place(h2.rectTransform, new Vector2(0f, 1f), new Vector2(1010f, -110f), new Vector2(700f, 40f));
        RectTransform storageBox = Box("StorageBox", window, new Vector2(700f, 560f), CardCol, 2f);
        storageBox.anchorMin = storageBox.anchorMax = new Vector2(0f, 1f);
        storageBox.anchoredPosition = new Vector2(1010f, -410f);
        storageContent = MakeScroll(storageBox);

        // 아래 왼쪽: 상점
        TextMeshProUGUI h3 = Text("Hdr_Shop", window, "상점에서 사기", 28, Gold, TextAlignmentOptions.Left);
        Place(h3.rectTransform, new Vector2(0f, 1f), new Vector2(330f, -500f), new Vector2(560f, 40f));
        shopArea = Empty("Shop", window);
        Place(shopArea, new Vector2(0f, 1f), new Vector2(330f, -620f), new Vector2(580f, 200f));

        messageText = Text("Message", window, "", 24, Body, TextAlignmentOptions.Center);
        Place(messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(1300f, 36f));
    }

    // ─── 새로 그리기 ─────────────────────────────────────

    void Refresh()
    {
        GameManager gm = GameManager.Instance;
        EquipmentSettings s = gm.equipment;
        titleText.text = $"장비 — {target.name} <size=70%><color=#{Hex(Dim)}>{DisplayNames.JobToKorean(target.job)} · {target.rank} 랭크 Lv.{target.level}</color></size>";
        goldText.text = $"보유 {gm.gold:N0} G";

        Clear(slotArea);
        SlotCard(EquipSlot.Weapon, "무기", 0);
        SlotCard(EquipSlot.Armor, "방어구", 1);

        Clear(storageContent);
        var usable = gm.equipmentStorage.FindAll(e => EquipmentRules.CanEquip(e, target.job));
        usable.Sort((a, b) => b.grade != a.grade ? b.grade.CompareTo(a.grade) : b.plus.CompareTo(a.plus));
        if (usable.Count == 0)
        {
            TextMeshProUGUI none = Text("None", storageContent, "낄 수 있는 장비가 없습니다.\n보스·상자에서 얻거나 상점에서 살 수 있습니다.", 24, Dim, TextAlignmentOptions.Center);
            none.textWrappingMode = TextWrappingModes.Normal;
            LayoutElement le = none.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 120f;
        }
        foreach (EquipmentItem item in usable) StorageRow(item);
        int others = gm.equipmentStorage.Count - usable.Count;
        if (others > 0)
        {
            TextMeshProUGUI more = Text("Others", storageContent, $"다른 직업용 무기 {others}개는 그 직업의 장비 창에서 보입니다.", 20, Dim, TextAlignmentOptions.Center);
            more.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        }

        Clear(shopArea);
        int col = 0;
        for (int g = 0; g < 5; g++)
        {
            RankType grade = (RankType)g;
            foreach (EquipSlot slot in new[] { EquipSlot.Weapon, EquipSlot.Armor })
            {
                int price = EquipmentRules.ShopPrice(s, slot, grade);
                if (price <= 0) continue;
                EquipmentItem preview = new EquipmentItem { id = "preview", slot = slot, grade = grade, job = target.job };
                RankType gCopy = grade; EquipSlot sCopy = slot;
                Button b = MakeButton($"Buy_{slot}_{grade}", shopArea, $"{EquipmentRules.Name(s, preview)}\n<size=75%>{EquipmentRules.StatText(s, preview)} · {price:N0} G</size>", BtnSubCol,
                    () => { gm.TryBuyEquipment(sCopy, gCopy, target.job, out string msg); Show(msg); });
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize = 22;
                RectTransform br = (RectTransform)b.transform;
                br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
                br.pivot = new Vector2(0f, 1f);
                br.sizeDelta = new Vector2(284f, 84f);
                br.anchoredPosition = new Vector2((col % 2) * 296f, -(col / 2) * 94f);
                col++;
            }
        }
    }

    void SlotCard(EquipSlot slot, string label, int index)
    {
        GameManager gm = GameManager.Instance;
        EquipmentSettings s = gm.equipment;
        EquipmentItem item = target.GetEquipment(slot);
        RectTransform card = Box($"Card_{slot}", slotArea, new Vector2(580f, 150f), CardCol, 2f);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
        card.pivot = new Vector2(0.5f, 1f);
        card.anchoredPosition = new Vector2(0f, -index * 165f);

        bool empty = EquipmentRules.IsEmpty(item);
        string head = empty ? $"<color=#{Hex(Dim)}>{label} — 비어 있음</color>" : $"<color=#{Hex(Dim)}>{label}</color>  {GradeColored(item, EquipmentRules.Name(s, item))}";
        TextMeshProUGUI t = Text("Name", card, head, 27, Body, TextAlignmentOptions.TopLeft);
        Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(290f, -34f), new Vector2(540f, 40f));
        if (empty) return;
        TextMeshProUGUI st = Text("Stat", card, EquipmentRules.StatText(s, item), 23, Good, TextAlignmentOptions.TopLeft);
        Place(st.rectTransform, new Vector2(0f, 1f), new Vector2(290f, -72f), new Vector2(540f, 34f));

        if (EquipmentRules.CanUpgrade(s, item))
        {
            int cost = EquipmentRules.UpgradeGold(s, item);
            string mat = EquipmentRules.UpgradeMaterial(s, item);
            int need = EquipmentRules.UpgradeMaterialCount(item), have = gm.GetItemCount(mat);
            bool ok = gm.gold >= cost && have >= need;
            Button up = MakeButton("Btn_Upgrade", card, $"강화 +{item.plus + 1}\n<size=72%>{cost:N0} G · {mat} {have}/{need}</size>", ok ? BtnCol : BtnSubCol,
                () => { gm.TryUpgradeEquipment(item, out string msg); Show(msg); });
            up.GetComponentInChildren<TextMeshProUGUI>().fontSize = 21;
            Place((RectTransform)up.transform, new Vector2(0f, 0f), new Vector2(150f, 34f), new Vector2(270f, 56f));
        }
        else
        {
            TextMeshProUGUI max = Text("Max", card, "최대 강화", 22, Gold, TextAlignmentOptions.Center);
            Place(max.rectTransform, new Vector2(0f, 0f), new Vector2(150f, 34f), new Vector2(270f, 56f));
        }
        Button off = MakeButton("Btn_Unequip", card, "해제", BtnSubCol, () => { gm.Unequip(target, slot); Show($"{EquipmentRules.Name(s, item)}을(를) 창고에 넣었습니다."); });
        Place((RectTransform)off.transform, new Vector2(1f, 0f), new Vector2(-90f, 34f), new Vector2(140f, 56f));
    }

    void StorageRow(EquipmentItem item)
    {
        GameManager gm = GameManager.Instance;
        EquipmentSettings s = gm.equipment;
        RectTransform row = Box($"Row_{item.id}", storageContent, new Vector2(660f, 86f), WindowCol, 1f);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 86f;

        EquipmentItem current = target.GetEquipment(item.slot);
        string diff = DiffText(s, current, item);
        TextMeshProUGUI t = Text("Name", row, $"{GradeColored(item, EquipmentRules.Name(s, item))}\n<size=80%><color=#{Hex(Good)}>{EquipmentRules.StatText(s, item)}</color>  {diff}</size>", 24, Body, TextAlignmentOptions.Left);
        Place(t.rectTransform, new Vector2(0f, 0.5f), new Vector2(200f, 0f), new Vector2(380f, 80f));

        Button eq = MakeButton("Btn_Equip", row, "장착", BtnCol, () => { gm.Equip(target, item, out string msg); Show(msg); });
        Place((RectTransform)eq.transform, new Vector2(1f, 0.5f), new Vector2(-200f, 0f), new Vector2(120f, 54f));
        int price = EquipmentRules.SellPrice(s, item);
        Button sell = MakeButton("Btn_Sell", row, $"판매\n<size=70%>{price:N0} G</size>", BtnSubCol, () => { gm.SellEquipment(item, out string msg); Show(msg); });
        sell.GetComponentInChildren<TextMeshProUGUI>().fontSize = 21;
        Place((RectTransform)sell.transform, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), new Vector2(120f, 54f));
    }

    // 지금 낀 것과 비교 ("▲3" / "▼2")
    static string DiffText(EquipmentSettings s, EquipmentItem current, EquipmentItem item)
    {
        int d = item.slot == EquipSlot.Weapon
            ? EquipmentRules.Atk(s, item) - EquipmentRules.Atk(s, current)
            : EquipmentRules.Hp(s, item) - EquipmentRules.Hp(s, current);
        if (d == 0) return $"<color=#{Hex(Dim)}>(같음)</color>";
        return d > 0 ? $"<color=#{Hex(Good)}>▲{d}</color>" : $"<color=#{Hex(Bad)}>▼{-d}</color>";
    }

    static string GradeColored(EquipmentItem item, string text)
    {
        string[] c = { "DDDDDD", "8FD18F", "7FB8FF", "D59BFF", "FFC857" };
        return $"<color=#{c[Mathf.Clamp((int)item.grade, 0, 4)]}>{text}</color>";
    }

    void Show(string msg)
    {
        Refresh();
        messageText.text = msg;
        onChanged?.Invoke();
    }

    // ─── 도우미 ──────────────────────────────────────────

    static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject); }

    static RectTransform Empty(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static RectTransform Box(string name, Transform parent, Vector2 size, Color color, float border)
    {
        Image img = NewImage(name, parent, color);
        if (border > 0f)
        {
            Outline o = img.gameObject.AddComponent<Outline>();
            o.effectColor = BorderCol;
            o.effectDistance = new Vector2(border, -border);
        }
        img.rectTransform.sizeDelta = size;
        return img.rectTransform;
    }

    static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    RectTransform MakeScroll(RectTransform box)
    {
        RectTransform viewport = Empty("Viewport", box);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(10f, 10f); viewport.offsetMax = new Vector2(-10f, -10f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Empty("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        VerticalLayoutGroup v = content.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 8f;
        v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect sr = box.gameObject.AddComponent<ScrollRect>();
        sr.viewport = viewport; sr.content = content; sr.horizontal = false; sr.scrollSensitivity = 30f;
        sr.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.richText = true; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    Button MakeButton(string name, Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        Image img = NewImage(name, parent, color);
        Outline o = img.gameObject.AddComponent<Outline>();
        o.effectColor = BorderCol;
        o.effectDistance = new Vector2(2f, -2f);
        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        b.colors = cb;
        b.onClick.AddListener(onClick);
        TextMeshProUGUI t = Text("Label", img.transform, label, 26, Color.white, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        return b;
    }
}
