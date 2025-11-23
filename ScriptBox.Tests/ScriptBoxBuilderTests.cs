using System;
using System.Threading.Tasks;
using global::ScriptBox;
using ScriptBox.Tests.TestApis;

namespace ScriptBox.Tests;

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
    public async Task RegisterApisFrom_SingleType_ExposesApi()
    {
        await using var scriptBox = ScriptBoxBuilder
            .Create()
            .RegisterApisFrom(typeof(AttributedCalculatorApi))
            .Build();

        await using var session = scriptBox.CreateSession();
        await session.RunAsync(@"
const result = calculator.add(2, 3);
if (result !== 5) {
    throw new Error('calculator.add returned ' + result);
}");
    }

    [Fact]
    public async Task RegisterApisFrom_CanBeCalledMultipleTimes()
    {
        await using var scriptBox = ScriptBoxBuilder
            .Create()
            .RegisterApisFrom(typeof(AttributedCalculatorApi))
            .RegisterApisFrom(typeof(AttributedCalculatorApi))
            .Build();

        await using var session = scriptBox.CreateSession();
        await session.RunAsync("const sum = calculator.add(4, 6); if (sum !== 10) throw new Error('unexpected sum ' + sum);");
    }

    [Fact]
    public async Task RegisterApisFrom_InstanceType_UsesActivatorByDefault()
    {
        await using var scriptBox = ScriptBoxBuilder
            .Create()
            .RegisterApisFrom<InstanceCalculatorApi>()
            .Build();

        await using var session = scriptBox.CreateSession();
        await session.RunAsync("const sum = instanceCalc.add(1, 4); if (sum !== 5) throw new Error('unexpected sum ' + sum);");
    }

    [Fact]
    public async Task RegisterApisFrom_UsesCustomFactory()
    {
        var factoryCalled = false;
        var customInstance = new InstanceCalculatorApi();

        await using var scriptBox = ScriptBoxBuilder
            .Create()
            .UseApiFactory(type =>
            {
                if (type == typeof(InstanceCalculatorApi))
                {
                    factoryCalled = true;
                    return customInstance;
                }

                return Activator.CreateInstance(type);
            })
            .RegisterApisFrom<InstanceCalculatorApi>()
            .Build();

        Assert.True(factoryCalled);

        await using var session = scriptBox.CreateSession();
        await session.RunAsync("const sum = instanceCalc.add(2, 8); if (sum !== 10) throw new Error('unexpected sum ' + sum);");
    }

    [Fact]
    public async Task RegisterApisFrom_WithExplicitName_ExposesApiWithoutAttribute()
    {
        await using var scriptBox = ScriptBoxBuilder
            .Create()
            .RegisterApisFrom<UnnamedCalculatorApi>("my_calc")
            .Build();

        await using var session = scriptBox.CreateSession();
        await session.RunAsync(@"
const result = my_calc.add(10, 5);
if (result !== 15) {
    throw new Error('my_calc.add returned ' + result);
}");
    }
}
