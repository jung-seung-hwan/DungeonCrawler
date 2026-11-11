using UnityEngine;

// 타일 데이터, 시야(FOV), 탐험 기록, 타일 렌더링을 담당.
public class DungeonMap
{
    static readonly Color ExploredTint = new(0.35f, 0.35f, 0.45f);

    public readonly int Width;
    public readonly int Height;

    readonly TileType[,] tiles;
    readonly bool[,] visible;
    readonly bool[,] explored;
    readonly SpriteRenderer[,] renderers;
    readonly Torch[,] torches;

    public DungeonMap(TileType[,] tiles, Transform parent)
    {
        this.tiles = tiles;
        Width = tiles.GetLength(0);
        Height = tiles.GetLength(1);
        visible = new bool[Width, Height];
        explored = new bool[Width, Height];
        renderers = new SpriteRenderer[Width, Height];
        torches = new Torch[Width, Height];

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // 바닥과 닿지 않는 벽(암반)은 보일 일이 없으므로 오브젝트를 만들지 않는다.
                if (tiles[x, y] == TileType.Wall && !TouchesFloor(x, y)) continue;

                var go = new GameObject($"Tile {x},{y}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x, y, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFor(tiles[x, y], x, y);
                sr.sortingOrder = 0;
                sr.enabled = false;
                renderers[x, y] = sr;

                // 바로 아래가 바닥인 벽에 가끔 횃불을 건다.
                if (tiles[x, y] == TileType.Wall && y > 0 && tiles[x, y - 1] != TileType.Wall && Hash(x, y) % 9 == 0)
                {
                    sr.sprite = SpriteFactory.WallTorch;
                    torches[x, y] = Torch.Create(new Vector3(x, y - 0.6f, 0), parent);
                }
            }
        }
    }

    // 좌표마다 항상 같은 값을 주는 간단한 해시. 타일 모양을 무작위처럼 고르되 다시 그려도 바뀌지 않게 한다.
    static int Hash(int x, int y) => ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;

    static Sprite SpriteFor(TileType type, int x, int y) => type switch
    {
        TileType.Wall => SpriteFactory.Wall,
        TileType.Stairs => SpriteFactory.Stairs,
        TileType.Altar => SpriteFactory.Altar,
        _ => (Hash(x, y) % 20) switch
        {
            < 2 => SpriteFactory.FloorVariant(1),
            < 4 => SpriteFactory.FloorVariant(2),
            < 6 => SpriteFactory.FloorVariant(3),
            _ => SpriteFactory.Floor,
        },
    };

    // 제단 설치/소멸처럼 생성 후 바닥 타일을 바꿀 때 쓴다. 벽으로 바꾸는 것은 지원하지 않는다.
    public void SetTile(Vector2Int p, TileType type)
    {
        tiles[p.x, p.y] = type;
        var sr = renderers[p.x, p.y];
        if (sr != null) sr.sprite = SpriteFor(type, p.x, p.y);
    }

    public bool InBounds(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;
    public TileType Get(Vector2Int p) => tiles[p.x, p.y];
    public bool IsWalkable(Vector2Int p) => InBounds(p) && tiles[p.x, p.y] != TileType.Wall;
    // from에서 바로 옆 칸 to로 한 걸음 움직이거나 근접 공격을 할 수 있는지.
    // 대각선은 양옆 두 칸이 모두 뚫려 있어야 한다 — 벽 모서리를 끼고 지나가거나 벽 너머를 때릴 수 없다.
    // 플레이어와 몬스터가 같은 규칙을 쓴다(이동, 근접 공격, 길 찾기).
    public bool CanStep(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x, dy = to.y - from.y;
        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != 1 || !IsWalkable(to)) return false;
        if (dx == 0 || dy == 0) return true;
        return IsWalkable(new Vector2Int(to.x, from.y)) && IsWalkable(new Vector2Int(from.x, to.y));
    }

    public bool IsVisible(Vector2Int p) => InBounds(p) && visible[p.x, p.y];
    public bool IsExplored(Vector2Int p) => InBounds(p) && explored[p.x, p.y];

    public void ComputeFov(Vector2Int origin, int radius)
    {
        System.Array.Clear(visible, 0, visible.Length);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > radius * radius + radius) continue;
                var target = new Vector2Int(origin.x + dx, origin.y + dy);
                if (!InBounds(target) || !HasLineOfSight(origin, target)) continue;
                visible[target.x, target.y] = true;
                explored[target.x, target.y] = true;
            }
        }

        RefreshRenderers();
    }

    // 브레젠험 직선으로 시야를 검사한다. 시작/끝 칸은 제외하고 벽이 있으면 가려진다.
    bool HasLineOfSight(Vector2Int a, Vector2Int b)
    {
        int x = a.x, y = a.y;
        int dx = Mathf.Abs(b.x - a.x), dy = -Mathf.Abs(b.y - a.y);
        int sx = a.x < b.x ? 1 : -1, sy = a.y < b.y ? 1 : -1;
        int err = dx + dy;

        while (x != b.x || y != b.y)
        {
            if ((x != a.x || y != a.y) && tiles[x, y] == TileType.Wall) return false;
            int e2 = 2 * err;
            bool stepX = e2 >= dy, stepY = e2 <= dx;
            // 대각선으로 맞닿은 두 벽 사이의 틈으로는 그 너머의 바닥이 보이지 않는다(방 구석의 벽 자체는 보인다).
            if (stepX && stepY && tiles[x + sx, y] == TileType.Wall && tiles[x, y + sy] == TileType.Wall
                && tiles[x + sx, y + sy] != TileType.Wall) return false;
            if (stepX) { err += dy; x += sx; }
            if (stepY) { err += dx; y += sy; }
        }
        return true;
    }

    // 지도 두루마리: 이 층에서 보일 수 있는 모든 칸을 탐험한 것으로 표시한다.
    public void RevealAll()
    {
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (renderers[x, y] != null) explored[x, y] = true;
        RefreshRenderers();
    }

    void RefreshRenderers()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var sr = renderers[x, y];
                if (sr == null) continue;
                sr.enabled = explored[x, y];
                sr.color = visible[x, y] ? Color.white : ExploredTint;
                torches[x, y]?.SetLit(visible[x, y]);
            }
        }
    }

    bool TouchesFloor(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                if (tiles[nx, ny] != TileType.Wall) return true;
            }
        }
        return false;
    }
}
