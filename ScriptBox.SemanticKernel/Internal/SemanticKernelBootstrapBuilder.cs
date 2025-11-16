using System.Collections.Generic;
using System.Text;

namespace ScriptBox.SemanticKernel.Internal;

internal static class SemanticKernelBootstrapBuilder
{
    public static string Build(
        string jsNamespace,
        IReadOnlyList<SemanticKernelFunctionMetadata> functions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("(function(root){");
        sb.AppendLine("  if (typeof __scriptbox === 'undefined') {");
        sb.AppendLine("    throw new Error('Missing __scriptbox helper.');");
        sb.AppendLine("  }");
        sb.AppendLine("  var api = {};");

        foreach (var fn in functions)
        {
            var hostMethod = $"{jsNamespace}.{fn.Name}";
            sb.AppendLine($"  api.{fn.Name} = __scriptbox.createMethod('{hostMethod}');");
        }

        sb.AppendLine($"  root.{jsNamespace} = api;");
        sb.AppendLine("})(typeof globalThis !== 'undefined' ? globalThis : typeof global !== 'undefined' ? global : typeof self !== 'undefined' ? self : this);");

        return sb.ToString();
    }
}
