using FlaUI.Core.Definitions;

namespace PoproshaykaBot.WinForms.Tests;

[TestFixture]
[NonParallelizable]
public class OnboardingWizardSmokeTests
{
    [SetUp]
    public void Setup()
    {
        _session = SmokeTestSession.Launch(new() { SeedConfiguredSettings = false });
    }

    [TearDown]
    public void TearDown()
    {
        _session?.Dispose();
        _session = null;
    }

    private const string WizardTitle = "Первичная настройка";
    private static readonly TimeSpan WizardAppearTimeout = TimeSpan.FromSeconds(15);

    private SmokeTestSession? _session;

    [Test]
    public void Wizard_ShouldOpen_OnFirstLaunch_WhenClientIdIsEmpty()
    {
        var wizard = _session!.FindTopLevelWindow(w => string.Equals(w.Title, WizardTitle, StringComparison.Ordinal),
            WizardAppearTimeout);

        Assert.That(wizard, Is.Not.Null,
            $"Окно '{WizardTitle}' должно открыться на старте, когда settings.json не содержит clientId");
    }

    [Test]
    public void Wizard_ShouldExposeNavigationButtons()
    {
        var wizard = _session!.FindTopLevelWindow(w => string.Equals(w.Title, WizardTitle, StringComparison.Ordinal),
            WizardAppearTimeout);

        Assert.That(wizard, Is.Not.Null, "Мастер должен быть найден перед проверкой кнопок навигации");

        var nextButton = wizard!.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Далее →")));

        var cancelButton = wizard.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("Отмена")));

        var backButton = wizard.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Button).And(cf.ByName("← Назад")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nextButton, Is.Not.Null, "Кнопка 'Далее →' должна быть в мастере");
            Assert.That(cancelButton, Is.Not.Null, "Кнопка 'Отмена' должна быть в мастере");
            Assert.That(backButton, Is.Not.Null, "Кнопка '← Назад' должна быть в мастере");
        }
    }

    [Test]
    public void Wizard_ShouldStartOnFirstStep()
    {
        var wizard = _session!.FindTopLevelWindow(w => string.Equals(w.Title, WizardTitle, StringComparison.Ordinal),
            WizardAppearTimeout);

        Assert.That(wizard, Is.Not.Null, "Мастер должен быть найден перед проверкой шага");

        var stepLabel = wizard!.FindFirstDescendant(cf =>
            cf.ByControlType(ControlType.Text).And(cf.ByName("Шаг 1 из 8")));

        Assert.That(stepLabel, Is.Not.Null,
            "Мастер должен открываться на первом шаге из 8 – текст 'Шаг 1 из 8' должен быть в окне");
    }

    [Test]
    public void Wizard_ShouldOpenOffScreen()
    {
        var wizard = _session!.FindTopLevelWindow(w => string.Equals(w.Title, WizardTitle, StringComparison.Ordinal),
            WizardAppearTimeout);

        Assert.That(wizard, Is.Not.Null, "Мастер должен быть найден перед проверкой координат");

        var bounds = wizard!.BoundingRectangle;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bounds.Width, Is.GreaterThan(0), "Ширина мастера должна быть положительной, иначе проверка координат ничего не значит");
            Assert.That(bounds.Height, Is.GreaterThan(0), "Высота мастера должна быть положительной, иначе проверка координат ничего не значит");
            Assert.That(bounds.Right, Is.LessThanOrEqualTo(0),
                "Под --ui-smoke мастер должен целиком уходить левее начала координат, а не вставать поверх работы пользователя");
            Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(0),
                "Под --ui-smoke мастер должен целиком уходить выше начала координат, а не вставать поверх работы пользователя");
        }
    }
}
