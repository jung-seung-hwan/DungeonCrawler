using UnityEngine;

// 아이템 생성, 줍기, 소모품 사용.
public partial class GameManager
{
    // 장비는 놓일 때마다 층 깊이에 맞춰 새로 만든다.
    void SpawnItem(ItemKind kind, Vector2Int pos)
    {
        var eq = kind == ItemKind.Equipment ? EquipmentFactory.Generate(rng, depth) : null;
        items.Add(Item.Create(kind, pos, floorRoot, equipment: eq));
    }

    void DropItem(ItemKind kind, Vector2Int pos)
    {
        if (TryFindDropCell(pos, out var cell)) SpawnItem(kind, cell);
    }

    void DropEquipment(Equipment eq, Vector2Int pos)
    {
        if (TryFindDropCell(pos, out var cell)) items.Add(Item.Create(ItemKind.Equipment, cell, floorRoot, equipment: eq));
    }

    // 몬스터가 떨어뜨린 아이템은 그 자리(이미 있으면 주변 빈칸)에 놓는다.
    bool TryFindDropCell(Vector2Int pos, out Vector2Int cell)
    {
        cell = pos;
        if (ItemAt(pos) == null && map.Get(pos) == TileType.Floor) return true;
        foreach (var dir in Pathfinder.Directions8)
        {
            cell = pos + dir;
            // 플레이어나 상인 발밑에 떨어지면 줍기 어렵거나(상인은) 아예 못 줍는다.
            if (map.IsWalkable(cell) && map.Get(cell) == TileType.Floor && ItemAt(cell) == null && !IsOccupied(cell)) return true;
        }
        return false;
    }

    void PickUpItemAt(Vector2Int pos)
    {
        var item = ItemAt(pos);
        if (item == null) return;

        if (item.Kind == ItemKind.Equipment)
        {
            ReceiveEquipment(item.Equipment, bought: false);
            Play(Sfx.Pickup);
        }
        else
        {
            GainItem(item.Kind, item.Amount, bought: false);
        }
        items.Remove(item);
        Destroy(item.gameObject);
    }

    // 주운 아이템과 상점에서 산 아이템을 똑같이 처리한다.
    void GainItem(ItemKind kind, int amount, bool bought)
    {
        switch (kind)
        {
            case ItemKind.Gold:
                amount = Mathf.RoundToInt(amount * (1f + GoldBonus));
                gold += amount;
                Log(Ko ? $"금화 {amount}개를 주웠다." : $"You pick up {amount} gold.");
                AddFloatingText(player.transform.position, $"+{amount}G", new Color(1f, 0.85f, 0.25f));
                break;
            default:
                inventory[kind] = Count(kind) + amount;
                int slot = System.Array.IndexOf(GameData.Consumables, kind) + 1;
                string name = GameData.ItemName(kind);
                Log(Ko ? $"{name}{Josa(name, "을", "를")} {(bought ? "샀다" : "주웠다")}. ({slot} 키로 사용)"
                       : $"{(bought ? "Bought" : "Picked up")} {Loc.A(name)}. (Press {slot} to use)");
                break;
        }
        Play(Sfx.Pickup);
    }

    void UseConsumable(ItemKind kind)
    {
        string name = GameData.ItemName(kind);
        if (Count(kind) <= 0)
        {
            Log(Ko ? $"{name}{Josa(name, "이", "가")} 없다." : $"You have no {name}.");
            return;
        }

        bool used = kind switch
        {
            ItemKind.HealthPotion => DrinkPotion(),
            ItemKind.FireScroll => CastFireball(),
            ItemKind.TeleportScroll => Teleport(),
            ItemKind.MapScroll => RevealMap(),
            _ => false,
        };
        if (!used) return;

        inventory[kind]--;
        EndPlayerTurn();
    }

    bool DrinkPotion()
    {
        if (player.Hp >= player.MaxHp) { Log(Ko ? "체력이 이미 가득하다." : "You are already at full health."); return false; }
        int healed = Mathf.Min(Mathf.Max(12, player.MaxHp * 2 / 5), player.MaxHp - player.Hp);        player.Hp += healed;
        Play(Sfx.Magic);
        AddFloatingText(player.transform.position, $"+{healed}", new Color(0.4f, 1f, 0.4f));
        Log(Ko ? $"물약을 마셨다. 체력 +{healed}" : $"You drink the potion. HP +{healed}");
        return true;
    }

    // 가장 가까운 적에게 화염구를 날린다. 맞은 적 주변 8칸에도 절반 피해.
    bool CastFireball()
    {
        Actor target = null;
        foreach (var enemy in enemies)
        {
            if (!map.IsVisible(enemy.Pos)) continue;
            if (target == null || Pathfinder.Distance(player.Pos, enemy.Pos) < Pathfinder.Distance(player.Pos, target.Pos))
                target = enemy;
        }
        if (target == null) { Log(Ko ? "보이는 적이 없다." : "No enemies in sight."); return false; }

        int damage = Mathf.RoundToInt((10 + depth * 2 + level) * ScrollPower);
        player.PlayAttack(target.Pos, 0.15f);
        Projectile.Spawn(player.Pos, target.Pos, new Color(1f, 0.5f, 0.15f), 1.5f);
        Play(Sfx.Fire);
        Shake(0.2f, 0.15f);
        Log(Ko ? $"<color=#ff9944>화염구!</color> {target.DisplayName}에게 {damage} 피해!"
               : $"<color=#ff9944>Fireball!</color> {damage} damage to the {target.DisplayName}!");

        var center = target.Pos;
        var splashed = enemies.FindAll(e => e != target && Pathfinder.Distance(e.Pos, center) == 1);
        DealDamage(target, damage, "화염");
        foreach (var e in splashed)
            if (!e.IsDead && enemies.Contains(e)) DealDamage(e, damage / 2, "화염");
        return true;
    }

    bool Teleport()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var room = rooms[rng.Next(rooms.Count)];
            if (!TryRandomFreeCell(room, out var cell)) continue;
            if (Pathfinder.Distance(cell, player.Pos) < 10) continue;

            player.SetPos(cell, snap: true);
            cam.transform.position = new Vector3(cell.x, cell.y, -10f);
            Play(Sfx.Magic);
            Log(Ko ? "몸이 빛에 휩싸이더니 다른 곳으로 이동했다!" : "Light engulfs you and carries you somewhere else!");
            PickUpItemAt(cell);
            return true;
        }
        Log(Ko ? "마법이 실패했다." : "The spell fizzles.");
        return false;
    }

    bool RevealMap()
    {
        map.RevealAll();
        RevealAllTraps();
        RefreshTraps();
        Play(Sfx.Magic);
        Log(Ko ? "이 층의 지도가 머릿속에 그려진다. 함정 위치도 보인다!" : "A map of this floor forms in your mind. Traps are revealed too!");
        RebuildMinimap();
        return true;
    }
}
