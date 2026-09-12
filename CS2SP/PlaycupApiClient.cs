using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CounterStrikeSharp.API;
using Microsoft.Extensions.Logging;

namespace CS2SP;

public sealed class PlaycupApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger _log;
    private volatile bool _disposed;

    public PlaycupApiClient(ILogger log)
    {
        _log = log;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void Post(
        Uri url,
        string jsonBody,
        string? serverId,
        Action<JsonNode?>? onSuccess = null,
        Action<int, string>? onError = null)
    {
        if (_disposed)
        {
            onError?.Invoke(0, "disposed");
            return;
        }

        _ = SendAsync(url, jsonBody, serverId, onSuccess, onError);
    }

    private async Task SendAsync(
        Uri url,
        string jsonBody,
        string? serverId,
        Action<JsonNode?>? onSuccess,
        Action<int, string>? onError)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (_disposed)
            {
                CompleteOnMain(onError, 0, "disposed");
                return;
            }

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrEmpty(serverId))
                    req.Headers.TryAddWithoutValidation("X-Server-Id", serverId);
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                using var resp = await _http.SendAsync(req).ConfigureAwait(false);
                var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var code = (int)resp.StatusCode;
                var ok = resp.IsSuccessStatusCode;
                RunOnMain(() => Complete(ok, code, text, url.ToString(), onSuccess, onError));
                return;
            }
            catch (ObjectDisposedException)
            {
                CompleteOnMain(onError, 0, "disposed");
                return;
            }
            catch (Exception ex) when (attempt == 0 && IsTransient(ex))
            {
                last = ex;
                try { await Task.Delay(250).ConfigureAwait(false); }
                catch { /* ignore */ }
            }
            catch (Exception ex)
            {
                last = ex;
                break;
            }
        }

        _log.LogWarning(last, "[CS2SP] HTTP POST {Url} threw", url);
        RunOnMain(() =>
        {
            if (_disposed)
            {
                onError?.Invoke(0, "disposed");
                return;
            }

            onError?.Invoke(0, last?.Message ?? "http failed");
        });
    }

    private void CompleteOnMain(Action<int, string>? onError, int code, string body) =>
        RunOnMain(() =>
        {
            if (_disposed)
            {
                onError?.Invoke(0, "disposed");
                return;
            }

            onError?.Invoke(code, body);
        });

    private void RunOnMain(Action action)
    {
        if (_disposed)
            return;
        try
        {
            Server.NextWorldUpdate(action);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[CS2SP] NextWorldUpdate failed after HTTP");
        }
    }

    private void Complete(
        bool ok,
        int code,
        string text,
        string url,
        Action<JsonNode?>? onSuccess,
        Action<int, string>? onError)
    {
        if (_disposed)
        {
            onError?.Invoke(0, "disposed");
            return;
        }

        if (!ok)
        {
            _log.LogWarning("[CS2SP] Stats upload failed (HTTP {Code}): {Body}", code, Truncate(text));
            onError?.Invoke(code, text);
            return;
        }

        JsonNode? node = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { node = JsonNode.Parse(text); }
            catch (Exception ex) { _log.LogWarning(ex, "[CS2SP] JSON parse failed for {Url}", url); }
        }

        onSuccess?.Invoke(node);
    }

    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or IOException;

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500];

    public void Dispose()
    {
        _disposed = true;
        _http.Dispose();
    }
}
