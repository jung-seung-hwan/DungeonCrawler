using System.Linq;
using UnityEngine;

// 플레이어 행동, 몬스터 AI, 전투, 성장.
public partial class GameManager
{
    const float IdleWanderChance = 0.25f;
    const int PackAlertRadius = 8;
    const int MaxMinionsPerSummoner = 3;
    const int SummonCooldownTurns = 6;

    bool bossSeen;

    // ───────────────────────── 플레이어 행동 ─────────────────────────

    void TryMovePlayer(Vector2Int dir)
    {
        var target = player.Pos + dir;

        if (IsMerchantAt(target))
        {
            OpenShop();
            return;
        }

        var enemy = EnemyAt(target);
        if (enemy != null)
        {
            Attack(player, enemy);
            EndPlayerTurn();
            return;
        }

        if (!map.IsWalkable(target)) return; // 벽에 부딪히면 턴을 쓰지 않는다.

        player.SetPos(target);
        PickUpItemAt(target);
        TriggerTrapAt(target);   // 순간이동 함정이면 여기서 위치가 바뀐다.

        if (!gameOver)
        {
            var tile = map.Get(player.Pos);
            if (tile == TileType.Altar) UseAltar();
            else if (tile == TileType.Stairs)
                Log(StairsSealed
                    ? (Ko ? "계단이 봉인되어 있다. 보스를 쓰러뜨려야 한다." : "The stairs are sealed. Defeat the boss first.")
                    : (Ko ? "계단을 발견했다. F 키로 내려갈 수 있다." : "You found the stairs. Press F to descend."));
        }
        EndPlayerTurn();
    }

    void TryDescend()
    {
        if (map.Get(player.Pos) != TileType.Stairs)
        {
            Log(Ko ? "여기에는 계단이 없다." : "There are no stairs here.");
            Play(Sfx.Deny);
            return;
        }
        if (StairsSealed)
        {
            Log(Ko ? $"계단이 봉인되어 있다. {boss.DisplayName}{Josa(boss.DisplayName, "을", "를")} 쓰러뜨려야 한다."
                   : $"The stairs are sealed. Defeat the {boss.DisplayName} first.");
            Play(Sfx.Deny);
            return;
        }

        depth++;
        // 처음 계단을 내려가면 조작법은 접어 둔다(Tab으로 다시 열 수 있다).
        if (!helpAutoHidden)
        {
            helpAutoHidden = true;
            showHelp = false;
        }
        Play(Sfx.Stairs);
        Log(IsBossFloor
            ? (Ko ? $"<color=#ffcc44>지하 {depth}층 — 보스 층이다!</color>" : $"<color=#ffcc44>Floor {depth} — a boss floor!</color>")
            : (Ko ? $"지하 {depth}층으로 내려갔다." : $"You descend to floor {depth}."));
        BuildFloor();
    }

    void EndPlayerTurn()
    {
        if (gameOver) return; // 함정 등으로 자기 턴에 쓰러졌으면 더 진행하지 않는다.
        turn++;
        turnsOnFloor++;
        if (turn % RegenInterval == 0 && player.Hp < player.MaxHp) player.Hp++;
        TickPlayerEffects();

        map.ComputeFov(player.Pos, FovRadius);
        SpotNearbyTraps();

        foreach (var enemy in enemies.ToArray())
        {
            if (!enemies.Contains(enemy)) continue; // 이번 턴에 사라진 몬스터(무너진 해골 등)
            for (int action = 0; action < enemy.Speed && !enemy.IsDead && !gameOver; action++)
                EnemyAct(enemy);
            if (enemy.Regen > 0 && !enemy.IsDead) enemy.Hp = Mathf.Min(enemy.MaxHp, enemy.Hp + enemy.Regen);
            if (gameOver) break;
        }

        RefreshVisibility();
    }

    // ───────────────────────── 몬스터 AI ─────────────────────────

    void EnemyAct(Actor enemy)
    {
        bool seesPlayer = map.IsVisible(enemy.Pos) && !IsHidden;
        if (seesPlayer)
        {
            if (enemy.LastKnownTarget == null && enemy.Pack) AlertPack(enemy);
            enemy.LastKnownTarget = player.Pos;
        }

        if (enemy.IsBoss && BossAct(enemy, seesPlayer)) return;

        // 박쥐처럼 제멋대로 움직이는 몬스터는 자주 엉뚱한 방향으로 날아간다.
        if (enemy.Erratic && rng.NextDouble() < 0.4)
        {
            MoveRandomly(enemy);
            return;
        }

        // 플레이어를 본 적 없으면 가끔 어슬렁거린다.
        if (enemy.LastKnownTarget == null)
        {
            if (rng.NextDouble() < IdleWanderChance) MoveRandomly(enemy);
            return;
        }

        int distance = Pathfinder.Distance(enemy.Pos, player.Pos);

        if (seesPlayer && enemy.FleeBelow > 0 && enemy.Hp < enemy.MaxHp * enemy.FleeBelow)
        {
            if (!enemy.FleeAnnounced)
            {
                enemy.FleeAnnounced = true;
                Log(Ko ? $"{enemy.DisplayName}{Josa(enemy.DisplayName, "이", "가")} 겁에 질려 도망친다!" : $"The {enemy.DisplayName} flees in terror!");
            }
            if (StepAwayFromPlayer(enemy)) return;
            // 막다른 곳에 몰리면 싸운다.
        }

        if (seesPlayer && enemy.Summons && --enemy.SummonCooldown <= 0 && TrySummon(enemy))
        {
            enemy.SummonCooldown = SummonCooldownTurns;
            return;
        }

        if (seesPlayer && distance == 1)
        {
            // 원거리형은 붙으면 절반 확률로 거리를 벌린다.
            if (enemy.Kites && rng.NextDouble() < 0.5 && StepAwayFromPlayer(enemy)) return;
            Attack(enemy, player);
            return;
        }
        if (seesPlayer && enemy.Range > 0 && distance <= enemy.Range)
        {
            Projectile.Spawn(enemy.Pos, player.Pos, enemy.IsBoss ? new Color(1f, 0.4f, 0.6f) : new Color(1f, 0.9f, 0.6f),
                enemy.IsBoss ? 1.4f : 0.7f);
            Play(enemy.IsBoss ? Sfx.Beam : Sfx.Shoot);
            Attack(enemy, player, enemy.RangedVerb);
            return;
        }

        var goal = enemy.LastKnownTarget.Value;
        var step = Pathfinder.FirstStep(map, enemy.Pos, p => p == goal, p => EnemyAt(p) != null || IsMerchantAt(p));
        if (step.HasValue && !IsOccupied(step.Value)) enemy.SetPos(step.Value);

        if (enemy.Pos == enemy.LastKnownTarget) enemy.LastKnownTarget = null;
    }

    void AlertPack(Actor caller)
    {
        bool alerted = false;
        foreach (var other in enemies)
        {
            if (other == caller || !other.Pack || other.LastKnownTarget != null) continue;
            if (Pathfinder.Distance(other.Pos, caller.Pos) > PackAlertRadius) continue;
            other.LastKnownTarget = player.Pos;
            alerted = true;
        }
        if (alerted) Log(Ko ? $"{caller.DisplayName}{Josa(caller.DisplayName, "이", "가")} 소리쳐 동료를 부른다!" : $"The {caller.DisplayName} shouts for its allies!");
    }

    bool TryMove(Actor enemy, Vector2Int cell)
    {
        if (!map.IsWalkable(cell) || IsOccupied(cell)) return false;
        enemy.SetPos(cell);
        return true;
    }

    void MoveRandomly(Actor enemy)
    {
        foreach (var dir in Pathfinder.Directions8.OrderBy(_ => rng.Next()))
            if (TryMove(enemy, enemy.Pos + dir)) return;
    }

    // 플레이어와 더 멀어지는 칸으로 한 걸음. 그런 칸이 없으면 false.
    bool StepAwayFromPlayer(Actor enemy)
    {
        int current = Pathfinder.Distance(enemy.Pos, player.Pos);
        var options = Pathfinder.Directions8
            .Select(dir => enemy.Pos + dir)
            .Where(cell => map.IsWalkable(cell) && !IsOccupied(cell) && Pathfinder.Distance(cell, player.Pos) > current)
            .ToList();
        if (options.Count == 0) return false;
        enemy.SetPos(options[rng.Next(options.Count)]);
        return true;
    }

    bool TrySummon(Actor summoner)
    {
        if (enemies.Count(e => e.Master == summoner) >= MaxMinionsPerSummoner) return false;
        if (SpawnMinions(summoner, GameData.Skeleton, 1, depth - 3) == 0) return false;

        Log(Ko ? $"{summoner.DisplayName}{Josa(summoner.DisplayName, "이", "가")} 해골을 불러냈다!" : $"The {summoner.DisplayName} raises a skeleton!");
        return true;
    }

    // ───────────────────────── 전투 / 성장 ─────────────────────────

    // verb가 null이면 근접 공격. multiplier는 스킬·보스 패턴의 피해 배율. quiet면 로그를 남기지 않는다.
    // animate가 false면 공격 모션을 생략한다(회전베기처럼 따로 연출하는 경우).
    void Attack(Actor attacker, Actor defender, string verb = null, float multiplier = 1f, bool quiet = false, bool animate = true)
    {
        if (animate)
        {
            bool melee = verb == null;
            attacker.PlayAttack(defender.Pos, melee ? 0.45f : 0.15f);
            if (melee)
            {
                var slashColor = attacker.IsPlayer ? playerClass.Color : new Color(1f, 0.45f, 0.4f);
                Effects.Slash(attacker.Pos, defender.Pos, slashColor, attacker.IsBoss ? 1.6f : 1f);
            }
        }

        // 방어력은 피해를 비율로 줄인다(방어 10이면 절반). 방어력을 아무리 쌓아도 무적이 되지는 않는다.
        float raw = attacker.Attack * multiplier * 10f / (10f + defender.Defense);
        bool crit = attacker.IsPlayer && rng.NextDouble() < PlayerCrit;
        bool ambush = attacker.IsPlayer && IsHidden;
        if (crit) raw *= 2f;
        if (ambush) raw *= StealthDamageMultiplier;
        int damage = Mathf.Max(1, Mathf.RoundToInt(raw * (0.85f + (float)rng.NextDouble() * 0.3f)));

        string name = defender.DisplayName;
        if (!quiet)
        {
            if (ambush)
                Log(Ko ? $"<color=#99ff99>기습!</color> {name}에게 {damage} 피해!" : $"<color=#99ff99>Ambush!</color> {damage} damage to the {name}!");
            else if (attacker.IsPlayer && crit)
                Log(Ko ? $"<color=#ffdd55>치명타!</color> {name}에게 {damage} 피해!" : $"<color=#ffdd55>Critical hit!</color> {damage} damage to the {name}!");
            else if (attacker.IsPlayer)
                Log(Ko ? $"{name}{Josa(name, "을", "를")} 공격해 {damage} 피해를 입혔다." : $"You hit the {name} for {damage} damage.");
            else
                Log(Ko ? $"<color=#ff9a8a>{attacker.DisplayName}의 {verb ?? "공격"}! {damage} 피해를 받았다.</color>"
                       : $"<color=#ff9a8a>The {attacker.DisplayName}'s {verb ?? "attack"}! You take {damage} damage.</color>");
        }
        if (ambush) BreakStealth(Ko ? "기습과 함께 모습을 드러냈다." : "Your ambush gives you away.");

        if (!defender.IsPlayer) Play(crit || ambush ? Sfx.Crit : Sfx.Hit);
        DealDamage(defender, damage, Ko ? attacker.DisplayName : "the " + attacker.DisplayName, crit || ambush, attacker.Pos);

        if (attacker.IsPlayer && Lifesteal > 0 && !gameOver)
        {
            int heal = Mathf.Min(Mathf.RoundToInt(damage * Lifesteal), player.MaxHp - player.Hp);
            if (heal > 0)
            {
                player.Hp += heal;
                AddFloatingText(player.transform.position, $"+{heal}", new Color(0.4f, 1f, 0.4f), 0.8f);
            }
        }

        // 가시 옵션: 근접 공격을 받으면 일부를 되돌려준다.
        if (defender.IsPlayer && verb == null && Thorns > 0 && !gameOver && enemies.Contains(attacker))
        {
            int reflect = Mathf.Max(1, Mathf.RoundToInt(damage * Thorns));
            DealDamage(attacker, reflect, "가시", from: player.Pos);
        }
    }

    // from: 공격이 날아온 칸(밀려나는 방향 계산용). 없으면 무작위 방향으로 움찔한다.
    void DealDamage(Actor target, int damage, string sourceName, bool crit = false, Vector2Int? from = null)
    {
        target.Hp -= damage;
        target.PlayHit(from);
        AddFloatingText(target.transform.position, crit ? $"{damage}!" : damage.ToString(),
            target.IsPlayer ? new Color(1f, 0.35f, 0.35f) : crit ? new Color(1f, 0.85f, 0.2f) : Color.white,
            crit ? 1.4f : 1f);

        if (target.IsPlayer)
        {
            Play(Sfx.Hurt);
            Shake(0.15f, 0.12f);
            input.CancelRepeat(); // 맞으면 꾹 누르던 이동을 멈춘다.
        }

        if (!target.IsDead)
        {
            if (!target.IsPlayer) OnEnemyHurt(target);
            return;
        }

        if (target.IsPlayer)
        {
            OnPlayerDied(sourceName);
            return;
        }

        OnEnemyKilled(target);
    }

    // 맞고 살아남은 몬스터의 특수 반응: 격노, 분열.
    void OnEnemyHurt(Actor enemy)
    {
        if (enemy.Enrages && !enemy.Enraged && enemy.Hp < enemy.MaxHp / 2)
        {
            enemy.Enraged = true;
            enemy.Attack += Mathf.Max(2, enemy.Attack / 2);
            enemy.SetBaseColor(new Color(1f, 0.55f, 0.5f));
            AddFloatingText(enemy.transform.position, Ko ? "격노!" : "Enraged!", new Color(1f, 0.4f, 0.2f), 1.2f);
            Play(Sfx.Frenzy, 0.7f);
            Log(Ko ? $"<color=#ff8855>{enemy.DisplayName}{Josa(enemy.DisplayName, "이", "가")} 격노했다! 공격력이 크게 올랐다.</color>"
                   : $"<color=#ff8855>The {enemy.DisplayName} is enraged! Its attack rises sharply.</color>");
        }

        if (enemy.Splits && enemy.Hp >= 4)
        {
            foreach (var dir in Pathfinder.Directions8.OrderBy(_ => rng.Next()))
            {
                var cell = enemy.Pos + dir;
                if (!map.IsWalkable(cell) || IsOccupied(cell)) continue;

                int half = enemy.Hp / 2;
                enemy.Hp -= half;
                enemy.XpReward = Mathf.Max(1, enemy.XpReward / 2);

                var clone = Actor.FromTemplate(enemy.Template, depth, floorRoot);
                clone.MaxHp = enemy.MaxHp;
                clone.Hp = half;
                clone.XpReward = enemy.XpReward;
                clone.LastKnownTarget = enemy.LastKnownTarget;
                clone.transform.position = enemy.transform.position;
                clone.SetPos(cell);
                clone.SetVisible(map.IsVisible(cell));
                enemies.Add(clone);
                Play(Sfx.Split);

                Log(Ko ? $"{enemy.DisplayName}{Josa(enemy.DisplayName, "이", "가")} 둘로 갈라졌다!" : $"The {enemy.DisplayName} splits in two!");
                return;
            }
        }
    }

    void OnEnemyKilled(Actor target)
    {
        kills++;
        Play(target.IsBoss ? Sfx.BossDown : Sfx.Kill);
        Effects.Death(target, target.Template?.Color ?? Color.white);
        enemies.Remove(target);
        Destroy(target.gameObject);

        if (target.IsBoss)
        {
            bossKills++;
            if (autopilotActive)
                Debug.Log($"[BOSS] {target.DisplayName} down on turn {turnsOnFloor} of floor; player {playerClass.Name} " +
                          $"hp {player.Hp}/{player.MaxHp}, atk {player.Attack}, def {player.Defense}, boss atk {target.Attack} def {target.Defense}");
            Log(Ko ? $"<color=#ffcc44>★ {target.DisplayName}{Josa(target.DisplayName, "을", "를")} 쓰러뜨렸다! 계단의 봉인이 풀렸다.</color>"
                   : $"<color=#ffcc44>★ The {target.DisplayName} is defeated! The seal on the stairs is broken.</color>");
            Shake(0.5f, 0.3f);
            ClearSlamWarnings();
            DropEquipment(EquipmentFactory.Generate(rng, depth, Rarity.Rare), target.Pos);
            DropEquipment(EquipmentFactory.Generate(rng, depth), target.Pos);
            DropItem(ItemKind.HealthPotion, target.Pos);
        }
        else
        {
            Log(Ko ? $"{target.DisplayName}{Josa(target.DisplayName, "을", "를")} 쓰러뜨렸다! (+{target.XpReward} XP)"
                   : $"You defeated the {target.DisplayName}! (+{target.XpReward} XP)");
            if (rng.NextDouble() < 0.12) DropItem(GameData.RollItem(rng), target.Pos);
        }

        // 소환된 해골은 골드를 주지 않는다.
        if (target.Master == null)
        {
            int coins = Mathf.RoundToInt(GameData.GoldFor(target.XpReward, rng) * (target.IsBoss ? 3 : 1) * (1f + GoldBonus));
            gold += coins;
            AddFloatingText(target.transform.position + Vector3.up * 0.3f, $"+{coins}G", new Color(1f, 0.85f, 0.25f), 0.8f);
        }

        // 소환한 주인이 죽으면 부하들도 함께 사라진다.
        var minions = enemies.Where(e => e.Master == target).ToList();
        foreach (var minion in minions)
        {
            Effects.Death(minion, minion.Template?.Color ?? Color.white);
            enemies.Remove(minion);
            Destroy(minion.gameObject);
        }
        if (minions.Count > 0) Log(Ko ? "주인을 잃은 부하들이 흩어져 사라진다." : "With their master gone, the minions scatter and vanish.");

        GainXp(target.XpReward);
    }

    void OnPlayerDied(string killer)
    {
        gameOver = true;
        killedBy = killer;
        shopOpen = false;
        Effects.Burst(player.transform.position, new Color(1f, 0.3f, 0.3f), 16);
        Play(Sfx.Death);
        Shake(0.4f, 0.3f);
        AwardSoulStones();
        if (depth > bestDepth && !autopilotActive)
        {
            bestDepth = depth;
            newRecord = true;
            PlayerPrefs.SetInt(BestDepthKey, bestDepth);
            PlayerPrefs.Save();
        }
        Log(Ko ? $"당신은 지하 {depth}층에서 {killer}에 의해 쓰러졌다... R 키로 다시 시작"
               : $"You were slain by {killer} on floor {depth}... Press R to restart");
    }

    void GainXp(int amount)
    {
        xp += amount;
        while (xp >= XpToNextLevel)
        {
            xp -= XpToNextLevel;
            level++;
            AddBaseStats(maxHp: playerClass.HpPerLevel, attack: 1);
            player.Hp = player.MaxHp;
            Play(Sfx.LevelUp);
            AddFloatingText(player.transform.position, "LEVEL UP!", new Color(0.5f, 1f, 0.5f), 1.3f);
            Log(Ko ? $"<color=#88ff88>레벨 업! Lv.{level}</color> — 체력 회복, 최대 체력 +{playerClass.HpPerLevel}, 공격력 +1"
                   : $"<color=#88ff88>Level up! Lv.{level}</color> — HP restored, max HP +{playerClass.HpPerLevel}, attack +1");
        }
    }
}
