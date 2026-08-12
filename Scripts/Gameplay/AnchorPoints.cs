using System.Collections.Generic;
using UnityEngine;
using NeatoTags;

namespace Runtime.Game
{
	[DisallowMultipleComponent]
	public class AnchorPoints : MonoBehaviour
	{
		[SerializeField, ReadOnly] SerializableDictionary<NeatoTag, List<Transform>> anchors = new();

		void Start()
		{
			OnValidate();
		}

		void OnValidate()
		{
			anchors.Clear();
			var taggers = GetComponentsInChildren<Tagger>( true );
			foreach ( var tagger in taggers )
			{
				foreach ( var tag in tagger.GetTags )
				{
					if ( anchors.TryGetValue( tag, out var existing ) )
					{
						existing.Add( tagger.transform );
						continue;
					}

					anchors[tag] = new List<Transform> { tagger.transform };
				}
			}
		}

		public Transform GetFirst( NeatoTag tag )
		{
			if ( anchors.TryGetValue( tag, out var result ) )
				return result[0];
			return null;
		}

		public List<Transform> GetAll( NeatoTag tag )
		{
			if ( anchors.TryGetValue( tag, out var result ) )
				return result;
			return null;
		}
	}
}

