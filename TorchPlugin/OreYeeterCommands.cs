using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace OreYeeter.TorchHost
{
    /// <summary>Torch wrapper around the shared OreActions. Keep it free of logic, add that to OreActions instead.</summary>
    [Category("ore")]
    public class OreYeeterCommands : CommandModule
    {
        private bool RequirePlayer()
        {
            if (Context.Player != null)
                return true;
            Context.Respond("Player only command.");
            return false;
        }

        [Command("list", "Show the ores your yeeting drills void")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            if (RequirePlayer()) Context.Respond(OreActions.List(Context.Player.SteamUserId));
        }

        [Command("add", "Add an ore to your list, e.g. !ore add Ice")]
        [Permission(MyPromoteLevel.None)]
        public void Add(string ore)
        {
            if (RequirePlayer()) Context.Respond(OreActions.Add(Context.Player.SteamUserId, ore));
        }

        [Command("remove", "Remove an ore from your list, e.g. !ore remove Ice")]
        [Permission(MyPromoteLevel.None)]
        public void Remove(string ore)
        {
            if (RequirePlayer()) Context.Respond(OreActions.Remove(Context.Player.SteamUserId, ore));
        }

        [Command("auto", "Toggle yeeting on ALL of your ship drills, not only the ones with the name tag")]
        [Permission(MyPromoteLevel.None)]
        public void Auto()
        {
            if (RequirePlayer()) Context.Respond(OreActions.ToggleAuto(Context.Player.SteamUserId));
        }

        [Command("clean", "Remove the listed ores already stored in the containers of the grid you sit in or look at")]
        [Permission(MyPromoteLevel.None)]
        public void Clean()
        {
            if (RequirePlayer()) Context.Respond(OreActions.Clean(Context.Player.SteamUserId));
        }

        [Command("master list", "Show the ores yeeted for every player")]
        [Permission(MyPromoteLevel.Admin)]
        public void MasterList() => Context.Respond(OreActions.MasterList());

        [Command("master add", "Add an ore that is yeeted for every player, e.g. !ore master add Stone")]
        [Permission(MyPromoteLevel.Admin)]
        public void MasterAdd(string ore) => Context.Respond(OreActions.MasterAdd(ore));

        [Command("master remove", "Remove an ore from the server list")]
        [Permission(MyPromoteLevel.Admin)]
        public void MasterRemove(string ore) => Context.Respond(OreActions.MasterRemove(ore));

        [Command("reload", "Reload the OreYeeter config from disk")]
        [Permission(MyPromoteLevel.Admin)]
        public void Reload()
        {
            ((OreYeeterPlugin)Context.Plugin).Reload();
            Context.Respond("OreYeeter config reloaded.");
        }
    }
}
