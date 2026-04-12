using Tarn.ClientApp.Play.Rendering;
using Tarn.ClientApp.Play.Screens.Collection;
using Tarn.ClientApp.Play.Screens.Collector;
using Tarn.ClientApp.Play.Screens.Dashboard;
using Tarn.ClientApp.Play.Screens.Deck;
using Tarn.ClientApp.Play.Screens.League;
using Tarn.ClientApp.Play.Screens.Market;
using Tarn.ClientApp.Play.Screens.MatchCenter;
using Tarn.ClientApp.Play.Screens.Schedule;
using Tarn.ClientApp.Play.Screens.WeekSummary;

namespace Tarn.ClientApp.Play.App;

public static class PlayScreenCatalog
{
    private static readonly IReadOnlyList<PlayScreenDefinition> AllScreens =
    [
        new(
            ScreenId.Dashboard,
            "Dashboard",
            "Dash",
            "Dashboard",
            InputAction.Screen1,
            "Arrows move | Enter select | A advance | ? help | Q quit",
            "Dashboard: arrows move, Enter selects, A advances the week.",
            new DashboardController(),
            DashboardRenderer.Render,
            null),
        new(
            ScreenId.Schedule,
            "Schedule",
            "Sch",
            "Schedule",
            InputAction.Screen2,
            "Arrows move/week | Enter replay | Esc back | ? help",
            "Schedule: Left/Right changes week, Enter opens an available replay.",
            new ScheduleController(),
            ScheduleRenderer.Render,
            static (refresh, state) => refresh.RefreshSchedule(state)),
        new(
            ScreenId.MatchCenter,
            "Match Center",
            "Match",
            "Match",
            InputAction.Screen3,
            "N event | R round | P autoplay | Esc back | ? help",
            "Replay: N steps events, R jumps rounds, P toggles autoplay.",
            new MatchCenterController(),
            MatchCenterRenderer.Render,
            static (refresh, state) => refresh.RefreshMatchCenter(state)),
        new(
            ScreenId.WeekSummary,
            "Week Summary",
            "Sum",
            "Summary",
            InputAction.Screen4,
            "Arrows move | Enter select | Esc back | ? help",
            "Summary: arrows move between actions and Enter opens the selected action.",
            new WeekSummaryController(),
            WeekSummaryRenderer.Render,
            null),
        new(
            ScreenId.League,
            "League",
            "Lg",
            "League",
            InputAction.Screen5,
            "Arrows move/league | Esc back | ? help",
            "League: Up/Down changes focus, Left/Right changes the league view.",
            new LeagueController(),
            LeagueRenderer.Render,
            static (refresh, state) => refresh.RefreshLeague(state)),
        new(
            ScreenId.Collection,
            "Collection",
            "Col",
            "Collection",
            InputAction.Screen6,
            "Arrows move/filter | Enter sort | Esc back | ? help",
            "Collection: Left/Right changes filter and Enter cycles sort.",
            new CollectionController(),
            CollectionRenderer.Render,
            static (refresh, state) => refresh.RefreshCollection(state)),
        new(
            ScreenId.Deck,
            "Deck",
            "Deck",
            "Deck",
            InputAction.Screen7,
            "Arrows move | Enter auto-build | Esc back | ? help",
            "Deck: Enter auto-builds the best legal deck from your collection.",
            new DeckController(),
            DeckRenderer.Render,
            static (refresh, state) => refresh.RefreshDeck(state)),
        new(
            ScreenId.Collector,
            "Collector",
            "Shop",
            "Collector",
            InputAction.Screen8,
            "Arrows move/tab | Enter confirm | Esc back | ? help",
            "Collector: Left/Right switches tabs and Enter confirms buy, open, or sell actions.",
            new CollectorController(),
            CollectorRenderer.Render,
            static (refresh, state) => refresh.RefreshCollector(state)),
        new(
            ScreenId.Market,
            "Market",
            "Market",
            "Market",
            null,
            "Arrows move/tab | N/R amount | Enter confirm | Esc back",
            "Market: Left/Right switches tabs, N/R adjusts amounts, and Enter confirms market actions.",
            new MarketController(),
            MarketRenderer.Render,
            static (refresh, state) => refresh.RefreshMarket(state)),
    ];

    private static readonly IReadOnlyDictionary<ScreenId, PlayScreenDefinition> ScreensById = AllScreens.ToDictionary(screen => screen.Id);
    private static readonly IReadOnlyDictionary<InputAction, PlayScreenDefinition> ScreensByShortcut = AllScreens
        .Where(screen => screen.ShortcutAction is not null)
        .ToDictionary(screen => screen.ShortcutAction!.Value);

    public static PlayScreenDefinition Get(ScreenId screenId) => ScreensById[screenId];

    public static bool TryGetShortcut(InputAction action, out PlayScreenDefinition screen) => ScreensByShortcut.TryGetValue(action, out screen!);

    public static string BuildGlobalNavigationText(bool compact, string prefix)
    {
        var labels = AllScreens
            .Where(screen => screen.ShortcutAction is not null)
            .Select(screen => $"{ResolveShortcutNumber(screen.ShortcutAction!.Value)} {(compact ? screen.CompactLabel : screen.FullLabel)}");
        return prefix + string.Join(compact ? " " : "  ", labels);
    }

    private static int ResolveShortcutNumber(InputAction action) => action switch
    {
        InputAction.Screen1 => 1,
        InputAction.Screen2 => 2,
        InputAction.Screen3 => 3,
        InputAction.Screen4 => 4,
        InputAction.Screen5 => 5,
        InputAction.Screen6 => 6,
        InputAction.Screen7 => 7,
        InputAction.Screen8 => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Only screen shortcut actions are supported."),
    };
}
