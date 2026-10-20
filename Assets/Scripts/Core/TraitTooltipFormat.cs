using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 성격·특성 이름을 TMP 링크(<link="id">)로 감싸 마우스 툴팁 대상으로 만들고, 링크 id별 툴팁 문구를 모아 줍니다.
/// 표시(말풍선)는 Runtime/TextLinkTooltip이 담당합니다.
/// </summary>
public static class TraitTooltipFormat
{
    public const string TraumaColor = "#FF8A8A"; // 트라우마 이름 색
    public const string DefectColor = "#FFB060"; // 결함 이름 색
    const string NoteColor = "#B0B0B0";          // 툴팁 아래 보충 설명 색
    const string PersonalityLinkId = "personality";

    // 성격 이름 (링크). tips에 툴팁 문구를 넣음
    public static string PersonalityLink(Adventurer adv, Dictionary<string, string> tips)
    {
        string name = adv.GetTraitName();
        tips[PersonalityLinkId] = $"<b>성격 · {name}</b>\n{adv.GetTraitDescription()}";
        return $"<link=\"{PersonalityLinkId}\"><u>{name}</u></link>";
    }

    // category에 속하는 특성만 쉼표로 이어 붙임. 해당하는 것이 없으면 빈 문자열
    public static string TraitLinks(Adventurer adv, TraitCategory category, Func<TraitData, TraitCategory> categoryOf,
                                    Func<TraitData, string> note, Dictionary<string, string> tips)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < adv.traits.Count; i++)
        {
            TraitData t = adv.traits[i];
            if (t == null || categoryOf(t) != category) continue;

            string id = "trait" + i;
            string tip = $"<b>{t.traitName}</b>\n{t.description}";
            string extra = note != null ? note(t) : null;
            if (!string.IsNullOrEmpty(extra)) tip += $"\n<size=85%><color={NoteColor}>{extra}</color></size>";
            tips[id] = tip;

            if (sb.Length > 0) sb.Append(", ");
            string color = ColorOf(category);
            string label = color != null ? $"<color={color}>{t.traitName}</color>" : t.traitName;
            sb.Append($"<link=\"{id}\"><u>{label}</u></link>");
        }
        return sb.ToString();
    }

    // "특성: a, b", "트라우마: c", "결함: d" 줄들 (가진 것만). separator로 이어 붙임
    public static string TraitSections(Adventurer adv, Func<TraitData, TraitCategory> categoryOf,
                                       Func<TraitData, string> note, Dictionary<string, string> tips, string separator)
    {
        var parts = new List<string>();
        foreach (TraitCategory category in new[] { TraitCategory.Positive, TraitCategory.Trauma, TraitCategory.Defect })
        {
            string links = TraitLinks(adv, category, categoryOf, note, tips);
            if (links.Length == 0) continue;
            string color = ColorOf(category);
            string title = LabelOf(category);
            parts.Add(color != null ? $"<color={color}>{title}:</color> {links}" : $"{title}: {links}");
        }
        return string.Join(separator, parts);
    }

    static string LabelOf(TraitCategory c) => c == TraitCategory.Trauma ? "트라우마" : c == TraitCategory.Defect ? "결함" : "특성";
    static string ColorOf(TraitCategory c) => c == TraitCategory.Trauma ? TraumaColor : c == TraitCategory.Defect ? DefectColor : null;
}
