using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PoproshaykaBot.Core.Settings;

namespace PoproshaykaBot.Wpf.ViewModels.Settings;

public sealed partial class RateLimitingSettingsViewModel : ObservableObject, IDisposable
{
	private const int MessagesAllowedMinimum = 1;
	private const int MessagesAllowedMaximum = 1000;
	private const int ThrottlingPeriodMinimum = 1;
	private const int ThrottlingPeriodMaximum = 300;

	private static readonly TwitchSettings DefaultSettings = new();

	[ObservableProperty]
	private int _messagesAllowedInPeriod;

	[ObservableProperty]
	private int _throttlingPeriodSeconds;

	public RateLimitingSettingsViewModel()
	{
	}

	public void LoadSettings(TwitchSettings settings)
	{
		MessagesAllowedInPeriod = settings.MessagesAllowedInPeriod;
		ThrottlingPeriodSeconds = settings.ThrottlingPeriodSeconds;
	}

	public void SaveSettings(TwitchSettings settings)
	{
		settings.MessagesAllowedInPeriod = MessagesAllowedInPeriod;
		settings.ThrottlingPeriodSeconds = ThrottlingPeriodSeconds;
	}

	[RelayCommand]
	private void IncreaseMessagesAllowed()
	{
		if (MessagesAllowedInPeriod < MessagesAllowedMaximum)
		{
			MessagesAllowedInPeriod++;
		}
	}

	[RelayCommand]
	private void DecreaseMessagesAllowed()
	{
		if (MessagesAllowedInPeriod > MessagesAllowedMinimum)
		{
			MessagesAllowedInPeriod--;
		}
	}

	[RelayCommand]
	private void IncreaseThrottlingPeriod()
	{
		if (ThrottlingPeriodSeconds < ThrottlingPeriodMaximum)
		{
			ThrottlingPeriodSeconds++;
		}
	}

	[RelayCommand]
	private void DecreaseThrottlingPeriod()
	{
		if (ThrottlingPeriodSeconds > ThrottlingPeriodMinimum)
		{
			ThrottlingPeriodSeconds--;
		}
	}

	public void ResetMessagesAllowed()
	{
		MessagesAllowedInPeriod = DefaultSettings.MessagesAllowedInPeriod;
	}

	public void ResetThrottlingPeriod()
	{
		ThrottlingPeriodSeconds = DefaultSettings.ThrottlingPeriodSeconds;
	}

	public void Dispose()
	{
	}
}
