using UnityEngine;

/// <summary>
/// 모험가 겉모습 무작위 생성 설정 (Resources/AdventurerLookSettings). 없으면 기본값을 씀
/// 헤어·옷 색은 이 목록에서 고른 뒤 살짝 흔들어(색상·밝기) 같은 색이 덜 겹치게 함
/// </summary>
[CreateAssetMenu(menuName = "Guild Manager/Adventurer Look Settings")]
public class AdventurerLookSettings : ScriptableObject
{
    [Range(0f, 1f)] public float femaleChance = 0.5f;

    [Tooltip("헤어 색 후보 (#rrggbb)")]
    public string[] hairColors =
    {
        "#2a2430", "#4a3428", "#7a4a2a", "#c89048", "#ecd078", "#f0e8d8", "#c8ccd8", "#d84a3a",
        "#e88aa8", "#8a5ad0", "#4a6ad8", "#3aa8c8", "#5aa860", "#e8a040",
    };

    [Tooltip("옷 색 후보 (#rrggbb)")]
    public string[] clothColors =
    {
        "#c83a3a", "#8a2a4a", "#3a6ac8", "#2a3a6a", "#3a9a5a", "#2e5a3a", "#e8d8b8", "#f0f0f0",
        "#5a3a8a", "#2a2a34", "#d88a2a", "#3aa8a8", "#7a5a3a", "#c8a050", "#6a7a8a", "#e07090",
    };

    [Tooltip("고른 색의 색상(0~1)을 이만큼 무작위로 흔듦")]
    [Range(0f, 0.1f)] public float hueJitter = 0.025f;
    [Tooltip("고른 색의 밝기를 이 비율만큼 무작위로 흔듦")]
    [Range(0f, 0.3f)] public float valueJitter = 0.08f;

    static AdventurerLookSettings cached;

    public static AdventurerLookSettings Get()
    {
        if (cached == null) cached = Resources.Load<AdventurerLookSettings>("AdventurerLookSettings");
        if (cached == null) cached = CreateInstance<AdventurerLookSettings>();
        return cached;
    }
}
