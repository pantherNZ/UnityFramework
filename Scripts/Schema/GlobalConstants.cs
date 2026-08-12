using System;
using System.Collections.Generic;
using NeatoTags;
using UnityEngine;

namespace Schema
{
	[CreateAssetMenu( fileName = "GameConstants", menuName = "NQ/Constants/GameConstants" )]
	public class GlobalConstants : ScriptableObject
	{
		public const int MaxPlayers = 4;
		public const int MaxMonsterTargets = 30;

		[Header( "General" )]
		public string rngSeed; // empty means non-fixed/random
		[ReadOnly] public int runtimeRngSeed; // empty means non-fixed/random
		public LayerMask playerLayerMask;
		public LayerMask monsterLayerMask;
		public LayerMask highlightableLayerMask;
		public LayerMask envObjectLayerMask;
		public LayerMask VFXLayerMask;
		public NeatoTag outpostTag;
		public NeatoTag playerTag;

		[Header( "Logging" )]
		public bool enableLogging = false;
		public bool enableTerrainStructureLogging = false;
		public bool enableOreLogging = false;
		public bool enableMonsterLogging = false;
		public bool enablePlayerLogging = false;
		public bool enableEventsLogging = false;

		[Header( "Visual" )]
		public UI.Combat.DeltaVisualStyle enemyDamageStyle;
		public UI.Combat.DeltaVisualStyle allyDamageStyle;
		public bool hideTankTracks;

		[Header( "Balance" )]
		public float fuelUseFromMovementPerMeter = 1;

		public float barrierCooldown = 2;
		public int nextActionGraceTicks = 10;
		public float recoupDuration = 2;

		public int equipmentAffixSlotCraftingStepMin = 4;
		public int equipmentAffixSlotCraftingStepMax = 10;
		public int equipmentDurability = 100;

		public List<int> qualityAdditiveTable = new();
		public List<Sprite> upgradeCountIcon = new();
		public int upgradeTierCount = 1000;

		public float playerSellMultiplier = 0.8f;

		[Header( "Saves" )]
		[HideInInspector] public string RootSavePath = "SaveGames";
		[HideInInspector] public string InventoryOutpostSavePath => "Inventory/outpost";
		[HideInInspector] public string InventoryPreGrindSavePath => "Inventory/preGrind";
		[HideInInspector] public string InventoryPlayerSavePath => "Inventory/player";
		[HideInInspector] public string MerchantSavePath => "Inventory/merchant";
		[HideInInspector] public string PlayerSavePath => "Player/player";
		[HideInInspector] public string PlayerSkillSaveFolderPath => $"Player/Skills/";
		[HideInInspector] public string PlayerDiscoverySavePath => "Player/discovery";
		[HideInInspector] public string PlayerStatMazeSavePath => "Player/statMaze";
		// Terrain save paths
		[HideInInspector] public string TerrainWaypointSavePath => "Terrain/waypoint/{0}/";
		[HideInInspector] public string TerrainHeaderSavePath => "Terrain/header/{0}/";
		[HideInInspector] public string TerrainSpawnNodeListSavePath => "Terrain/spawnNodeList/{0}/";
		[HideInInspector] public string TerrainMapSaveFolderPath => "Terrain/Map/{0}/";
		[HideInInspector] public string TerrainMonstersSavePath => "Terrain/globalMonster/{0}/";
		[HideInInspector] public string TerrainclustersSaveFolderPath = "Terrain/Clusters/{0}/";
		[HideInInspector] public string TerraindungeonsSaveFolderPath = "Terrain/Dungeons/{0}/";


		// Callback
		public event Action onConstantsChanged;

		void OnValidate()
		{
			onConstantsChanged?.Invoke();

			runtimeRngSeed = Mathf.Abs( rngSeed.GetHashCode() );
		}

		public int GenerateSeed( params int[] vars )
			=> GenerateSeedStatic( runtimeRngSeed, vars );

		public static int GenerateSeedStatic( int runtimeRngSeed, params int[] vars )
		{
			var result = runtimeRngSeed;
			for ( int i = 0; i < vars.Length; ++i )
				result = ( result * 9176 ) + vars[i];
			return Utility.Mod( result, 23423645 );
		}
	}
}
