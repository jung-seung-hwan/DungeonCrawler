using UnityEngine;

public enum Language { Korean, English }

// 언어 선택. 저장된 선택이 없으면 시스템 언어를 따른다(한국어 PC는 한국어, 그 외에는 영어).
// 직업 선택 화면에서 L 키로 바꾸고 PlayerPrefs에 저장한다. 실행 인자 -lang ko|en 은 저장하지 않고 그 실행에만 적용한다.
public static class Loc
{
    const string LanguageKey = "Language";

    public static Language Current { get; private set; } = Language.Korean;
    public static bool IsKorean => Current == Language.Korean;

    public static void Init(string overrideCode = null)
    {
        if (overrideCode != null)
        {
            Current = overrideCode.ToLowerInvariant().StartsWith("en") ? Language.English : Language.Korean;
            return;
        }
        if (PlayerPrefs.HasKey(LanguageKey))
            Current = PlayerPrefs.GetInt(LanguageKey) == (int)Language.English ? Language.English : Language.Korean;
        else
            Current = Application.systemLanguage == SystemLanguage.Korean ? Language.Korean : Language.English;
    }

    public static void Toggle()
    {
        Current = IsKorean ? Language.English : Language.Korean;
        PlayerPrefs.SetInt(LanguageKey, (int)Current);
        PlayerPrefs.Save();
    }

    public static string T(string ko, string en) => IsKorean ? ko : en;

    // 영어 부정관사: "a Goblin", "an Orc".
    public static string A(string word) => ("aeiouAEIOU".IndexOf(word[0]) >= 0 ? "an " : "a ") + word;
}

// 한국어/영어 한 쌍. 문자열 자리에 그대로 쓰면 현재 언어의 글자가 나온다.
// Ko는 픽셀아트 조회 같은 내부 키로도 쓴다.
public readonly struct Txt
{
    public readonly string Ko, En;

    public Txt(string ko, string en) { Ko = ko; En = en; }

    public override string ToString() => Loc.IsKorean ? Ko : En;
    public static implicit operator string(Txt t) => t.ToString();
}
