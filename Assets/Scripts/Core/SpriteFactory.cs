using System;
using System.Collections.Generic;
using UnityEngine;

// 외부 아트 없이 16x16 픽셀 스프라이트를 코드로 그려낸다.
// 나중에 실제 아트로 교체할 때는 이 클래스의 프로퍼티만 바꾸면 된다.
public static class SpriteFactory
{
    const int Size = 16;
    static readonly Dictionary<string, Sprite> cache = new();
    static readonly Color32 Clear = new(0, 0, 0, 0);

    public static Sprite Floor => Get("floor", (x, y) =>
    {
        if (x == 0 || y == 0) return new Color(0.14f, 0.14f, 0.18f);
        if ((x * 7 + y * 13) % 23 == 0) return new Color(0.24f, 0.24f, 0.29f);
        return new Color(0.18f, 0.18f, 0.22f);
    });

    // 바닥 변형: 0 기본, 1 금 간 바닥, 2 자갈, 3 이끼.
    public static Sprite FloorVariant(int variant) => variant switch
    {
        1 => Get("floor_crack", (x, y) =>
        {
            bool crack = (x == y + 2 && x > 4 && x < 12) || (x == 9 && y > 2 && y < 7) || (y == 10 && x > 1 && x < 5);
            if (crack) return new Color(0.1f, 0.1f, 0.13f);
            return FloorBase(x, y);
        }),
        2 => Get("floor_pebble", (x, y) =>
        {
            foreach (var (cx, cy) in new[] { (4, 4), (11, 6), (7, 11), (12, 12) })
            {
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= 1) return dy > 0 ? new Color(0.36f, 0.35f, 0.4f) : new Color(0.26f, 0.25f, 0.3f);
            }
            return FloorBase(x, y);
        }),
        3 => Get("floor_moss", (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 0.35f + 3.1f, y * 0.35f + 7.7f);
            if (n > 0.62f) return new Color(0.2f, 0.32f, 0.2f);
            if (n > 0.55f) return new Color(0.19f, 0.26f, 0.2f);
            return FloorBase(x, y);
        }),
        _ => Floor,
    };

    static Color FloorBase(int x, int y)
    {
        if (x == 0 || y == 0) return new Color(0.14f, 0.14f, 0.18f);
        if ((x * 7 + y * 13) % 23 == 0) return new Color(0.24f, 0.24f, 0.29f);
        return new Color(0.18f, 0.18f, 0.22f);
    }

    // 벽에 걸린 횃불. 불꽃 아래에 나무 손잡이와 받침.
    public static Sprite WallTorch => Get("wall_torch", (x, y) =>
    {
        if (x >= 6 && x <= 9)
        {
            if (y >= 10 && y <= 13)
            {
                float d = Dist(x, y, 7.5f, 11f);
                if (d < 1.2f) return new Color(1f, 0.95f, 0.6f);
                if (d < 2.3f) return y == 13 && (x == 6 || x == 9) ? WallPixel(x, y) : new Color(1f, 0.55f, 0.15f);
            }
            if (y == 9) return new Color(0.35f, 0.3f, 0.3f);
            if (y >= 4 && y <= 8 && (x == 7 || x == 8)) return new Color(0.5f, 0.32f, 0.18f);
        }
        return WallPixel(x, y);
    });

    static Color WallPixel(int x, int y)
    {
        int offset = (y / 4) % 2 == 0 ? 0 : 4;
        bool mortar = y % 4 == 0 || (x + offset) % 8 == 0;
        return mortar ? new Color(0.28f, 0.26f, 0.33f) : new Color(0.47f, 0.44f, 0.53f);
    }

    // 횃불 빛 번짐. 부드러워야 하므로 다른 스프라이트와 달리 32px 이중선형 필터를 쓴다.
    static Sprite glow;
    public static Sprite Glow
    {
        get
        {
            if (glow != null) return glow;
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Dist(x, y, 15.5f, 15.5f) / 15.5f;
                    float a = Mathf.Clamp01(1f - d);
                    pixels[y * s + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            glow = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
            return glow;
        }
    }

    public static Sprite Wall => Get("wall", (x, y) =>
    {
        int offset = (y / 4) % 2 == 0 ? 0 : 4;
        bool mortar = y % 4 == 0 || (x + offset) % 8 == 0;
        return mortar ? new Color(0.28f, 0.26f, 0.33f) : new Color(0.47f, 0.44f, 0.53f);
    });

    // 사다리 모양의 내려가는 계단.
    public static Sprite Stairs => Get("stairs", (x, y) =>
    {
        bool rail = (x == 3 || x == 12) && y >= 1 && y <= 14;
        bool rung = y % 3 == 1 && x > 3 && x < 12;
        if (rail || rung) return new Color(0.95f, 0.8f, 0.3f);
        return new Color(0.08f, 0.08f, 0.1f);
    });

    // 몸통은 흰색이라 SpriteRenderer.color로 종류별 색을 입힌다.
    public static Sprite Creature => Get("creature", (x, y) =>
    {
        float d = Dist(x, y, 7.5f, 7f);
        if (d > 6.5f) return Clear;
        if (d > 5.5f) return new Color(0.15f, 0.15f, 0.15f);
        if (y == 8 && (x == 5 || x == 10)) return Color.black;
        return Color.white;
    });

    public static Sprite Player => Get("player", (x, y) =>
    {
        float d = Dist(x, y, 7.5f, 7f);
        if (d > 6.5f) return Clear;
        if (d > 5.5f) return new Color(0.15f, 0.15f, 0.15f);
        if (y == 8 && x >= 4 && x <= 11) return new Color(0.1f, 0.1f, 0.2f); // 투구 바이저
        return Color.white;
    });

    public static Sprite Potion => Get("potion", (x, y) =>
    {
        if (y >= 10 && y <= 13 && x >= 6 && x <= 9)
            return y == 13 ? new Color(0.55f, 0.35f, 0.2f) : new Color(0.8f, 0.85f, 0.9f);
        float d = Dist(x, y, 7.5f, 6f);
        if (d <= 4.6f) return d > 3.8f ? new Color(0.8f, 0.85f, 0.9f) : new Color(0.9f, 0.15f, 0.25f);
        return Clear;
    });

    public static Sprite Sword => Get("sword", (x, y) =>
    {
        if (Math.Abs(x - y) <= 1 && x >= 6 && x <= 13 && y >= 6 && y <= 13)
            return x == y ? Color.white : new Color(0.75f, 0.8f, 0.9f);
        if (x + y == 10 && Math.Abs(x - y) <= 4) return new Color(0.85f, 0.65f, 0.2f);
        if (x == y && x >= 2 && x <= 4) return new Color(0.5f, 0.3f, 0.15f);
        return Clear;
    });

    public static Sprite Armor => Get("armor", (x, y) =>
    {
        if (y < 2 || y > 13 || x < 2 || x > 13) return Clear;
        float halfWidth = y >= 7 ? 5.5f : 5.5f - (7 - y) * 0.9f;
        float dx = Math.Abs(x - 7.5f);
        if (dx > halfWidth) return Clear;
        if (dx > halfWidth - 1 || y == 13) return new Color(0.85f, 0.75f, 0.3f);
        return x == 7 || x == 8 ? new Color(0.85f, 0.75f, 0.3f) : new Color(0.3f, 0.45f, 0.75f);
    });

    // 둘둘 말린 두루마리. 종류별 색은 SpriteRenderer.color로 입힌다.
    public static Sprite Scroll => Get("scroll", (x, y) =>
    {
        if (x < 2 || x > 13 || y < 4 || y > 11) return Clear;
        if (x <= 3 || x >= 12) return new Color(0.75f, 0.68f, 0.55f);   // 말린 양 끝
        if (y == 4 || y == 11) return new Color(0.8f, 0.72f, 0.58f);
        if (y % 2 == 1 && x >= 5 && x <= 10) return new Color(0.55f, 0.45f, 0.4f); // 글씨
        return new Color(1f, 0.97f, 0.88f);
    });

    // 투사체용 빛나는 구슬.
    public static Sprite Bolt => Get("bolt", (x, y) =>
    {
        float d = Dist(x, y, 7.5f, 7.5f);
        if (d > 4.5f) return Clear;
        return d < 2.5f ? Color.white : new Color(1f, 1f, 1f, 0.6f);
    });

    // 두건을 쓴 상인.
    public static Sprite Merchant => Get("merchant", (x, y) =>
    {
        float d = Dist(x, y, 7.5f, 6f);
        bool hood = y >= 9 && y <= 14 && Mathf.Abs(x - 7.5f) <= 6.5f - (y - 9) * 0.9f;
        if (hood) return y == 9 ? new Color(0.3f, 0.2f, 0.1f) : new Color(0.55f, 0.35f, 0.15f);
        if (d > 6f) return Clear;
        if (d > 5f) return new Color(0.15f, 0.15f, 0.15f);
        if (y == 7 && (x == 5 || x == 10)) return Color.black;
        if (y == 4 && x >= 6 && x <= 9) return new Color(0.4f, 0.25f, 0.15f); // 수염
        return Color.white;
    });

    public static Sprite Coins => Get("coins", (x, y) =>
    {
        foreach (var (cx, cy) in new[] { (5f, 5f), (10f, 6f), (7.5f, 9.5f) })
        {
            float d = Dist(x, y, cx, cy);
            if (d <= 2.8f) return d > 2f ? new Color(0.7f, 0.5f, 0.1f) : new Color(1f, 0.85f, 0.25f);
        }
        return Clear;
    });

    // 바닥에 박힌 가시 판. 종류별 색은 SpriteRenderer.color로 입힌다.
    public static Sprite Trap => Get("trap", (x, y) =>
    {
        if (x < 2 || x > 13 || y < 2 || y > 13) return Clear;
        if (x == 2 || x == 13 || y == 2 || y == 13) return new Color(0.55f, 0.55f, 0.6f);
        bool spike = (x % 4 == 1 && y % 4 == 1) || (x % 4 == 3 && y % 4 == 3);
        return spike ? Color.white : new Color(0.25f, 0.25f, 0.3f, 0.8f);
    });

    // 보석이 빛나는 돌 제단.
    public static Sprite Altar => Get("altar", (x, y) =>
    {
        float gem = Dist(x, y, 7.5f, 10.5f);
        if (gem <= 2.2f) return gem < 1.2f ? Color.white : new Color(0.6f, 0.9f, 1f);
        if (y >= 2 && y <= 7 && x >= 3 && x <= 12)
            return y == 7 || x == 3 || x == 12 ? new Color(0.75f, 0.72f, 0.8f) : new Color(0.55f, 0.52f, 0.62f);
        if (y < 2 && x >= 2 && x <= 13) return new Color(0.4f, 0.38f, 0.45f);
        return new Color(0.18f, 0.18f, 0.22f); // 바닥색
    });

    // 보석 박힌 반지. 등급 색은 SpriteRenderer.color로 입힌다.
    public static Sprite Ring => Get("ring", (x, y) =>
    {
        float gem = Dist(x, y, 7.5f, 11f);
        if (gem <= 2.2f) return gem < 1.2f ? Color.white : new Color(0.9f, 0.3f, 0.5f);
        float d = Dist(x, y, 7.5f, 6f);
        if (d >= 3f && d <= 4.6f) return d > 4f ? new Color(0.7f, 0.55f, 0.15f) : new Color(1f, 0.85f, 0.35f);
        return Clear;
    });

    // 베기 궤적용 반달. 오른쪽(+x)을 향해 휘어 있고 양 끝으로 갈수록 가늘고 옅어진다.
    public static Sprite Slash => Get("slash", (x, y) =>
    {
        float dx = x - 3f, dy = y - 7.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        if (dx <= 0f) return Clear;
        float t = Mathf.Abs(dy) / Mathf.Max(r, 0.01f);
        float inner = 7.4f + 2f * t;
        if (r < inner || r > 9.8f) return Clear;
        return new Color(1f, 1f, 1f, 1f - t * 0.7f);
    });

    public static Sprite Pixel => Get("pixel", (x, y) => Color.white);

    static float Dist(int x, int y, float cx, float cy)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    static Sprite Get(string key, Func<int, int, Color> paint)
    {
        if (cache.TryGetValue(key, out var sprite)) return sprite;

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = key,
        };
        var pixels = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = paint(x, y);
        tex.SetPixels32(pixels);
        tex.Apply();

        sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = key;
        cache[key] = sprite;
        return sprite;
    }
}
