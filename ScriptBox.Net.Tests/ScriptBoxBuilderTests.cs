using System;
using System.Threading.Tasks;
using global::ScriptBox.Net;
using ScriptBox.Net.Tests.TestApis;

namespace ScriptBox.Net.Tests;

public class ScriptBoxBuilderTests
{
    [Fact]
    public async Task ScriptBoxBuilder_ExecutesCustomJsonHandler()
    {
        var bootstrap = @"
(function() {
  function callHost(method, params) {
    var payload = JSON.stringify({ method: method, params: params });
    var response = __host.bridge(payload);
    if (!response) {
      throw new Error('Host returned empty response');
    }
    var parsed = JSON.parse(response);
    if (parsed.error) {
      throw new Error(parsed.error);
    }
    return parsed.result;
  }

  globalThis.assistantApi = {
    add: function(a, b) {
      return callHost('assistant.add', { a: a, b: b });
    }
  };
})();";

        await using var scriptBox = ScriptBoxBuilder
            .Create()
            .WithAdditionalBootstrap(_ => Task.FromResult(bootstrap))
            .ConfigureHostApi(api => api.RegisterJsonHandler(
                "assistant.add",
                ctx =>
                {
                    var a = Convert.ToInt32(ctx.Params["a"]);
                    var b = Convert.ToInt32(ctx.Params["b"]);
                    return Task.FromResult<object?>(a + b);
                }))
            .Build();

        await using var session = scriptBox.CreateSession();
        await session.RunAsync("const value = assistantApi.add(3, 7); console.log('value=' + value);");
    }

    [Fact]
    public async Task UseAttributedApis_ExposesGeneratedApi()
    {
        await using var scriptBox = ScriptBoxBuilder
            .Create()
            .UseAttributedApis(typeof(AttributedCalculatorApi).Assembly)
            .Build();

        await using var session = scriptBox.CreateSession();
        await session.RunAsync(@"
const result = calculator.add(2, 3);
if (result !== 5) {
    throw new Error('calculator.add returned ' + result);
}");
    }
}
