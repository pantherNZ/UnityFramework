using System.Collections.Generic;
using System.IO;
using System.Threading;
using Schema;
using UnityEngine;

namespace Runtime.Game
{
	public class GlobalRuntimeConstants : Save.BaseSave.IThreadManager
	{
		public GameObject localPlayer;
		public GameObject outpost;
		public UnityEngine.UIElements.VisualElement centralElement;

		Save.SaveMetaData _saveMetaData;
		Save.Items.InventorySave _inventoryOutpostSave;
		Save.Items.InventorySave _inventoryPreGrindSave;
		Save.Items.InventorySave _inventoryPlayerSave;
		Save.DiscoverySave _discoverySave;
		Save.WaypointSave _waypointSave;
		Save.PlayerSave _playerSave;
		Save.GlobalMonsterSave _globalMonsterSave;
		Save.Items.MerchantSave _merchantSave;
		Save.StatMazeSave _statMazeSave;
		Save.Terrain.SpawnNodeListSave _terrainSpawnNodeListSave;

		Schema.GlobalConstants _constants => GlobalConstantsHandler.Constants;

		private HashSet<Save.BaseSave> _pendingSaves = new HashSet<Save.BaseSave>();
		object _pendingSavesLock = new object();

		int? _mainThreadId;
		public static string GameName;
		public static int SaveIdx;
		public static int? Seed;
		public static int VersionNumber;
		public static bool Hardcore;
		public static int DepthLayer;

		public void Init()
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;

			Save.BaseSave save = saveMetaData;
			save = inventoryOutpostSave;
			save = inventoryPreGrindSave;
			save = inventoryPlayerSave;
			save = discoverySave;
			save = waypointSave;
			save = playerSave;
			save = globalMonsterSave;
			save = merchantSave;
			save = statMazeSave;
			save = terrainSpawnNodeListSave;
		}

		public bool IsMainThread()
		{
			Debug.Assert( _mainThreadId.HasValue, "Thread manager not initialized!" );
			return Thread.CurrentThread.ManagedThreadId == _mainThreadId.Value;
		}

		public void ScheduleSave( Save.BaseSave save )
		{
			lock ( _pendingSavesLock )
			{
				_pendingSaves.Add( save );
			}
		}

		public void Update()
		{
			if ( _pendingSaves.Count > 0 )
			{
				lock ( _pendingSavesLock )
				{
					foreach ( var save in _pendingSaves )
					{
						save.Save();
					}
					_pendingSaves.Clear();
				}
			}
		}

		public string GetPath( string ending )
		{
			return $"{_constants.RootSavePath}/{GameName}/World/{ending}";
		}

		public T GetSave<T>( string ending ) where T : Save.BaseSave
		{
			var save = Save.SaveManager.Instance.Get<T>( GetPath( ending ) );
			save.threadManager = this;
			return save;
		}

		public Save.SaveMetaData saveMetaDataReadOnly => _saveMetaData;

		public Save.SaveMetaData saveMetaData
		{
			get
			{
				if ( Save.SaveManager.Instance == null )
					return null;

				_saveMetaData ??= Save.SaveManager.Instance.Get<Save.SaveMetaData>( $"{_constants.RootSavePath}/{GameName}" );
				_saveMetaData.threadManager = this;
				_saveMetaData.gameName = GameName;
				_saveMetaData.saveIdx = SaveIdx;
				_saveMetaData.pendingSave = true;
				_saveMetaData.alwaysAutoSave = true;
				_saveMetaData.versionNumber = VersionNumber;
				if ( !_saveMetaData.ExistsOnDisk() )
				{
					_saveMetaData.isHardcore = Hardcore;
					_saveMetaData.Save();
				}
				return _saveMetaData;
			}
		}

		public Save.Items.InventorySave inventoryOutpostSave
		{
			get
			{
				_inventoryOutpostSave ??= GetSave<Save.Items.InventorySave>( _constants.InventoryOutpostSavePath );
				return _inventoryOutpostSave;
			}
		}

		public Save.Items.InventorySave inventoryPreGrindSave
		{
			get
			{
				_inventoryPreGrindSave ??= GetSave<Save.Items.InventorySave>( _constants.InventoryPreGrindSavePath );
				return _inventoryPreGrindSave;
			}
		}

		public Save.Items.InventorySave inventoryPlayerSave
		{
			get
			{
				_inventoryPlayerSave ??= GetSave<Save.Items.InventorySave>( _constants.InventoryPlayerSavePath );
				return _inventoryPlayerSave;
			}
		}

		public Save.DiscoverySave discoverySave
		{
			get
			{
				_discoverySave ??= GetSave<Save.DiscoverySave>( _constants.PlayerDiscoverySavePath );
				return _discoverySave;
			}
		}

		public Save.WaypointSave waypointSave
		{
			get
			{
				_waypointSave ??= GetSave<Save.WaypointSave>( _constants.TerrainWaypointSavePath.Format( DepthLayer ) );
				return _waypointSave;
			}
		}

		public Save.PlayerSave playerSave
		{
			get
			{
				_playerSave ??= GetSave<Save.PlayerSave>( _constants.PlayerSavePath );
				return _playerSave;
			}
		}

		public Save.GlobalMonsterSave globalMonsterSave
		{
			get
			{
				_globalMonsterSave ??= GetSave<Save.GlobalMonsterSave>( _constants.TerrainMonstersSavePath.Format( DepthLayer ) );
				return _globalMonsterSave;
			}
		}

		public Save.Items.MerchantSave merchantSave
		{
			get
			{
				_merchantSave ??= GetSave<Save.Items.MerchantSave>( _constants.MerchantSavePath );
				return _merchantSave;
			}
		}

		public Save.StatMazeSave statMazeSave
		{
			get
			{
				_statMazeSave ??= GetSave<Save.StatMazeSave>( _constants.PlayerStatMazeSavePath );
				return _statMazeSave;
			}
		}

		public Save.Terrain.SpawnNodeListSave terrainSpawnNodeListSave
		{
			get
			{
				_terrainSpawnNodeListSave ??= GetSave<Save.Terrain.SpawnNodeListSave>( _constants.TerrainSpawnNodeListSavePath.Format( DepthLayer ) );
				return _terrainSpawnNodeListSave;
			}
		}

		public Save.Skills.PlayerSkillSave GetPlayerSkillSave( int index )
		{
			return GetSave<Save.Skills.PlayerSkillSave>( $"{_constants.PlayerSkillSaveFolderPath}{index}" );
		}
	}
}
