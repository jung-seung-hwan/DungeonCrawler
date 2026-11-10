using System.Collections.Generic;
using UnityEngine;

public enum EquipSlot { Weapon, Armor, Ring }

public enum Rarity { Common, Rare, Legendary }

public enum Affix { Power, Vitality, Keen, Lifesteal, Thorns, Regen, Fortune }

// 무기·갑옷·반지 한 개. 기본 수치에 특수 옵션(Affix)이 붙는다.
public class Equipment
{
    public EquipSlot Slot;
    public Rarity Rarity;
    public string Name;
    public int Attack;
    public int Defense;
    public int MaxHp;
    public float Crit;
    public float Lifesteal;
    public float Thorns;
    public float GoldBonus;
    public bool Regen;

    // 장비끼리 비교할 때 쓰는 대략적인 가치 점수.
    public int Score =>
        Attack * 3 + Defense * 4 + MaxHp / 2 + (int)(Crit * 60) + (int)(Lifesteal * 60) +
        (int)(Thorns * 40) + (int)(GoldBonus * 20) + (Regen ? 8 : 0);

    public int SellValue => 5 + Score / 2;

    public string RarityHex => Rarity switch
    {
        Rarity.Legendary => "#ffb340",
        Rarity.Rare => "#66bbff",
        _ => "#dddddd",
    };

    public Color RarityColor => Rarity switch
    {
        Rarity.Legendary => new Color(1f, 0.7f, 0.25f),
        Rarity.Rare => new Color(0.45f, 0.75f, 1f),
        _ => Color.white,
    };

    public string ColoredName => $"<color={RarityHex}>{Name}</color>";

    public string Describe()
    {
        var parts = new List<string>();
        if (Attack > 0) parts.Add($"{Loc.T("공격", "ATK")} +{Attack}");
        if (Defense > 0) parts.Add($"{Loc.T("방어", "DEF")} +{Defense}");
        if (MaxHp > 0) parts.Add($"{Loc.T("체력", "HP")} +{MaxHp}");
        if (Crit > 0) parts.Add($"{Loc.T("치명", "Crit")} +{Crit * 100:0}%");
        if (Lifesteal > 0) parts.Add($"{Loc.T("흡혈", "Leech")} {Lifesteal * 100:0}%");
        if (Thorns > 0) parts.Add($"{Loc.T("가시", "Thorns")} {Thorns * 100:0}%");
        if (Regen) parts.Add(Loc.T("재생", "Regen"));
        if (GoldBonus > 0) parts.Add($"{Loc.T("골드", "Gold")} +{GoldBonus * 100:0}%");
        return string.Join(", ", parts);
    }
}

// 층 깊이에 맞는 장비를 무작위로 만든다.
public static class EquipmentFactory
{
    // 장비 이름은 만들어질 때의 언어로 정해진다(언어는 런 도중에 바뀌지 않는다).
    static readonly Txt[] WeaponNames =
    {
        new("녹슨 단검", "Rusty Dagger"), new("철검", "Iron Sword"), new("강철 장검", "Longsword"),
        new("미스릴 검", "Mithril Sword"), new("룬 대검", "Runeblade"),
    };
    static readonly Txt[] ArmorNames =
    {
        new("가죽 갑옷", "Leather Armor"), new("사슬 갑옷", "Chain Mail"), new("강철 판금", "Steel Plate"),
        new("미스릴 갑옷", "Mithril Mail"), new("용비늘 갑옷", "Dragon Mail"),
    };
    static readonly Txt[] RingNames =
    {
        new("구리 반지", "Copper Ring"), new("은 반지", "Silver Ring"), new("금 반지", "Gold Ring"),
        new("마력의 반지", "Arcane Ring"), new("별빛 반지", "Starlight Ring"),
    };

    static readonly Affix[] WeaponAffixes = { Affix.Power, Affix.Keen, Affix.Lifesteal };
    static readonly Affix[] ArmorAffixes = { Affix.Vitality, Affix.Thorns, Affix.Regen };
    static readonly Affix[] RingAffixes = { Affix.Power, Affix.Vitality, Affix.Keen, Affix.Lifesteal, Affix.Thorns, Affix.Regen, Affix.Fortune };

    public static Equipment Generate(System.Random rng, int depth, Rarity minRarity = Rarity.Common, EquipSlot? slot = null)
    {
        var s = slot ?? RollSlot(rng);
        var rarity = RollRarity(rng, depth);
        if (rarity < minRarity) rarity = minRarity;
        int tier = Mathf.Clamp(depth / 4 + rng.Next(-1, 2), 0, 4);
        int rarityBonus = (int)rarity;

        var eq = new Equipment { Slot = s, Rarity = rarity };
        int affixCount;
        Affix[] pool;
        string baseName;

        switch (s)
        {
            case EquipSlot.Weapon:
                eq.Attack = 1 + tier * 2 + rng.Next(0, 2) + rarityBonus;
                affixCount = rarityBonus;
                pool = WeaponAffixes;
                baseName = WeaponNames[tier];
                break;
            case EquipSlot.Armor:
                eq.Defense = Mathf.Max(1, tier + rarityBonus);
                eq.MaxHp = tier * 2;
                affixCount = rarityBonus;
                pool = ArmorAffixes;
                baseName = ArmorNames[tier];
                break;
            default:
                affixCount = 1 + rarityBonus; // 반지는 기본 수치 없이 옵션만 붙는다.
                pool = RingAffixes;
                baseName = RingNames[tier];
                break;
        }

        var chosen = new List<Affix>(pool);
        Affix? first = null;
        for (int i = 0; i < affixCount && chosen.Count > 0; i++)
        {
            var affix = chosen[rng.Next(chosen.Count)];
            chosen.Remove(affix);
            first ??= affix;
            Apply(eq, affix, tier);
        }

        string prefix = first.HasValue ? AffixPrefix(first.Value) + " " : "";
        eq.Name = rarity == Rarity.Legendary ? $"{Loc.T("전설의", "Legendary")} {prefix}{baseName}" : $"{prefix}{baseName}";
        return eq;
    }

    static EquipSlot RollSlot(System.Random rng)
    {
        double r = rng.NextDouble();
        return r < 0.4 ? EquipSlot.Weapon : r < 0.8 ? EquipSlot.Armor : EquipSlot.Ring;
    }

    static Rarity RollRarity(System.Random rng, int depth)
    {
        double r = rng.NextDouble();
        if (r < 0.04 + depth * 0.005) return Rarity.Legendary;
        if (r < 0.28 + depth * 0.015) return Rarity.Rare;
        return Rarity.Common;
    }

    static void Apply(Equipment eq, Affix affix, int tier)
    {
        switch (affix)
        {
            case Affix.Power: eq.Attack += 1 + tier / 2; break;
            case Affix.Vitality: eq.MaxHp += 5 + tier * 2; break;
            case Affix.Keen: eq.Crit += 0.1f; break;
            case Affix.Lifesteal: eq.Lifesteal += 0.1f; break;
            case Affix.Thorns: eq.Thorns += 0.3f; break;
            case Affix.Regen: eq.Regen = true; break;
            case Affix.Fortune: eq.GoldBonus += 0.5f; break;
        }
    }

    static string AffixPrefix(Affix affix) => affix switch
    {
        Affix.Power => Loc.T("강력한", "Mighty"),
        Affix.Vitality => Loc.T("활력의", "Vital"),
        Affix.Keen => Loc.T("예리한", "Keen"),
        Affix.Lifesteal => Loc.T("흡혈의", "Vampiric"),
        Affix.Thorns => Loc.T("가시 돋친", "Thorned"),
        Affix.Regen => Loc.T("재생의", "Mending"),
        _ => Loc.T("행운의", "Lucky"),
    };
}
