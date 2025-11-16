using System.Text;
using ScriptBox.Net.Core.WasmExecution;
using ScriptBox.Net.Services;

const string LogPrefix = "[ScriptBox.Net]";

try
{
    // Get the JavaScript code to execute
    var scriptPath = GetScriptPath();
    var jsCode = File.ReadAllText(scriptPath);

    // Set up script execution.
    var scriptExecutor = new WasmScriptExecutor();
    var workerMethods = new WorkerMethods(scriptExecutor);

    // Execute the script directly
    Console.WriteLine($"{LogPrefix} Executing script from {scriptPath}");
    workerMethods.RunScript(jsCode);
    Console.WriteLine($"{LogPrefix} Script completed successfully");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogPrefix} Fatal error: {ex}");
    Environment.Exit(1);
}

static string GetScriptPath()
{
    // Default to sample-script.js in the scripts/dist directory
    var repoRoot = GetRepoRoot();
    var scriptPath = Path.Combine(repoRoot, "scripts", "dist", "sample-script.js");
    
    if (!File.Exists(scriptPath))
    {
        throw new FileNotFoundException($"Script not found at {scriptPath}");
    }
    
    return scriptPath;
}

static string GetRepoRoot()
{
    var currentDir = AppContext.BaseDirectory;
    while (!string.IsNullOrEmpty(currentDir) && !Directory.Exists(Path.Combine(currentDir, "scripts")))
    {
        currentDir = Path.GetDirectoryName(currentDir);
    }

    if (string.IsNullOrEmpty(currentDir))
        throw new DirectoryNotFoundException("Could not find scripts directory.");

    return currentDir;
}
