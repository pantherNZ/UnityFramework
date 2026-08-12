using System;
using System.Collections.Generic;
using DG.Tweening;
using Runtime.Events;
using Runtime.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Runtime.Audio
{
	[Serializable]
	public class MusicEntry
	{
		public AudioClip lowIntensityClip;
		public AudioClip highIntensityClip;
	}

	[DisallowMultipleComponent]
	public class MusicManager : MonoEventReceiver
	{
		// Singleton
		static MusicManager musicManager;
		public static MusicManager Instance => musicManager;
		[SerializeField] float fadeInTimeSec = 5.0f;
		[SerializeField] Interval timeBetweenMusicSec;
		[SerializeField] List<MusicEntry> gameplayTracks;
		[SerializeField] List<AudioClip> mainMenuTracks;
		[SerializeField] AudioSource musicTrack1;
		[SerializeField] AudioSource musicTrack2;
		[SerializeField] float combatEnterCrossfadeDurationSec = 1.0f;
		[SerializeField] float combatExitCrossfadeDurationSec = 10.0f;
		[SerializeField] float noCombatFadeOutDelaySec = 60.0f;
		[SerializeField] float noCombatFadeOutDurationSec = 15.0f;
		[SerializeField] float volumeScale = 1.0f;
		[SerializeField] bool musicEnabled;
		public bool MusicEnabled
		{
			get => musicEnabled; set
			{
				if ( musicEnabled != value )
				{
					musicEnabled = value;
					RestartMusic();
				}
			}
		}

		float volumeScaleFromSettings = 1.0f;
		float volumeScaleFromLorePlaying = 1.0f;

		Utility.FunctionTimer timer;
		MusicEntry lastPlayedEntry;
		AudioClip lastPlayedMainMenuClip;
		MusicEntry currentMusicEntry;
		bool localPlayerInCombat;
		AudioClip currentTrack;
		float currentTrackStartTime;
		float currentTrackLengthSec;
		int lastReportedTrackSecond = -1;
		Sequence resumeMusicFade;
		Sequence transitionSequence;
		Sequence noCombatFadeOutSequence;
		Utility.UnityRandom rng;
		AudioSource activeMusicSource;
		Utility.FunctionTimer noCombatFadeOutTimer;
		bool noCombatFadeOutApplied;
		float musicTrack1Mix = 1.0f;
		float musicTrack2Mix;

		[Header( "Debug (Runtime)" )]
		[SerializeField] string currentTrackName = "(none)";
		[SerializeField] List<string> lastSelectorTrackNames = new();
		[SerializeField, Range( 0.0f, 1.0f )] float currentTrackProgress01;
		[SerializeField] string currentTrackProgressText = "[0:00/0:00]";
		[SerializeField] bool debugCombatIntensityHigh;
		[SerializeField] string debugCurrentSongPair = "(none)";
		[SerializeField] string debugCurrentIntensity = "Low";
		[SerializeField] string debugTargetIntensity = "Low";
		[SerializeField] string debugTrackPool = "Gameplay";
		[SerializeField] string debugSourceMixes = "Track1:0.00 Track2:0.00";
		[SerializeField] string debugSyncedTransitionTime = "0:00";

// warning 0414 is "variable is assigned but its value is never used"
// these variables are for debug display in inspector only so this is fine
#pragma warning disable 0414
		[SerializeField] string debugActiveMusicSource = "(none)";
		[SerializeField] string debugLastTransitionReason = "(none)";
		[SerializeField] string transitionDebugState = "Idle";
#pragma warning restore 0414

		private void Awake()
		{
			if ( musicManager != null && musicManager != this )
			{
				Destroy( gameObject );
				return;
			}

			musicManager = this;
			DontDestroyOnLoad( this );
			rng = new Utility.UnityRandom();
			activeMusicSource = musicTrack1;
		}

		private void OnDisable()
		{
			SceneManager.activeSceneChanged -= ActiveSceneChanged;
			StopMusic();
		}

		private void OnEnable()
		{
			SceneManager.activeSceneChanged += ActiveSceneChanged;
			RestartMusic();
		}

		private void Start()
		{
			Events.SettingsModified.Subscribe( this, UpdateSettings );
			Events.LorePlayed.Subscribe( this, _ => DialogueOrLorePlayed() );
			Events.DialoguePlayed.Subscribe( this, _ => DialogueOrLorePlayed() );
			Events.DialogueOrLoreFinished.Subscribe( this, DialogueOrLoreFinished );
			Events.SceneChanging.Subscribe( this, SceneChanging );
			Events.PlayerCombatStatusChanged.Subscribe( this, PlayerCombatStatusChanged );

			RestartMusic();
		}

		void Update()
		{
			UpdateTrackProgressDebug();
			UpdateRuntimeDebugStrings();
		}

		void UpdateSettings( Events.SettingsModified _e )
		{
			volumeScaleFromSettings = Settings.MusicVolume;
			UpdateAudioVolume();
		}

		void DialogueOrLorePlayed()
		{
			resumeMusicFade?.Kill();
			volumeScaleFromLorePlaying = 0.4f;
			UpdateAudioVolume();
		}

		void DialogueOrLoreFinished( Events.DialogueOrLoreFinished _e )
		{
			resumeMusicFade?.Kill();
			resumeMusicFade = DOTween.Sequence().Append( DOTween.To( () => volumeScaleFromLorePlaying, x =>
			{
				volumeScaleFromLorePlaying = x;
				UpdateAudioVolume();
			}, 1.0f, 1.0f ) );
		}

		void SceneChanging( Events.SceneChanging sceneChanging )
		{
			// SceneChanging can fire before GameSceneManager has a reliable active scene context.
			// Stop immediately, then let ActiveSceneChanged restart against the final loaded scene.
			debugLastTransitionReason = "SceneChanging";
			StopMusic();
		}

		void ActiveSceneChanged( Scene oldScene, Scene newScene )
		{
			if ( !isActiveAndEnabled )
				return;

			debugLastTransitionReason = $"ActiveSceneChanged -> {newScene.name}";
			RestartMusic();
		}

		public void StopMusic()
		{
			timer?.Stop();
			CancelNoCombatFadeOut( false );
			transitionSequence?.Kill();
			musicTrack1?.Stop();
			musicTrack2?.Stop();
			musicTrack1Mix = 1.0f;
			musicTrack2Mix = 0.0f;
			transitionDebugState = "Idle";
			currentMusicEntry = null;
			debugLastTransitionReason = "StopMusic";
			ClearCurrentTrackDebugInfo();
		}

		public void RestartMusic()
		{
			StopMusic();
			timer = Utility.FunctionTimer.CreateTimer( fadeInTimeSec, StartTrack );
		}

		private void StartTrack()
		{
			if ( !musicEnabled )
				return;

			lastSelectorTrackNames.Clear();
			localPlayerInCombat = Network.ClientSession.Instance?.LocalPlayerController?.IsInCombat ?? false;
			debugCombatIntensityHigh = localPlayerInCombat;
			if ( localPlayerInCombat )
				CancelNoCombatFadeOut( false );
			else
				ScheduleNoCombatFadeOut();

			if ( IsMainMenuScene() )
			{
				StartMainMenuTrack();
				return;
			}

			var candidates = new List<MusicEntry>();

			foreach ( var track in gameplayTracks )
			{
				if ( !HasAnyClip( track ) )
					continue;

				lastSelectorTrackNames.Add( track.lowIntensityClip != null ? track.lowIntensityClip.name : track.highIntensityClip.name );
				candidates.Add( track );
			}

			if ( candidates.Count > 1 && lastPlayedEntry != null )
				candidates.RemoveAll( track => track == lastPlayedEntry );

			var selectedEntry = candidates.Count > 0 ? candidates[rng.Range( 0, candidates.Count )] : null;

			if ( selectedEntry == null )
			{
				Debug.LogWarning( "Failed to select a music track" );
				timer = Utility.FunctionTimer.CreateTimer( timeBetweenMusicSec.Random( rng ), StartTrack );
				return;
			}

			currentMusicEntry = selectedEntry;
			lastPlayedEntry = selectedEntry;
			debugLastTransitionReason = "StartTrack rotation";
			PlayMusicTrackNow( GetClipForIntensity( selectedEntry, localPlayerInCombat ) );
		}

		void StartMainMenuTrack()
		{
			CancelNoCombatFadeOut( true );
			var candidates = new List<AudioClip>();

			foreach ( var clip in mainMenuTracks )
			{
				if ( clip == null )
					continue;

				lastSelectorTrackNames.Add( clip.name );
				candidates.Add( clip );
			}

			if ( candidates.Count > 1 && lastPlayedMainMenuClip != null )
				candidates.RemoveAll( clip => clip == lastPlayedMainMenuClip );

			var selectedClip = candidates.Count > 0 ? candidates[rng.Range( 0, candidates.Count )] : null;
			if ( selectedClip == null )
			{
				Debug.LogWarning( "Failed to select a main menu music track" );
				timer = Utility.FunctionTimer.CreateTimer( timeBetweenMusicSec.Random( rng ), StartTrack );
				return;
			}

			currentMusicEntry = null;
			lastPlayedMainMenuClip = selectedClip;
			debugLastTransitionReason = "StartTrack (MainMenu pool)";
			PlayMusicTrackNow( selectedClip );
		}

		void PlayerCombatStatusChanged( Events.PlayerCombatStatusChanged e )
		{
			var localPlayer = Network.ClientSession.Instance?.LocalPlayerController;
			if ( localPlayer == null || e.player != localPlayer )
				return;

			if ( localPlayerInCombat == e.isInCombat )
				return;

			localPlayerInCombat = e.isInCombat;
			debugCombatIntensityHigh = localPlayerInCombat;

			if ( !musicEnabled || currentMusicEntry == null )
				return;

			var targetClip = GetClipForIntensity( currentMusicEntry, localPlayerInCombat );
			if ( targetClip == null || targetClip == currentTrack )
				return;

			if ( localPlayerInCombat )
			{
				CancelNoCombatFadeOut( false );
				debugLastTransitionReason = "Combat entered (Low->High)";
				TransitionToTrack( targetClip, combatEnterCrossfadeDurationSec );
			}
			else
			{
				debugLastTransitionReason = "Combat exited (High->Low)";
				TransitionToTrack( targetClip, combatExitCrossfadeDurationSec );
				ScheduleNoCombatFadeOut();
			}
		}

		public void TransitionToTrack( AudioClip nextTrack, float crossfadeDurationSec )
		{
			if ( !musicEnabled || nextTrack == null )
				return;

			if ( localPlayerInCombat )
				CancelNoCombatFadeOut( false );

			if ( musicTrack1 == null || musicTrack2 == null )
			{
				Debug.LogWarning( "MusicManager needs both music audio sources assigned for transitions." );
				debugLastTransitionReason = "Fallback: missing source for crossfade";
				PlayMusicTrackNow( nextTrack );
				return;
			}

			timer?.Stop();
			transitionSequence?.Kill();

			var sourceToFadeOut = ResolveCurrentMusicSource();
			var sourceToFadeIn = sourceToFadeOut == musicTrack1 ? musicTrack2 : musicTrack1;
			var syncedPlaybackTimeSec = GetCurrentPlaybackSeconds( sourceToFadeOut );
			var crossfadeDuration = Mathf.Max( 0.01f, crossfadeDurationSec );

			transitionDebugState = "Crossfading";
			debugSyncedTransitionTime = FormatTimestamp( syncedPlaybackTimeSec );

			SetSourceMix( sourceToFadeIn, 0.0f );
			StartTrackOnSource( sourceToFadeIn, nextTrack, syncedPlaybackTimeSec );
			activeMusicSource = sourceToFadeIn;

			transitionSequence = DOTween.Sequence();

			if ( sourceToFadeOut != null && sourceToFadeOut != sourceToFadeIn && sourceToFadeOut.isPlaying )
			{
				transitionSequence.Insert( 0.0f, DOTween.To( () => GetSourceMix( sourceToFadeOut ), x => SetSourceMix( sourceToFadeOut, x ), 0.0f, crossfadeDuration ) );
			}

			transitionSequence.Insert( 0.0f, DOTween.To( () => GetSourceMix( sourceToFadeIn ), x => SetSourceMix( sourceToFadeIn, x ), 1.0f, crossfadeDuration ) );

			transitionSequence.AppendInterval( crossfadeDuration );
			transitionSequence.OnComplete( () =>
			{
				sourceToFadeOut?.Stop();
				SetSourceMix( sourceToFadeOut, 0.0f );
				SetSourceMix( sourceToFadeIn, 1.0f );
				activeMusicSource = sourceToFadeIn;
				transitionDebugState = "Idle";
				UpdateAudioVolume();
			} );
		}

		void PlayMusicTrackNow( AudioClip clip )
		{
			if ( clip == null )
				return;

			timer?.Stop();
			transitionSequence?.Kill();

			var source = ResolveCurrentMusicSource() ?? musicTrack1 ?? musicTrack2;
			var other = source == musicTrack1 ? musicTrack2 : musicTrack1;

			other?.Stop();
			SetSourceMix( other, 0.0f );
			var shouldStartMutedFromNoCombat = !localPlayerInCombat && noCombatFadeOutApplied;
			SetSourceMix( source, shouldStartMutedFromNoCombat ? 0.0f : 1.0f );
			StartTrackOnSource( source, clip, 0.0f );

			activeMusicSource = source;
			transitionDebugState = "Idle";
		}

		void ScheduleNoCombatFadeOut()
		{
			if ( localPlayerInCombat || !musicEnabled || currentMusicEntry == null || IsMainMenuScene() || noCombatFadeOutApplied )
				return;

			noCombatFadeOutTimer?.Stop();
			if ( noCombatFadeOutSequence != null && noCombatFadeOutSequence.IsActive() && noCombatFadeOutSequence.IsPlaying() )
				return;

			// Use scaled game time so pause (timeScale=0) also pauses this countdown.
			noCombatFadeOutTimer = Utility.FunctionTimer.CreateTimer( noCombatFadeOutDelaySec, BeginNoCombatFadeOut, useUnscaledDeltaTime: false );
		}

		void BeginNoCombatFadeOut()
		{
			noCombatFadeOutTimer = null;
			if ( localPlayerInCombat || !musicEnabled || currentMusicEntry == null || IsMainMenuScene() )
				return;

			var sourceToFadeOut = ResolveCurrentMusicSource();
			if ( sourceToFadeOut == null )
				return;

			noCombatFadeOutSequence?.Kill();
			var fadeDuration = Mathf.Max( 0.01f, noCombatFadeOutDurationSec );
			noCombatFadeOutSequence = DOTween.Sequence();
			noCombatFadeOutSequence.SetUpdate( UpdateType.Normal, false );
			noCombatFadeOutSequence.Append( DOTween.To( () => GetSourceMix( sourceToFadeOut ), x => SetSourceMix( sourceToFadeOut, x ), 0.0f, fadeDuration ) );
			noCombatFadeOutSequence.OnComplete( () =>
			{
				noCombatFadeOutApplied = true;
				debugLastTransitionReason = "No combat timeout (fade out)";
			} );
		}

		void CancelNoCombatFadeOut( bool restoreVolume )
		{
			noCombatFadeOutTimer?.Stop();
			noCombatFadeOutTimer = null;

			noCombatFadeOutSequence?.Kill();
			noCombatFadeOutSequence = null;

			if ( !noCombatFadeOutApplied )
				return;

			noCombatFadeOutApplied = false;
			if ( restoreVolume )
			{
				SetSourceMix( ResolveCurrentMusicSource(), 1.0f );
				debugLastTransitionReason = "No combat fade cancelled (restore)";
			}
		}

		void StartTrackOnSource( AudioSource source, AudioClip clip, float startAtTimeSec )
		{
			if ( source == null || clip == null )
				return;

			var clampedStartSec = Mathf.Clamp( startAtTimeSec, 0.0f, Mathf.Max( 0.0f, clip.length - 0.01f ) );
			source.clip = clip;
			source.loop = false;
			SetPlaybackTime( source, clip, clampedStartSec );
			UpdateDebugForTrackStart( clip, clampedStartSec );
			UpdateAudioVolume();
			source.Play();
			var remainingSec = Mathf.Max( 0.0f, clip.length - clampedStartSec );
			timer = Utility.FunctionTimer.CreateTimer( remainingSec + timeBetweenMusicSec.Random( rng ), StartTrack );
			UpdateTrackProgressDebug( true );
		}

		void UpdateDebugForTrackStart( AudioClip clip, float startAtTimeSec )
		{
			currentTrack = clip;
			currentTrackName = clip.name;
			currentTrackStartTime = Time.time - Mathf.Max( 0.0f, startAtTimeSec );
			currentTrackLengthSec = clip.length;
			lastReportedTrackSecond = -1;
		}

		static void SetPlaybackTime( AudioSource source, AudioClip clip, float timeSec )
		{
			if ( source == null || clip == null )
				return;

			var safeTimeSec = Mathf.Clamp( timeSec, 0.0f, Mathf.Max( 0.0f, clip.length - 0.01f ) );
			var targetSamples = Mathf.RoundToInt( safeTimeSec * clip.frequency );
			source.timeSamples = Mathf.Clamp( targetSamples, 0, Mathf.Max( 0, clip.samples - 1 ) );
		}

		float GetCurrentPlaybackSeconds( AudioSource source )
		{
			if ( source?.clip == null )
				return Mathf.Max( 0.0f, Time.time - currentTrackStartTime );

			if ( source.clip.frequency <= 0 )
				return Mathf.Max( 0.0f, source.time );

			return Mathf.Clamp( source.timeSamples / ( float )source.clip.frequency, 0.0f, source.clip.length );
		}

		static bool HasAnyClip( MusicEntry entry )
		{
			return entry != null && ( entry.lowIntensityClip != null || entry.highIntensityClip != null );
		}

		static AudioClip GetClipForIntensity( MusicEntry entry, bool highIntensity )
		{
			if ( entry == null )
				return null;

			if ( highIntensity )
				return entry.highIntensityClip != null ? entry.highIntensityClip : entry.lowIntensityClip;

			return entry.lowIntensityClip != null ? entry.lowIntensityClip : entry.highIntensityClip;
		}

		bool IsMainMenuScene()
		{
			return GameSceneManager.Instance != null &&
				GameSceneManager.Instance.CurrentSceneType == GameSceneManager.SceneType.MainMenu;
		}

		AudioSource ResolveCurrentMusicSource()
		{
			if ( activeMusicSource != null && activeMusicSource.isPlaying )
				return activeMusicSource;

			if ( musicTrack1 != null && musicTrack1.isPlaying )
				return musicTrack1;

			if ( musicTrack2 != null && musicTrack2.isPlaying )
				return musicTrack2;

			return activeMusicSource ?? musicTrack1 ?? musicTrack2;
		}

		float GetSourceMix( AudioSource source )
		{
			if ( source == musicTrack1 )
				return musicTrack1Mix;
			if ( source == musicTrack2 )
				return musicTrack2Mix;
			return 0.0f;
		}

		void SetSourceMix( AudioSource source, float value )
		{
			var clamped = Mathf.Clamp01( value );
			if ( source == musicTrack1 )
				musicTrack1Mix = clamped;
			else if ( source == musicTrack2 )
				musicTrack2Mix = clamped;

			UpdateAudioVolume();
		}

		void ClearCurrentTrackDebugInfo()
		{
			currentTrack = null;
			currentTrackName = "(none)";
			currentTrackProgress01 = 0.0f;
			currentTrackProgressText = "[0:00/0:00]";
			lastReportedTrackSecond = -1;
			currentTrackLengthSec = 0.0f;
			UpdateRuntimeDebugStrings();
		}

		void UpdateTrackProgressDebug( bool forceTextUpdate = false )
		{
			if ( currentTrack == null )
				return;

			var elapsed = Mathf.Max( 0.0f, Time.time - currentTrackStartTime );
			var safeLength = Mathf.Max( 0.01f, currentTrackLengthSec );
			currentTrackProgress01 = Mathf.Clamp01( elapsed / safeLength );

			var elapsedWholeSeconds = Mathf.FloorToInt( elapsed );
			var playingSource = ResolveCurrentMusicSource();
			if ( forceTextUpdate || elapsedWholeSeconds != lastReportedTrackSecond || playingSource == null || !playingSource.isPlaying )
			{
				lastReportedTrackSecond = elapsedWholeSeconds;
				currentTrackProgressText = $"[{FormatTimestamp( elapsed )}/{FormatTimestamp( currentTrackLengthSec )}]";
			}
		}

		static string FormatTimestamp( float seconds )
		{
			var totalSeconds = Mathf.Max( 0, Mathf.FloorToInt( seconds ) );
			var minutes = totalSeconds / 60;
			var remSeconds = totalSeconds % 60;
			return $"{minutes}:{remSeconds:00}";
		}

		void UpdateRuntimeDebugStrings()
		{
			var lowName = currentMusicEntry?.lowIntensityClip != null ? currentMusicEntry.lowIntensityClip.name : "(none)";
			var highName = currentMusicEntry?.highIntensityClip != null ? currentMusicEntry.highIntensityClip.name : "(none)";
			debugCurrentSongPair = $"Low:{lowName} | High:{highName}";

			debugCurrentIntensity = IsCurrentTrackHighIntensity() ? "High" : "Low";
			debugTargetIntensity = localPlayerInCombat ? "High" : "Low";
			debugTrackPool = IsMainMenuScene() ? "MainMenu" : "Gameplay";

			if ( activeMusicSource == musicTrack1 )
				debugActiveMusicSource = "MusicTrack1";
			else if ( activeMusicSource == musicTrack2 )
				debugActiveMusicSource = "MusicTrack2";
			else
				debugActiveMusicSource = "(none)";

			debugSourceMixes = $"Track1:{musicTrack1Mix:0.00} Track2:{musicTrack2Mix:0.00}";
		}

		bool IsCurrentTrackHighIntensity()
		{
			if ( currentMusicEntry == null || currentTrack == null )
				return false;

			if ( currentMusicEntry.highIntensityClip == currentTrack )
				return true;
			if ( currentMusicEntry.lowIntensityClip == currentTrack )
				return false;
			return localPlayerInCombat;
		}

		void UpdateAudioVolume()
		{
			var finalVolume = volumeScale * volumeScaleFromSettings * volumeScaleFromLorePlaying * Settings.MasterVolume;
			if ( musicTrack1 != null )
				musicTrack1.volume = finalVolume * musicTrack1Mix;
			if ( musicTrack2 != null )
				musicTrack2.volume = finalVolume * musicTrack2Mix;
		}
	}
}
