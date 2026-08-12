using System;
using Newtonsoft.Json;
using Runtime.Game;
using Schema;
using Schema.Functional.Float;

namespace Save
{
    public partial class SaveMetaData : JSONSave
    {
        [JsonProperty] public string gameName;
        [JsonProperty] public DateTime lastPlayed;
        [JsonProperty] public TimeSpan timePlayed;
        [JsonProperty] public int level;
        [JsonProperty] public int maxDangerReached;
        [JsonProperty] public int saveIdx;
        [JsonProperty] public int seed;
        [JsonProperty] public int versionNumber;
        [JsonProperty] public bool isHardcore;
        [JsonProperty] public bool inOutpost;
        [JsonProperty] public int depthLayer;

        public SaveMetaData( string path ) : base( path )
        {
            lastPlayed = DateTime.Now;
        }

        public override void Save()
        {
            timePlayed += DateTime.Now - lastPlayed;
            lastPlayed = DateTime.Now;

            var localPlayer = GlobalConstantsHandler.RuntimeConstants.localPlayer;
            if ( localPlayer != null )
                maxDangerReached = Math.Max( maxDangerReached, localPlayer.GetComponent<PlayerController>().maxDangerReached );

            depthLayer = GlobalConstantsHandler.RuntimeConstants.playerSave?.depthLayer ?? 0;
            inOutpost = GlobalConstantsHandler.RuntimeConstants.playerSave?.IsLoggedOutInOutpost() ?? false;
            level = GlobalConstantsHandler.RuntimeConstants.statMazeSave.GetUnlockedCount();
            seed = GlobalConstantsHandler.Constants.runtimeRngSeed;
            versionNumber = GlobalRuntimeConstants.VersionNumber;

            var totalDepth = new Runtime.Terrain.DepthScaleData( maxDangerReached, depthLayer );
            SteamManager.Instance?.UpdateLeaderboard( totalDepth.scale, seed, level, isHardcore, timePlayed );
            SteamManager.Instance?.UpdateGameStat( GameStatType.MAX_DEPTH, totalDepth.scale );

            base.Save();
        }
    }
}
