using System.Diagnostics;

namespace Sensei.LocalAgent.Core;

public sealed record GitOutput(int ExitCode, byte[] Bytes);

public sealed class GitProcess(TimeSpan? timeout = null, int outputLimit = 8 * 1024 * 1024)
{
    public async Task<GitOutput> Run(string root, IEnumerable<string> arguments, CancellationToken ct, byte[]? stdin = null)
    {
        var supplied = arguments.ToArray();
        var filters = new List<string>();
        if (supplied.FirstOrDefault() != "config")
        {
            var configured = await Run(root, ["config", "--name-only", "--get-regexp", @"^filter\..*\.(clean|smudge|process|required)$"], ct);
            if (configured.ExitCode is not (0 or 1)) throw new AgentException("git_configuration_unavailable");
            var names = SourcePolicy.StrictUtf8.GetString(configured.Bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.TrimEnd('\r')).Select(s => s[..s.LastIndexOf('.')]).Distinct(StringComparer.Ordinal);
            foreach (var name in names)
                foreach (var option in new[] { "clean=", "smudge=", "process=", "required=false" }) filters.AddRange(["-c", name + "." + option]);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(20));
        using var process = new Process();
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        info.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // check-ignore does not accept pathspec magic, including Git's global
        // literal mode. Its stdin is already a NUL-delimited list of filenames.
        foreach (var arg in new[] { "--no-pager", "-c", "core.fsmonitor=false", "-c", "core.quotePath=false" }
            .Concat(supplied.FirstOrDefault() == "check-ignore" ? [] : new[] { "--literal-pathspecs" }).Concat(filters).Concat(supplied)) info.ArgumentList.Add(arg);
        process.StartInfo = info;
        var started = false;
        try
        {
            if (!process.Start()) throw new AgentException("git_unavailable");
            started = true;
            var stdout = Read(process.StandardOutput.BaseStream, outputLimit, deadline.Token);
            var stderr = Read(process.StandardError.BaseStream, 64 * 1024, deadline.Token);
            var input = WriteInput(process.StandardInput.BaseStream, stdin, deadline.Token);
            await Task.WhenAll(stdout, stderr, input, process.WaitForExitAsync(deadline.Token));
            return new(process.ExitCode, await stdout);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AgentException("git_timeout"); }
        catch (System.ComponentModel.Win32Exception) { throw new AgentException("git_unavailable"); }
        finally
        {
            if (started && !process.HasExited) process.Kill(entireProcessTree: true);
        }
    }

    private static async Task<byte[]> Read(Stream stream, int maximum, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var bytes = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(bytes, ct)) != 0)
        {
            if (buffer.Length + count > maximum) throw new AgentException("git_output_limit");
            buffer.Write(bytes, 0, count);
        }
        return buffer.ToArray();
    }

    private static async Task WriteInput(Stream stream, byte[]? input, CancellationToken ct)
    {
        if (input is not null) await stream.WriteAsync(input, ct);
        stream.Close();
    }
}
