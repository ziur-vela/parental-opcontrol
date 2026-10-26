using System.IO.Pipes;
using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Luna;

public sealed record KidSnapshot(
    ChildSummaryDto? Summary,
    RuntimeLockStateDto? State,
    IReadOnlyList<ChildMessageDto> Messages,
    bool Online,
    string? Error = null)
{
    public string ThemeKey => State?.Theme ?? Summary?.Theme ?? "night";
    public string Language => State?.Language ?? "en";
    public string ChildName => State?.ChildName ?? Summary?.ChildName ?? "";
}

public interface IKidDataSource
{
    Task<KidSnapshot> LoadAsync(CancellationToken ct);
    Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct);
    Task MarkSeenAsync(Guid messageId, CancellationToken ct);
    Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct);
    Task SetThemeAsync(string theme, CancellationToken ct);
}

public sealed class PipeKidDataSource : IKidDataSource
{
    private const string PipeName = "OpControlParental";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<KidSnapshot> LoadAsync(CancellationToken ct)
    {
        var response = await SendAsync(new PinPipeRequest(LunaPipePolicy.KidSummaryRequest), ct);
        if (!response.Ok)
            return new KidSnapshot(null, null, [], false, response.Error);
        return new KidSnapshot(response.Summary, response.State, response.Messages ?? [], response.Online == true);
    }

    public Task<PinPipeResponse> SendMessageAsync(string text, CancellationToken ct) =>
        SendAsync(new PinPipeRequest("message-send", Text: text), ct, 25);

    public Task MarkSeenAsync(Guid messageId, CancellationToken ct) =>
        SendAsync(new PinPipeRequest("message-seen", MessageId: messageId), ct);

    public Task MarkAcknowledgedAsync(Guid messageId, CancellationToken ct) =>
        SendAsync(new PinPipeRequest("message-ack", MessageId: messageId), ct);

    public Task SetThemeAsync(string theme, CancellationToken ct) =>
        SendAsync(new PinPipeRequest("set-theme", Theme: theme), ct);

    private static async Task<PinPipeResponse> SendAsync(PinPipeRequest request, CancellationToken ct,
        int timeoutSeconds = 5)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(Math.Min(5000, timeoutSeconds * 1000), timeout.Token);
            using var reader = new StreamReader(pipe, leaveOpen: true);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions));
            var line = await reader.ReadLineAsync(timeout.Token);
            if (string.IsNullOrWhiteSpace(line)) return new PinPipeResponse(false, "Helper is not available.");
            return JsonSerializer.Deserialize<PinPipeResponse>(line, JsonOptions)
                   ?? new PinPipeResponse(false, "Invalid helper response.");
        }
        catch (Exception ex)
        {
            LunaLog.Exception("kid-pipe", ex);
            return new PinPipeResponse(false, "Helper is not available.");
        }
    }
}
