using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Schema;
using UnityEditor;
using UnityEngine;

namespace UI
{
	[Serializable]
	public enum UIPanelType
	{
		Outpost,
		Inventory,
		IngameMenu,
	}
}

namespace Schema
{
	public static partial class AssetBinaryConverter
	{
		public static void Write<T>( this BinaryWriter writer, T value ) where T : BaseDataSchema
		{
			writer.Write( value?.GetHashCode() ?? 0 );
		}

		public static T ReadDataSchema<T>( this BinaryReader reader ) where T : BaseDataSchema
		{
			var hash = reader.ReadInt32();
			if ( hash == 0 )
				return null;
			return DataManager.Instance.FindAssetByHash( hash ) as T;
		}
	}

	public class AssetJsonConverter : JsonConverter
	{
		public override void WriteJson( JsonWriter writer, object value, JsonSerializer serializer )
		{
			BaseDataSchema asset = ( BaseDataSchema )value;
			if ( asset == null )
			{
				writer?.WriteValue( 0 );
			}
			else
			{
				var hash = asset.GetHashCode();
				writer?.WriteValue( hash );
			}
		}

		public override object ReadJson( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer )
		{
			if ( reader.Value == null )
				return null;

			long hash = ( long )reader.Value;
			if ( hash == 0 )
				return null;

			return DataManager.Instance.FindAssetByHash( ( int )hash );
		}

		public override bool CanConvert( Type objectType )
		{
			return objectType == typeof( BaseDataSchema );
		}
	}
}

public struct DungeonIndex
{
	public DungeonIndex( int x, int y )
	{
		value = new Vector2Int( x, y );
	}
	public DungeonIndex( Vector2Int value )
	{
		this.value = value;
	}
	public Vector2Int value;
	public int x => value.x;
	public int y => value.y;
}

public struct ClusterIndex
{
	public ClusterIndex( int x, int y )
	{
		value = new Vector2Int( x, y );
	}
	public ClusterIndex( Vector2Int value )
	{
		this.value = value;
	}
	public Vector2Int value;
	public int x => value.x;
	public int y => value.y;
}

public static partial class CommonTypes
{
	public static void Write( this BinaryWriter writer, DungeonIndex value )
	{
		writer.Write( value.value );
	}

	public static DungeonIndex ReadDungeonIndex( this BinaryReader reader )
		=> new DungeonIndex( reader.ReadVector2Int() );

	public static void Write( this BinaryWriter writer, ClusterIndex value )
	{
		writer.Write( value.value );
	}

	public static ClusterIndex ReadClusterIndex( this BinaryReader reader )
		=> new ClusterIndex( reader.ReadVector2Int() );
}