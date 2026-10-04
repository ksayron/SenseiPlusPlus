using System.Diagnostics;
using System.Security.Cryptography;
using Sensei.LocalAgent.Core;
using Sensei.LocalAgent.Windows;

namespace Sensei.LocalAgent.Tests;

internal sealed class AgentFixture : IDisposable
{
    public string DirectoryRoot { get; }
    public string RepositoryRoot { get; }
    public string StateRoot { get; }
    public readonly FakeClock Clock = new();
    public readonly GitContext Git = new();
    public readonly FileAgentStore Store;
    public readonly AgentService Service;
    private readonly IContentProtector protector;

    public AgentFixture()
    {
        DirectoryRoot = Path.Combine(Path.GetTempPath(), "sensei-agent-tests", Guid.NewGuid().ToString("N"));
        RepositoryRoot = Path.Combine(DirectoryRoot, "repository");
        StateRoot = Path.Combine(DirectoryRoot, "state");
        Directory.CreateDirectory(RepositoryRoot);
        Run("init", "-b", "main");
        Run("config", "user.name", "Fixture Developer");
        Run("config", "user.email", "fixture@example.invalid");
        Run("config", "core.autocrlf", "false");
        Write("src/a.txt", "before\n");
        Commit();
        protector = OperatingSystem.IsWindows() ? new WindowsContentProtector() : new TestProtector();
        Store = new(StateRoot, protector);
        Service = new(Store, Git, Clock);
    }
    public AgentService Restart() => new(new FileAgentStore(StateRoot, protector), Git, Clock);
    public Task<RepositoryRegistration> Register(bool source = false, string[]? selection = null) => Service.Register(RepositoryRoot, selection ?? ["src"], WorkSetting.PersonalProject, source, CancellationToken.None);
    public void Write(string path, string content) => Bytes(path, System.Text.Encoding.UTF8.GetBytes(content));
    public void Bytes(string path, byte[] bytes)
    {
        var full = Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }
    public string Commit(params string[] paths)
    {
        Run(new[] { "add", "--" }.Concat(paths.Length == 0 ? new[] { "." } : paths).ToArray());
        Run("commit", "-m", "fixture change");
        return Run("rev-parse", "HEAD").Trim();
    }
    public string Run(params string[] args) => RunAt(RepositoryRoot, args);
    public static string RunAt(string directory, params string[] args)
    {
        using var p = new Process();
        p.StartInfo = new("git") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var key in p.StartInfo.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray()) p.StartInfo.Environment.Remove(key);
        foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
        p.Start();
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(20000)) { p.Kill(true); throw new TimeoutException("Fixture Git command timed out."); }
        if (p.ExitCode != 0) throw new InvalidOperationException(stderr.GetAwaiter().GetResult());
        return stdout.GetAwaiter().GetResult();
    }
    public void Dispose()
    {
        var allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "sensei-agent-tests")) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(DirectoryRoot);
        if (!resolved.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Fixture cleanup outside test root.");
        if (Directory.Exists(resolved))
        {
            // Git object files can be read-only on Windows.
            foreach (var file in Directory.EnumerateFiles(resolved, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(resolved, recursive: true);
        }
    }
    internal sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 3, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TestProtector : IContentProtector
    {
        private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
        public byte[] Protect(byte[] plaintext)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var result = new byte[28 + plaintext.Length];
            nonce.CopyTo(result, 0);
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, result.AsSpan(28), result.AsSpan(12, 16));
            return result;
        }
        public byte[] Unprotect(byte[] ciphertext)
        {
            if (ciphertext.Length < 28) throw new CryptographicException();
            var result = new byte[ciphertext.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(ciphertext.AsSpan(0, 12), ciphertext.AsSpan(28), ciphertext.AsSpan(12, 16), result);
            return result;
        }
    }
}
