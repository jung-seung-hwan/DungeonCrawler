using UnityEngine;

public class EnemyTemplate
{
    public readonly string Name;     // 한국어 이름. 픽셀아트 조회와 EnemyNamed의 키로도 쓴다.
    public readonly string NameEn;
    public readonly int Hp, Attack, Defense, Xp, MinDepth;
    public readonly Color Color;
    public int Speed = 1;            // 한 턴에 행동하는 횟수
    public int Range;                // 0이면 근접 전용, 1 이상이면 그 거리까지 원거리 공격
    public int Regen;                // 매 턴 회복량
    public float Scale = 1f;
    public Txt RangedVerb = new("화살", "arrow");

    public string DisplayName => Loc.T(Name, NameEn);

    // 행동 특성
    public float FleeBelow;          // 체력이 이 비율 아래로 떨어지면 도망친다 (0이면 안 도망침)
    public bool Pack;                // 플레이어를 발견하면 주변 동료(Pack)를 부른다
    public bool Kites;               // 원거리형: 붙으면 거리를 벌리려 한다
    public bool Erratic;             // 이리저리 제멋대로 날아다닌다
    public bool Splits;              // 맞으면 둘로 갈라진다
    public bool Enrages;             // 체력이 절반 아래면 격노해 공격력이 오른다
    public bool Summons;             // 해골을 불러낸다

    // 보스 패턴
    public string PhaseSummon;       // 체력 2/3, 1/3 지점에서 이 몬스터를 2마리씩 소환
    public int SlamRadius;           // 0보다 크면 범위 내려찍기를 예고 후 사용
    public bool Blinks;              // 멀리 있는 플레이어 옆으로 순간이동
    public bool Frenzy;              // 체력 30% 아래에서 광폭화 (2회 행동)
    public bool SummonsShades;       // 체력 2/3, 1/3 지점에서 앞선 보스들의 그림자를 2마리씩 소환 (최종 보스)

    public EnemyTemplate(string name, string nameEn, int hp, int attack, int defense, int xp, int minDepth, Color color)
    {
        Name = name; NameEn = nameEn; Hp = hp; Attack = attack; Defense = defense; Xp = xp; MinDepth = minDepth; Color = color;
    }
}

public enum PlayerClass { Warrior, Rogue, Mage }

public class ClassDef
{
    public PlayerClass Id;
    public Txt Name;
    public int Hp, Attack, Defense, HpPerLevel;
    public float Crit;
    public int SkillCooldown;
    public Txt SkillName, SkillDesc, Passive;
    public Color Color;

    public string ArtKey => Name.Ko;   // PixelArt는 한국어 이름으로 그림을 찾는다.
}

public enum MetaUpgrade { Vitality, Might, Guard, Potion, Greed, Mastery, Ward, Wisdom, Revive }

// 영혼석으로 사는 영구 강화 한 종류.
public class MetaUpgradeDef
{
    public MetaUpgrade Id;
    public Txt Name;
    public Txt Effect;         // "{0}" 자리에 수치가 들어간다
    public int PerLevel;       // 단계당 수치
    public int MaxLevel;
    public int BaseCost;       // n단계(1부터) 가격 = BaseCost × n

    public int CostFor(int currentLevel) => BaseCost * (currentLevel + 1);

    public string Describe(int currentLevel)
    {
        string now = string.Format(Effect, currentLevel * PerLevel);
        return currentLevel >= MaxLevel ? now : $"{now}  →  {string.Format(Effect, (currentLevel + 1) * PerLevel)}";
    }
}

// 어느 층에 어떤 보스가 어떤 능력치로 나오는지.
public struct BossSpec
{
    public EnemyTemplate Template;
    public int Round;          // 0이면 첫 등장, 1 이상이면 색이 바뀐 반복 회차
    public bool IsFinal;       // 50층 주기의 최종 보스
    public int Hp, Attack, Defense, Xp;
    public string DisplayName;
}

public enum ItemKind { HealthPotion, FireScroll, TeleportScroll, MapScroll, Equipment, Gold }

public enum RoomKind { Normal, Start, Stairs, Shop, Treasure, Shrine }

public enum TrapKind { Spike, Alarm, Teleport }

// 밸런스 조정은 이 파일의 숫자만 바꾸면 된다.
public static class GameData
{
    public const int BossFloorInterval = 5;

    public static readonly ClassDef[] Classes =
    {
        new()
        {
            Id = PlayerClass.Warrior, Name = new("전사", "Warrior"), Hp = 40, Attack = 6, Defense = 2, HpPerLevel = 8, Crit = 0.1f,
            SkillName = new("회전베기", "Whirlwind"), SkillCooldown = 5,
            SkillDesc = new("주변 8칸의 모든 적에게 공격력 150% 피해", "Hits every enemy in the 8 surrounding tiles for 150% attack damage."),
            Passive = new("튼튼한 체력, 빠른 자연 회복", "High health, fast natural regeneration."),
            Color = new Color(1f, 0.65f, 0.45f),
        },
        new()
        {
            Id = PlayerClass.Rogue, Name = new("도적", "Rogue"), Hp = 32, Attack = 7, Defense = 1, HpPerLevel = 7, Crit = 0.25f,
            SkillName = new("그림자 숨기", "Shadow Cloak"), SkillCooldown = 8,
            SkillDesc = new("4턴간 숨어 적의 추적을 끊는다. 숨은 채로 하는 공격은 3배 피해", "Hide for 4 turns so enemies lose track of you. An attack from hiding deals triple damage."),
            Passive = new("치명타 확률 25%, 함정을 잘 발견", "25% critical chance, spots traps easily."),
            Color = new Color(0.6f, 0.95f, 0.55f),
        },
        new()
        {
            Id = PlayerClass.Mage, Name = new("마법사", "Mage"), Hp = 24, Attack = 4, Defense = 0, HpPerLevel = 5, Crit = 0.1f,
            SkillName = new("마법 화살", "Magic Missile"), SkillCooldown = 3,
            SkillDesc = new("가까운 적 2명에게 방어를 무시하는 마법 피해", "Hits the 2 nearest enemies with magic damage that ignores defense."),
            Passive = new("두루마리 효과 1.5배, 화염 두루마리 2개로 시작", "Scrolls are 1.5x stronger. Starts with 2 Fire Scrolls."),
            Color = new Color(0.75f, 0.65f, 1f),
        },
    };

    // 영구 강화. 직업 선택 화면에서 영혼석으로 산다.
    public static readonly MetaUpgradeDef[] MetaUpgrades =
    {
        // 시작 수치: 싸고 초반에 바로 체감된다.
        new() { Id = MetaUpgrade.Vitality, Name = new("강인함", "Toughness"), Effect = new("시작 최대 체력 +{0}", "Starting max HP +{0}"), PerLevel = 5, MaxLevel = 5, BaseCost = 8 },
        new() { Id = MetaUpgrade.Might, Name = new("날카로움", "Sharpness"), Effect = new("시작 공격력 +{0}", "Starting attack +{0}"), PerLevel = 1, MaxLevel = 5, BaseCost = 15 },
        new() { Id = MetaUpgrade.Guard, Name = new("단단함", "Sturdiness"), Effect = new("시작 방어력 +{0}", "Starting defense +{0}"), PerLevel = 1, MaxLevel = 3, BaseCost = 25 },
        new() { Id = MetaUpgrade.Potion, Name = new("비상 물약", "Spare Potion"), Effect = new("시작 체력 물약 +{0}", "Starting Health Potions +{0}"), PerLevel = 1, MaxLevel = 3, BaseCost = 20 },
        new() { Id = MetaUpgrade.Greed, Name = new("황금 손", "Golden Touch"), Effect = new("골드 획득 +{0}%", "Gold gained +{0}%"), PerLevel = 15, MaxLevel = 5, BaseCost = 12 },
        // 비율 강화: 깊은 층에서도 장비·레벨에 묻히지 않는다.
        new() { Id = MetaUpgrade.Mastery, Name = new("숙련", "Mastery"), Effect = new("주는 피해 +{0}%", "Damage dealt +{0}%"), PerLevel = 4, MaxLevel = 5, BaseCost = 20 },
        new() { Id = MetaUpgrade.Ward, Name = new("수호", "Ward"), Effect = new("적에게 받는 피해 -{0}%", "Damage taken from enemies -{0}%"), PerLevel = 3, MaxLevel = 5, BaseCost = 20 },
        new() { Id = MetaUpgrade.Wisdom, Name = new("배움", "Wisdom"), Effect = new("경험치 획득 +{0}%", "Experience gained +{0}%"), PerLevel = 10, MaxLevel = 5, BaseCost = 15 },
        new() { Id = MetaUpgrade.Revive, Name = new("재기", "Second Wind"), Effect = new("한 판에 부활 {0}회 (체력 절반)", "Revives per run: {0} (half HP)"), PerLevel = 1, MaxLevel = 1, BaseCost = 120 },
    };

    public static MetaUpgradeDef MetaOf(MetaUpgrade id) => System.Array.Find(MetaUpgrades, m => m.Id == id);

    // 런이 끝났을 때 받는 영혼석: 내려간 층, 쓰러뜨린 보스, 처치 수.
    public static int SoulStonesFor(int depth, int kills, int bossKills) => (depth - 1) * 3 + bossKills * 10 + kills / 5;

    public static ClassDef ClassOf(PlayerClass id) => System.Array.Find(Classes, c => c.Id == id);

    public static readonly EnemyTemplate[] Enemies =
    {
        new("쥐", "Rat", 4, 2, 0, 2, 1, new Color(0.65f, 0.5f, 0.35f)),
        new("박쥐", "Bat", 3, 2, 0, 3, 1, new Color(0.55f, 0.55f, 0.65f)) { Speed = 2, Scale = 0.75f, Erratic = true },
        new("고블린", "Goblin", 8, 3, 1, 4, 1, new Color(0.4f, 0.8f, 0.35f)) { Pack = true, FleeBelow = 0.3f },
        new("고블린 궁수", "Goblin Archer", 6, 3, 0, 5, 2, new Color(0.75f, 0.9f, 0.4f)) { Range = 5, Pack = true, Kites = true },
        new("슬라임", "Slime", 10, 3, 0, 4, 3, new Color(0.3f, 0.9f, 0.6f)) { Splits = true },
        new("오크", "Orc", 14, 5, 2, 8, 3, new Color(0.85f, 0.45f, 0.2f)) { Scale = 1.1f, Enrages = true },
        new("해골", "Skeleton", 12, 6, 1, 9, 4, new Color(0.92f, 0.9f, 0.82f)),
        new("트롤", "Troll", 24, 7, 3, 15, 6, new Color(0.6f, 0.35f, 0.75f)) { Regen = 1, Scale = 1.2f },
        new("암흑 마법사", "Dark Mage", 16, 8, 1, 18, 8, new Color(0.45f, 0.3f, 0.9f)) { Range = 4, RangedVerb = new("암흑 화살", "dark bolt"), Kites = true },
        new("강령술사", "Necromancer", 18, 5, 1, 20, 9, new Color(0.4f, 0.55f, 0.45f)) { Range = 3, RangedVerb = new("저주", "curse"), Kites = true, Summons = true },
    };

    public static EnemyTemplate EnemyNamed(string name) => System.Array.Find(Enemies, t => t.Name == name);
    public static EnemyTemplate Skeleton => EnemyNamed("해골");

    // ───────────────────────── 몬스터 층별 강화 ─────────────────────────

    // 출현 층보다 깊을수록 강해진다(체력은 제곱으로). 20층까지는 이 공식 그대로이고,
    // 20층을 넘으면 공격력이 층마다 조금씩(0.5%), 50층을 넘으면 크게(2.5%) 추가로 커진다.
    // 장비 성장(DeepGearBonus)이 50층까지는 거의 따라가므로 "조금씩 어려워지다가 50층 뒤로 뚜렷이 버거워지는" 곡선이 된다.
    // 체력을 더 키우지 않는 이유: 싸움이 길어지기만 하는 것보다 한 대 한 대가 아파지는 편이 낫기 때문.
    public const int MidStartDepth = 20;
    public const int DeepStartDepth = 50;
    public const float MidAttackGrowthPerFloor = 0.005f;
    public const float DeepAttackGrowthPerFloor = 0.025f;

    static float DeepFactor(int depth) =>
        1f + Mathf.Clamp(depth - MidStartDepth, 0, DeepStartDepth - MidStartDepth) * MidAttackGrowthPerFloor
           + Mathf.Max(0, depth - DeepStartDepth) * DeepAttackGrowthPerFloor;

    public static int EnemyHp(EnemyTemplate t, int depth)
    {
        int bonus = depth - t.MinDepth;
        return t.Hp + bonus * 4 + bonus * bonus / 5;
    }

    public static int EnemyAttack(EnemyTemplate t, int depth) =>
        Mathf.RoundToInt((t.Attack + (depth - t.MinDepth) * 3 / 4) * DeepFactor(depth));

    public static int EnemyDefense(EnemyTemplate t, int depth) => t.Defense + (depth - t.MinDepth) / 3;

    public static int EnemyXp(EnemyTemplate t, int depth) => Mathf.RoundToInt((t.Xp + depth - t.MinDepth) * DeepFactor(depth));

    // 그 층의 "튼튼한 일반 몬스터" 기준치(트롤). 반복 보스와 그림자 부하의 능력치를 여기에 맞춘다.
    static EnemyTemplate Reference => EnemyNamed("트롤");
    public static int ReferenceHp(int depth) => EnemyHp(Reference, Mathf.Max(depth, Reference.MinDepth));
    public static int ReferenceAttack(int depth) => EnemyAttack(Reference, Mathf.Max(depth, Reference.MinDepth));
    public static int ReferenceDefense(int depth) => EnemyDefense(Reference, Mathf.Max(depth, Reference.MinDepth));

    // ───────────────────────── 보스 ─────────────────────────

    // 보스는 5층마다 등장한다. 50층마다 최종 보스가 나오고, 그 사이 층은 아래 4종이 차례로 돌아가며 나온다.
    // 한 바퀴 돌 때마다(회차) 색이 바뀌고 그 층 몬스터에 맞춰 강해진다.
    public const int FinalBossInterval = 50;
    const float VariantHpRatio = 2.2f;      // 반복 보스 체력 = 그 층 기준 몬스터 체력 × 이 값 × 보스별 비율
    const float VariantAttackRatio = 1.2f;
    const float ShadeHpRatio = 0.6f;        // 최종 보스가 부르는 그림자 = 그 층 기준 몬스터 × 이 값
    const float ShadeAttackRatio = 0.9f;

    public static readonly EnemyTemplate[] Bosses =
    {
        new("고블린 왕", "Goblin King", 100, 10, 3, 30, 5, new Color(1f, 0.85f, 0.25f))
            { Scale = 1.5f, PhaseSummon = "고블린", SlamRadius = 1 },
        new("오우거 족장", "Ogre Chieftain", 170, 15, 6, 60, 10, new Color(0.9f, 0.35f, 0.3f))
            { Scale = 1.7f, Regen = 1, SlamRadius = 2, Frenzy = true },
        new("리치", "Lich", 200, 17, 6, 100, 15, new Color(0.5f, 0.95f, 0.9f))
            { Scale = 1.5f, Range = 4, RangedVerb = new("죽음의 광선", "death ray"), PhaseSummon = "해골", Blinks = true },
        new("심연의 용", "Abyssal Dragon", 300, 20, 9, 200, 20, new Color(0.95f, 0.25f, 0.55f))
            { Scale = 1.9f, Range = 3, RangedVerb = new("화염 숨결", "fire breath"), SlamRadius = 2, Frenzy = true, Blinks = true },
    };

    // 50층마다 나오는 최종 보스. 앞선 보스들의 패턴을 모두 쓰고, 체력 단계마다 앞선 보스의 그림자를 부른다.
    public static readonly EnemyTemplate FinalBoss =
        new("공허의 군주", "Void Sovereign", 1800, 58, 18, 1000, 50, new Color(0.65f, 0.35f, 1f))
        {
            Scale = 2f, Range = 4, RangedVerb = new("공허의 광선", "void ray"), SlamRadius = 3,
            Frenzy = true, Blinks = true, SummonsShades = true,
        };

    public static EnemyTemplate BossNamed(string name) =>
        name == FinalBoss.Name ? FinalBoss : System.Array.Find(Bosses, b => b.Name == name);

    // 반복 회차의 이름 앞에 붙는 말. 네 회차를 다 쓰면 뒤에 숫자가 붙는다.
    static readonly Txt[] VariantTitles =
    {
        new("흉포한", "Savage"), new("저주받은", "Cursed"), new("불멸의", "Undying"), new("태고의", "Ancient"),
    };

    // 회차별 색조 이동(도). 픽셀아트의 색을 이만큼 돌려서 구분한다.
    static readonly float[] VariantHues = { 0f, 150f, 60f, 240f, 300f, 105f, 195f, 330f };
    public static float VariantHue(int round) => VariantHues[round % VariantHues.Length];

    public static bool IsFinalBossFloor(int depth) => depth % FinalBossInterval == 0;

    public static BossSpec BossFor(int depth)
    {
        int finals = depth / FinalBossInterval;
        var spec = new BossSpec { IsFinal = IsFinalBossFloor(depth) };
        if (spec.IsFinal)
        {
            spec.Template = FinalBoss;
            spec.Round = finals - 1;
        }
        else
        {
            int n = depth / BossFloorInterval - 1 - finals;   // 최종 보스 층을 뺀 보스 층 순번
            spec.Template = Bosses[n % Bosses.Length];
            spec.Round = n / Bosses.Length;
        }

        var t = spec.Template;
        int extra = depth - t.MinDepth;
        spec.Hp = t.Hp; spec.Attack = t.Attack;
        spec.Defense = t.Defense + extra / 3;
        spec.Xp = t.Xp + extra * 10;
        spec.DisplayName = t.DisplayName;
        if (extra <= 0) return spec;

        if (spec.IsFinal)
        {
            // 100층, 150층…의 최종 보스는 그 층 몬스터가 강해진 만큼 함께 강해진다.
            spec.Hp = Mathf.RoundToInt((float)t.Hp * ReferenceHp(depth) / ReferenceHp(t.MinDepth));
            spec.Attack = Mathf.RoundToInt((float)t.Attack * ReferenceAttack(depth) / ReferenceAttack(t.MinDepth));
        }
        else
        {
            // 원래 층에서 그 보스가 얼마나 센 편이었는지(보스별 비율)를 유지한 채 그 층 기준치에 맞춘다.
            float hpShare = t.Hp / (13f * t.MinDepth + 40f);
            float attackShare = t.Attack / (0.67f * t.MinDepth + 7f);
            spec.Hp = Mathf.Max(t.Hp, Mathf.RoundToInt(VariantHpRatio * hpShare * ReferenceHp(depth)));
            spec.Attack = Mathf.Max(t.Attack, Mathf.RoundToInt(VariantAttackRatio * attackShare * ReferenceAttack(depth)));
        }

        var title = VariantTitles[(spec.Round - 1) % VariantTitles.Length];
        int lap = (spec.Round - 1) / VariantTitles.Length;
        spec.DisplayName = lap > 0 ? $"{title} {t.DisplayName} {lap + 1}" : $"{title} {t.DisplayName}";
        return spec;
    }

    // 최종 보스가 부르는 그림자의 능력치(체력, 공격, 방어).
    public static (int hp, int attack, int defense) ShadeStats(int depth) =>
        (Mathf.RoundToInt(ReferenceHp(depth) * ShadeHpRatio), Mathf.RoundToInt(ReferenceAttack(depth) * ShadeAttackRatio), ReferenceDefense(depth));

    // ───────────────────────── 장비 층별 성장 ─────────────────────────

    // 장비 단계 = 층 ÷ 4 (±1). 4단계까지는 단계당 일정하게, 그 뒤로는 무기 공격력이 가파르게(1.65제곱) 좋아져서
    // 제곱으로 느는 몬스터 체력을 조금 못 미치게 따라간다. 갑옷의 방어·체력은 단계에 비례해 끝없이 오른다.
    // 계수는 시뮬레이션으로 맞췄다: 몬스터 하나를 잡는 데 드는 타수가 20층 약 5.5타 → 50층 약 8.5타.
    public const int GearLinearTiers = 4;
    const float DeepGearScale = 2.7f, DeepGearPower = 1.65f;
    public static int DeepGearBonus(int tier) =>
        tier <= GearLinearTiers ? 0 : Mathf.RoundToInt(DeepGearScale * Mathf.Pow(tier - GearLinearTiers, DeepGearPower));

    // 마법 화살·화염 두루마리 피해. 깊은 층에서는 장비로 오른 공격력을 따라가서 뒤처지지 않는다(얕은 층은 예전 공식 그대로).
    // gearAttack: 장비로 오른 공격력. 그 절반이 항상 더해져서 무기를 바꾸면 얕은 층에서도 마법 피해가 바로 오른다.
    public static int MagicMissileDamage(int level, int depth, int attack, int gearAttack) => Mathf.Max(3 + level + depth + gearAttack / 2, attack * 3 / 4);
    public static int FireScrollDamage(int level, int depth, int attack, int gearAttack) => Mathf.Max(10 + depth * 2 + level + gearAttack / 2, attack);

    // 자연 회복 한 번에 차는 체력. 최대 체력이 커지면 조금씩 늘어난다(240 미만은 1).
    public static int RegenAmount(int maxHp) => Mathf.Max(1, maxHp / 120);

    // 숫자 키 1~4에 대응하는 소모품 순서.
    public static readonly ItemKind[] Consumables =
        { ItemKind.HealthPotion, ItemKind.FireScroll, ItemKind.TeleportScroll, ItemKind.MapScroll };

    static readonly (ItemKind kind, float weight)[] ItemWeights =
    {
        (ItemKind.HealthPotion, 20), (ItemKind.FireScroll, 15), (ItemKind.TeleportScroll, 8),
        (ItemKind.MapScroll, 10), (ItemKind.Equipment, 16),
    };

    public static ItemKind RollItem(System.Random rng)
    {
        float total = 0;
        foreach (var (_, w) in ItemWeights) total += w;
        double roll = rng.NextDouble() * total;
        foreach (var (kind, w) in ItemWeights)
        {
            roll -= w;
            if (roll <= 0) return kind;
        }
        return ItemKind.HealthPotion;
    }

    // 상점 가격. 깊은 층일수록 비싸지만 그만큼 골드도 많이 모인다.
    public static int Price(ItemKind kind, int depth) => kind switch
    {
        ItemKind.HealthPotion => 20 + depth * 3,
        ItemKind.FireScroll => 30 + depth * 3,
        ItemKind.TeleportScroll => 25 + depth * 2,
        ItemKind.MapScroll => 20 + depth * 2,
        _ => 10,
    };

    public static int Price(Equipment eq, int depth) => 30 + eq.Score * 3 + depth * 3;

    public static int GoldFor(int xpReward, System.Random rng) => xpReward / 4 + rng.Next(1, 3);

    public static string TrapName(TrapKind kind) => kind switch
    {
        TrapKind.Spike => Loc.T("가시 함정", "spike trap"),
        TrapKind.Alarm => Loc.T("경보 함정", "alarm trap"),
        _ => Loc.T("순간이동 함정", "teleport trap"),
    };

    public static Color TrapTint(TrapKind kind) => kind switch
    {
        TrapKind.Spike => new Color(1f, 0.45f, 0.45f),
        TrapKind.Alarm => new Color(1f, 0.85f, 0.3f),
        _ => new Color(0.7f, 0.55f, 1f),
    };

    public static string ItemName(ItemKind kind) => kind switch
    {
        ItemKind.Gold => Loc.T("금화", "Gold"),
        ItemKind.HealthPotion => Loc.T("체력 물약", "Health Potion"),
        ItemKind.FireScroll => Loc.T("화염 두루마리", "Fire Scroll"),
        ItemKind.TeleportScroll => Loc.T("순간이동 두루마리", "Teleport Scroll"),
        ItemKind.MapScroll => Loc.T("지도 두루마리", "Map Scroll"),
        ItemKind.Equipment => Loc.T("장비", "Equipment"),
        _ => kind.ToString(),
    };

    public static string ShortName(ItemKind kind) => kind switch
    {
        ItemKind.HealthPotion => Loc.T("물약", "Potion"),
        ItemKind.FireScroll => Loc.T("화염", "Fire"),
        ItemKind.TeleportScroll => Loc.T("순간이동", "Warp"),
        ItemKind.MapScroll => Loc.T("지도", "Map"),
        _ => ItemName(kind),
    };

    public static Color ItemTint(ItemKind kind) => kind switch
    {
        ItemKind.FireScroll => new Color(1f, 0.55f, 0.4f),
        ItemKind.TeleportScroll => new Color(0.75f, 0.6f, 1f),
        ItemKind.MapScroll => new Color(0.6f, 1f, 0.65f),
        _ => Color.white,
    };
}
