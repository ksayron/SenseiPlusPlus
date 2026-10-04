using System.Text.Json;
using Sensei.LocalAgent.Core;
using Sensei.LocalAgent.Sync;

namespace Sensei.LocalAgent.Cli;

public sealed class SyncCommands(SubmissionService service, TextWriter output)
{
    private void Json<T>(T value) => output.WriteLine(JsonSerializer.Serialize(value, AgentJson.Options));
    private static Guid Id(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : throw new AgentException("invalid_identifier");
    public async Task Run(string[] args, CancellationToken ct)
    {
        switch (args[0])
        {
            case "auth":
                if (args.Length == 2 && args[1] == "status") { var p = service.Profile(); Json(new { SignedIn = p is not null, p?.Issuer, p?.OwnerId, p?.DisplayName }); }
                else if (args.Length == 2 && args[1] == "logout") output.WriteLine(await service.Logout(ct) ? "signed_out" : "signed_out_local; remote_revocation_failed; revoke_in_web_connections");
                else if (args.Length is 4 or 5 && args[1] == "login" && args[2] == "--server" && (args.Length == 4 || args[4] == "--allow-insecure-loopback"))
                { await service.Login(args[3], args.Length == 5, ct); output.WriteLine("signed_in"); }
                else throw new AgentException("invalid_auth_arguments");
                break;
            case "submit":
                if (args.Length != 3 && args.Length != 4) throw new AgentException("invalid_submit_arguments");
                var id = Id(args[1]);
                if (args.Length == 3 && args[2] == "--preview")
                { Json(await service.Preview(id, ct)); output.WriteLine("Review all prose and the destination. Confirmation queues this exact draft; run sync to send it."); }
                else if (args.Length == 4 && args[2] == "--confirm") { await service.Confirm(id, args[3], ct); output.WriteLine("submission_queued; run_sync_to_send"); }
                else throw new AgentException("preview_or_exact_confirmation_required");
                break;
            case "outbox":
                if (args.Length == 2 && args[1] == "list") Json(service.Outbox().Select(x => new { x.Payload.EventId, x.CandidateId, x.Issuer, x.OwnerId, x.State, x.ConfirmedAt, x.Error, x.Receipt?.ExperiencePath }));
                else if (args.Length == 3 && args[1] == "show") Json(service.Outbox().Where(x => x.Payload.EventId == Id(args[2])).Select(x => new { x.Payload, x.Issuer, x.OwnerId, x.State, x.Receipt, x.Error }));
                else if (args.Length == 3 && args[1] == "cancel") { service.Cancel(Id(args[2])); output.WriteLine("local_delivery_cancelled; accepted_server_records_are_not_deleted"); }
                else throw new AgentException("invalid_outbox_arguments");
                break;
            case "sync":
                if (args.Length != 1) throw new AgentException("invalid_sync_arguments");
                await service.Sync(ct, (id, status) =>
                {
                    output.WriteLine($"event={id} status={status}");
                    if (status != "imported") return;
                    var profile = service.Profile();
                    var delivered = service.Outbox().Single(x => x.Payload.EventId == id && x.Issuer == profile!.Issuer && x.OwnerId == profile.OwnerId);
                    output.WriteLine($"draft={new Uri(new Uri(delivered.Issuer), $"/experience?entry={delivered.Receipt!.EntryId:D}")}");
                }); break;
            case "concepts":
                if (args.Length != 1) throw new AgentException("invalid_concept_arguments");
                Json(await service.Concepts(ct)); break;
            default: throw new AgentException("unknown_command");
        }
    }
}
