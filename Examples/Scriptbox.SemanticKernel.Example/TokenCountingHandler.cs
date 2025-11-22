using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Scriptbox.SemanticKernel.Example;

public class TokenCountingHandler : DelegatingHandler
{
    private readonly TokenSink _sink;

    public TokenCountingHandler(TokenSink sink)
    {
        _sink = sink;
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        // We only care about successful responses from OpenAI that contain usage data
        if (response.IsSuccessStatusCode && response.Content != null)
        {
            // Buffer the content so we can read it without consuming the stream for the downstream caller
            await response.Content.LoadIntoBufferAsync();
            var content = await response.Content.ReadAsStringAsync();

            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    long input = 0;
                    long output = 0;

                    if (usage.TryGetProperty("prompt_tokens", out var promptTokens))
                    {
                        input = promptTokens.GetInt64();
                    }
                    
                    if (usage.TryGetProperty("completion_tokens", out var completionTokens))
                    {
                        output = completionTokens.GetInt64();
                    }
                    else if (usage.TryGetProperty("total_tokens", out var totalTokens))
                    {
                        // Fallback if completion_tokens is missing but total is there
                        output = totalTokens.GetInt64() - input;
                    }

                    if (input > 0 || output > 0)
                    {
                        _sink.Add(input, output);
                        // Console.WriteLine($"[TokenCountingHandler] Captured: Input={input}, Output={output}");
                    }
                }
            }
            catch
            {
                // Ignore parsing errors (e.g. if response is not JSON or not what we expect)
            }
        }

        return response;
    }
}
