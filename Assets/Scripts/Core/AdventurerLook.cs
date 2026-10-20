using System;

// 모험가 겉모습 (저장 파일에 들어감). 부위 번호는 Resources/Adventurers/manifest.json 목록의 순서
// 색은 "#rrggbb". 헤어·상의·하의만 색을 바꿀 수 있고, 몸(피부·눈)과 무기는 그림 그대로
[Serializable]
public class AdventurerLook
{
    public bool female;
    public int body;
    public int hair;
    public string hairColor;
    public int top;
    public string topColor;
    public int bottom;
    public string bottomColor;
    public int weapon;

    public bool IsValid => !string.IsNullOrEmpty(hairColor) && !string.IsNullOrEmpty(topColor) && !string.IsNullOrEmpty(bottomColor);

    public AdventurerLook Clone() => (AdventurerLook)MemberwiseClone();

    /// <summary>조립 결과를 다시 쓸 때 쓰는 열쇠 (같은 겉모습 + 직업이면 같은 그림)</summary>
    public string Key(JobType job) =>
        $"{(female ? 'f' : 'm')}{body}-{hair}{hairColor}-{top}{topColor}-{bottom}{bottomColor}-{job}{weapon}";
}

// 겉모습 무작위 조합 (순수 규칙). 부위 개수와 색 목록은 호출하는 쪽이 넘김
public static class AdventurerLookRules
{
    public struct Counts
    {
        public int femaleBody, femaleHair, femaleTop, femaleBottom;
        public int maleBody, maleHair, maleTop, maleBottom;
        public int weapons;
    }

    public static AdventurerLook Roll(Random rng, Counts n, float femaleChance, string[] hairColors, string[] clothColors)
    {
        bool female = rng.NextDouble() < femaleChance;
        AdventurerLook look = new AdventurerLook
        {
            female = female,
            body = Pick(rng, female ? n.femaleBody : n.maleBody),
            hair = Pick(rng, female ? n.femaleHair : n.maleHair),
            top = Pick(rng, female ? n.femaleTop : n.maleTop),
            bottom = Pick(rng, female ? n.femaleBottom : n.maleBottom),
            weapon = Pick(rng, n.weapons),
            hairColor = PickColor(rng, hairColors),
            topColor = PickColor(rng, clothColors),
        };
        // 위아래가 같은 색이면 한 번 더 뽑아 봄 (단색 옷처럼 보이지 않게)
        look.bottomColor = PickColor(rng, clothColors);
        if (look.bottomColor == look.topColor) look.bottomColor = PickColor(rng, clothColors);
        return look;
    }

    static int Pick(Random rng, int count) => count > 0 ? rng.Next(count) : 0;

    static string PickColor(Random rng, string[] colors) =>
        colors != null && colors.Length > 0 ? colors[rng.Next(colors.Length)] : "#808080";
}
