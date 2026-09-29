using Microsoft.Extensions.Logging;
using PoproshaykaBot.Core.Obs;
using PoproshaykaBot.WinForms.Infrastructure.Di;
using System.Drawing.Drawing2D;

namespace PoproshaykaBot.WinForms.Widgets;

public sealed partial class ObsOutputStatusCard : UserControl
{
    private static readonly Color ColorIdle = Color.FromArgb(140, 148, 168);
    private static readonly Color ColorActive = Color.FromArgb(220, 38, 56);
    private static readonly Color ColorPaused = Color.FromArgb(230, 150, 30);
    private static readonly Color ColorError = Color.FromArgb(200, 50, 80);
    private static readonly Color ColorUnknown = Color.Silver;

    private ObsOutputCardKind _kind = ObsOutputCardKind.Stream;
    private ObsOutputCardState _state = ObsOutputCardState.Unknown;
    private string? _secondaryChip;
    private ObsCardChipTone _secondaryChipTone;
    private double _pulsePhaseSeconds;
    private bool _initialized;
    private bool _actionInFlight;

    public ObsOutputStatusCard()
    {
        InitializeComponent();
    }

    [Inject]
    public ObsIntegrationService ObsIntegration { get; internal init; } = null!;

    [Inject]
    public ILogger<ObsOutputStatusCard> Logger { get; internal init; } = null!;

    public void Configure(ObsOutputCardKind kind)
    {
        _kind = kind;
        _kickerLabel.Text = kind switch
        {
            ObsOutputCardKind.Stream => "ЭФИР",
            ObsOutputCardKind.Record => "ЗАПИСЬ",
            _ => string.Empty,
        };

        var streamMode = kind == ObsOutputCardKind.Stream;
        _secondaryChipLabel.Visible = streamMode;
        _secondaryButton.Visible = !streamMode;

        ApplyVisualState();
    }

    public void ApplySnapshot(
        bool? active,
        bool? paused,
        string? timecode,
        string? meta,
        string? secondaryChip = null,
        ObsCardChipTone secondaryChipTone = ObsCardChipTone.Neutral)
    {
        var nextState = ResolveState(active, paused);
        _state = nextState;
        _timecodeLabel.Text = string.IsNullOrWhiteSpace(timecode)
            ? "–:–:–"
            : FormatTimecode(timecode);

        _metaLabel.Text = meta ?? string.Empty;
        _secondaryChip = secondaryChip;
        _secondaryChipTone = secondaryChipTone;
        ApplyVisualState();
    }

    public void ApplyUnavailable(string? errorMessage)
    {
        _state = ObsOutputCardState.Unknown;
        _timecodeLabel.Text = "–:–:–";
        _metaLabel.Text = string.IsNullOrWhiteSpace(errorMessage) ? "OBS недоступен" : errorMessage;
        _secondaryChip = null;
        _secondaryChipTone = ObsCardChipTone.Neutral;
        ApplyVisualState();
    }

    public void ApplyUnknown()
    {
        _state = ObsOutputCardState.Unknown;
        _timecodeLabel.Text = "–:–:–";
        _metaLabel.Text = string.Empty;
        _secondaryChip = null;
        _secondaryChipTone = ObsCardChipTone.Neutral;
        ApplyVisualState();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_initialized)
        {
            return;
        }

        if (this.IsInDesignMode())
        {
            ApplyVisualState();
            return;
        }

        _initialized = true;
        ApplyVisualState();
        Disposed += OnCardDisposed;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        var (color, alpha) = ResolveBorderAccent();
        using var pen = new Pen(Color.FromArgb(alpha, color), 2F);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawRectangle(pen, bounds);
    }

    private void OnPulseTimerTick(object? sender, EventArgs e)
    {
        _pulsePhaseSeconds += _pulseTimer.Interval / 1000D;
        if (_pulsePhaseSeconds > 1000D)
        {
            _pulsePhaseSeconds -= 1000D;
        }

        if (_state == ObsOutputCardState.Paused)
        {
            ApplyStateLabelPulse();
        }

        _dotPanel.Invalidate();
        Invalidate();
    }

    private void OnDotPanelPaint(object? sender, PaintEventArgs e)
    {
        var color = ResolveStateColor(_state);
        var (fillColor, diameter) = ResolveDotAccent(color);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var clientSize = _dotPanel.ClientSize;
        var x = (clientSize.Width - diameter) / 2F;
        var y = (clientSize.Height - diameter) / 2F;

        using var brush = new SolidBrush(fillColor);
        e.Graphics.FillEllipse(brush, x, y, diameter, diameter);

        if (_state is ObsOutputCardState.Active or ObsOutputCardState.Paused)
        {
            var haloAlpha = (int)Math.Clamp(fillColor.A * 0.35D, 0D, 90D);
            using var halo = new SolidBrush(Color.FromArgb(haloAlpha, color));
            var haloDiameter = diameter + 4F;
            e.Graphics.FillEllipse(halo,
                x - 2F,
                y - 2F,
                haloDiameter,
                haloDiameter);
        }
    }

    private void OnPrimaryButtonClick(object? sender, EventArgs e)
    {
        if (_actionInFlight)
        {
            return;
        }

        var action = ResolvePrimaryAction();
        if (action is null)
        {
            return;
        }

        _ = InvokeObsActionAsync(action);
    }

    private void OnSecondaryButtonClick(object? sender, EventArgs e)
    {
        if (_actionInFlight || _kind != ObsOutputCardKind.Record)
        {
            return;
        }

        if (_state is not (ObsOutputCardState.Active or ObsOutputCardState.Paused))
        {
            return;
        }

        _ = InvokeObsActionAsync(token => ObsIntegration.ToggleRecordPauseAsync(token));
    }

    private void OnCardDisposed(object? sender, EventArgs e)
    {
        _pulseTimer.Stop();
    }

    private static string FormatTimecode(string timecode)
    {
        var dotIndex = timecode.IndexOf('.');
        var withoutMilliseconds = dotIndex >= 0 ? timecode[..dotIndex] : timecode;

        var parts = withoutMilliseconds.Split(':');
        if (parts.Length != 3)
        {
            return withoutMilliseconds;
        }

        var trimmedHours = parts[0].TrimStart('0');
        return trimmedHours.Length == 0
            ? $"{parts[1]}:{parts[2]}"
            : $"{trimmedHours}:{parts[1]}:{parts[2]}";
    }

    private static ObsOutputCardState ResolveState(bool? active, bool? paused)
    {
        return (active, paused) switch
        {
            (true, true) => ObsOutputCardState.Paused,
            (true, _) => ObsOutputCardState.Active,
            (false, _) => ObsOutputCardState.Idle,
            _ => ObsOutputCardState.Unknown,
        };
    }

    private static Color ResolveStateColor(ObsOutputCardState state)
    {
        return state switch
        {
            ObsOutputCardState.Active => ColorActive,
            ObsOutputCardState.Paused => ColorPaused,
            ObsOutputCardState.Idle => ColorIdle,
            ObsOutputCardState.Error => ColorError,
            _ => ColorUnknown,
        };
    }

    private static Color Lerp(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0D, 1D);
        return Color.FromArgb((int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));
    }

    private static Color ResolveChipToneColor(ObsCardChipTone tone)
    {
        return tone switch
        {
            ObsCardChipTone.Ok => Color.Green,
            ObsCardChipTone.Warn => Color.Orange,
            ObsCardChipTone.Bad => Color.Red,
            _ => Color.Gray,
        };
    }

    private Func<CancellationToken, Task>? ResolvePrimaryAction()
    {
        return (_kind, _state) switch
        {
            (ObsOutputCardKind.Stream, ObsOutputCardState.Idle) => token => ObsIntegration.StartStreamAsync(token),
            (ObsOutputCardKind.Stream, ObsOutputCardState.Active) => token => ObsIntegration.StopStreamAsync(token),
            (ObsOutputCardKind.Record, ObsOutputCardState.Idle) => token => ObsIntegration.StartRecordAsync(token),
            (ObsOutputCardKind.Record, ObsOutputCardState.Active) => token => ObsIntegration.StopRecordAsync(token),
            (ObsOutputCardKind.Record, ObsOutputCardState.Paused) => token => ObsIntegration.StopRecordAsync(token),
            _ => null,
        };
    }

    private async Task InvokeObsActionAsync(Func<CancellationToken, Task> action)
    {
        _actionInFlight = true;
        SetButtonsEnabled(false);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await action(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (IsDisposed)
            {
                return;
            }

            ShowMetaError("превышено время ожидания");
        }
        catch (Exception exception)
        {
            if (IsDisposed)
            {
                return;
            }

            Logger.LogWarning(exception, "Не удалось выполнить OBS-действие для карточки {Kind}", _kind);
            ShowMetaError("не удалось выполнить");
        }
        finally
        {
            _actionInFlight = false;
            if (!IsDisposed)
            {
                ApplyVisualState();
            }
        }
    }

    private void ShowMetaError(string message)
    {
        _metaLabel.Text = $"⚠ {message}";
        _metaLabel.ForeColor = ColorActive;
    }

    private void ApplyVisualState()
    {
        var color = ResolveStateColor(_state);

        _stateLabel.Text = ResolveStateText();
        _stateLabel.ForeColor = color;

        if (!_metaLabel.Text.StartsWith('⚠'))
        {
            _metaLabel.ForeColor = Color.Gray;
        }

        _timecodeLabel.ForeColor = _state switch
        {
            ObsOutputCardState.Active => Color.Black,
            ObsOutputCardState.Paused => ColorPaused,
            _ => Color.DimGray,
        };

        ApplyPrimaryButtonState();
        ApplySecondaryButtonState();
        ApplyPulseTimerState();

        _dotPanel.Invalidate();
        Invalidate();
    }

    private string ResolveStateText()
    {
        return (_kind, _state) switch
        {
            (_, ObsOutputCardState.Unknown) => "–",
            (_, ObsOutputCardState.Idle) => "Готов",
            (ObsOutputCardKind.Stream, ObsOutputCardState.Active) => "В ЭФИРЕ",
            (ObsOutputCardKind.Record, ObsOutputCardState.Active) => "ЗАПИСЬ",
            (_, ObsOutputCardState.Paused) => "ПАУЗА",
            (_, ObsOutputCardState.Error) => "Ошибка",
            _ => "–",
        };
    }

    private void ApplyPrimaryButtonState()
    {
        var (text, enabled, intent) = (_kind, _state) switch
        {
            (ObsOutputCardKind.Stream, ObsOutputCardState.Idle) => ("▶ Старт", true, Color.Green),
            (ObsOutputCardKind.Stream, ObsOutputCardState.Active) => ("■ Стоп", true, Color.Red),
            (ObsOutputCardKind.Record, ObsOutputCardState.Idle) => ("● Старт", true, Color.Green),
            (ObsOutputCardKind.Record, ObsOutputCardState.Active) => ("■ Стоп", true, Color.Red),
            (ObsOutputCardKind.Record, ObsOutputCardState.Paused) => ("■ Стоп", true, Color.Red),
            _ => ("–", false, Color.Gray),
        };

        _primaryButton.Text = text;
        _primaryButton.Enabled = enabled && !_actionInFlight;
        _primaryButton.ForeColor = enabled ? intent : Color.Gray;
        _primaryButton.FlatAppearance.BorderColor = Color.Silver;
    }

    private void ApplySecondaryButtonState()
    {
        if (_kind != ObsOutputCardKind.Record)
        {
            ApplyStreamChip();
            return;
        }

        var (text, enabled, intent) = _state switch
        {
            ObsOutputCardState.Active => ("⏸ Пауза", true, Color.Orange),
            ObsOutputCardState.Paused => ("▶ Дальше", true, Color.Green),
            _ => ("⏸ Пауза", false, Color.Gray),
        };

        _secondaryButton.Text = text;
        _secondaryButton.Enabled = enabled && !_actionInFlight;
        _secondaryButton.ForeColor = enabled ? intent : Color.Gray;
        _secondaryButton.FlatAppearance.BorderColor = Color.Silver;

        if (_state == ObsOutputCardState.Paused)
        {
            _secondaryButton.UseVisualStyleBackColor = false;
            _secondaryButton.BackColor = Color.LemonChiffon;
        }
        else
        {
            _secondaryButton.BackColor = SystemColors.Control;
            _secondaryButton.UseVisualStyleBackColor = true;
        }
    }

    private void ApplyStreamChip()
    {
        var (text, color) = ResolveStreamChip();
        _secondaryChipLabel.Text = text;
        _secondaryChipLabel.ForeColor = color;
    }

    private (string Text, Color Color) ResolveStreamChip()
    {
        if (!string.IsNullOrWhiteSpace(_secondaryChip))
        {
            return (_secondaryChip, ResolveChipToneColor(_secondaryChipTone));
        }

        return _state switch
        {
            ObsOutputCardState.Active => ("в эфире", Color.Green),
            ObsOutputCardState.Idle => ("офлайн", Color.Gray),
            ObsOutputCardState.Error => ("OBS WS", Color.Red),
            _ => ("–", Color.Silver),
        };
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _primaryButton.Enabled = enabled && _primaryButton.Text != "–";
        _secondaryButton.Enabled = enabled
                                   && _kind == ObsOutputCardKind.Record
                                   && _state is ObsOutputCardState.Active or ObsOutputCardState.Paused;
    }

    private void ApplyPulseTimerState()
    {
        var shouldPulse = _state is ObsOutputCardState.Active or ObsOutputCardState.Paused;
        if (shouldPulse && !_pulseTimer.Enabled)
        {
            _pulsePhaseSeconds = 0D;
            _pulseTimer.Start();
        }
        else if (!shouldPulse && _pulseTimer.Enabled)
        {
            _pulseTimer.Stop();
        }
    }

    private void ApplyStateLabelPulse()
    {
        var color = ResolveStateColor(_state);
        var phase = (Math.Sin(2D * Math.PI * _pulsePhaseSeconds / 1.8D) + 1D) / 2D;
        var lerp = 0.45D + 0.55D * phase;
        _stateLabel.ForeColor = Lerp(SystemColors.Control, color, lerp);
    }

    private (Color BorderColor, int Alpha) ResolveBorderAccent()
    {
        var color = ResolveStateColor(_state);

        return _state switch
        {
            ObsOutputCardState.Paused => (color, ComputePulseAlpha(60, 220, 1.8D)),
            ObsOutputCardState.Active => (color, 180),
            ObsOutputCardState.Error => (color, 200),
            ObsOutputCardState.Idle => (color, 110),
            _ => (color, 80),
        };
    }

    private (Color FillColor, float Diameter) ResolveDotAccent(Color baseColor)
    {
        const float BaseDiameter = 10F;

        switch (_state)
        {
            case ObsOutputCardState.Paused:
                {
                    var phase = (Math.Sin(2D * Math.PI * _pulsePhaseSeconds / 1.2D) + 1D) / 2D;
                    var alpha = (int)Math.Round(140D + 115D * phase);
                    var diameter = BaseDiameter * (float)(0.85D + 0.30D * phase);
                    return (Color.FromArgb(Math.Clamp(alpha, 0, 255), baseColor), diameter);
                }

            case ObsOutputCardState.Active:
                {
                    var phase = Math.Pow((Math.Sin(2D * Math.PI * _pulsePhaseSeconds / 1.6D) + 1D) / 2D, 4D);
                    var diameter = BaseDiameter * (float)(1.0D + 0.25D * phase);
                    return (baseColor, diameter);
                }

            default:
                return (baseColor, BaseDiameter);
        }
    }

    private int ComputePulseAlpha(int min, int max, double periodSeconds)
    {
        var phase = (Math.Sin(2D * Math.PI * _pulsePhaseSeconds / periodSeconds) + 1D) / 2D;
        return (int)Math.Round(min + (max - min) * phase);
    }
}
