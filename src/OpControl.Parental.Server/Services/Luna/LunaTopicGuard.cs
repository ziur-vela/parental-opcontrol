// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.RegularExpressions;

namespace OpControl.Parental.Server.Services.Luna;

public sealed record LunaTopicMatch(string CategoryKey);

/// <summary>
/// Catches child messages that are clearly about a category the child's DNS
/// filter blocks. The model moderators do not have a gambling category, so a
/// model refusal alone would otherwise leave no trace for the parent.
/// </summary>
public static class LunaTopicGuard
{
    public const string PrivacyCategory = "privacy";
    public const string BypassCategory = "bypass";

    public static readonly string[] TopicCategories = ["adult", "gambling", "social", "fakenews", "ai"];

    private static readonly Regex Adult = new(
        @"\b(porn|porno|pornograf\w*|xxx|onlyfans|nsfw|hentai|nudes?|desnudos?|" +
        @"naked (pictures?|photos?|videos?)|fotos? desnudas?|sexo expl[ií]cito|videos? para adultos?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Gambling = new(
        @"\b(online casino|casino online|casino games?|juegos? de casino|play(?:ing)? (?:the )?" +
        @"(?:casino|poker|slots?|roulette|blackjack)|win (?:money|real money|cash)|" +
        @"make money (?:from|with|playing)|bet (?:real )?money|place (?:a )?bet|sports bet(?:ting)?|" +
        @"betting (?:site|website)|gambling (?:site|website)|slot machines?|jackpot|bookmaker|" +
        @"odds of winning|ruleta|tragamonedas|apuestas? (?:deportivas|online|en l[ií]nea)|" +
        @"apostar (?:dinero|en)|ganar dinero (?:con|en) (?:el casino|las apuestas)|juegos? de azar|" +
        @"p[oó]ker|blackjack|baccarat|lottery|loteria|loter[ií]a)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GamblingAction = new(
        @"\b(online|win|money|real|play|bet|apuesta|apostar|ganar|dinero|casino games?|juegos? de casino|" +
        @"tragamonedas|ruleta|p[oó]ker|blackjack|slots?|jackpot)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Social = new(
        @"\b(tiktok|instagram|insta|snapchat|discord|facebook|whatsapp|twitter|x\.com|" +
        @"social media|redes sociales)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SocialAction = new(
        @"\b(trends?|viral|followers?|likes?|post(?:ing)?|videos?|accounts?|famous|dance|challenges?|" +
        @"dms?|follow|unblock|download|use|open|addict\w*|algorithm|fyp|snaps?|stories|reels?|" +
        @"tendencias?|seguidores?|publicar|viral(?:es)?|cuentas?|famos[oa]|bailes?|retos?)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Ai = new(
        @"\b(chatgpt|chat gpt|character\.ai|character ai|janitor ai|poe\.com|perplexity|" +
        @"uncensored ai|jailbreak(?:ed)? (?:ai|chatgpt)|ai (?:girlfriend|boyfriend))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AiAction = new(
        @"\b(chat|talk|use|open|make|create|roleplay|jailbreak|bypass|unblock|without (?:a )?(?:filter|limit)|" +
        @"no filter|girlfriend|boyfriend|uncensored|hablar|usar|abrir|crear|sin filtro)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FakeNews = new(
        @"\b(fake news|noticias falsas|conspiracy theories?|conspiracy|teor[ií]as? de la conspiraci[oó]n|" +
        @"qanon|pizzagate)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FakeNewsAction = new(
        @"\b(find|read|share|spread|believe|prove|proof|website|site|where can i|buscar|leer|compartir|" +
        @"creer|probar|demostrar|sitio|p[aá]gina|d[oó]nde puedo)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Bypass = new(
        @"\b(bypass|get(?:ting)? around|unblock|evade|disable|turn off|saltar|evadir|desbloquear|desactivar)\b" +
        @".{0,40}\b(parental|block|blocked|filter|controls?|school|restrictions?|bloqueo|filtro|" +
        @"control parental|restricci[oó]n)\b|" +
        @"\b(bypass|unblock|evade|saltar|evadir|desbloquear)\b.{0,25}\b(website|site|game|app|filter|block|" +
        @"sitio|p[aá]gina|juego|aplicaci[oó]n|filtro|bloqueo)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PrivacyTopic = new(
        @"\b(home address|address|phone number|mobile number|cell number|where|d[oó]nde|" +
        @"direcci[oó]n|n[uú]mero de tel[eé]fono|tel[eé]fono)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PrivacyAction = new(
        @"\b(live|lives|living|stay|stays|vive|viven|vivir|vive en|domicilio)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PersonContext = new(
        @"\b(classmate|schoolmate|friend|neighbor|neighbour|teacher|crush|girlfriend|boyfriend|" +
        @"compa[nñ]er[oa]|amig[oa]|vecin[oa]|maestr[oa]|novi[oa])\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PersonName = new(
        @"\b\p{Lu}\p{Ll}+(?:\s+\p{Lu}\p{Ll}+)+\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Returns the blocked category the message is clearly about, or null.
    /// The check is deliberately narrow: school questions about dice, Monopoly,
    /// or the history of Las Vegas must pass.
    /// </summary>
    public static LunaTopicMatch? Topic(string? text, IReadOnlySet<string> enabled)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var value = text.ToLowerInvariant();

        if (enabled.Contains("adult") && Adult.IsMatch(value))
            return new LunaTopicMatch("adult");

        if (enabled.Contains("gambling") && IsGambling(value))
            return new LunaTopicMatch("gambling");

        if (enabled.Contains("social") && Social.IsMatch(value) && SocialAction.IsMatch(value))
            return new LunaTopicMatch("social");

        if (enabled.Contains("ai") && IsAi(value))
            return new LunaTopicMatch("ai");

        if (enabled.Contains("fakenews") && FakeNews.IsMatch(value) && FakeNewsAction.IsMatch(value))
            return new LunaTopicMatch("fakenews");

        if (Bypass.IsMatch(value))
            return new LunaTopicMatch(BypassCategory);

        return null;
    }

    /// <summary>
    /// A conservative personal-information request. Kept low priority: visible in
    /// review, but no push alert. "Where do polar bears live?" must pass.
    /// </summary>
    public static bool Privacy(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.ToLowerInvariant();
        if (!PrivacyTopic.IsMatch(value)) return false;

        var relation = PersonContext.IsMatch(value);
        var explicitContact = Regex.IsMatch(value, @"\b(home address|address|phone number|mobile number|" +
            @"cell number|direcci[oó]n|n[uú]mero de tel[eé]fono|tel[eé]fono)\b",
            RegexOptions.IgnoreCase);
        if (relation && (explicitContact || PrivacyAction.IsMatch(value))) return true;
        return explicitContact && PersonName.IsMatch(text);
    }

    /// <summary>True while a topic-category decline still deserves a parent alert.</summary>
    public static bool IsTopicCategory(string key) =>
        TopicCategories.Contains(key, StringComparer.OrdinalIgnoreCase);

    private static bool IsGambling(string value)
    {
        if (!Gambling.IsMatch(value)) return false;
        var schoolContext = Regex.IsMatch(value,
            @"\b(monopoly|board game|juego de mesa|history of|school report|for school|para la escuela|" +
            @"class project|proyecto escolar|probability|probabilidad|math homework|tarea de matem[aá]ticas)\b",
            RegexOptions.IgnoreCase);
        return !schoolContext || GamblingAction.IsMatch(value) &&
            Regex.IsMatch(value, @"\b(online|win|money|real|apuesta|apostar|ganar|dinero)\b", RegexOptions.IgnoreCase);
    }

    private static bool IsAi(string value)
    {
        if (!Ai.IsMatch(value)) return false;
        if (Regex.IsMatch(value, @"\b(character\.ai|character ai|janitor ai|poe\.com)\b",
                RegexOptions.IgnoreCase))
            return true;
        return AiAction.IsMatch(value);
    }
}
