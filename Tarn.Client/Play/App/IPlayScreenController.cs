namespace Tarn.ClientApp.Play.App;

public interface IPlayScreenController
{
    ScreenControllerResult Handle(AppState state, InputAction action);
}
