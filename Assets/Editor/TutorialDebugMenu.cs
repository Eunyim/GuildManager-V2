using UnityEditor;
using UnityEngine;

// 튜토리얼 확인용 메뉴 (플레이 중에만)
public static class TutorialDebugMenu
{
    [MenuItem("Tools/튜토리얼 처음부터 (플레이 중)")]
    static void Restart()
    {
        if (!Application.isPlaying || TutorialManager.Instance == null)
        {
            Debug.LogWarning("[튜토리얼] 플레이 중에 로비에서 실행하세요.");
            return;
        }
        TutorialManager.Instance.RestartTutorial();
        Debug.Log("[튜토리얼] 기본 튜토리얼을 처음부터 다시 시작합니다. (상황별 도움말 기록도 지움)");
    }

    [MenuItem("Tools/튜토리얼 건너뛰기 (플레이 중)")]
    static void Skip()
    {
        if (!Application.isPlaying || GameManager.Instance == null) return;
        GameManager.Instance.tutorial.Skip();
        Debug.Log("[튜토리얼] 기본 튜토리얼을 끝낸 것으로 처리했습니다.");
    }
}
