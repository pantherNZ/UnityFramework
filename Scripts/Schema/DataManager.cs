using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using System.Collections.ObjectModel;
using UnityEditor;
using UnityEngine.TextCore.Text;
using System.Xml.Serialization;
using UnityEngine.InputSystem;
using Schema.Story;
using Runtime.Game;



#if UNITY_EDITOR
using UnityEditor.Build;
#endif

namespace Schema
{
	public enum DataType
	{
		NoiseLayers,
		Rarities,
		TerrainBiomes,
		TerrainPlugins,
		TerrainStructures,
		TerrainDungeonTiles,
		TerrainDungeons,
		Mechanics,
		Actions,
		Skills,
		Items,
		Ores,
		Equipment,
		MonsterPopulations,
		GameStrings,
		FTUE,
		Audio,
		Modifiers,
		Missions,
		Milestones,
		AltCurrencies,
		Lores,
		Cosmetics,

		// Add new entires before Misc
		Misc,
	}

	[ExecuteAlways]
	[DisallowMultipleComponent]
	public class DataManager : MonoBehaviour
	{
		public struct DataTypeParams
		{
			public DataTypeParams( string path, Type type, bool recursiveLoad = false )
			{
				this.path = path;
				this.type = type;
				this.recursiveLoad = recursiveLoad;
			}

			public string path;
			public Type type;
			public bool recursiveLoad;
		};

		public Dictionary<DataType, DataTypeParams> DataSourcePaths => new()
		{
			{ DataType.NoiseLayers, new( "Data/Terrain/NoiseLayers/", typeof( Terrain.INoiseLayerDataSchema ) ) },
			{ DataType.TerrainBiomes, new( "Data/Terrain/Biomes/", typeof( Terrain.BiomeDataSchema ) ) },
			{ DataType.TerrainPlugins, new( "Data/Terrain/TerrainPlugins/", typeof( Terrain.BaseTerrainPluginSchema ) ) },
			{ DataType.TerrainStructures, new( "Data/Terrain/Structures", typeof( Terrain.IStructureSchema ), recursiveLoad: true ) },
			{ DataType.TerrainDungeons, new( "Data/Terrain/Dungeons/", typeof( Terrain.DungeonSchema ) ) },
			{ DataType.TerrainDungeonTiles, new( "Data/Terrain/Dungeons/Tiles/", typeof( Terrain.DungeonTileSchema ) ) },
			{ DataType.Mechanics, new( "Data/Mechanic/", typeof( Tree.MechanicSchema ) ) },
			{ DataType.Skills, new( "Data/Tree/Skill/", typeof( Skills.AxiomSchema ) ) },
			{ DataType.Items, new( "Data/Items/", typeof( Items.OreItemSchema ),  recursiveLoad: true ) },
			{ DataType.Ores, new( "Data/Terrain/Ores/", typeof( Terrain.OreDataSchema ) ) },
			{ DataType.MonsterPopulations, new( "Data/Monsters/Populations/", typeof( Monsters.MonsterPopulationSchema ) ) },
			{ DataType.GameStrings, new( "Data/GameStrings/", typeof( GameStringsSchema ) ) },
			{ DataType.FTUE, new( "Data/FTUE/", typeof( BaseDataSchema ),  recursiveLoad: true ) },
			{ DataType.Audio, new( "Data/Audio/", typeof( Audio.AudioDataSchema ), recursiveLoad: true )  },
			{ DataType.Modifiers, new( "Data/Combat/Modifier/Status/", typeof( Combat.Modifier.ModifierSchema ) ) },
			{ DataType.Missions, new( "Data/FTUE/Mission/", typeof( FTUE.MissionSchema ) ) },
			{ DataType.Milestones, new( "Data/FTUE/Milestone/", typeof( FTUE.Milestone ) ) },
			{ DataType.AltCurrencies, new( "Data/Items/AltCurrency/", typeof( Items.AltCurrencySchema ) ) },
			{ DataType.Lores, new( "Data/FTUE/Lore/", typeof( Story.LoreSchema ) ) },
			{ DataType.Cosmetics, new( "Data/Equipment/Cosmetics/", typeof( Items.CosmeticBodyPartSchema ), recursiveLoad: true ) },

			// Add new entires before Misc
			{ DataType.Misc, new( "Data/", typeof( BaseDataSchema ),  recursiveLoad: true ) },
		};

		// Singleton
		static DataManager dataManager;
		public static DataManager Instance { get { return dataManager; } }

		static SerializableHashSet<BaseDataSchema> registeredAssets = new();

		Dictionary<int, BaseDataSchema> allDataAssets = new();


		// Terrain data ----------
		List<Terrain.INoiseLayerDataSchema> noiseLayers;
		List<Terrain.NoiseGeneratorSchema> rockNoiseLayers;
		public ReadOnlyCollection<Terrain.INoiseLayerDataSchema> NoiseLayers => noiseLayers.AsReadOnly();
		public ReadOnlyCollection<Terrain.NoiseGeneratorSchema> RockNoiseLayers => rockNoiseLayers.AsReadOnly();

		List<Terrain.OreDataSchema> ores;
		public ReadOnlyCollection<Terrain.OreDataSchema> Ores => ores.AsReadOnly();
		public Dictionary<Items.OreItemSchema, List<Terrain.OreDataSchema>> OreTiersPerType;
		List<Items.AltCurrencySchema> altCurrencies;
		public ReadOnlyCollection<Items.AltCurrencySchema> AltCurrencies => altCurrencies.AsReadOnly();

		public Items.RaritiesSchema rarities;

		List<Terrain.BiomeDataSchema> biomes;
		public ReadOnlyCollection<Terrain.BiomeDataSchema> Biomes => biomes.AsReadOnly();

		List<Terrain.BaseTerrainPluginSchema> terrainPlugins = new();
		public ReadOnlyCollection<Terrain.BaseTerrainPluginSchema> TerrainPlugins => terrainPlugins.AsReadOnly();
		public PluginType GetTerrainPlugin<PluginType>() where PluginType : Terrain.BaseTerrainPluginSchema => terrainPlugins.First( x => x is PluginType ) as PluginType;

		List<Terrain.IStructureSchema> structures = new();
		public ReadOnlyCollection<Terrain.IStructureSchema> Structures => structures.AsReadOnly();
		List<Terrain.DungeonSchema> dungeons = new();
		public ReadOnlyCollection<Terrain.DungeonSchema> Dungeons => dungeons.AsReadOnly();
		List<Terrain.DungeonTileSchema> dungeonTiles = new();
		public ReadOnlyCollection<Terrain.DungeonTileSchema> DungeonTiles => dungeonTiles.AsReadOnly();


		// Gameplay data --------------
		List<StatMetaData> statsMetaData;
		public ReadOnlyCollection<StatMetaData> StatsMetaData => statsMetaData.AsReadOnly();
		List<TagMetaData> tagsMetaData;
		public ReadOnlyCollection<TagMetaData> TagsMetaData => tagsMetaData.AsReadOnly();
		List<Tree.MechanicSchema> mechanics;
		public ReadOnlyCollection<Tree.MechanicSchema> Mechanics => mechanics.AsReadOnly();
		List<Combat.Modifier.ModifierSchema> modifiers;
		public ReadOnlyCollection<Combat.Modifier.ModifierSchema> Modifiers => modifiers.AsReadOnly();
		List<FTUE.MissionSchema> missions;
		public ReadOnlyCollection<FTUE.MissionSchema> Missions => missions.AsReadOnly();

		List<Skills.AxiomSchema> axioms;
		public ReadOnlyCollection<Skills.AxiomSchema> Axioms => axioms.AsReadOnly();

		List<FTUE.SlideShowSchema> slideShows;
		public ReadOnlyCollection<FTUE.SlideShowSchema> SlideShows => slideShows.AsReadOnly();
		List<FTUE.Milestone> milestones;
		public ReadOnlyCollection<FTUE.Milestone> Milestones => milestones.AsReadOnly();
		List<Story.LoreSchema> lores;
		public ReadOnlyCollection<Story.LoreSchema> Lores => lores.AsReadOnly();


		// Items data ----------------
		List<Items.OreItemSchema> oreItems;
		public ReadOnlyCollection<Items.OreItemSchema> OreItems => oreItems.AsReadOnly();
		public Items.OreItemSchema defaultOreItem;
		public Dictionary<Items.OreItemSchema, List<UI.Combat.DeltaVisualStyle>> currencyDeltaVisualStyles;

		List<Items.CosmeticBodyPartSchema> cosmetics;
		public ReadOnlyCollection<Items.CosmeticBodyPartSchema> Cosmetics => cosmetics.AsReadOnly();


		// Monsters data -----------------
		List<Monsters.MonsterPopulationSchema> monsterPopulations;
		public ReadOnlyCollection<Monsters.MonsterPopulationSchema> MonsterPopulations => monsterPopulations.AsReadOnly();


		// Misc ----------------------
		public GlobalConstants gameConstants;
		[HideInInspector] public GameStringsManager gameStrings;

		List<Audio.AudioDataSchema> audioClips;
		public Dictionary<Audio.AudioDataSchema, List<Audio.AudioDataSchema>> GroupedAudioClips;
		public Dictionary<Audio.AudioDataSchema, List<Audio.AudioDataSchema>> AudioCooldownGroups;
		public ReadOnlyCollection<Audio.AudioDataSchema> AudioClips => audioClips.AsReadOnly();


		public static string GetDataJsonResourcePath( DataType source ) => $"DataPaths/{source}";
		public static string GetDataJsonPathFull( DataType source ) => $"{Application.dataPath}/Resources/{GetDataJsonResourcePath( source )}.json";
		public static string GetCurrentVersionPath() => $"{Application.dataPath}/Resources/DataPaths/CurrentVersion.txt";


		private void OnEnable()
		{
			Init();
		}

		private void Init()
		{
			if ( dataManager != null && dataManager != this )
			{
				Debug.LogError( "Multiple data managers found!" );
				Destroy( gameObject );
				return;
			}

			dataManager = this;
			LoadAllData();
		}

		public BaseDataSchema FindAssetByHash( int hash )
		{
			if ( allDataAssets.TryGetValue( hash, out var result ) )
				return result;
			Debug.LogError( $"DataManager failed to find asset by hash: {hash}" );
			return null;
		}

		// Game strings
		public string GetGameString( GameStringType key )
			=> gameStrings.GetGameString( key );
		public string GetGameString( StatType stat, bool errorOnFail = true )
		 	=> gameStrings.GetGameString( stat, errorOnFail );
		public string GetGameStringFormatted( GameStringType key, int arg )
			=> gameStrings.GetGameStringFormatted( key, arg );
		public string GetGameStringFormatted( GameStringType key, string arg )
		=> gameStrings.GetGameStringFormatted( key, arg );
		public string GetGameStringFormatted( Runtime.Game.TypedStatRuntime stat, bool errorOnFail = true )
			=> GetGameStringFormatted( stat.type, ( int )stat.GetValue().value, errorOnFail );
		public string GetGameStringFormatted( StatType stat, int arg, bool errorOnFail = true )
			=> gameStrings.GetGameStringFormatted( stat, arg, errorOnFail );

#if UNITY_EDITOR
		[MenuItem( "Scripts/Regenerate Game Data" )]
		public static void LoadDataEditor()
		{
			if ( Schema.DataManager.Instance == null )
			{
				Debug.LogError( "Tried to generate loot schemas for ore drops but DataManager instance was not found (likely in wrong scene)" );
				return;
			}

			new PreBuildFileNamesSaver().OnPreprocessBuild( null );
			DataManager.Instance.GenerateAllData();
		}

		public void GenerateAllData()
		{
			GenerateData();
		}
#endif

		public void LoadAllData()
		{
			LoadData();
		}

		public static string StringToEnumVal( string val )
			=> val.Replace( " ", string.Empty ).Replace( "-", string.Empty );

		private string TerrainPluginNameFromType( Terrain.BaseTerrainPluginSchema plugin )
		{
			return $"{StringToEnumVal( plugin.name ).Replace( "PluginSchema", string.Empty )}";
		}

		private List<T> LoadDataOfType<T>( DataType dataType, bool allowEmptyResults = false ) where T : BaseDataSchema
		{
			var path = GetDataJsonResourcePath( dataType );
			var fileListJson = Resources.Load<UnityEngine.TextAsset>( path );

#if UNITY_EDITOR
			if ( fileListJson == null )
			{
				new PreBuildFileNamesSaver().OnPreprocessBuild( null );
				fileListJson = Resources.Load<UnityEngine.TextAsset>( path );
			}
#endif

			if ( fileListJson == null )
			{
				Debug.LogError( $"LoadDataOfType failed to find file list json for {dataType} at path: {path}\nDid you miss adding the entry to DataSourcePaths?" );
				return null;
			}

			var filePaths = JsonHelper.FromJson<string>( fileListJson.text );
			return LoadDataOfType<T>( path, filePaths, allowEmptyResults );
		}

		private List<T> LoadDataOfType<T>( string path, string[] filePaths, bool allowEmptyResults = false ) where T : BaseDataSchema
		{
			if ( filePaths.IsEmpty() )
			{
				Debug.LogError( $"LoadDataOfType failed to find any assets from path: {path}" );
				return null;
			}

			var dataFiles = filePaths
				.Select( x => (x, Resources.Load( x )) )
				.Where( x => x.Item2 != null && typeof( T ).IsAssignableFrom( x.Item2.GetType() ) ).ToList();
			var results = new List<T>();

			foreach ( var (newAssetPath, newAsset) in dataFiles )
				if ( AddDataAsset( newAssetPath, newAsset as T ) )
					results.Add( newAsset as T );

			if ( !allowEmptyResults && results.IsEmpty() )
			{
				Debug.LogError( $"LoadDataOfType failed to find any matching assets of type {typeof( T )} from path: {path}" );
				return null;
			}

			return results.OrderBy( x => x.name ).ToList();
		}

		private bool AddDataAsset( string path, BaseDataSchema newAsset )
		{
			if ( !registeredAssets.Contains( newAsset ) )
			{
				newAsset.Init( true );
				registeredAssets.Add( newAsset );
			}

			// Debug hash collision checker
			var found = allDataAssets.TryGetValue( newAsset.GetHashCode(), out var existing );

#if UNITY_EDITOR
			if ( found && path != existing.path )
			{
				existing.Init( true );
				newAsset.Init( true );
				found = allDataAssets.TryGetValue( newAsset.GetHashCode(), out existing );
			}

			if ( found && path != existing.path )
			{
				Debug.LogError( $"Data type hash collision detected between: {path}   |   {existing.path}" );
				return false;
			}
			newAsset.path = path;
#endif

			if ( !found )
			{
				allDataAssets.Add( newAsset.GetHashCode(), newAsset );

				if ( Application.isPlaying )
					newAsset.OnDataLoaded();
			}

			return !found;
		}

#if UNITY_EDITOR
		private void GenerateData()
		{
			LoadData( true );

			GenerateEnumSchema( nameof( TerrainPlugins ), typeof( Terrain.BaseTerrainPluginSchema ), "TerrainPlugins", terrainPlugins.Select( x => TerrainPluginNameFromType( x ) ) );
			GenerateEnumSchema( nameof( NoiseLayers ), typeof( Terrain.INoiseLayerDataSchema ), "NoiseLayer", noiseLayers.Select( x => x.name ), true );
			GenerateEnumSchema( nameof( Biomes ), typeof( Terrain.BiomeDataSchema ), "BiomeSubType", biomes.Select( x => x.name ), true );
			GenerateEnumSchema( nameof( Mechanics ), typeof( Tree.MechanicSchema ), "MechanicType", mechanics.Select( x => x.name ) );
			GenerateEnumSchema( nameof( Modifiers ), typeof( Combat.Modifier.ModifierSchema ), "ModifierType", modifiers.Select( x => x.name ) );
			GenerateEnumSchema( nameof( Missions ), typeof( FTUE.MissionSchema ), "MissionType", missions.Select( x => x.name ) );
			GenerateEnumSchema( nameof( Axioms ), typeof( Skills.AxiomSchema ), "AxiomType", axioms.Select( x => x.name ) );
			GenerateEnumSchema( nameof( OreItems ), typeof( Items.OreItemSchema ), "OreItemType", oreItems.Select( x => x.name ) );
			GenerateEnumSchema( nameof( Ores ), typeof( Terrain.OreDataSchema ), "OreType", ores.Select( x => x.name ), true );
			GenerateEnumSchema( nameof( AltCurrencies ), typeof( Items.AltCurrencySchema ), "AltCurrencyType", altCurrencies.Select( x => x.name ), true );
			GenerateEnumSchema( nameof( SlideShows ), typeof( FTUE.SlideShowSchema ), "SlideShowType", slideShows.Select( x => x.name ) );
			GenerateEnumSchema( nameof( Milestones ), typeof( FTUE.Milestone ), "MilestoneType", milestones.Select( x => x.name ) );
			GenerateEnumSchema( nameof( Lores ), typeof( Story.LoreSchema ), "LoreType", lores.Select( x => x.name ) );
			GenerateEnumSchema( nameof( AudioClips ), typeof( Audio.AudioDataSchema ), "AudioType", audioClips.Select( x => x.name ), prependFlags: new string[] { "None" } );
			GenerateGameStringsEnum( "GameStringType", gameStrings.gameStrings[( int )Language.English].strings.Select( x => x.key ) );

			statsMetaData = MetaDataFactory<StatType, StatMetaData>.FindOrCreateMetaDataAssets( "Stat" );
			tagsMetaData = MetaDataFactory<TagType, TagMetaData>.FindOrCreateMetaDataAssets( "Tag" );

			for ( int i = 0; i < noiseLayers.Count; ++i )
			{
				if ( noiseLayers[i].layerType != ( NoiseLayer )( 1 + i ) )
				{
					noiseLayers[i].layerType = ( NoiseLayer )( 1 + i );
					EditorUtility.SetDirty( noiseLayers[i] );
				}
			}
			for ( int i = 0; i < biomes.Count; ++i )
			{
				bool valid = Utility.TryParseEnum( StringToEnumVal( biomes[i].name ), out BiomeType biomeType );

				if ( biomes[i].biomeSubType != ( BiomeSubType )( 1 + i ) || ( valid && biomes[i].biomeType != biomeType ) )
				{
					if ( valid )
						biomes[i].biomeType = biomeType;
					biomes[i].biomeSubType = ( BiomeSubType )( 1 + i );
					EditorUtility.SetDirty( biomes[i] );
				}
			}
			for ( int i = 0; i < terrainPlugins.Count; ++i )
			{
				if ( terrainPlugins[i].pluginType != ( TerrainPlugins )( i ) )
				{
					terrainPlugins[i].pluginType = ( TerrainPlugins )( i );
					EditorUtility.SetDirty( terrainPlugins[i] );
				}
			}
			for ( int i = 0; i < oreItems.Count; ++i )
			{
				if ( oreItems[i].oreType != ( OreItemType )( i ) )
				{
					oreItems[i].oreType = ( OreItemType )( i );
					EditorUtility.SetDirty( oreItems[i] );
				}
			}
			for ( int i = 0; i < ores.Count; ++i )
			{
				if ( ores[i].oreType != ( OreType )( 1 + i ) )
				{
					ores[i].oreType = ( OreType )( 1 + i );
					EditorUtility.SetDirty( ores[i] );
				}
			}


			Validate();

			AssetDatabase.SaveAssets();
		}

		void Validate()
		{
			foreach ( var entry in audioClips )
				if ( entry.entries.IsEmpty() || entry.entries.All( x => x.clip == null ) )
					Debug.LogError( $"{( entry is DialogueSchema ? "Dialogue" : "Audio" )} clip '{entry.name}' has no audio clip entries defined!" );
		}
#endif

		private void LoadData( bool ignoreMetadata = false )
		{
			allDataAssets.Clear();

			registeredAssets = new( registeredAssets.Where( obj => obj != null ).ToHashSet() );

			noiseLayers = LoadDataOfType<Terrain.INoiseLayerDataSchema>( DataType.NoiseLayers );
			rockNoiseLayers = noiseLayers.OfType<Terrain.NoiseGeneratorSchema>().Where( x => x.isBaseRockLayer ).ToList();
			rarities = Resources.Load<Items.RaritiesSchema>( "Data/Inventory/Rarities" );
			biomes = LoadDataOfType<Terrain.BiomeDataSchema>( DataType.TerrainBiomes );
			terrainPlugins = LoadDataOfType<Terrain.BaseTerrainPluginSchema>( DataType.TerrainPlugins );
			structures = LoadDataOfType<Terrain.IStructureSchema>( DataType.TerrainStructures );
			dungeons = LoadDataOfType<Terrain.DungeonSchema>( DataType.TerrainDungeons );
			dungeonTiles = LoadDataOfType<Terrain.DungeonTileSchema>( DataType.TerrainDungeonTiles );
			mechanics = LoadDataOfType<Tree.MechanicSchema>( DataType.Mechanics );
			modifiers = LoadDataOfType<Combat.Modifier.ModifierSchema>( DataType.Modifiers );
			missions = LoadDataOfType<FTUE.MissionSchema>( DataType.Missions );
			axioms = LoadDataOfType<Skills.AxiomSchema>( DataType.Skills );
			oreItems = LoadDataOfType<Items.OreItemSchema>( DataType.Items, true );
			currencyDeltaVisualStyles = PopulateCurrencyDeltaVisualStyles( OreItems );
			ores = LoadDataOfType<Terrain.OreDataSchema>( DataType.Ores );
			altCurrencies = LoadDataOfType<Items.AltCurrencySchema>( DataType.AltCurrencies );
			slideShows = LoadDataOfType<FTUE.SlideShowSchema>( DataType.FTUE );
			milestones = LoadDataOfType<FTUE.Milestone>( DataType.FTUE );
			lores = LoadDataOfType<Story.LoreSchema>( DataType.Lores );
			cosmetics = LoadDataOfType<Items.CosmeticBodyPartSchema>( DataType.Cosmetics, allowEmptyResults: true );
			monsterPopulations = LoadDataOfType<Monsters.MonsterPopulationSchema>( DataType.MonsterPopulations );
			audioClips = LoadDataOfType<Audio.AudioDataSchema>( DataType.Audio );

			OreTiersPerType = ores.GroupBy( x => x.itemType ).ToDictionary( g => g.Key, g => g.OrderBy( x => x.fixedTierValue ).ToList() );
			GroupedAudioClips = audioClips.Where( x => x.groupedWith != null ).GroupBy( x => x.groupedWith ).ToDictionary( g => g.Key, g => g.ToList() );
			foreach ( var (key, group) in GroupedAudioClips )
			{
				bool isDialogue = key is Story.DialogueSchema;
				foreach ( var entry in group )
					if ( isDialogue != ( entry is Story.DialogueSchema ) )
						Debug.LogError( $"Audio clip '{entry.name}' is grouped with '{key.name}' but one is a dialogue and the other isn't!" );
				group.Add( key );
			}

			AudioCooldownGroups = new();
			foreach ( var clip in audioClips )
				foreach ( var group in clip.cooldownGroups )
					AudioCooldownGroups.GetOrAdd( group ).Add( clip );

			if ( !ignoreMetadata )
			{
				statsMetaData = MetaDataFactory<StatType, StatMetaData>.LoadMetaDataAssets( "Stat" );
				tagsMetaData = MetaDataFactory<TagType, TagMetaData>.LoadMetaDataAssets( "Tag" );
			}

			var gameStringFiles = LoadDataOfType<GameStringsSchema>( DataType.GameStrings );
			gameStrings = new GameStringsManager( gameStringFiles );

			// Load all assets generically so they are cached and hashed
			LoadDataOfType<BaseDataSchema>( DataType.Misc, allowEmptyResults: true );

#if UNITY_EDITOR
			// Try to find assets outside of Data folder
			var wrongFolderDataAssets = new List<string>();
			Utility.GetResourcePaths( "", ref wrongFolderDataAssets );
			foreach ( var found in LoadDataOfType<BaseDataSchema>( "", wrongFolderDataAssets.ToArray(), allowEmptyResults: true ) )
				Debug.LogError( $"LoadDataOfType found a BaseDataSchema asset outside of the data folder: {Utility.GetResourcePath( found )}" );
#endif
		}

		public string GetRichTextInputIconString( InputAction action ) =>
			UI.InputBindIconData.GetRichTextInputIconString( action );

		public string InjectRichTextInputIconString( string text ) =>
			UI.InputBindIconData.InjectRichTextInputIconString( text, out _ );

		public string InjectRichTextInputIconString( string text, out bool containsInputAction ) =>
			UI.InputBindIconData.InjectRichTextInputIconString( text, out containsInputAction );

		Dictionary<Items.OreItemSchema, List<UI.Combat.DeltaVisualStyle>> PopulateCurrencyDeltaVisualStyles( ReadOnlyCollection<Items.OreItemSchema> oreItems )
		{
			Dictionary<Items.OreItemSchema, List<UI.Combat.DeltaVisualStyle>> dictionary = new();
			foreach ( var ore in oreItems )
			{
				var styles = new List<UI.Combat.DeltaVisualStyle>();
				for ( int tier = 0; tier < ore.tiers.Count; ++tier )
				{
					var style = ScriptableObject.CreateInstance<UI.Combat.DeltaVisualStyle>();
					style.showPositiveSign = true;
					style.foreground = new UI.Combat.DeltaVisualStyle.Picture();
					style.foreground.sprite = ore.icon;
					style.background = new UI.Combat.DeltaVisualStyle.Picture();
					style.background.sprite = ore.iconFill;
					style.background.color = rarities.list[tier].color;
					styles.Add( style );
				}
				dictionary.Add( ore, styles );
			}
			return dictionary;
		}

		void GenerateEnumSchema( string dataContainer, Type dataType, string name, IEnumerable<string> values, bool generateEnumFlags = false, IEnumerable<string> extraFlags = null, IEnumerable<string> prependFlags = null )
		{
			int enumOffset = 0;
			values = values.Where( x => !x.IsEmpty() ).Select( x => StringToEnumVal( x ) );
			if ( extraFlags != null )
				extraFlags = extraFlags.Where( x => !x.IsEmpty() );
			if ( prependFlags != null )
			{
				foreach ( var v in prependFlags )
				{
					values = values.Prepend( v );
					enumOffset++;
				}
			}
			if ( generateEnumFlags )
			{
				values = values.Prepend( "None" );
				++enumOffset;

				if ( values.Count() + ( extraFlags != null ? extraFlags.Count() : 0 ) >= 32 )
					Debug.LogError( "Over 32 flags is not supported!" );
			}

#if UNITY_EDITOR
			var lines =
@"// This file is manually generated by DataManager.cs, don't modify directly
// Entries are generated from found {1} scriptable objects in the Data folder

public static partial class {0}Utility
{{
	public static {1} GetSchema( this Schema.{5} enumValue )
	{{
		return Schema.DataManager.Instance.{2}[( int )enumValue{3}];
	}}
}}

namespace Schema
{{
	public enum {5}
	{{
		{4}
		_Count
	}}
";
			lines = lines.Format(
				name,
				dataType.ToString(),
				dataContainer,
				enumOffset > 0 ? ( " - " + enumOffset ) : string.Empty,
				string.Join( "\r\n\t\t", values.Select( x => x + ',' ) ),
				name );

			var path = $"{Application.dataPath}/Scripts/Schema/{name}Schema_Generated.cs";

			if ( generateEnumFlags )
			{
				var flags = values.Skip( 1 );
				if ( extraFlags != null )
					flags = flags.Concat( extraFlags );

				var flagsLines = @"
	[System.Flags]
	public enum {1}Flags
	{{
		None,
		{0}
		All = ~0
	}}";
				lines += flagsLines.Format(
					string.Join( "\r\n\t\t",
					flags.Select( ( x, idx ) => $"{x} = 1 << {idx}," ) ),
					name );
			}

			lines += "\r\n}";

			if ( !File.Exists( path ) || File.ReadAllText( path ) != lines )
				File.WriteAllText( path, lines );
#endif
		}

		void GenerateGameStringsEnum( string name, IEnumerable<string> values )
		{
			values = values.Where( x => !x.IsEmpty() ).Select( x => StringToEnumVal( x ) );

#if UNITY_EDITOR
			var lines =
@"// This file is manually generated by DataManager.cs, don't modify directly
// Entries are generated from English GameStrings keys

public static partial class {0}Utility
{{
	public static string GetString( this Schema.{0} enumValue )
	{{
		return Schema.DataManager.Instance.GetGameString(enumValue);
	}}
}}

namespace Schema
{{
	public enum {0}
	{{
		{1}
		_Count
	}}
";
			lines = lines.Format(
				name,
				string.Join( "\r\n\t\t", values.Select( x => x + ',' ) ) );

			var path = $"{Application.dataPath}/Scripts/Schema/{name}Schema_Generated.cs";

			lines += "\r\n}";

			if ( !File.Exists( path ) || File.ReadAllText( path ) != lines )
				File.WriteAllText( path, lines );
#endif
		}

#if UNITY_EDITOR
		[MenuItem( "Scripts/Generate AudioDataSchema" )]
		static void GeneratorLoreAudioAssets()
		{
			GeneratorAudioAssets<Story.DialogueSchema>( "Audio/Dialogue/", "Assets/Resources/Data/Audio/Narrator/Dialogue_" );
			GeneratorAudioAssets<Audio.AudioDataSchema>( "Audio/Lore/", "Assets/Resources/Data/Audio/Lore/Lore_" );
		}

		static void GeneratorAudioAssets<T>( string sourcePath, string targetPath ) where T : Audio.AudioDataSchema
		{
			List<string> filePaths = new();
			Utility.GetResourcePaths( sourcePath, ref filePaths, recursive: true, extension: ".mp3" );

			bool anyGenerated = false;
			foreach ( var dialoguePath in filePaths )
			{
				var dialogue = Resources.Load<AudioClip>( dialoguePath );
				var audioName = $"{dialogue.name.RemoveChars( '\n', ' ', '\r', '\t' )}"; ;
				var path = $"{targetPath}{audioName}.asset";
				var audioData = UnityEditor.AssetDatabase.LoadAssetAtPath<T>( path );

				if ( audioData != null )
					continue;

				audioData = ScriptableObject.CreateInstance<T>();
				audioData.name = audioName;
				audioData.entries = new List<Audio.SfxEntry>() { new() { clip = dialogue } };
				audioData.looping = false;
				audioData.startDelaySec = 0f;
				audioData.playMethod = Audio.PlayMethod.Random;
				audioData.volumeOverride = 1.0f;
				audioData.disabled = false;
				UnityEditor.AssetDatabase.CreateAsset( audioData, path );
				audioData.Init( true );
				anyGenerated = true;
			}

			if ( anyGenerated )
				UnityEditor.AssetDatabase.SaveAssets();
		}
#endif
	}

	public static class JsonHelper
	{
		public static T[] FromJson<T>( string json )
		{
			Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>( json );
			return wrapper.Items;
		}

		public static string ToJson<T>( T[] array, bool prettyPrint = false )
		{
			Wrapper<T> wrapper = new Wrapper<T> { Items = array };
			return JsonUtility.ToJson( wrapper, prettyPrint );
		}

		[Serializable]
		private class Wrapper<T>
		{
			public T[] Items;
		}
	}

#if UNITY_EDITOR
	class PreBuildFileNamesSaver : IPreprocessBuildWithReport
	{
		public int callbackOrder { get { return 0; } }
		public void OnPreprocessBuild( UnityEditor.Build.Reporting.BuildReport _ )
		{
			var allAssets = new HashSet<string>();

			foreach ( var (source, dataParams) in DataManager.Instance.DataSourcePaths )
			{
				List<string> filePaths = new();
				Utility.GetResourcePaths( dataParams.path, ref filePaths, recursive: dataParams.recursiveLoad );

				filePaths.RemoveAll( x => !dataParams.type.IsAssignableFrom( Resources.Load( x )?.GetType() ) );

				if ( source == DataType.Misc )
					filePaths.RemoveAll( x => allAssets.Contains( x ) );

				var fileInfoJson = JsonHelper.ToJson( filePaths.ToArray(), true );
				var path = DataManager.GetDataJsonPathFull( source );
				if ( !File.Exists( path ) || File.ReadAllText( path ) != fileInfoJson )
					File.WriteAllText( path, fileInfoJson );

				if ( source != DataType.Misc )
					foreach ( var assetPath in filePaths )
						allAssets.Add( assetPath );
			}

			AssetDatabase.Refresh();


			var proc = new System.Diagnostics.Process
			{
				StartInfo = new System.Diagnostics.ProcessStartInfo()
				{
					FileName = "cmd.exe",
					Arguments = "/C cm status --compact --nochanges",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true
				}
			};

			proc.Start();

			while ( !proc.StandardOutput.EndOfStream )
			{
				string line = proc.StandardOutput.ReadLine();
				if ( line.StartsWith( "cs:" ) )
				{
					var version = line[3..line.IndexOf( '@' )];
					if ( int.TryParse( version, out int versionNumber ) )
					{
						var path = DataManager.GetCurrentVersionPath();
						if ( !File.Exists( path ) || File.ReadAllText( path ) != version.ToString() )
							File.WriteAllText( path, version.ToString() );
						break;
					}
					else
					{
						Debug.LogError( $"Failed to parse version number from cm status output: {line}" );
					}
				}
			}
		}
	}
#endif
}
