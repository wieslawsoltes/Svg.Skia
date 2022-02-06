using Avalonia.ReactiveUI;
using Avalonia.Web.Blazor;

namespace TestApp.Web;

public partial class App
{
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        WebAppBuilder.Configure<TestApp.App>()
            .UseReactiveUI()
            .SetupWithSingleViewLifetime();
    }
}
