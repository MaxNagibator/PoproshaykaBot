using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PoproshaykaBot.Wpf.Tests.Smoke;

[TestFixture]
[NonParallelizable]
public class SmokeTestSessionHungWindowTests
{
    private static readonly TimeSpan HungFor = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SessionBudget = TimeSpan.FromSeconds(30);

    [Test]
    public void Session_IsNotHeld_ByAForeignWindowThatPumpsNoMessages()
    {
        using var hung = HungWindow.Open(HungFor);

        var watch = Stopwatch.StartNew();
        var session = SmokeTestSession.Launch(new() { SeedConfiguredSettings = true });
        var launched = watch.Elapsed;

        watch.Restart();
        session.Dispose();
        var disposed = watch.Elapsed;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(launched, Is.LessThan(SessionBudget),
                "Поиск окна приложения при запуске не должен ждать чужое окно, которое не разбирает сообщения.");
            Assert.That(disposed, Is.LessThan(SessionBudget),
                "Закрытие окон приложения не должно ждать чужое окно, которое не разбирает сообщения.");
        }
    }

    private sealed class HungWindow : IDisposable
    {
        private readonly ManualResetEventSlim _release = new();
        private readonly Thread _thread;

        private HungWindow(TimeSpan hungFor)
        {
            using var created = new ManualResetEventSlim();

            _thread = new(() =>
            {
                CreateWindowEx(0, "Static", "PoproshaykaBot-hung-window-probe", 0, 0, 0, 100, 100,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                created.Set();
                _release.Wait(hungFor);
            })
            {
                IsBackground = true,
            };

            _thread.Start();
            created.Wait();
        }

        public static HungWindow Open(TimeSpan hungFor)
        {
            return new(hungFor);
        }

        public void Dispose()
        {
            _release.Set();
            _thread.Join();
            _release.Dispose();
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int extendedStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parent,
            IntPtr menu,
            IntPtr instance,
            IntPtr parameter);
    }
}
