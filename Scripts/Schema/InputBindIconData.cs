
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Runtime.Game;
using Schema;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.Text;

namespace UI
{
	[XmlRoot( ElementName = "SubTexture" )]
	public class SubTexture
	{
		[XmlAttribute( AttributeName = "name" )]
		public string Name { get; set; }

		[XmlAttribute( AttributeName = "x" )]
		public int X { get; set; }

		[XmlAttribute( AttributeName = "y" )]
		public int Y { get; set; }

		[XmlAttribute( AttributeName = "width" )]
		public int Width { get; set; }

		[XmlAttribute( AttributeName = "height" )]
		public int Height { get; set; }
	}

	[XmlRoot( ElementName = "TextureAtlas" )]
	public class TextureAtlas
	{
		[XmlElement( ElementName = "SubTexture" )]
		public List<SubTexture> SubTextures { get; set; }

		[XmlAttribute( AttributeName = "imagePath" )]
		public string ImagePath { get; set; }

		[XmlIgnore] public int width;
		[XmlIgnore] public int height;
		[XmlIgnore] public SpriteAsset spriteAtlas;
	}

	public static class InputBindIconData
	{
		public static string InjectRichTextInputIconString( string text, out bool containsInputAction )
		{
			bool foundAction = false;
			var result = Regex.Replace( text, @"(.*?)\{(.*?)\}", match =>
				{
					var prefix = match.Groups[1].Value;
					var key = match.Groups[2].Value;
					var inputAction = InputSystem.actions.FindAction( key );

					if ( inputAction == null )
					{
						UnityEngine.Debug.LogError( $"Failed to find input action for key '{key}' while injecting input icons into text: {text}" );
						return key;
					}
					foundAction = true;
					return prefix + GetRichTextInputIconString( inputAction );
				} );
			containsInputAction = foundAction;
			return result;
		}

		/// <summary>Returns the sprite for the first matching gamepad binding of <paramref name="action"/>.</summary>
		public static UnityEngine.Sprite GetSprite( this InputAction action )
		{
			var atlases = GlobalConstantsHandler.UIConstants?.uiInputBindIconAtlases;
			if ( atlases == null || !atlases.TryGetValue( InputTypeTracker.InputType.Gamepad, out var atlas ) )
				return null;

			for ( int i = 0; i < action.bindings.Count; i++ )
			{
				var path = action.bindings[i].path;
				if ( string.IsNullOrEmpty( path ) || !action.bindings[i].groups.Contains( "Gamepad" ) ) continue;

				var iconName = "xbox_" + path[( path.LastIndexOf( '/' ) + 1 )..].ToLower();
				return GetSprite( iconName );
			}
			return null;
		}

		/// <summary>Returns the sprite for the sub-texture with the given <paramref name="iconName"/> in the gamepad atlas.</summary>
		public static UnityEngine.Sprite GetSprite( string iconName )
		{
			var atlases = GlobalConstantsHandler.UIConstants?.uiInputBindIconAtlases;
			if ( atlases == null || !atlases.TryGetValue( InputTypeTracker.InputType.Gamepad, out var atlas ) )
				return null;

			var subTexture = atlas.SubTextures?.Find( s => s.Name == iconName );
			if ( subTexture == null ) return null;

			int index = subTexture.X / subTexture.Width
				+ ( atlas.height - subTexture.Y - subTexture.Width ) / subTexture.Width
				* ( atlas.width / subTexture.Width );
			return GetSprite( index );
		}

		/// <summary>Returns the sprite at the given sheet <paramref name="index"/> in the gamepad atlas.</summary>
		public static UnityEngine.Sprite GetSprite( int index )
		{
			var atlases = GlobalConstantsHandler.UIConstants?.uiInputBindIconAtlases;
			if ( atlases == null || !atlases.TryGetValue( InputTypeTracker.InputType.Gamepad, out var atlas ) )
				return null;

			var sprites = UnityEngine.Resources.LoadAll<UnityEngine.Sprite>(
				$"UI/Images/Button Glyphs/{atlas.ImagePath.Replace( ".png", "" )}" );
			return sprites != null && index < sprites.Length ? sprites[index] : null;
		}

		public static string GetRichTextInputIconString( this InputAction action )
		{
			string ActionName()
			{
				var currentActionSetName = InputTypeTracker.Instance.currentType;
				return currentActionSetName switch
				{
					InputTypeTracker.InputType.Gamepad => "Gamepad",
					_ => "Keyboard&Mouse",
				};
			}

			string IconPrefix()
			{
				var currentActionSetName = InputTypeTracker.Instance.currentType;
				return currentActionSetName switch
				{
					InputTypeTracker.InputType.Gamepad => "xbox",
					_ => "keyboard",
				};
			}

			var inputBindings = GlobalConstantsHandler.UIConstants.uiInputBindIconAtlases;
			var currentActionSetName = InputTypeTracker.Instance.currentType;
			var foundActionSet = inputBindings.TryGetValue( currentActionSetName, out var binding ) ? binding : null;
			UnityEngine.Debug.Assert( foundActionSet != null, $"Failed to find action set for '{currentActionSetName}'" );

			if ( foundActionSet == null )
				return string.Empty;

			StringBuilder result = new();
			HashSet<string> bindings = new();
			int count = 0;
			for ( int i = 0; i < action.bindings.Count; ++i )
			{
				var bindingName = action.bindings[i].path;
				if ( !bindingName.IsEmpty() && action.bindings[i].groups.Contains( ActionName() ) )
				{
					bindingName = IconPrefix() + "_" + bindingName[( bindingName.LastIndexOf( '/' ) + 1 )..].ToLower();
					bindings.Add( bindingName );
					var foundBinding = foundActionSet.SubTextures.Find( b => b.Name == bindingName );
					UnityEngine.Debug.Assert( foundBinding != null, $"Failed to find input texture icon for '{bindingName}'" );
					if ( foundBinding != null )
					{
						var index = foundBinding.X / foundBinding.Width + ( foundActionSet.height - foundBinding.Y - foundBinding.Width ) / foundBinding.Width * ( foundActionSet.width / foundBinding.Width );
						result.Append( $"<sprite=\"{foundActionSet.spriteAtlas.name}\" index={index}>" );
						if ( ++count >= 4 )
							break;
					}
				}
			}

			return result.ToString();
		}
	}
}