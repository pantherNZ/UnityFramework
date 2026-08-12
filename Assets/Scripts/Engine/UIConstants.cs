using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.TextCore.Text;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace Schema
{
	[CreateAssetMenu(fileName = "UIConstants", menuName = "NQ/Constants/UIConstants")]
	public class UIConstants : ScriptableObject
	{
		[Serializable]
		public class Helper
		{
			public VisualTreeAsset genericPopupTemplate;
			public VisualTreeAsset yesNoPopupTemplate;
			public VisualTreeAsset closePopupTemplate;
			public VisualTreeAsset toastTemplate;
			public VisualTreeAsset highlightTemplate;
		}


		public Helper helper;

		[Header("UI Input Data")]
		[SerializeField] SerializableDictionary<InputTypeTracker.InputType, Pair<UnityEngine.TextAsset, SpriteAsset>> uiInputBindIconMappingFiles;
		Dictionary<InputTypeTracker.InputType, UI.TextureAtlas> _uiInputBindIconAtlases;
		public Dictionary<InputTypeTracker.InputType, UI.TextureAtlas> uiInputBindIconAtlases
		{
			get
			{
				if (_uiInputBindIconAtlases == null)
					LoadInputIconData();
				return _uiInputBindIconAtlases;
			}
		}

		private void OnValidate()
		{

			LoadInputIconData();
		}

		void LoadInputIconData()
		{
			_uiInputBindIconAtlases = new();
			foreach (var kvp in uiInputBindIconMappingFiles)
			{
				if (kvp.Value.First == null || kvp.Value.Second == null)
					continue;

				try
				{
					var serializer = new XmlSerializer(typeof(UI.TextureAtlas));
					using var reader = new System.IO.StringReader(kvp.Value.First.text);
					var atlas = (UI.TextureAtlas)serializer.Deserialize(reader);
					var file = UnityEngine.Resources.Load<Texture2D>($"UI/Images/Button Glyphs/{atlas.ImagePath.Replace(".png", "")}");
					atlas.width = file.width;
					atlas.height = file.height;
					atlas.spriteAtlas = kvp.Value.Second;
					_uiInputBindIconAtlases[kvp.Key] = atlas;
				}
				catch (Exception ex)
				{
					Debug.LogError($"Failed to parse input bind icon mapping for '{kvp.Key}': {ex.Message}");
				}
			}
		}

	}
}