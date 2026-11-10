using System.Collections.Generic;
using UnityEngine;

// 직업·몬스터·보스의 16x16 픽셀아트. 모두 오른쪽을 바라보는 모습으로 그린다(왼쪽은 뒤집어서 표시).
//
// 그리는 법: 위에서 아래로 16줄, 한 줄에 16칸. 글자 하나가 픽셀 하나다.
//   '.' = 투명, 'k' = 외곽선(모든 그림 공통), 나머지 글자는 그림마다 팔레트에서 색을 정한다.
// 새 몬스터를 추가하려면 Defs에 이름(GameData의 몬스터 이름과 같게)으로 한 항목을 더하면 된다.
public static class PixelArt
{
    const int Size = 16;
    const string Outline = "#14121a";

    static readonly Dictionary<string, Sprite> cache = new();

    public static IEnumerable<string> Names => Defs.Keys;

    // hueShift(도)를 주면 외곽선을 뺀 모든 색의 색조를 그만큼 돌린 그림을 만든다(반복 등장하는 보스의 색 변형).
    public static Sprite Get(string name, float hueShift = 0f)
    {
        string key = hueShift == 0f ? name : $"{name}#{hueShift:0}";
        if (cache.TryGetValue(key, out var sprite)) return sprite;
        if (!Defs.TryGetValue(name, out var def)) return null;
        sprite = Build(key, def.rows, hueShift == 0f ? def.palette : Shifted(def.palette, hueShift));
        cache[key] = sprite;
        return sprite;
    }

    static Dictionary<char, Color> Shifted(Dictionary<char, Color> palette, float degrees)
    {
        var shifted = new Dictionary<char, Color>();
        foreach (var (key, color) in palette)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            shifted[key] = Color.HSVToRGB(Mathf.Repeat(h + degrees / 360f, 1f), s, v);
        }
        return shifted;
    }

    static Sprite Build(string name, string[] rows, Dictionary<char, Color> palette)
    {
        var pixels = new Color32[Size * Size];
        int topPadding = Mathf.Max(0, Size - rows.Length);

        for (int row = 0; row < Size; row++)
        {
            string line = row - topPadding >= 0 && row - topPadding < rows.Length ? rows[row - topPadding] : "";
            int y = Size - 1 - row; // 텍스처는 아래쪽이 y=0
            for (int x = 0; x < Size; x++)
            {
                char c = x < line.Length ? line[x] : '.';
                Color color = new(0, 0, 0, 0);
                if (c == 'k') ColorUtility.TryParseHtmlString(Outline, out color);
                else if (c != '.' && !palette.TryGetValue(c, out color))
                    Debug.LogWarning($"PixelArt '{name}': 팔레트에 없는 글자 '{c}'");
                pixels[y * Size + x] = color;
            }
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };
        tex.SetPixels32(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        sprite.name = name;
        return sprite;
    }

    static Dictionary<char, Color> P(params (char key, string hex)[] entries)
    {
        var palette = new Dictionary<char, Color>();
        foreach (var (key, hex) in entries)
            if (ColorUtility.TryParseHtmlString(hex, out var color)) palette[key] = color;
        return palette;
    }

    static readonly Dictionary<string, (string[] rows, Dictionary<char, Color> palette)> Defs = new()
    {
        // ───────────────────────── 직업 ─────────────────────────

        ["전사"] = (new[]
        {
            "................",
            "......rrr.......",
            ".....kkkkk......",
            "....khhhhhk..k..",
            "....khkkkhk.kwk.",
            "....khhhhhk.kwk.",
            "...kkkssskk.kwk.",
            "..kHHkaaaak.kwk.",
            "..kHgHkaaaakgggk",
            "..kHHHkaaaakkbk.",
            "..kHHHkbbbbk.k..",
            "...kHkaaaaak....",
            "....kaak.aak....",
            "....kaak.aak....",
            "...kbbk..kbbk...",
            "...kkk....kkk...",
        }, P(('r', "#d03030"), ('h', "#b8bcc8"), ('H', "#6e7380"), ('s', "#f2c09a"), ('a', "#8a91a3"),
             ('b', "#6b4423"), ('w', "#eef2ff"), ('g', "#f0c040"))),

        ["도적"] = (new[]
        {
            "................",
            "......kkkk......",
            ".....kcccck.....",
            "....kcccccck....",
            "....kcCssssk....",
            "....kCsesesk....",
            "....kCmmmmmk....",
            ".....kcmmmck....",
            "....kccllcck....",
            "...kccclllcckw..",
            "...kcCcllcckwww.",
            "...kcCccccck.k..",
            "....kcCcccck....",
            "....kckk.kck....",
            "....klk...klk...",
            "....kkk...kkk...",
        }, P(('c', "#3f7f4a"), ('C', "#25502e"), ('s', "#e8b896"), ('e', "#ffe066"), ('m', "#2e2e36"),
             ('l', "#7a5230"), ('w', "#e6ecf5"))),

        ["마법사"] = (new[]
        {
            "........kk......",
            ".......kpk...kk.",
            "......kppk..kOok",
            ".....kppypk.kook",
            "....kpppppk..kk.",
            "...kkkkkkkkk.kb.",
            "....kssssk...kb.",
            "....ksesek...kb.",
            "....kWWWWk..ksb.",
            "...kpWWWWpkksbk.",
            "...kppWWppppkb..",
            "...kpppppppk.b..",
            "...kpPpppPpk.b..",
            "..kpPpppppPpkb..",
            "..kpppppppppkb..",
            "..kkkkkkkkkkk...",
        }, P(('p', "#7048c0"), ('P', "#4a2c86"), ('y', "#ffd84a"), ('s', "#f2c09a"), ('e', "#1a1a1a"),
             ('W', "#f0f0f5"), ('b', "#8a5a30"), ('o', "#58d8ff"), ('O', "#d8f8ff"))),

        // ───────────────────────── 몬스터 ─────────────────────────

        ["쥐"] = (new[]
        {
            "..........kk....",
            ".........kgpk...",
            "....kkkkkkgggk..",
            "...kgggggggegkk.",
            "..kggggggggggpk.",
            ".kGgggggggggkk..",
            "pkGGgggGGggk....",
            "p.kGkkGkkGk.....",
            ".p.k..k..k......",
        }, P(('g', "#8c7462"), ('G', "#5e4a3e"), ('p', "#f0a0a8"), ('e', "#e02020"))),

        ["박쥐"] = (new[]
        {
            "................",
            "................",
            "................",
            "k......kk......k",
            "kk....kddk....kk",
            "kdk..kddddk..kdk",
            "kddk.kdeedk.kddk",
            "kdDdkkddddkkdDdk",
            "kdDDddddddddDDdk",
            ".kdDDdkwwkdDDdk.",
            "..kdDk.kk.kDdk..",
            "...kdk....kdk...",
            "....kk....kk....",
        }, P(('d', "#4a3d5c"), ('D', "#6e5a88"), ('e', "#ff3030"), ('w', "#ffffff"))),

        ["고블린"] = (new[]
        {
            "................",
            "................",
            ".kk..kkkkk..kk..",
            ".kgk.kgggk.kgk..",
            "..kgkgggggkgk...",
            "...kgegggegk....",
            "...kggggggk.....",
            "...kgkttkgk.....",
            "....kggggk..kk..",
            "...kbbbbbbk.kck.",
            "..kgkbbbbkgkkck.",
            "..kgkbbbbkggcck.",
            "...kkbbbbk.kck..",
            "....kGk.kGk.k...",
            "....kGk.kGk.....",
            "...kkk..kkk.....",
        }, P(('g', "#72bf4a"), ('G', "#4a8030"), ('e', "#ffe030"), ('b', "#7a5230"), ('c', "#9a6a3a"), ('t', "#ffffff"))),

        ["고블린 궁수"] = (new[]
        {
            "................",
            ".....kkkkk...k..",
            "....khhhhhk..kb.",
            "...khhhhhhhk.lkb",
            "..kgkgegegkgklkb",
            "...kkggggkk..lkb",
            "....kgttgk...lkb",
            "....kkggkk..klkb",
            "...khhhhhhkkglkb",
            "..kghhhhhhkgklkb",
            "..kgkhhhhk.k.lkb",
            "...kkhhhhk...kb.",
            "....kGkkGk...k..",
            "....kGk.kGk.....",
            "...kkk..kkk.....",
        }, P(('h', "#4e7a34"), ('g', "#8ad05a"), ('G', "#4a8030"), ('e', "#ffe030"), ('t', "#ffffff"),
             ('b', "#8a5a30"), ('l', "#dddddd"))),

        ["슬라임"] = (new[]
        {
            "......kkkk......",
            "....kkggggkk....",
            "...kggwwggggk...",
            "..kggwggggggGk..",
            "..kggekggekggk..",
            ".kgggekggekgggk.",
            ".kggggggggggGGk.",
            ".kgggggggggGGGk.",
            ".kGGgggggGGGGGk.",
            "..kkkkkkkkkkkk..",
            "................",
        }, P(('g', "#5ad88a"), ('G', "#2f9a5c"), ('w', "#ffffff"), ('e', "#14121a"))),

        ["오크"] = (new[]
        {
            "............kk..",
            "....kkkkkk.kmmk.",
            "...kooooook.kmmk",
            "...kooeooek..kmk",
            "...koooooook.hk.",
            "...kotkkktok.hk.",
            "..kkoooooookkhk.",
            ".koookaaaakooook",
            ".kookaaaaaakook.",
            ".kookaaaaaak.k..",
            "..kkkaaaaaak....",
            "....kaaaaaak....",
            "....kOOkkOOk....",
            "....kOOk.kOOk...",
            "...kkkk..kkkk...",
            "................",
        }, P(('o', "#6f9a52"), ('O', "#4a6a36"), ('e', "#ff3030"), ('t', "#f5f0e0"), ('a', "#6b4a2e"),
             ('m', "#c0c4cc"), ('h', "#7a5230"))),

        ["해골"] = (new[]
        {
            "................",
            ".....kkkkk......",
            "....kwwwwwk.....",
            "....kwrwrwk.....",
            "....kwwwwwk.....",
            ".....kwkwk......",
            "......kwk....k..",
            "....kwwwwwk.kwk.",
            "...kwkwWwkwkkwk.",
            "...kwkwwwkwkwk..",
            "...k.kwWwk.kk...",
            ".....kwwwk......",
            ".....kwkwk......",
            ".....kwkwk......",
            "....kwk.kwk.....",
            "....kk...kk.....",
        }, P(('w', "#ece6d2"), ('W', "#b0aa96"), ('r', "#ff5030"))),

        ["트롤"] = (new[]
        {
            "................",
            "...kkkkkk.......",
            "..kttttttk......",
            "..ktettettk.....",
            "..kttnntttk.....",
            "..kttkkktttk.kk.",
            ".kkttttttttkkcck",
            "kttkTttttTkttcck",
            "kttkttttttkkkcck",
            "ktk.kfffffk..kck",
            "kk..kfffffk...k.",
            "....kttTttk.....",
            "...kttk.kttk....",
            "...kTTk.kTTk....",
            "..kkkkk.kkkkk...",
            "................",
        }, P(('t', "#8a66a6"), ('T', "#5e4478"), ('e', "#ffd030"), ('n', "#a07ab8"), ('c', "#7a5230"), ('f', "#6b5238"))),

        ["암흑 마법사"] = (new[]
        {
            "................",
            "......kkkk......",
            ".....kppppk.....",
            "....kppppppk....",
            "....kpkkkkpk....",
            "....kpkekepk....",
            "....kpkkkkpk..k.",
            "...kpppppppk.kok",
            "..kpPpppppPkkOok",
            "..kpPpppppPpkkk.",
            "..kpPpppppPpk...",
            "..kPpppppppPk...",
            "..kPPpppppPPk...",
            ".kPPPpppppPPPk..",
            ".kkkkkkkkkkkkk..",
            "................",
        }, P(('p', "#4a3480"), ('P', "#2e1e56"), ('e', "#d080ff"), ('o', "#b050ff"), ('O', "#ffd0ff"))),

        ["강령술사"] = (new[]
        {
            ".............kk.",
            "......kkkk..kwwk",
            ".....knnnnk.kwek",
            "....knnnnnnk.kk.",
            "....knssssnk.bk.",
            "....knsesesk.bk.",
            "....knssssnk.bk.",
            "...knnnnnnnkkbk.",
            "..knNnnnnnNnsbk.",
            "..knNnnnnnNk.bk.",
            "..knNnnnnnNk.bk.",
            "..knnnnnnnnk.bk.",
            "..kNnnnnnnNk.bk.",
            ".kNNnnnnnnNNk.k.",
            ".kkkkkkkkkkkk...",
            "................",
        }, P(('n', "#4e6a52"), ('N', "#334838"), ('s', "#c0ccb4"), ('e', "#60ff80"), ('w', "#ece6d2"), ('b', "#6b4a2e"))),

        // ───────────────────────── 보스 ─────────────────────────

        ["고블린 왕"] = (new[]
        {
            "....kykykyk.....",
            "....kyyYyyk.....",
            ".kk.kyyyyyk.kk..",
            ".kgkkgggggkkgk..",
            "..kgkgegegkgk...",
            "...kgggggggk....",
            "...kgkttkgk.....",
            "..rkkggggkkr....",
            ".rrkbbyybbkrr...",
            ".rkgkbbbbkgkr.kk",
            ".rkgkbbbbkgkrkyk",
            ".rrkkbbbbkkrrky.",
            "..rrkGkkGkrr.k..",
            "...rkGk.kGkr....",
            "...kkkk.kkkk....",
            "................",
        }, P(('g', "#72bf4a"), ('G', "#4a8030"), ('e', "#ffe030"), ('y', "#ffd030"), ('Y', "#e02040"),
             ('r', "#b02030"), ('b', "#5a3a8a"), ('t', "#ffffff"))),

        ["오우거 족장"] = (new[]
        {
            ".h.........h....",
            ".hh.kkkkk.hh....",
            "..hkmmmmmkh.....",
            "..kmmmmmmmk.....",
            "..koeooeook..kk.",
            "..kooooooook.ksk",
            "..kokkkkkook.kck",
            ".kkooooooookkcck",
            "kooOkffffkOokcck",
            "kookffffffkookck",
            "kok.kffffk.kkk..",
            "kk..kooOok......",
            "...koOk.kOok....",
            "...kOOk.kOOk....",
            "..kkkkk.kkkkk...",
            "................",
        }, P(('o', "#d0705a"), ('O', "#9a4a3a"), ('e', "#ffe030"), ('h', "#eee8d8"), ('m', "#80848c"),
             ('f', "#7a5a3a"), ('c', "#6b4a2e"), ('s', "#c8c8c8"))),

        ["리치"] = (new[]
        {
            "....kykykyk.....",
            "....kyyyyyk.....",
            "....kwwwwwk..kk.",
            "....kwcwcwk.kcck",
            "....kwwwwwk.kcck",
            ".....kwkwk...kk.",
            "...kkpppppkk..k.",
            "..kpppwwwpppk.wk",
            "..kpPpppppPpkwk.",
            "..kpPpppppPk..k.",
            "..kpPpppppPk..k.",
            "..kPpppppppPk.k.",
            "..kPPpppppPPk.k.",
            ".kPPPpppppPPPk..",
            ".kkkkkkkkkkkkk..",
            "................",
        }, P(('w', "#ece6d2"), ('c', "#70fff0"), ('p', "#20505a"), ('P', "#143840"), ('y', "#ffd030"))),

        ["심연의 용"] = (new[]
        {
            "................",
            "..kk......kkk...",
            ".kwwk....kdddk..",
            ".kwwwk..kdddedk.",
            "kwwwwwk.kddddddk",
            "kwwwwwwkkddkttk.",
            "kwwkwwwkddddkk..",
            ".kk.kwkdddbk....",
            "....kkdddbbk....",
            "...kddddbbbk....",
            "..kdddDdbbbk....",
            ".kddkDddddk.....",
            "kddk.kDkkDk.....",
            "kdk..kDk.kDk....",
            ".k..kkkk.kkkk...",
            "................",
        }, P(('d', "#d84080"), ('D', "#8a2050"), ('w', "#9a3060"), ('b', "#ffc090"), ('e', "#ffe030"), ('t', "#ffffff"))),

        // 최종 보스: 보랏빛 왕관을 쓴 검은 형체. 얼굴은 텅 빈 어둠이고 가슴에 공허의 핵이 빛난다.
        ["공허의 군주"] = (new[]
        {
            "....kvkvkvk.....",
            "....kvvVvvk.....",
            "...kkkkkkkkk....",
            "..kdddddddddk...",
            "..kddkkkkkddk.k.",
            "..kdkekkekdk.kvk",
            "..kddkkkkddkkvVk",
            "..kkdddddddk.kvk",
            ".kdDdddvdddDk.k.",
            "kddDddvVvddDdkk.",
            "kdkDdddvdddDkdk.",
            "kk.kDdddddDk.kk.",
            "...kDdddddDk....",
            "..kDDdddddDDk...",
            ".kDDDdddddDDDk..",
            ".kkkkkkkkkkkkk..",
        }, P(('d', "#4a3a78"), ('D', "#2c2050"), ('v', "#a855ff"), ('V', "#f0d0ff"), ('e', "#ff66ff"))),
    };
}
