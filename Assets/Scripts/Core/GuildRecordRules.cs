using System;
using System.Collections.Generic;

// 한 판의 기록 (엔딩 화면에 보여 줌). 저장 파일에 함께 저장됨
[Serializable]
public class GuildRecord
{
    public int hired;                                     // 고용한 모험가 수 (시작 모험가 제외)
    public int questsSucceeded;                           // 의뢰 성공 (직접 지휘 + 동시 파견)
    public int questsFailed;                              // 의뢰 실패 (전멸·도주·귀환 포함)
    public List<string> fallen = new List<string>();      // 한 번이라도 죽은 모험가 이름 (부활해도 남음)
    public bool endingSeen;                               // 데모 엔딩을 봤는가
    public int endingMonth;                               // 엔딩을 본 날짜
    public int endingWeek;
    public List<string> dragonSlayers = new List<string>(); // 드래곤을 쓰러뜨린 파티 (살아 돌아온 사람)
}

// 데모 엔딩 조건 (BattleManager 인스펙터에서 조정)
[Serializable]
public class DemoEndingSettings
{
    [UnityEngine.Tooltip("이 프리팹 이름의 보스를 쓰러뜨리고 의뢰에 성공하면 데모 엔딩 (새끼 드래곤은 이름이 달라 해당 없음)")]
    public string endingBossPrefabName = "Enemy_Dragon";
    [UnityEngine.Tooltip("엔딩을 한 번 본 뒤에도 다시 드래곤을 잡으면 또 보여 줄지")]
    public bool repeatAfterSeen = false;
}

// 기록·엔딩 규칙 (순수 로직)
public static class GuildRecordRules
{
    // 시작부터 지난 주 수 (1월 1주차 = 1주)
    public static int TotalWeeks(int month, int week) => Math.Max(1, (month - 1) * 4 + week);

    public static void NoteFallen(GuildRecord r, string name)
    {
        if (r == null || string.IsNullOrEmpty(name)) return;
        if (r.fallen == null) r.fallen = new List<string>();
        if (!r.fallen.Contains(name)) r.fallen.Add(name);
    }

    public static void NoteQuest(GuildRecord r, bool success)
    {
        if (r == null) return;
        if (success) r.questsSucceeded++;
        else r.questsFailed++;
    }

    // 쓰러뜨린 보스가 엔딩 보스인가 (프리팹 이름 기준, 복제본 "(Clone)" 꼬리는 무시)
    public static bool IsEndingBoss(DemoEndingSettings s, string bossPrefabName)
    {
        s = s ?? new DemoEndingSettings();
        if (string.IsNullOrEmpty(bossPrefabName) || string.IsNullOrEmpty(s.endingBossPrefabName)) return false;
        string n = bossPrefabName.Replace("(Clone)", "").Trim();
        return n == s.endingBossPrefabName;
    }

    // 이번 의뢰로 엔딩을 보여 줄지: 직접 지휘로 엔딩 보스를 쓰러뜨리고 의뢰에 성공 (동시 파견은 엔딩 아님)
    public static bool ShouldTriggerEnding(DemoEndingSettings s, GuildRecord r, bool questSucceeded, bool endingBossSlain)
    {
        s = s ?? new DemoEndingSettings();
        if (!questSucceeded || !endingBossSlain) return false;
        return r == null || !r.endingSeen || s.repeatAfterSeen;
    }
}
