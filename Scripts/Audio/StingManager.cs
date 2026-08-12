using System;
using System.Collections.Generic;
using Runtime.Game;
using UnityEngine;

namespace Runtime.Audio
{
	public enum StingType
	{
		None = 0,
		BiomeDiscovered = 1,
		BrokenOutpostDiscovered = 2,
		LoreObjectDiscovered = 3,
	}

	[Serializable]
	public class StingEntry
	{
		public StingType stingType = StingType.None;
		public AudioClip clip;
	}

	[DisallowMultipleComponent]
	public class StingManager : MonoBehaviour
	{
		static StingManager _instance;
		public static StingManager Instance => _instance;

		[SerializeField] AudioSource stingSource;
		[SerializeField] List<StingEntry> stings = new();
		[SerializeField] float volumeScale = 1.0f;
		[SerializeField] bool stingsEnabled = true;

		readonly Dictionary<StingType, StingEntry> stingByType = new();

		void Awake()
		{
			if ( _instance != null && _instance != this )
			{
				Destroy( gameObject );
				return;
			}

			_instance = this;
			DontDestroyOnLoad( gameObject );

			RebuildLookupTables();
		}

		void OnValidate()
		{
			RebuildLookupTables();
		}

		public void SetEnabled( bool enabled )
		{
			stingsEnabled = enabled;
		}

		public static bool Play( StingType stingType, float volumeMultiplier = 1.0f, bool playDuringCombat = false )
		{
			if ( Instance == null )
			{
				Debug.LogWarning( $"Cannot play sting {stingType}: no StingManager instance." );
				return false;
			}

			return Instance.TryPlay( stingType, volumeMultiplier, playDuringCombat );
		}

		public bool TryPlay( StingType stingType, float volumeMultiplier = 1.0f, bool playDuringCombat = false )
		{
			if ( stingType == StingType.None )
				return false;

			if ( !stingByType.TryGetValue( stingType, out var entry ) || entry == null )
			{
				Debug.LogWarning( $"No sting configured for type {stingType}." );
				return false;
			}

			return TryPlayEntry( entry, volumeMultiplier, playDuringCombat );
		}

		bool TryPlayEntry( StingEntry entry, float volumeMultiplier, bool playDuringCombat )
		{
			if ( !stingsEnabled )
				return false;

			if ( !playDuringCombat && IsLocalPlayerInCombat() )
				return false;

			if ( stingSource == null )
			{
				Debug.LogWarning( "StingManager requires an AudioSource." );
				return false;
			}

			if ( entry.clip == null )
			{
				Debug.LogWarning( $"World sting '{entry.stingType}' is missing an AudioClip." );
				return false;
			}

			var finalVolume = Mathf.Max( 0.0f, volumeScale * Settings.MasterVolume * Settings.SfxVolume * volumeMultiplier );
			stingSource.PlayOneShot( entry.clip, finalVolume );
			return true;
		}

		static bool IsLocalPlayerInCombat()
		{
			var localPlayerObject = GlobalConstantsHandler.RuntimeConstants?.localPlayer;
			if ( localPlayerObject == null )
				return false;

			var localPlayerController = localPlayerObject.GetComponent<PlayerController>();
			return localPlayerController != null && localPlayerController.IsInCombat;
		}

		void RebuildLookupTables()
		{
			stingByType.Clear();

			for ( int i = 0; i < stings.Count; i++ )
			{
				var entry = stings[i];
				if ( entry == null )
					continue;

				if ( entry.stingType != StingType.None )
				{
					if ( stingByType.ContainsKey( entry.stingType ) )
					{
						Debug.LogWarning( $"Duplicate sting type '{entry.stingType}' at index {i}. First one will be used." );
					}
					else
					{
						stingByType.Add( entry.stingType, entry );
					}
				}
			}
		}
	}
}
