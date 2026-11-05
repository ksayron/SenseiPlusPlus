using Sensei.LocalAgent.Cli;
using Sensei.LocalAgent.Core;
using Sensei.LocalAgent.Windows;
using Sensei.LocalAgent.Sync;

if (args.Length == 0 || args.Contains("--help") || args[0] == "help")
{
    Console.WriteLine(CliApp.Help);
    return 0;
}
if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("windows_storage_required");
    return 2;
}
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
try
{
    var actual = args.ToList();
    var stateRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sensei++", "LocalAgent");
    var index = actual.IndexOf("--state-dir");
    if (index >= 0)
    {
        if (index + 1 >= actual.Count) throw new AgentException("state_directory_required");
        stateRoot = Path.GetFullPath(actual[index + 1]);
        actual.RemoveRange(index, 2);
    }
    // Custom storage is still DPAPI-protected and must not be inside a Git repository.
    var ancestor = new DirectoryInfo(stateRoot);
    while (ancestor is not null)
    {
        if (File.Exists(Path.Combine(ancestor.FullName, ".git")) || Directory.Exists(Path.Combine(ancestor.FullName, ".git")))
            throw new AgentException("state_directory_inside_repository");
        ancestor = ancestor.Parent;
    }
    var store = new FileAgentStore(stateRoot, new WindowsContentProtector());
    var service = new AgentService(store, new GitContext());
    return await new CliApp(service, Console.In, Console.Out, stateRoot, new SubmissionService(service, new SyncStore(store, stateRoot), new AgentConnection())).Run(actual.ToArray(), cancel.Token);
}
catch (OperationCanceledException) { return 0; }
catch (Exception ex) when (ex is AgentException or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException or TransportException or HttpRequestException or OpenIddict.Abstractions.OpenIddictExceptions.ProtocolException)
{
    Console.Error.WriteLine(ex is AgentException a ? a.Code : "agent_operation_failed");
    return 2;
}
