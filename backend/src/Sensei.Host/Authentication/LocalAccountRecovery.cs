using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sensei.BuildingBlocks.Persistence;
using Sensei.Modules.Identity.Domain;
using Sensei.Modules.Identity.Infrastructure;

namespace Sensei.Host.Authentication;

public static class LocalAccountRecovery
{
    public static async Task RunAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || Console.IsInputRedirected)
            throw new InvalidOperationException("Recovery requires an interactive local Development terminal.");
        Console.Write("Existing profile ID: ");
        if (!Guid.TryParse(Console.ReadLine(), out var id)) throw new ArgumentException("A profile UUID is required.");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SenseiDbContext>();
        var profile = await db.Set<UserAccount>().SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new ArgumentException("Profile not found.");
        Console.WriteLine($"Provision/reset local credentials for {profile.Email} ({profile.Id}). This revokes existing sessions and preserves the profile's data.");
        Console.Write("Confirm by typing the profile ID again: ");
        if (Console.ReadLine() != id.ToString()) throw new InvalidOperationException("Recovery cancelled.");
        Console.Write("New password: "); var password = ReadPassword();
        Console.Write("Repeat password: "); var repeated = ReadPassword();
        if (password != repeated) throw new ArgumentException("Passwords do not match.");
        if (password.Length > 256) throw new ArgumentException("Maximum password length is 256 characters.");
        var result = await RecoverAsync(scope.ServiceProvider.GetRequiredService<UserManager<LoginAccount>>(), profile, password);
        if (!result.Succeeded) throw new ArgumentException(string.Join(" ", result.Errors.Select(x => x.Description)));
        Console.WriteLine("Local credentials updated. Sign in using the profile email and your new password.");
    }

    public static async Task<IdentityResult> RecoverAsync(UserManager<LoginAccount> users, UserAccount profile, string password)
    {
        var account = await users.FindByIdAsync(profile.Id.ToString());
        if (account is null)
            return await users.CreateAsync(new LoginAccount { Id = profile.Id, Email = profile.Email, UserName = profile.Email, LockoutEnabled = true }, password);
        var token = await users.GeneratePasswordResetTokenAsync(account);
        var result = await users.ResetPasswordAsync(account, token, password);
        if (!result.Succeeded) return result;
        await users.SetLockoutEndDateAsync(account, null);
        await users.ResetAccessFailedCountAsync(account);
        return result;
    }

    private static string ReadPassword()
    {
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return new string(chars.ToArray()); }
            if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
            else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
        }
    }
}
