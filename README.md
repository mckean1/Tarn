# Tarn MVP Simulator

Run a deterministic single Tarn match replay with:

```bash
dotnet run --project Tarn.Client -- match --seed 123
```

Run the automated tests with:

```bash
dotnet test
```

Notes:

- The simulator hardcodes the current Tarn rules and current card pool directly in code.
- The same seed will produce the same champions, decks, replay log, and winner.
- Fatigue and Overtime are intentionally isolated in the engine so those policies can be updated later without a full rewrite.
