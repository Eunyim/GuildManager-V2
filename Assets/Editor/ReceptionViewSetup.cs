using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// [메뉴] Tools > 접수처 고용 화면 세팅
/// LobbyScene에 접수처 1인칭 고용 화면(ReceptionView)을 만들고 LobbyManager에 연결합니다.
/// 예전 뽑기 팝업(Popup_Recruit, SummonResultPanel)은 제거하고,
/// 접수처를 클릭하던 연결(OnClickRecruit)은 OnClickReception으로 바꿉니다.
/// 이미 만들어진 화면은 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class ReceptionViewSetup : Editor
{
    const string FONT_PATH = "Assets/Fonts/DungGeunMo SDF.asset";
    const string VIEW_NAME = "ReceptionView";
    const string UNDO_NAME = "접수처 고용 화면 세팅";
    const string BG_PATH   = "Assets/Art/Lobby/Reception/reception_bg_temp.png"; // 임시 배경 (교체 시 같은 경로에 덮어쓰기)
    const string BG_NAME   = "Bg_Image";

    static readonly string[] legacyObjects = { "SummonResultPanel", "Popup_Recruit" };
    const string LEGACY_METHOD = "OnClickRecruit";

    static TMP_FontAsset font;

    [MenuItem("Tools/접수처 고용 화면 세팅")]
    static void Setup()
    {
        LobbyManager lobby = Object.FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("[ReceptionViewSetup] LobbyManager를 찾을 수 없습니다. LobbyScene을 열고 실행해주세요.");
            return;
        }

        Canvas canvas = lobby.lobbyFloor != null
            ? lobby.lobbyFloor.GetComponentInParent<Canvas>(true)
            : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[ReceptionViewSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);

        RemoveLegacyObjects();

        Transform existing = canvas.transform.Find(VIEW_NAME);
        ReceptionView view = existing != null ? existing.GetComponent<ReceptionView>() : null;
        if (view == null) view = BuildView(canvas);
        else Debug.Log($"[ReceptionViewSetup] {VIEW_NAME} 이미 존재, 건너뜀");

        ApplyBackground(view.transform);

        Undo.RecordObject(lobby, UNDO_NAME);
        lobby.receptionView = view;
        EditorUtility.SetDirty(lobby);

        int rewired = RewireReceptionClicks(lobby);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        Debug.Log($"=== 접수처 고용 화면 세팅 완료! (클릭 연결 {rewired}개 교체) ===\n씬을 저장(Ctrl+S)하세요.");
    }

    // ═══════════════════════════════════════════
    // 예전 뽑기 UI 제거
    // ═══════════════════════════════════════════
    static void RemoveLegacyObjects()
    {
        foreach (string legacyName in legacyObjects)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t.name != legacyName) continue;
                Debug.Log($"[ReceptionViewSetup] 예전 뽑기 UI 제거: {legacyName}");
                Undo.DestroyObjectImmediate(t.gameObject);
            }
        }
    }

    // ═══════════════════════════════════════════
    // 접수처 클릭 연결 교체 (OnClickRecruit -> OnClickReception)
    // ═══════════════════════════════════════════
    static int RewireReceptionClicks(LobbyManager lobby)
    {
        UnityAction action = lobby.OnClickReception;
        int count = 0;

        foreach (InteractiveObject io in Object.FindObjectsByType<InteractiveObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            count += Rewire(io, io.onClickAction, action);
        foreach (Button btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            count += Rewire(btn, btn.onClick, action);

        return count;
    }

    static int Rewire(Object owner, UnityEventBase evt, UnityAction action)
    {
        if (evt == null) return 0;

        int count = 0;
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
        {
            if (evt.GetPersistentMethodName(i) != LEGACY_METHOD) continue;

            Undo.RecordObject(owner, UNDO_NAME);
            UnityEventTools.RegisterPersistentListener((UnityEvent)evt, i, action);
            EditorUtility.SetDirty(owner);
            count++;
        }
        return count;
    }

    // ═══════════════════════════════════════════
    // 화면 구성 (1920x1080 기준)
    // ═══════════════════════════════════════════
    static ReceptionView BuildView(Canvas canvas)
    {
        // 루트: 화면 전체를 덮는 길드 홀 벽 (뒤쪽 로비 클릭 차단)
        RectTransform root = CreateRect(VIEW_NAME, canvas.transform);
        Undo.RegisterCreatedObjectUndo(root.gameObject, UNDO_NAME);
        Stretch(root);
        AddImage(root, new Color(0.16f, 0.12f, 0.10f, 1f), true);

        // 정면의 길드 입구 + 바닥 (임시 배경)
        RectTransform door = CreateRect("Bg_Door", root);
        SetAnchored(door, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(520f, 720f));
        AddImage(door, new Color(0.08f, 0.06f, 0.05f, 1f), false);

        RectTransform floor = CreateRect("Bg_Floor", root);
        floor.anchorMin = new Vector2(0f, 0f);
        floor.anchorMax = new Vector2(1f, 0f);
        floor.pivot = new Vector2(0.5f, 0f);
        floor.anchoredPosition = Vector2.zero;
        floor.sizeDelta = new Vector2(0f, 330f);
        AddImage(floor, new Color(0.24f, 0.18f, 0.13f, 1f), false);

        // 모험가 카드 (드래그 대상)
        RectTransform cardRect = CreateRect("Card_Adventurer", root);
        SetAnchored(cardRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(520f, 640f));
        AddImage(cardRect, new Color(1f, 1f, 1f, 0f), true); // 투명한 드래그 판정 영역
        cardRect.gameObject.AddComponent<CanvasGroup>();
        SwipeCard card = cardRect.gameObject.AddComponent<SwipeCard>();
        card.canvas = canvas;

        RectTransform portraitRect = CreateRect("Img_Model", cardRect);
        Stretch(portraitRect);
        Image portrait = AddImage(portraitRect, Color.white, false);
        portrait.preserveAspect = true;

        // 접수대 (화면 하단, 모험가의 하반신을 가림 → 접수대 안쪽에서 보는 시점)
        RectTransform counter = CreateRect("Counter", root);
        counter.anchorMin = new Vector2(0f, 0f);
        counter.anchorMax = new Vector2(1f, 0f);
        counter.pivot = new Vector2(0.5f, 0f);
        counter.anchoredPosition = Vector2.zero;
        counter.sizeDelta = new Vector2(0f, 230f);
        AddImage(counter, new Color(0.42f, 0.26f, 0.13f, 1f), false);

        RectTransform counterTop = CreateRect("Counter_Top", counter);
        counterTop.anchorMin = new Vector2(0f, 1f);
        counterTop.anchorMax = new Vector2(1f, 1f);
        counterTop.pivot = new Vector2(0.5f, 1f);
        counterTop.anchoredPosition = Vector2.zero;
        counterTop.sizeDelta = new Vector2(0f, 28f);
        AddImage(counterTop, new Color(0.58f, 0.38f, 0.20f, 1f), false);

        // 드래그 방향 안내 (접수대 위 좌/우)
        CanvasGroup rejectHint = CreateHint("Hint_Reject", counter, new Vector2(0f, 0.5f), new Vector2(80f, -10f),
            "<< 돌려보내기", new Color(1f, 0.45f, 0.4f), TextAlignmentOptions.Left);
        CanvasGroup hireHint = CreateHint("Hint_Hire", counter, new Vector2(1f, 0.5f), new Vector2(-80f, -10f),
            "고용하기 >>", new Color(0.5f, 1f, 0.55f), TextAlignmentOptions.Right);

        TextMeshProUGUI remaining = CreateText("Txt_Remaining", counter, "대기 중인 모험가: 0명", 26, Color.white, TextAlignmentOptions.Center);
        SetAnchored(remaining.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(760f, 50f));

        // 모험가 정보 패널 (우상단)
        RectTransform info = CreateRect("Panel_Info", root);
        SetAnchored(info, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(480f, 0f));
        AddImage(info, new Color(0.08f, 0.06f, 0.05f, 0.9f), false);

        VerticalLayoutGroup layout = info.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 22, 22);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = info.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI rankText  = CreateText("Txt_Rank",  info, "D 랭크", 44, Color.white, TextAlignmentOptions.Left);
        TextMeshProUGUI nameText  = CreateText("Txt_Name",  info, "이름", 38, Color.white, TextAlignmentOptions.Left);
        TextMeshProUGUI jobText   = CreateText("Txt_Job",   info, "직업  Lv.1", 30, new Color(0.85f, 0.8f, 0.7f), TextAlignmentOptions.Left);
        TextMeshProUGUI statsText = CreateText("Txt_Stats", info, "HP\nATK\nSPD", 28, Color.white, TextAlignmentOptions.Left);
        TextMeshProUGUI costText  = CreateText("Txt_Cost",  info, "고용 비용: 100 G", 32, Color.white, TextAlignmentOptions.Left);

        // 좌상단: 로비로 돌아가기 + 보유 골드
        RectTransform closeRect = CreateRect("Btn_Close", root);
        SetAnchored(closeRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(240f, 70f));
        Image closeImg = AddImage(closeRect, new Color(0.42f, 0.26f, 0.13f, 1f), true);
        Button closeBtn = closeRect.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        TextMeshProUGUI closeText = CreateText("Txt_Label", closeRect, "로비로", 30, Color.white, TextAlignmentOptions.Center);
        Stretch(closeText.rectTransform);

        TextMeshProUGUI goldText = CreateText("Txt_Gold", root, "0 G", 34, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Left);
        SetAnchored(goldText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -130f), new Vector2(420f, 50f));

        // 중앙: 방문 모험가 없음 / 실패 메시지
        TextMeshProUGUI emptyText = CreateText("Txt_Empty", root, "오늘은 더 이상 길드를 찾아온 모험가가 없습니다.", 36,
            new Color(0.85f, 0.8f, 0.7f), TextAlignmentOptions.Center);
        SetAnchored(emptyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1000f, 120f));
        emptyText.gameObject.SetActive(false);

        TextMeshProUGUI messageText = CreateText("Txt_Message", root, "", 36, new Color(1f, 0.4f, 0.35f), TextAlignmentOptions.Center);
        SetAnchored(messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 60f));
        messageText.gameObject.SetActive(false);

        // 컴포넌트 연결
        ReceptionView view = root.gameObject.AddComponent<ReceptionView>();
        view.card = card;
        view.portraitImage = portrait;
        view.infoPanel = info.gameObject;
        view.rankText = rankText;
        view.nameText = nameText;
        view.jobText = jobText;
        view.statsText = statsText;
        view.costText = costText;
        view.goldText = goldText;
        view.remainingText = remaining;
        view.emptyMessage = emptyText.gameObject;
        view.messageText = messageText;
        view.hireHint = hireHint;
        view.rejectHint = rejectHint;

        UnityEventTools.AddPersistentListener(closeBtn.onClick, view.Close);

        // 팝업들과 같은 층에 두되, 마우스 라벨/툴팁보다는 아래 (열 때 SetAsLastSibling으로 다시 맨 앞으로 옴)
        root.SetAsLastSibling();
        root.gameObject.SetActive(false);

        Debug.Log($"[ReceptionViewSetup] {VIEW_NAME} 생성 완료");
        return view;
    }

    // ═══════════════════════════════════════════
    // 배경 이미지 적용 (BG_PATH에 이미지가 있을 때만)
    // 단색 임시 배경(입구/바닥/접수대)을 끄고, 길드 홀 이미지를 화면에 꽉 차게 깝니다.
    // ═══════════════════════════════════════════
    static void ApplyBackground(Transform root)
    {
        if (!System.IO.File.Exists(BG_PATH)) return;

        TextureImporter importer = AssetImporter.GetAtPath(BG_PATH) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(BG_PATH, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(BG_PATH) as TextureImporter;
        }
        if (importer != null && (importer.textureType != TextureImporterType.Sprite
            || importer.filterMode != FilterMode.Point
            || importer.textureCompression != TextureImporterCompression.Uncompressed))
        {
            importer.textureType        = TextureImporterType.Sprite;
            importer.spriteImportMode   = SpriteImportMode.Single;
            importer.filterMode         = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled      = false;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BG_PATH);
        if (sprite == null)
        {
            Debug.LogWarning($"[ReceptionViewSetup] 배경 스프라이트를 불러올 수 없습니다: {BG_PATH}");
            return;
        }

        Transform bg = root.Find(BG_NAME);
        if (bg == null)
        {
            RectTransform bgRect = CreateRect(BG_NAME, root);
            Undo.RegisterCreatedObjectUndo(bgRect.gameObject, UNDO_NAME);
            Stretch(bgRect);
            AddImage(bgRect, Color.white, false);

            // 화면 비율이 달라도 찌그러지지 않고 꽉 차게
            AspectRatioFitter fitter = bgRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bg = bgRect;
        }
        bg.SetAsFirstSibling();

        Image bgImage = bg.GetComponent<Image>();
        Undo.RecordObject(bgImage, UNDO_NAME);
        bgImage.sprite = sprite;
        EditorUtility.SetDirty(bgImage);

        AspectRatioFitter bgFitter = bg.GetComponent<AspectRatioFitter>();
        if (bgFitter != null)
        {
            Undo.RecordObject(bgFitter, UNDO_NAME);
            bgFitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            EditorUtility.SetDirty(bgFitter);
        }

        // 단색 임시 배경은 숨김
        foreach (string placeholder in new[] { "Bg_Door", "Bg_Floor", "Counter/Counter_Top" })
        {
            Transform t = root.Find(placeholder);
            if (t == null || !t.gameObject.activeSelf) continue;
            Undo.RecordObject(t.gameObject, UNDO_NAME);
            t.gameObject.SetActive(false);
        }

        // 접수대 색 판은 안내 문구가 읽히도록 반투명 띠로만 남김
        Transform counter = root.Find("Counter");
        Image counterImage = counter != null ? counter.GetComponent<Image>() : null;
        if (counterImage != null)
        {
            Undo.RecordObject(counterImage, UNDO_NAME);
            counterImage.color = new Color(0f, 0f, 0f, 0.5f);
            EditorUtility.SetDirty(counterImage);

            RectTransform counterRect = (RectTransform)counter;
            Undo.RecordObject(counterRect, UNDO_NAME);
            counterRect.sizeDelta = new Vector2(0f, 110f);
            EditorUtility.SetDirty(counterRect);
        }

        Debug.Log($"[ReceptionViewSetup] 배경 이미지 적용: {BG_PATH}");
    }

    // ═══════════════════════════════════════════
    // UI 생성 도우미
    // ═══════════════════════════════════════════
    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // anchor와 pivot을 같은 점으로 두고 위치/크기 지정
    static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static Image AddImage(RectTransform rect, Color color, bool raycastTarget)
    {
        Image img = rect.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    static CanvasGroup CreateHint(string name, Transform parent, Vector2 anchor, Vector2 position, string text, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI tmp = CreateText(name, parent, text, 40, color, alignment);
        tmp.richText = false; // "<<" 가 태그로 해석되지 않도록
        SetAnchored(tmp.rectTransform, anchor, anchor, position, new Vector2(420f, 70f));

        CanvasGroup group = tmp.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        return group;
    }
}
