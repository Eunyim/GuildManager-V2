using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// [메뉴] Tools > 던전 모닥불 세팅
/// DungeonScene에 방과 방 사이의 모닥불 휴식 화면([다음 방으로] / [귀환])과 맵 위의 모닥불(임시 그래픽)을 만들고
/// BattleManager에 연결합니다. 이미 만들어져 있으면 건너뛰므로 다시 실행해도 안전합니다.
/// </summary>
public class DungeonCampfireSetup : Editor
{
    const string PANEL_NAME  = "Panel_Campfire";
    const string OBJECT_NAME = "Campfire";
    const string UNDO_NAME   = "던전 모닥불 세팅";
    const float  PANEL_WIDTH = 900f;

    [MenuItem("Tools/던전 모닥불 세팅")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("[DungeonCampfireSetup] 플레이 모드에서는 실행할 수 없습니다. 플레이를 끄고 다시 실행해주세요.");
            return;
        }

        BattleManager bm = Object.FindFirstObjectByType<BattleManager>();
        if (bm == null)
        {
            Debug.LogError("[DungeonCampfireSetup] BattleManager를 찾을 수 없습니다. DungeonScene을 열고 실행해주세요.");
            return;
        }

        // 승리/패배 패널이 있는 캔버스를 사용
        Canvas canvas = bm.victoryPanel != null ? bm.victoryPanel.GetComponentInParent<Canvas>(true) : null;
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[DungeonCampfireSetup] Canvas를 찾을 수 없습니다.");
            return;
        }
        canvas = canvas.rootCanvas;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LobbyFacilitySetup.FONT_PATH);

        Undo.RecordObject(bm, UNDO_NAME);

        // 1. 휴식 화면
        Transform panel = canvas.transform.Find(PANEL_NAME);
        if (panel == null)
        {
            Transform window = LobbyFacilitySetup.CreateMenuWindow(canvas, PANEL_NAME, "모닥불",
                new Color(0.22f, 0.14f, 0.09f, 0.95f), font, out _);
            RectTransform windowRect = (RectTransform)window;
            windowRect.sizeDelta = new Vector2(PANEL_WIDTH, windowRect.sizeDelta.y);
            window.GetComponent<VerticalLayoutGroup>().spacing = 16f;

            panel = window.parent;
            // 뒤의 던전이 보이도록 배경을 옅게
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);

            TextMeshProUGUI status = LobbyFacilitySetup.CreateMenuText("Txt_Status", window, "파티 상태", 28, font);
            status.alignment = TextAlignmentOptions.Center;
            bm.campfireStatusText = status;

            LobbyFacilitySetup.CreateMenuButton("Btn_Continue", window, "다음 방으로", LobbyFacilitySetup.MenuButtonColor, font, bm.OnClickCampfireContinue);
            LobbyFacilitySetup.CreateMenuButton("Btn_Return", window, "귀환 (전리품만 챙겨 돌아가기)", LobbyFacilitySetup.MenuCloseColor, font, bm.OnClickCampfireReturn);

            Debug.Log($"[DungeonCampfireSetup] {PANEL_NAME} 생성 완료");
        }
        else Debug.Log($"[DungeonCampfireSetup] {PANEL_NAME} 이미 존재, 건너뜀");

        bm.campfirePanel = panel.gameObject;
        if (bm.campfireStatusText == null)
            bm.campfireStatusText = panel.Find("Window/Txt_Status")?.GetComponent<TextMeshProUGUI>();

        // 2. 맵 위의 모닥불 (임시 그래픽): 아군 대기 위치와 적 등장 위치의 중간쯤
        GameObject campfire = GameObject.Find(OBJECT_NAME);
        if (campfire == null && bm.campfireObject == null)
        {
            campfire = new GameObject(OBJECT_NAME);
            Undo.RegisterCreatedObjectUndo(campfire, UNDO_NAME);

            Vector3 allySide  = bm.playerSpawnPoint != null ? bm.playerSpawnPoint.position : new Vector3(-3f, 0f, 0f);
            Vector3 enemySide = bm.enemySpawnPoint  != null ? bm.enemySpawnPoint.position  : new Vector3(3f, 0f, 0f);
            Vector3 pos = Vector3.Lerp(allySide, enemySide, 0.4f);
            pos.z = 0f;
            campfire.transform.position = pos;
            campfire.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            SpriteRenderer sr = campfire.AddComponent<SpriteRenderer>();
            sr.sprite = LobbyFacilitySetup.CreatePlaceholderSprite("Campfire", new Color(1f, 0.55f, 0.15f));
            sr.sortingOrder = LobbyFacilitySetup.GetSortingOrder(pos.y);

            campfire.SetActive(false); // 쉬는 동안에만 보임
            Debug.Log($"[DungeonCampfireSetup] {OBJECT_NAME} 생성 완료 (위치 {pos})");
        }
        if (bm.campfireObject == null) bm.campfireObject = campfire;

        EditorUtility.SetDirty(bm);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bm.gameObject.scene);
        Debug.Log("=== 던전 모닥불 세팅 완료! ===\n씬을 저장(Ctrl+S)하세요.");
    }
}
