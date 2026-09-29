using PoproshaykaBot.Wpf.ViewModels.Settings;
using System.Windows.Controls;

namespace PoproshaykaBot.Wpf.Views.Settings;

public partial class RateLimitingSettingsView : UserControl, IView<RateLimitingSettingsViewModel>
{
	public RateLimitingSettingsView()
	{
		InitializeComponent();
	}
}
