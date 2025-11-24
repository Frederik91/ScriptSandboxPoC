using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ScriptBox;

namespace ScriptBox.SemanticKernel;

/// <summary>
/// Module initializer that automatically registers the Semantic Kernel API scanner
/// when the ScriptBox.SemanticKernel package is referenced.
/// This allows RegisterApisFrom to work seamlessly with both [SandboxApi] and [KernelFunction] attributes.
/// </summary>
internal static class SemanticKernelModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ScriptBoxBuilder.RegisterDefaultScanner(() =>
        {
            // Access the current builder's metadata storage via BuilderMetadataContext
            var builderMetadata = BuilderMetadataContext.Current;

            return new SemanticKernelApiScanner(metadata =>
            {
                // Store metadata in the builder for later retrieval
                if (builderMetadata != null)
                {
                    var list = builderMetadata.GetValueOrDefault("SemanticKernelPlugins") as List<SemanticKernelNamespaceMetadata>;
                    if (list == null)
                    {
                        list = new List<SemanticKernelNamespaceMetadata>();
                        builderMetadata["SemanticKernelPlugins"] = list;
                    }
                    list.Add(metadata);
                }
            });
        });
    }
}
