using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System.Reflection;

/// <summary>
/// [메뉴] Tools > 로비 포인트앤클릭 세팅
/// 실행하면 LobbyScene에 시설 핫스팟 + 라벨 UI를 자동 생성하고,
/// BottomMenu를 비활성화합니다.
/// </summary>
public class LobbyPointClickSetup : Editor
{
    // 시설 정의: (오브젝트 이름, 표시 이름, LobbyManager 메서드 이름, 대략적 앵커 위치)
    static readonly (string id, string label, string method, Vector2 anchorPos, Vector2 size)[] facilities =
    {
        ("Hotspot_Reception",   "접수처",       "OnClickReception",         new Vector2(0.12f, 0.55f), new Vector2(200, 180)),
        ("Hotspot_QuestBoard",  "퀘스트 보드",   "OnClickquestPopup",        new Vector2(0.35f, 0.60f), new Vector2(180, 160)),
        ("Hotspot_Inn",         "여관",         "OnClickOpenPartyManage",   new Vector2(0.75f, 0.55f), new Vector2(200, 180)),
        ("Hotspot_Manage",      "명단 관리",     "OnClickManage",            new Vector2(0.55f, 0.70f), new Vector2(180, 160)),
        ("Hotspot_Temple",      "신전",         "OnClickOpenSpiritStonePopup", new Vector2(0.88f, 0.60f), new Vector2(180, 160)),
    };

    [MenuItem("Tools/로비 포인트앤클릭 세팅")]
    static void Setup()
    {
        // 1. Canvas 찾기
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Canvas를 찾을 수 없습니다! LobbyScene을 열고 실행해주세요.");
            return;
        }

        // 2. LobbyManager 찾기
        LobbyManager lobby = FindFirstObjectByType<LobbyManager>();
        if (lobby == null)
        {
            Debug.LogError("LobbyManager를 찾을 수 없습니다!");
            return;
        }

        // 3. BottomMenu 비활성화
        Transform bottomMenu = canvas.transform.Find("BottomMenu");
        if (bottomMenu != null)
        {
            bottomMenu.gameObject.SetActive(false);
            EditorUtility.SetDirty(bottomMenu.gameObject);
            Debug.Log("BottomMenu 비활성화 완료");
        }

        // 4. 핫스팟 부모 생성 (BackGround Image 바로 위, Lobby_Floor 바로 아래)
        Transform hotspotParent = canvas.transform.Find("Hotspot_Parent");
        if (hotspotParent == null)
        {
            GameObject parentGO = new GameObject("Hotspot_Parent");
            parentGO.transform.SetParent(canvas.transform, false);
            RectTransform parentRect = parentGO.AddComponent<RectTransform>();
            // 전체 화면을 덮도록
            parentRect.anchorMin = Vector2.zero;
            parentRect.anchorMax = Vector2.one;
            parentRect.offsetMin = Vector2.zero;
            parentRect.offsetMax = Vector2.zero;
            hotspotParent = parentGO.transform;

            // BackGround Image 바로 뒤에 놓기 (sibling index 1)
            hotspotParent.SetSiblingIndex(1);
        }

        // 5. 시설 핫스팟 생성
        foreach (var fac in facilities)
        {
            Transform existing = hotspotParent.Find(fac.id);
            if (existing != null)
            {
                Debug.Log($"{fac.id} 이미 존재, 건너뜀");
                continue;
            }

            GameObject hotspot = new GameObject(fac.id);
            hotspot.transform.SetParent(hotspotParent, false);

            // RectTransform 설정
            RectTransform rect = hotspot.AddComponent<RectTransform>();
            rect.anchorMin = fac.anchorPos;
            rect.anchorMax = fac.anchorPos;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = fac.size;

            // Image (반투명 — 위치 확인용, 나중에 알파를 0으로 하거나 스프라이트 교체)
            Image img = hotspot.AddComponent<Image>();
            img.color = new Color(1f, 1f, 0.5f, 0.25f); // 반투명 노란색 (위치 확인용)
            img.raycastTarget = true;

            // InteractiveObject 컴포넌트
            InteractiveObject interactive = hotspot.AddComponent<InteractiveObject>();
            interactive.objectName = fac.label;
            interactive.highlightBrightness = 0.3f;
            interactive.onClickAction = new UnityEvent(); // 명시적 초기화

            // UnityEvent에 LobbyManager 메서드 연결
            MethodInfo method = typeof(LobbyManager).GetMethod(fac.method, BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                UnityAction action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), lobby, method);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(interactive.onClickAction, action);
            }
            else
            {
                Debug.LogWarning($"{fac.method} 메서드를 찾을 수 없습니다!");
            }

            EditorUtility.SetDirty(hotspot);
            Debug.Log($"{fac.label} 핫스팟 생성 완료");
        }

        // 6. 오브젝트 라벨 UI 생성
        Transform labelPanel = canvas.transform.Find("ObjectLabelPanel");
        if (labelPanel == null)
        {
            // 패널 생성
            GameObject panelGO = new GameObject("ObjectLabelPanel");
            panelGO.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelGO.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(200, 40);
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(0, 0);
            panelRect.pivot = new Vector2(0, 0);

            // 배경 이미지 (어두운 반투명)
            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.08f, 0.05f, 0.85f);
            panelImg.raycastTarget = false;

            // ContentSizeFitter로 텍스트에 맞게 자동 크기 조절
            var fitter = panelGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 패딩용 HorizontalLayoutGroup
            var layout = panelGO.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;

            // 텍스트 생성
            GameObject textGO = new GameObject("Txt_Label");
            textGO.transform.SetParent(panelGO.transform, false);

            RectTransform textRect = textGO.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(180, 30);

            TextMeshProUGUI labelText = textGO.AddComponent<TextMeshProUGUI>();
            labelText.text = "시설 이름";
            labelText.fontSize = 20;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;

            // 맨 앞에 배치 & 기본 비활성화
            panelGO.transform.SetAsLastSibling();
            panelGO.SetActive(false);

            // LobbyManager에 연결
            lobby.objectLabelPanel = panelGO;
            lobby.objectLabelText = labelText;

            EditorUtility.SetDirty(panelGO);
            EditorUtility.SetDirty(lobby);
            Debug.Log("오브젝트 라벨 UI 생성 완료");

            labelPanel = panelGO.transform;
        }
        else
        {
            // 이미 있으면 LobbyManager 연결만 확인
            lobby.objectLabelPanel = labelPanel.gameObject;
            TextMeshProUGUI existingText = labelPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (existingText != null) lobby.objectLabelText = existingText;
            EditorUtility.SetDirty(lobby);
        }

        // 7. Tooltip 패널도 맨 앞으로
        if (lobby.tooltipPanel != null)
            lobby.tooltipPanel.transform.SetAsLastSibling();

        // 씬 변경 표시
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("=== 로비 포인트앤클릭 세팅 완료! ===\n" +
                  "각 핫스팟(노란 반투명 영역)을 배경에 맞게 위치/크기를 조정하세요.\n" +
                  "위치가 확정되면 Image의 Alpha를 0으로 바꾸거나 시설 스프라이트를 넣으세요.");
    }
}
