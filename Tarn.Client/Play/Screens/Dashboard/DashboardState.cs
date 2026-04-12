using Tarn.ClientApp.Play.Queries;

namespace Tarn.ClientApp.Play.Screens.Dashboard;

public sealed class DashboardState
{
    public int SelectedActionIndex { get; set; }
    public DashboardViewModel? ViewModel { get; set; }
}
