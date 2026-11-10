using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 보스 전용 패턴: 체력 단계별 소환, 예고 후 범위 내려찍기, 순간이동, 광폭화.
public partial class GameManager
{
    const int SlamCooldownTurns = 4;
    const int BlinkCooldownTurns = 5;
    const float SlamMultiplier = 1.7f;

    readonly List<GameObject> slamWarnings = new();

    // 보스가 이번 행동을 패턴에 썼으면 true. false면 일반 몬스터처럼 행동한다.
    bool BossAct(Actor boss, bool seesPlayer)
    {
        var t = boss.Template;

        // 지난 턴에 예고한 내려찍기를 실행한다. 예고한 그 턴이면(2회 행동) 플레이어가 피할 기회를 주기 위해 기다린다.
        if (boss.SlamCharging)
        {
            if (boss.SlamChargedTurn != turn) ExecuteSlam(boss);
            return true;
        }

        // 체력 2/3, 1/3 지점에서 부하를 부른다.
        if (t.PhaseSummon != null)
        {
            int phase = boss.Hp < boss.MaxHp / 3 ? 2 : boss.Hp < boss.MaxHp * 2 / 3 ? 1 : 0;
            if (phase > boss.SummonPhase)
            {
                int count = SpawnMinions(boss, GameData.EnemyNamed(t.PhaseSummon), 2, Mathf.Max(1, depth - 2));
                if (count > 0) // 빈칸이 없어 실패하면 다음 행동 때 다시 시도한다.
                {
                    boss.SummonPhase = phase;
                    Log(Ko ? $"<color=#ffcc44>{boss.DisplayName}{Josa(boss.DisplayName, "이", "가")} 부하들을 불러 모은다!</color>"
                           : $"<color=#ffcc44>The {boss.DisplayName} calls its minions!</color>");
                    Shake(0.25f, 0.15f);
                    return true;
                }
            }
        }

        if (t.Frenzy && !boss.Frenzied && boss.Hp < boss.MaxHp * 0.3f)
        {
            boss.Frenzied = true;
            boss.Speed = 2;
            boss.SetBaseColor(new Color(1f, 0.5f, 0.5f));
            AddFloatingText(boss.transform.position, Ko ? "광폭화!" : "Frenzy!", new Color(1f, 0.3f, 0.2f), 1.5f);
            Log(Ko ? $"<color=#ff5544>{boss.DisplayName}{Josa(boss.DisplayName, "이", "가")} 광폭화했다! 두 배로 빠르게 움직인다!</color>"
                   : $"<color=#ff5544>The {boss.DisplayName} goes into a frenzy! It moves twice as fast!</color>");
            Shake(0.4f, 0.25f);
            Play(Sfx.Frenzy);
        }

        if (!seesPlayer) return false;
        int distance = Pathfinder.Distance(boss.Pos, player.Pos);

        if (t.SlamRadius > 0 && --boss.SlamCooldown <= 0 && distance <= t.SlamRadius)
        {
            boss.SlamCharging = true;
            boss.SlamChargedTurn = turn;
            boss.SlamCooldown = SlamCooldownTurns;
            ShowSlamWarning(boss, t.SlamRadius);
            Play(Sfx.SlamWarn);
            Log(Ko ? $"<color=#ff7755>{boss.DisplayName}{Josa(boss.DisplayName, "이", "가")} 거대한 일격을 준비한다! 붉은 범위에서 벗어나라!</color>"
                   : $"<color=#ff7755>The {boss.DisplayName} winds up a massive blow! Get out of the red zone!</color>");
            input.CancelRepeat();
            return true;
        }

        if (t.Blinks && --boss.BlinkCooldown <= 0 && distance >= 4)
        {
            var cell = Pathfinder.Directions8.Select(d => player.Pos + d)
                .Where(c => map.IsWalkable(c) && !IsOccupied(c))
                .OrderBy(_ => rng.Next()).FirstOrDefault();
            if (cell != default)
            {
                boss.BlinkCooldown = BlinkCooldownTurns;
                boss.SetPos(cell, snap: true);
                Play(Sfx.Blink);
                Log(Ko ? $"{boss.DisplayName}{Josa(boss.DisplayName, "이", "가")} 순간이동해 눈앞에 나타났다!"
                       : $"The {boss.DisplayName} teleports right in front of you!");
                return true;
            }
        }
        return false;
    }

    void ShowSlamWarning(Actor boss, int radius)
    {
        ClearSlamWarnings();
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                var cell = boss.Pos + new Vector2Int(dx, dy);
                if (!map.IsWalkable(cell)) continue;
                var go = new GameObject("SlamWarning");
                go.transform.SetParent(floorRoot, false);
                go.transform.localPosition = new Vector3(cell.x, cell.y, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Pixel;
                sr.color = new Color(1f, 0.15f, 0.1f, 0.35f);
                sr.sortingOrder = 4;
                slamWarnings.Add(go);
            }
        }
    }

    void ClearSlamWarnings()
    {
        foreach (var go in slamWarnings)
            if (go != null) Destroy(go);
        slamWarnings.Clear();
    }

    void ExecuteSlam(Actor boss)
    {
        boss.SlamCharging = false;
        ClearSlamWarnings();
        Shake(0.35f, 0.3f);
        Play(Sfx.Slam);
        Effects.Burst(boss.transform.position, new Color(0.6f, 0.55f, 0.5f), 18); // 바닥을 내려칠 때 튀는 흙먼지

        if (Pathfinder.Distance(boss.Pos, player.Pos) <= boss.Template.SlamRadius)
        {
            Attack(boss, player, Ko ? "내려찍기" : "slam", SlamMultiplier, animate: false);
            boss.PlayAttack(player.Pos, 0.6f);
            Effects.Slash(boss.Pos, player.Pos, new Color(1f, 0.4f, 0.3f), 2f);
        }
        else
        {
            Log(Ko ? $"{boss.DisplayName}의 내려찍기! 아슬아슬하게 피했다!" : $"The {boss.DisplayName}'s slam! You barely dodge it!");
            AddFloatingText(player.transform.position, Ko ? "회피!" : "Dodge!", new Color(0.6f, 1f, 1f));
            Play(Sfx.Dodge);
        }
    }

    // 주인 주변 빈칸에 부하를 소환한다. 실제로 소환한 수를 돌려준다.
    int SpawnMinions(Actor master, EnemyTemplate template, int count, int minionDepth)
    {
        int spawned = 0;
        foreach (var dir in Pathfinder.Directions8.OrderBy(_ => rng.Next()))
        {
            if (spawned >= count) break;
            var cell = master.Pos + dir;
            if (!map.IsWalkable(cell) || IsOccupied(cell)) continue;

            var minion = Actor.FromTemplate(template, Mathf.Max(template.MinDepth, minionDepth), floorRoot);
            minion.Master = master;
            minion.XpReward = Mathf.Max(1, minion.XpReward / 3);
            minion.LastKnownTarget = IsHidden ? null : player.Pos; // 숨은 도적의 위치는 모른다.
            minion.transform.position = master.transform.position;
            minion.SetPos(cell);
            minion.SetVisible(map.IsVisible(cell));
            enemies.Add(minion);
            spawned++;
        }
        if (spawned > 0) Play(Sfx.Summon);
        return spawned;
    }
}
