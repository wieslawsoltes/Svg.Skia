using Microsoft.Maui.Controls;

namespace SvgML.Maui.Demo;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		MainPage = new AppShell();
	}
}
