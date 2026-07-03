using System.Text.RegularExpressions;

namespace DiscordBot.Services;

public sealed partial class AllCapsMessageModerator
{
    public static AllCapsEvaluation Evaluate(string content, int minLetters, double minUppercaseRatio)
    {
        var stripped = CodeBlockPattern().Replace(content, " ");
        stripped = UrlPattern().Replace(stripped, " ");
        stripped = MentionPattern().Replace(stripped, " ");
        stripped = EmojiPattern().Replace(stripped, " ");

        var letterCount = 0;
        var uppercaseCount = 0;
        foreach (var c in stripped)
        {
            if (!char.IsLetter(c)) continue;
            letterCount++;
            if (char.IsUpper(c)) uppercaseCount++;
        }

        var shouldTrigger = letterCount >= minLetters &&
            uppercaseCount / (double)letterCount >= minUppercaseRatio;

        return new AllCapsEvaluation(shouldTrigger, letterCount, uppercaseCount);
    }

    [GeneratedRegex(@"```[\s\S]*?```|`[^`]*`")]
    private static partial Regex CodeBlockPattern();

    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"<@!?\d+>|<#\d+>|<@&\d+>")]
    private static partial Regex MentionPattern();

    [GeneratedRegex(@"<a?:\w+:\d+>")]
    private static partial Regex EmojiPattern();
}

public readonly record struct AllCapsEvaluation(bool ShouldTrigger, int LetterCount, int UppercaseCount);
