using PoproshaykaBot.Wpf.Bootstrap;

namespace PoproshaykaBot.Wpf.Tests.Shell;

[TestFixture]
public class GalleryPageCaseTests
{
    [Test]
    public void Кейс_с_выбранной_строкой_распознаётся_только_у_своих_страниц()
    {
        var requested = string.Join(',', SectionKeys.Users, SectionKeys.UsersSelected, SectionKeys.StreamsSelected, "logs:selected");
        var arguments = GalleryHost.Parse(["--pages", requested], "gallery-out");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(arguments.Pages,
                Is.EqualTo(new[] { SectionKeys.Users, SectionKeys.UsersSelected, SectionKeys.StreamsSelected }),
                "Кейсы с выбранной строкой должны проходить разбор наравне со страницами");
            Assert.That(arguments.Unknown, Is.EqualTo(new[] { "logs:selected" }),
                "Суффикс :selected у страницы без выбора строки должен уходить в нераспознанное");
        }
    }

    [Test]
    public void Кейсы_страницы_команд_разводят_инспектор_с_параметрами_и_без_них()
    {
        var requested = string.Join(',', SectionKeys.CommandsSelected, SectionKeys.CommandsParams, "users:params");
        var arguments = GalleryHost.Parse(["--pages", requested], "gallery-out");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(arguments.Pages, Is.EqualTo(new[] { SectionKeys.CommandsSelected, SectionKeys.CommandsParams }));
            Assert.That(arguments.Unknown, Is.EqualTo(new[] { "users:params" }),
                "Суффикс :params есть только у страницы «Команды»");

            Assert.That(SectionKeys.PageOf(SectionKeys.CommandsParams), Is.EqualTo(SectionKeys.Commands));
            Assert.That(SectionKeys.PageOf(SectionKeys.CommandsSelected), Is.EqualTo(SectionKeys.Commands));
        }
    }

    [Test]
    public void Кейсы_плитки_OBS_снимаются_на_обзоре_с_подложенным_состоянием()
    {
        var requested = string.Join(',', SectionKeys.OverviewObs, SectionKeys.OverviewObsFull, "users:obs");
        var arguments = GalleryHost.Parse(["--pages", requested], "gallery-out");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(arguments.Pages, Is.EqualTo(new[] { SectionKeys.OverviewObs, SectionKeys.OverviewObsFull }));
            Assert.That(arguments.Unknown, Is.EqualTo(new[] { "users:obs" }));
            Assert.That(SectionKeys.PageOf(SectionKeys.OverviewObsFull), Is.EqualTo(SectionKeys.Overview));
            Assert.That(GalleryObs.Find(SectionKeys.OverviewObs)?.AudioSources, Has.Count.EqualTo(1));
            Assert.That(GalleryObs.Find(SectionKeys.OverviewObsFull)?.AudioSources, Has.Count.EqualTo(5));
        }
    }

    [Test]
    public void Кейс_карточек_распознаётся_только_у_истории_стримов()
    {
        var requested = string.Join(',', SectionKeys.StreamsCards, "users:cards");
        var arguments = GalleryHost.Parse(["--pages", requested], "gallery-out");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(arguments.Pages, Is.EqualTo(new[] { SectionKeys.StreamsCards }));
            Assert.That(arguments.Unknown, Is.EqualTo(new[] { "users:cards" }));
            Assert.That(SectionKeys.PageOf(SectionKeys.StreamsCards), Is.EqualTo(SectionKeys.Streams));
        }
    }
}
