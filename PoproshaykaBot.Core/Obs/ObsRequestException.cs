namespace PoproshaykaBot.Core.Obs;

public sealed class ObsRequestException : Exception
{
    public ObsRequestException()
    {
        RequestType = string.Empty;
    }

    public ObsRequestException(string message)
        : base(message)
    {
        RequestType = string.Empty;
    }

    public ObsRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
        RequestType = string.Empty;
    }

    public ObsRequestException(string requestType, int code, string? comment)
        : base(CreateMessage(requestType, code, comment))
    {
        RequestType = requestType;
        Code = code;
        Comment = comment;
    }

    public string RequestType { get; }

    public int Code { get; }

    public string? Comment { get; }

    private static string CreateMessage(string requestType, int code, string? comment)
    {
        return string.IsNullOrWhiteSpace(comment)
            ? $"OBS отклонил запрос {requestType} (код {code})."
            : $"OBS отклонил запрос {requestType} (код {code}): {comment}";
    }
}
