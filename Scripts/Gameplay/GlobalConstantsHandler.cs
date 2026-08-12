using System.ComponentModel;
using System.IO;
using Schema;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runtime.Game
{
	[ExecuteAlways]
	public class GlobalConstantsHandler : MonoBehaviour
	{
		// Set via editor
		[SerializeField] bool constructRuntimeConstants = true;
		public Schema.GlobalConstants _constants;
		public Schema.Terrain.TerrainConstants _terrainConstants;
		public Schema.Monsters.MonsterConstants _monsterConstants;
		public Schema.Skills.SkillConstants _skillConstants;
		public Schema.Items.ItemConstants _itemConstants;
		public Schema.UIConstants _uiConstants;
		public Schema.FTUE.FTUEConstants _ftueConstants;
		public TextAsset versionFile;

		public static GlobalConstantsHandler Instance;
		public static Schema.GlobalConstants Constants;
		public static Schema.Terrain.TerrainConstants TerrainConstants;
		public static Schema.Monsters.MonsterConstants MonsterConstants;
		public static Schema.Skills.SkillConstants SkillConstants;
		public static Schema.Items.ItemConstants ItemConstants;
		public static Schema.UIConstants UIConstants;
		public static GlobalRuntimeConstants RuntimeConstants;
		public static Schema.FTUE.FTUEConstants FTUEConstants;


		private void OnEnable()
		{
			Instance = this;
			Constants = _constants;
			TerrainConstants = _terrainConstants;
			MonsterConstants = _monsterConstants;
			SkillConstants = _skillConstants;
			ItemConstants = _itemConstants;
			UIConstants = _uiConstants;
			FTUEConstants = _ftueConstants;

			if ( constructRuntimeConstants )
				RuntimeConstants = new();

			SetupSeed();

			IEventSystem.EnableLogging = _constants.enableEventsLogging;
			GlobalRuntimeConstants.VersionNumber = int.Parse( versionFile?.text ?? "0" );
		}

		void SetupSeed()
		{
			if ( GlobalRuntimeConstants.Seed != null )
			{
				Constants.runtimeRngSeed = GlobalRuntimeConstants.Seed.Value;
			}
			else
			{
				if ( !int.TryParse( Settings.Seed, out var seed ) )
				{
#if UNITY_EDITOR
					seed = _constants.rngSeed.Length > 0 ? _constants.rngSeed.GetHashCode() : 0;
#else
					seed = Time.time.ToString().GetHashCode();
#endif
				}

				Constants.runtimeRngSeed = Mathf.Abs( seed );
			}
		}

		public static void Log( string str )
		{
			if ( Constants.enableLogging )
				Debug.Log( str );
		}

		public static void LogWarning( string str )
		{
			if ( Constants.enableLogging )
				Debug.LogWarning( str );
		}

		public static void LogError( string str )
		{
			if ( Constants.enableLogging )
				Debug.LogError( str );
		}

		void Start()
		{
			if ( RuntimeConstants != null )
				RuntimeConstants.Init();

			if ( Application.isPlaying && MonsterConstants != null )
			{
				var poolingData = new Memory.PoolContainer();
				poolingData.poolName = MonsterConstants.ToString();
				if ( _monsterConstants.defaultDeathEffects.bloodDeathPrefab != null )
					poolingData.AddTemplate( _monsterConstants.defaultDeathEffects.bloodDeathPrefab?.transform, 10, 100 );
				if ( _monsterConstants.defaultDeathEffects.bloodHitPrefab != null )
					poolingData.AddTemplate( _monsterConstants.defaultDeathEffects.bloodHitPrefab?.transform, 20, 500 );
				_monsterConstants.defaultDeathEffects.poolOverride = Memory.PoolManager.instance.AddPool( poolingData );
			}
		}

		void Update()
		{
			if ( RuntimeConstants != null )
				RuntimeConstants.Update();
		}

		void OnApplicationQuit()
		{
			if ( RuntimeConstants != null )
			{
				RuntimeConstants.globalMonsterSave.Save();
				RuntimeConstants.Update();
			}
		}
	}
}
