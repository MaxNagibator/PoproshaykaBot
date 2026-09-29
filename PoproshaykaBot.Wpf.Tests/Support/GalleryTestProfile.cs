using System.IO;
using System.Text;

namespace PoproshaykaBot.Wpf.Tests.Support;

internal static class GalleryTestProfile
{
    private const string SettingsJson = """
                                        {
                                          "twitch": {
                                            "channel": "gallery-test",
                                            "clientId": "gallery-test-client-id",
                                            "clientSecret": "gallery-test-client-secret"
                                          }
                                        }
                                        """;

    private const string BroadcastProfilesJson = """
                                                 {
                                                   "profiles": [
                                                     {
                                                       "id": "3f2b0f0e-4d1f-4a4a-9c1a-9f1f6f0a1b2c",
                                                       "name": "Вечерний стрим",
                                                       "title": "Играем и общаемся",
                                                       "gameId": "509658",
                                                       "gameName": "Just Chatting",
                                                       "broadcasterLanguage": "ru",
                                                       "tags": ["Русский", "Общение"],
                                                       "obsSceneName": "Основная",
                                                       "currentNumber": 42
                                                     }
                                                   ]
                                                 }
                                                 """;

    private const string UserStatisticsJson = """
                                              [
                                                {
                                                  "userId": "101",
                                                  "name": "alice",
                                                  "messageCount": 420,
                                                  "bonusPoints": 150,
                                                  "penaltyPoints": 10,
                                                  "firstSeen": "2026-08-01T10:00:00Z",
                                                  "lastSeen": "2026-09-10T22:00:00Z"
                                                },
                                                {
                                                  "userId": "102",
                                                  "name": "bob",
                                                  "messageCount": 210,
                                                  "bonusPoints": 30,
                                                  "penaltyPoints": 0,
                                                  "firstSeen": "2026-08-03T10:00:00Z",
                                                  "lastSeen": "2026-09-09T21:00:00Z"
                                                },
                                                {
                                                  "userId": "103",
                                                  "name": "carol",
                                                  "messageCount": 75,
                                                  "bonusPoints": 0,
                                                  "penaltyPoints": 25,
                                                  "firstSeen": "2026-08-14T10:00:00Z",
                                                  "lastSeen": "2026-09-01T18:00:00Z"
                                                }
                                              ]
                                              """;

    private const string BlockersText = """
                                        # блокировка баннеров
                                        [data-a-target="community-highlight"]
                                        .stream-chat-header
                                        """;

    public static string Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "PoproshaykaBot-gallery-test-" + Guid.NewGuid().ToString("N"));
        var settingsDirectory = Path.Combine(directory, "settings");

        Directory.CreateDirectory(settingsDirectory);

        Write(Path.Combine(settingsDirectory, "settings.json"), SettingsJson);
        Write(Path.Combine(settingsDirectory, "broadcast-profiles.json"), BroadcastProfilesJson);
        Write(Path.Combine(directory, "chat-blockers.txt"), BlockersText);
        Write(Path.Combine(directory, "users_statistics.json"), UserStatisticsJson);

        return directory;
    }

    public static void Delete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Write(string path, string content)
    {
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
