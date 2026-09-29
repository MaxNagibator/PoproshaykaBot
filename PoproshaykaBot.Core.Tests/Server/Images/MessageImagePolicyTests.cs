using PoproshaykaBot.Core.Chat;
using PoproshaykaBot.Core.Server;
using PoproshaykaBot.Core.Server.Images;
using PoproshaykaBot.Core.Settings.Obs;
using PoproshaykaBot.Core.Users;
using System.Text.Json;

namespace PoproshaykaBot.Core.Tests.Server.Images;

[TestFixture]
public sealed class MessageImagePolicyTests
{
    private const string Link = "https://i.imgur.com/abc.png";

    private static ObsChatSettings EnabledSettings()
    {
        return new()
        {
            ShowMessageImages = true,
        };
    }

    private static ChatMessageData Message(UserStatus status, string text = "смотри " + Link)
    {
        return new()
        {
            MessageId = "1",
            Timestamp = DateTime.UtcNow,
            UserId = "42",
            DisplayName = "user",
            Message = text,
            MessageType = ChatMessageType.UserMessage,
            Status = status,
        };
    }

    [TestCase(UserStatus.Broadcaster, true)]
    [TestCase(UserStatus.Moderator, true)]
    [TestCase(UserStatus.Vip, true)]
    [TestCase(UserStatus.Subscriber, false)]
    [TestCase(UserStatus.None, false)]
    public void BuildImages_DefaultRoles_AllowOnlyBroadcasterModeratorAndVip(UserStatus status, bool expected)
    {
        var urls = MessageImagePolicy.BuildImages(Message(status), EnabledSettings());

        Assert.That(urls.Count > 0, Is.EqualTo(expected));
    }

    [Test]
    public void BuildImages_EveryoneRole_AllowsPlainViewer()
    {
        var settings = EnabledSettings();
        settings.MessageImageRoles = MessageImageSenderRoles.Everyone;

        var urls = MessageImagePolicy.BuildImages(Message(UserStatus.None), settings);

        Assert.That(urls, Has.Count.EqualTo(1));
    }

    [Test]
    public void BuildImages_Disabled_ReturnsNothing()
    {
        var settings = EnabledSettings();
        settings.ShowMessageImages = false;

        Assert.That(MessageImagePolicy.BuildImages(Message(UserStatus.Moderator), settings), Is.Empty);
    }

    [Test]
    public void BuildImages_DefaultSettings_ReturnNothing()
    {
        Assert.That(MessageImagePolicy.BuildImages(Message(UserStatus.Broadcaster), new()), Is.Empty,
            "По умолчанию функция выключена – обновление не должно включать её само.");
    }

    [Test]
    public void BuildImages_ReturnsLocalProxyUrlWithEncodedSource()
    {
        var urls = MessageImagePolicy.BuildImages(Message(UserStatus.Moderator), EnabledSettings());

        Assert.That(urls.Single().Url, Is.EqualTo("/api/image?u=https%3A%2F%2Fi.imgur.com%2Fabc.png"),
            "Браузер OBS обязан ходить только на свой прокси, внешний URL в DTO попадать не должен.");
    }

    [Test]
    public void BuildImages_KeepsAtMostOneImage()
    {
        var text = "https://i.imgur.com/one.png и https://i.imgur.com/two.png";

        Assert.That(MessageImagePolicy.BuildImages(Message(UserStatus.Moderator, text), EnabledSettings()), Has.Count.EqualTo(1));
    }

    [TestCase(ChatMessageType.BotResponse)]
    [TestCase(ChatMessageType.SystemNotification)]
    public void BuildImages_NonUserMessage_ReturnsNothing(ChatMessageType messageType)
    {
        var settings = EnabledSettings();
        settings.MessageImageRoles = MessageImageSenderRoles.Everyone;

        var message = new ChatMessageData
        {
            MessageId = "1",
            Timestamp = DateTime.UtcNow,
            Message = "смотри " + Link,
            MessageType = messageType,
        };

        Assert.That(MessageImagePolicy.BuildImages(message, settings), Is.Empty);
    }

    [Test]
    public void ToServerMessage_CarriesImagesArray()
    {
        var dto = DtoMapper.ToServerMessage(Message(UserStatus.Moderator), EnabledSettings());
        var json = JsonSerializer.Serialize(dto, ServerJsonOptions.Default);

        using var document = JsonDocument.Parse(json);
        var images = document.RootElement.GetProperty("images");
        var links = document.RootElement.GetProperty("imageLinks");

        Assert.That(images.GetArrayLength(), Is.EqualTo(1));
        Assert.That(images[0].ValueKind, Is.EqualTo(JsonValueKind.String),
            "Элемент images – строка: Browser Source, открытый со старым obs.js, иначе перестаёт вставлять картинки.");
        Assert.That(images[0].GetString(), Does.StartWith("/api/image?u="));
        Assert.That(links.GetArrayLength(), Is.EqualTo(images.GetArrayLength()));

        var start = links[0].GetProperty("startIndex").GetInt32();
        var end = links[0].GetProperty("endIndex").GetInt32();

        Assert.That(("смотри " + Link)[start..(end + 1)], Is.EqualTo(Link),
            "obs.js прячет ссылку по этому отрезку, когда картинка загрузилась, – индексы обязаны указывать на текст ссылки.");
    }

    [Test]
    public void ToServerMessage_WhenDisabled_CarriesEmptyImagesArray()
    {
        var dto = DtoMapper.ToServerMessage(Message(UserStatus.Moderator), new());
        var json = JsonSerializer.Serialize(dto, ServerJsonOptions.Default);

        using var document = JsonDocument.Parse(json);

        Assert.That(document.RootElement.GetProperty("images").GetArrayLength(), Is.Zero);
        Assert.That(document.RootElement.GetProperty("imageLinks").GetArrayLength(), Is.Zero);
    }
}
