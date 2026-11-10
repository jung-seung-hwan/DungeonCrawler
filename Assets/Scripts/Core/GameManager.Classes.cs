using System.Linq;
using UnityEngine;

// 직업 선택, 직업 스킬, 장비 착용과 능력치 계산.
public partial class GameManager
{
    const int StealthTurns = 4;
    const float StealthDamageMultiplier = 3f;
    const float WhirlwindMultiplier = 1.5f;

    ClassDef playerClass;
    bool choosingClass;
    int skillCooldown;
    int stealthTurns;

    // 레벨 업·제단 등으로 오르는 기본 능력치. 최종 능력치 = 기본 + 장비.
    int baseMaxHp;
    int baseAttack;
    int baseDefense;

    Equipment weapon;
    Equipment armor;
    Equipment ring;

    bool IsHidden => stealthTurns > 0;
    bool SkillReady => skillCooldown <= 0;

    Equipment[] Equipped => new[] { weapon, armor, ring };
    float PlayerCrit => playerClass.Crit + Equipped.Where(e => e != null).Sum(e => e.Crit);
    float Lifesteal => Equipped.Where(e => e != null).Sum(e => e.Lifesteal);
    float Thorns => Equipped.Where(e => e != null).Sum(e => e.Thorns);
    float GoldBonus => Equipped.Where(e => e != null).Sum(e => e.GoldBonus) + MetaBonus(MetaUpgrade.Greed) / 100f;
    float ScrollPower => playerClass.Id == PlayerClass.Mage ? 1.5f : 1f;

    int RegenInterval
    {
        get
        {
            int interval = playerClass.Id == PlayerClass.Warrior ? 5 : 8;
            if (Equipped.Any(e => e != null && e.Regen)) interval /= 2;
            return interval;
        }
    }

    // ───────────────────────── 능력치 ─────────────────────────

    void RecalculateStats()
    {
        int oldMax = player.MaxHp;
        var gear = Equipped.Where(e => e != null).ToArray();
        player.MaxHp = baseMaxHp + gear.Sum(e => e.MaxHp);
        player.Attack = baseAttack + gear.Sum(e => e.Attack);
        player.Defense = baseDefense + gear.Sum(e => e.Defense);

        // 최대 체력이 늘어난 만큼은 현재 체력도 함께 늘린다.
        player.Hp = Mathf.Clamp(player.Hp + Mathf.Max(0, player.MaxHp - oldMax), 1, player.MaxHp);
    }

    void AddBaseStats(int maxHp = 0, int attack = 0, int defense = 0)
    {
        baseMaxHp += maxHp;
        baseAttack += attack;
        baseDefense += defense;
        RecalculateStats();
    }

    // ───────────────────────── 장비 ─────────────────────────

    Equipment GetEquipped(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => weapon,
        EquipSlot.Armor => armor,
        _ => ring,
    };

    void SetEquipped(EquipSlot slot, Equipment eq)
    {
        switch (slot)
        {
            case EquipSlot.Weapon: weapon = eq; break;
            case EquipSlot.Armor: armor = eq; break;
            default: ring = eq; break;
        }
        RecalculateStats();
    }

    // 더 좋은 장비면 바로 착용하고 이전 장비는 판다. 아니면 새 장비를 판다.
    // forceEquip: 상점에서 직접 산 물건은 점수와 상관없이 착용한다.
    void ReceiveEquipment(Equipment eq, bool bought, bool forceEquip = false)
    {
        var current = GetEquipped(eq.Slot);
        if (forceEquip || current == null || eq.Score > current.Score)
        {
            SetEquipped(eq.Slot, eq);
            string sold = "";
            if (current != null)
            {
                gold += current.SellValue;
                sold = Ko ? $" (이전 장비는 {current.SellValue}G에 팔았다)" : $" (old one sold for {current.SellValue}G)";
            }
            Log(Ko ? $"{eq.ColoredName}{Josa(eq.Name, "을", "를")} {(bought ? "사서" : "주워")} 장착했다! {eq.Describe()}{sold}"
                   : $"{(bought ? "Bought" : "Found")} and equipped {eq.ColoredName}! {eq.Describe()}{sold}");
            AddFloatingText(player.transform.position, eq.Name, eq.RarityColor);
        }
        else
        {
            gold += eq.SellValue;
            Log(Ko ? $"{eq.ColoredName}{Josa(eq.Name, "은", "는")} 지금 장비보다 약해 {eq.SellValue}G에 팔았다."
                   : $"{eq.ColoredName} is weaker than your gear — sold for {eq.SellValue}G.");
        }
    }

    // ───────────────────────── 직업 선택 ─────────────────────────

    void ShowClassSelect()
    {
        choosingClass = true;
        metaOpen = false;
        gameOver = false;
        if (player != null) Destroy(player.gameObject);
        if (floorRoot != null) Destroy(floorRoot.gameObject);
        player = null;
        floorRoot = null;
    }

    void HandleClassSelectInput(PlayerCommand cmd)
    {
        if (metaOpen) { HandleMetaInput(cmd); return; }
        if (cmd.Kind == CommandKind.Meta) { OpenMeta(); return; }
        if (cmd.Kind == CommandKind.Language) { Loc.Toggle(); Play(Sfx.UiMove); return; }
        if (cmd.Kind == CommandKind.UseItem && !cmd.FromPotionKey && cmd.Slot < GameData.Classes.Length)
            BeginRun(GameData.Classes[cmd.Slot].Id);
    }

    void ApplyClass(PlayerClass id)
    {
        playerClass = GameData.ClassOf(id);
        baseMaxHp = playerClass.Hp + MetaBonus(MetaUpgrade.Vitality);
        baseAttack = playerClass.Attack + MetaBonus(MetaUpgrade.Might);
        baseDefense = playerClass.Defense + MetaBonus(MetaUpgrade.Guard);
        weapon = armor = ring = null;
        skillCooldown = 0;
        stealthTurns = 0;

        player.SetSprite(PixelArt.Get(playerClass.ArtKey) ?? SpriteFactory.Player);
        player.SetBaseColor(Color.white);
        player.MaxHp = player.Hp = baseMaxHp;
        RecalculateStats();
        if (id == PlayerClass.Mage) inventory[ItemKind.FireScroll] = 2;
    }

    // ───────────────────────── 직업 스킬 ─────────────────────────

    void UseSkill()
    {
        if (!SkillReady)
        {
            Log(Ko ? $"{playerClass.SkillName}{Josa(playerClass.SkillName, "은", "는")} {skillCooldown}턴 뒤에 쓸 수 있다."
                   : $"{playerClass.SkillName} will be ready in {skillCooldown} {(skillCooldown == 1 ? "turn" : "turns")}.");
            Play(Sfx.Deny);
            return;
        }

        bool used = playerClass.Id switch
        {
            PlayerClass.Warrior => Whirlwind(),
            PlayerClass.Rogue => Stealth(),
            _ => MagicMissile(),
        };
        if (!used) return;

        skillCooldown = playerClass.SkillCooldown + 1; // 이번 턴 종료 처리에서 1 줄어든다.
        EndPlayerTurn();
    }

    bool Whirlwind()
    {
        var targets = enemies.Where(e => Pathfinder.Distance(e.Pos, player.Pos) == 1).ToList();
        if (targets.Count == 0) { Log(Ko ? "주변에 적이 없다." : "No enemies nearby."); Play(Sfx.Deny); return false; }

        Log(Ko ? $"<color=#ffaa66>회전베기!</color> 주변의 적 {targets.Count}명을 벤다."
               : $"<color=#ffaa66>Whirlwind!</color> You slash {targets.Count} nearby {(targets.Count == 1 ? "enemy" : "enemies")}.");
        Play(Sfx.Whirl);
        Shake(0.2f, 0.15f);
        Effects.Whirl(player.transform.position, playerClass.Color);
        foreach (var target in targets)
            if (enemies.Contains(target)) Attack(player, target, multiplier: WhirlwindMultiplier, quiet: true, animate: false);
        return true;
    }

    bool Stealth()
    {
        stealthTurns = StealthTurns + 1;
        foreach (var enemy in enemies) enemy.LastKnownTarget = null;
        player.SetAlpha(0.35f);
        Play(Sfx.Stealth);
        Log(Ko ? "<color=#99ff99>그림자 속으로 숨었다!</color> 적들이 당신을 놓쳤다."
               : "<color=#99ff99>You slip into the shadows!</color> The enemies lose track of you.");
        return true;
    }

    bool MagicMissile()
    {
        var targets = enemies.Where(e => map.IsVisible(e.Pos))
            .OrderBy(e => Pathfinder.Distance(e.Pos, player.Pos)).Take(2).ToList();
        if (targets.Count == 0) { Log(Ko ? "보이는 적이 없다." : "No enemies in sight."); Play(Sfx.Deny); return false; }

        int damage = 3 + level + depth;
        Play(Sfx.Magic);
        player.PlayAttack(targets[0].Pos, 0.15f);
        foreach (var target in targets)
        {
            if (!enemies.Contains(target)) continue; // 첫 화살에 주인이 죽어 함께 사라진 부하
            Projectile.Spawn(player.Pos, target.Pos, new Color(0.7f, 0.6f, 1f), 1.1f);
            Log(Ko ? $"<color=#bb99ff>마법 화살!</color> {target.DisplayName}에게 {damage} 피해!"
                   : $"<color=#bb99ff>Magic Missile!</color> {damage} damage to the {target.DisplayName}!");
            DealDamage(target, damage, "마법 화살");
        }
        return true;
    }

    // 플레이어 턴이 끝날 때마다 스킬 쿨다운과 은신 시간을 줄인다.
    void TickPlayerEffects()
    {
        if (skillCooldown > 0) skillCooldown--;
        if (stealthTurns > 0)
        {
            stealthTurns--;
            if (stealthTurns == 0) BreakStealth(Ko ? "그림자에서 모습을 드러냈다." : "You step out of the shadows.");
        }
    }

    void BreakStealth(string message)
    {
        stealthTurns = 0;
        player.SetAlpha(1f);
        Log(message);
    }
}
