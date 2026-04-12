# Tarn MVP Simulator

Run a deterministic single Tarn match replay with:

```bash
dotnet run --project Tarn.Client -- match --seed 123
```

Launch the interactive terminal UI foundation with:

```bash
dotnet run --project Tarn.Client -- play
```

Run the automated tests with:

```bash
dotnet test
```

Notes:

- The simulator hardcodes the current Tarn rules and current card pool directly in code.
- The same seed will produce the same champions, decks, replay log, and winner.
- Fatigue and Overtime are intentionally isolated in the engine so those policies can be updated later without a full rewrite.
- `play` currently provides the shared shell, navigation, modal, message bar, and save/refresh wiring for the interactive UI.
- Global play keys: `1-4` navigate screens, arrows move, `Enter` selects, `Esc` backs out, `A` advances week from Dashboard, `Q` quits, `?`/`H` opens help.
- First playable loop: start on Dashboard, open Schedule, inspect nearby fixtures, open Match Center when a replay is available, advance the week from Dashboard, confirm with `Y`, then review the generated Week Summary and return to Dashboard.
- Strategy screens: `5` opens League standings, `6` opens Collection browsing with filter/sort controls, and `7` opens Deck review where `Enter` auto-builds the best legal deck.
- Economy screens: `8` opens Collector with Singles, Packs, and Sell tabs. Market is available from Dashboard, with bid/listing amounts adjusted by `N` and `R`, and successful pack purchases show a text reveal overlay.
