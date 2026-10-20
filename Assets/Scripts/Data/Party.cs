using System.Collections.Generic;
using UnityEngine;

public enum PartyState 
{ 
    Idle,       // 대기 중
    OnQuest,    // 파견 나감
    Resting     // 휴식 중 (나중을 위해)
}

[System.Serializable]
public class Party // 1. 클래스 이름이 'Party' 여야 합니다.
{
    public string partyName;
    public List<Adventurer> members = new List<Adventurer>();

    public PartyState state = PartyState.Idle; // 파티 현재 상태 변수

    // 묵고 있는 여관 방 번호 (0부터). -1이면 아직 방을 배정받지 못한 파티
    public int roomIndex = -1;

    // 케미는 파티가 아니라 모험가 사이의 관계로 관리 (GameManager.GetBond / GetChemistryLevel)

    // 생성자 (이 이름도 반드시 'Party' 여야 합니다)
    public Party(string name) 
    {
        this.partyName = name;
    }

    // 파티원 평균 레벨 (파티 레벨은 묵는 여관 방의 레벨: GameManager.GetPartyLevel)
    public int GetAverageMemberLevel()
    {
        if (members == null || members.Count == 0) return 0;

        int sumLevel = 0;
        foreach (Adventurer member in members)
        {
            sumLevel += member.level;
        }
        return sumLevel / members.Count; 
    }
}