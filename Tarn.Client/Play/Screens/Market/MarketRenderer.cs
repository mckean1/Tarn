using Tarn.ClientApp.Play.App;
using Tarn.ClientApp.Play.Queries;
using Tarn.ClientApp.Play.Rendering;

namespace Tarn.ClientApp.Play.Screens.Market;

public static class MarketRenderer
{
    public static string Render(AppState state, Rect body)
    {
        var model = state.Market.ViewModel;
        if (model is null)
        {
            return "Market unavailable.";
        }

        var lines = new List<string>
        {
            $"Market | Tab: {model.Tab}",
            $"Price/Bid: {model.ProposedBidOrPrice} | Duration: {model.ProposedDurationWeeks} week",
            "Left/Right tabs, Up/Down selection, N/R adjust amount, Enter confirm.",
        };

        if (model.Rows.Count == 0)
        {
            lines.Add(ScreenText.EmptyState(
                "Market",
                model.Tab switch
                {
                    MarketTab.Browse => "No active auctions are on the board.",
                    MarketTab.MyListings => "You do not have any listings yet.",
                    _ => "No cards are currently eligible for listing.",
                },
                body.Width));
        }
        else
        {
            lines.AddRange(model.Rows.Take(Math.Max(1, body.Height - 9)).Select((row, index) =>
                ScreenText.InteractiveRow(
                    index == state.Market.SelectedIndex,
                    $"{Layout.Truncate(row.CardName, 20).TrimEnd()} {Layout.Truncate(row.Seller, 10).TrimEnd()} {row.CurrentBid,4} {Layout.Truncate(row.TimeLeft, 14).TrimEnd()} {row.BidCount,3} {row.Status}")));
        }

        if (model.Detail is not null)
        {
            lines.Add(string.Empty);
            lines.Add($"Detail: {model.Detail.CardName}");
            lines.Add(model.Detail.StatsText);
            lines.Add(model.Detail.RulesText);
            lines.Add($"Seller: {model.Detail.Seller} | Bid count: {model.Detail.BidCount} | Status: {model.Detail.Status}");
        }

        return ScreenText.FitLines(lines, body.Width, body.Height);
    }
}
