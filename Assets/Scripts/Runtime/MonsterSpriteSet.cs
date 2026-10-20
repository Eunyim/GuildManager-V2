using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 한 종(또는 모험가 한 명)의 도트 애니메이션 묶음: 동작(idle·walk·attack·death·skill) × 방향(side·front·back)
/// 모험가 것은 AdventurerSprites가 게임 중에 조립해 만듦
/// Tools/monster_art로 만든 시트를 Tools > 몬스터 도트 스프라이트 적용 이 잘라서 채움
/// side 그림은 오른쪽을 봄 (왼쪽은 BattleUnit이 크기 x를 뒤집어 처리)
/// </summary>
[CreateAssetMenu(menuName = "Guild Manager/Monster Sprite Set")]
public class MonsterSpriteSet : ScriptableObject
{
    public enum Anim { Idle, Walk, Attack, Death, Skill, Sit }   // 저장된 세트가 숫자로 기억하므로 뒤에만 추가 (Sit: 모닥불에 앉기, 모험가만)
    public enum Dir { Side, Front, Back }

    [System.Serializable]
    public class Clip
    {
        public Anim anim;
        public Dir dir;
        public float fps = 8f;
        public bool loop = true;
        public Sprite[] frames;
    }

    public string key;
    [Tooltip("대기 정면 그림의 실제 키 (프리팹 크기 1 기준 유닛). 킹 슬라임 크기 계산 등에 씀")]
    public float bodyHeight = 1f;
    [Tooltip("발 위치 (프리팹 로컬 유닛). 예전 그림의 아랫변과 같게 맞춘 값 — 다시 적용해도 그대로 쓰도록 저장")]
    public float feetY;
    public List<Clip> clips = new List<Clip>();

    public Clip Get(Anim anim, Dir dir)
    {
        Clip fallback = null;
        foreach (Clip c in clips)
        {
            if (c.anim != anim || c.frames == null || c.frames.Length == 0) continue;
            if (c.dir == dir) return c;
            if (fallback == null || c.dir == Dir.Side) fallback = c;
        }
        return fallback;
    }

    public bool Has(Anim anim)
    {
        foreach (Clip c in clips) if (c.anim == anim && c.frames != null && c.frames.Length > 0) return true;
        return false;
    }

    public Sprite FirstSprite(Anim anim = Anim.Idle, Dir dir = Dir.Side)
    {
        Clip c = Get(anim, dir);
        return c != null ? c.frames[0] : null;
    }
}
