using PluginSdk.Commands;
using VRage.Game.ModAPI;

namespace OreYeeter.MagnetarHost
{
    /// <summary>Magnetar wrapper around the shared OreActions. Keep it free of logic, add that to OreActions instead.</summary>
    [CommandRoot("ore", "OreYeeter", "void unwanted ores as your drills mine them")]
    public sealed class OreYeeterCommands : CommandModule
    {
        private const string PlayerOnly = "Player only command.";

        [Command("list", "Show the ores your yeeting drills void")]
        [Permission(MyPromoteLevel.None)]
        public string List() => Context.Caller.IsConsole ? PlayerOnly : OreActions.List(Context.Caller.SteamId);

        [Command("add", "Add an ore to your list, e.g. !ore add Ice")]
        [Permission(MyPromoteLevel.None)]
        public string Add(string ore) => Context.Caller.IsConsole ? PlayerOnly : OreActions.Add(Context.Caller.SteamId, ore);

        [Command("remove", "Remove an ore from your list, e.g. !ore remove Ice")]
        [Permission(MyPromoteLevel.None)]
        public string Remove(string ore) => Context.Caller.IsConsole ? PlayerOnly : OreActions.Remove(Context.Caller.SteamId, ore);

        [Command("auto", "Toggle yeeting on ALL of your ship drills, not only the ones with the name tag")]
        [Permission(MyPromoteLevel.None)]
        public string Auto() => Context.Caller.IsConsole ? PlayerOnly : OreActions.ToggleAuto(Context.Caller.SteamId);

        [Command("clean", "Remove the listed ores already stored in the containers of the grid you sit in or look at")]
        [Permission(MyPromoteLevel.None)]
        public string Clean() => Context.Caller.IsConsole ? PlayerOnly : OreActions.Clean(Context.Caller.SteamId);

        [Command("master list", "Show the ores yeeted for every player")]
        [Permission(MyPromoteLevel.Admin)]
        public string MasterList() => OreActions.MasterList();

        [Command("master add", "Add an ore that is yeeted for every player, e.g. !ore master add Stone")]
        [Permission(MyPromoteLevel.Admin)]
        public string MasterAdd(string ore) => OreActions.MasterAdd(ore);

        [Command("master remove", "Remove an ore from the server list")]
        [Permission(MyPromoteLevel.Admin)]
        public string MasterRemove(string ore) => OreActions.MasterRemove(ore);
    }
}
