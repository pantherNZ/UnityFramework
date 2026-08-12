namespace Schema
{
    // Don't delete entries, either set as UNUSED or add a new entry.
    // These strings match the achievement names in the Steamworks API.
    public enum AchievementType
    {
        None = 0,
        COMPLETED_ONBOARDING = 1, // Mission schema
        DEPTH_500 = 2, // Implemented via max depth game stat 
        DEPTH_1000 = 3, // Implemented via max depth game stat 
        OUTPOST_OUT_OF_FUEL = 4,
        DRIVEN_1000 = 5, // Implemented via distance travelled game stat 
        DPS_1000 = 6, // Implemented via max dps game stat 
        ITEM_CRAFT = 7,
        BEAT_ACT1 = 8,
        BEAT_ACT_HC = 9,
        BUILD_WAYPOINT = 10,
        OUTPOST_LOW_HP = 11,
        KILL_BOSS_ICE = 12, // BeastBossStructureSchema
        KILL_BOSS_FIRE = 13, // BeastBossStructureSchema
    }
}