using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;

namespace CS2SP;

public sealed partial class CS2SPPlugin
{
    [ConsoleCommand("sp_send_stats", "Force an immediate CS2StatsPlugin stats upload to the API.")]
    [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY)]
    public void CmdSendStats(CCSPlayerController? _, CommandInfo _info) =>
        Stats.ForceUpload();
}
