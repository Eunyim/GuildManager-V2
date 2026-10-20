using TMPro;
using UnityEngine;

/// <summary>
/// 2층 여관. 방 하나에 파티 하나가 묵고, 여관 레벨만큼 방이 열립니다.
/// 빈 방을 누르면 이미 만들어진 파티 중 하나를 배정하고, 파티가 있는 방을 누르면 파티 관리/방 비우기를 고릅니다.
/// 방 수와 배정 규칙은 GameManager가 정하고, 여기서는 표시와 클릭만 담당합니다.
/// </summary>
public class InnFloor : MonoBehaviour
{
    [System.Serializable]
    public class Room
    {
        public InteractiveObject interactive; // 방 문 오브젝트
        public TextMeshPro label;             // 문 위에 떠 있는 방 이름표
    }

    [Header("방 (순서대로 1번 방, 2번 방 ...)")]
    public Room[] rooms;

    [Header("여관 안내판 (현재 레벨 / 방 수 표시)")]
    public InteractiveObject signObject;
    public TextMeshPro signLabel;

    [Header("방 상태별 색")]
    public Color lockedColor   = new Color(0.3f, 0.3f, 0.3f, 1f);
    public Color emptyColor    = Color.white;
    public Color occupiedColor = new Color(1f, 0.85f, 0.45f, 1f);

    private int lastSignature;
    private bool hasSignature;

    void OnEnable()
    {
        hasSignature = false;
    }

    // 파티 생성/해체, 건축 공방에서의 여관 업그레이드가 어디서 일어나든 표시가 따라가도록 변화가 있을 때만 갱신
    void LateUpdate()
    {
        if (GameManager.Instance == null) return;

        int signature = ComputeSignature();
        if (hasSignature && signature == lastSignature) return;

        hasSignature = true;
        lastSignature = signature;
        Refresh();
    }

    int ComputeSignature()
    {
        GameManager gm = GameManager.Instance;
        int sig = gm.innLevel * 31 + gm.GetMaxParties();
        foreach (Party party in gm.partyList)
        {
            sig = sig * 31 + (party.partyName != null ? party.partyName.GetHashCode() : 0);
            sig = sig * 31 + party.members.Count;
            sig = sig * 31 + party.roomIndex;
        }
        return sig;
    }

    void Refresh()
    {
        GameManager gm = GameManager.Instance;
        int maxParties = gm.GetMaxParties();

        for (int i = 0; i < rooms.Length; i++)
        {
            Room room = rooms[i];
            if (room == null || room.interactive == null) continue;

            string title = $"{i + 1}번 방";
            string hover, status;
            Color color;

            if (i >= maxParties)
            {
                int needLevel = gm.GetInnLevelForRoom(i);
                status = "잠김";
                hover  = needLevel > 0 ? $"{title} (여관 Lv.{needLevel} 필요)" : $"{title} (잠김)";
                color  = lockedColor;
            }
            else if (gm.GetPartyInRoom(i) != null)
            {
                Party party = gm.GetPartyInRoom(i);
                status = party.partyName;
                hover  = $"{title}: {party.partyName} ({party.members.Count}명)";
                color  = occupiedColor;
            }
            else
            {
                status = "빈 방";
                hover  = $"{title}: 빈 방 (클릭해서 파티 배정)";
                color  = emptyColor;
            }

            room.interactive.SetObjectName(hover);
            room.interactive.SetBaseColor(color);
            if (room.label != null) room.label.text = $"{title}\n{status}";
        }

        RefreshSign(gm, maxParties);
    }

    void RefreshSign(GameManager gm, int maxParties)
    {
        int usedRooms = 0;
        for (int i = 0; i < maxParties; i++)
            if (gm.GetPartyInRoom(i) != null) usedRooms++;

        if (signLabel != null) signLabel.text = $"여관 Lv.{gm.innLevel}\n방 {usedRooms}/{maxParties}";
        if (signObject != null) signObject.SetObjectName($"여관 Lv.{gm.innLevel} (방 {maxParties}개 중 {usedRooms}개 사용)");
    }

    // 안내판 클릭 -> 업그레이드하는 곳 안내
    public void OnClickSign()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.ShowNotification("여관 업그레이드는 길드 밖 건축 공방에서 할 수 있습니다.");
    }

    // 방 문 클릭 (roomIndex: 0부터)
    public void OnClickRoom(int roomIndex)
    {
        GameManager gm = GameManager.Instance;
        LobbyManager lobby = LobbyManager.Instance;
        if (gm == null || lobby == null) return;

        if (roomIndex >= gm.GetMaxParties())
        {
            int needLevel = gm.GetInnLevelForRoom(roomIndex);
            lobby.ShowNotification(needLevel > 0
                ? $"잠긴 방입니다. 길드 밖 건축 공방에서 여관을 Lv.{needLevel}까지 업그레이드하세요."
                : "잠긴 방입니다.");
        }
        else if (gm.GetPartyInRoom(roomIndex) != null)
        {
            lobby.OpenRoomMenu(gm.GetPartyInRoom(roomIndex)); // 파티 관리 / 방 비우기
        }
        else
        {
            lobby.OpenRoomAssignPopup(roomIndex); // 빈 방 → 만들어 둔 파티 중에서 배정
        }
    }
}
