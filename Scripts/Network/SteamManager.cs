// The SteamManager is designed to work with Steamworks.NET
// This file is released into the public domain.
// Where that dedication is not recognized you are granted a perpetual,
// irrevocable license to copy and modify this file as you see fit.
//
// Version: 1.0.12

#if !( UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX )
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using System.Collections;
using Steamworks;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using System.Linq;
using Schema;
#endif


//
// The SteamManager provides a base implementation of Steamworks.NET on which you can build upon.
// It handles the basics of starting up and shutting down the SteamAPI for use.
//
[DisallowMultipleComponent]
public class SteamManager : MonoEventReceiver
{

	protected static SteamManager s_instance;
	public static SteamManager Instance => s_instance;
	const string GameStatsCacheKeyPrefix = "SteamManager.GameStats.";
	static readonly TimeSpan GameStatsCacheLifetime = TimeSpan.FromMinutes( 5 );
	Dictionary<Schema.GameStatType, (int value, bool dirty, DateTime timestamp, bool fromSteam)> cachedGameStats = new();

#if !DISABLESTEAMWORKS
	protected static bool s_EverInitialized = false;
	protected bool m_bInitialized = false;
	public static bool Initialized
	{
		get
		{
			return Instance != null && Instance.m_bInitialized;
		}
	}

	protected SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

	Callback<GetTicketForWebApiResponse_t> m_AuthTicketForWebApiResponseCallback;
	string m_SessionTicket;

	Dictionary<string, (int score, SteamLeaderboard_t? handle)> leaderboardHandles = new();
	CallResult<LeaderboardFindResult_t> leaderboardFindResult;
	CallResult<LeaderboardScoreUploaded_t> leaderboardUploadResult;
	Callback<UserStatsReceived_t> userStatsReceivedResult;
	HashSet<AchievementType> cachedAchievements = new();
	bool statsReady;


	[AOT.MonoPInvokeCallback( typeof( SteamAPIWarningMessageHook_t ) )]
	protected static void SteamAPIDebugTextHook( int nSeverity, System.Text.StringBuilder pchDebugText )
	{
		Debug.LogWarning( pchDebugText );
	}

#if UNITY_2019_3_OR_NEWER
	// In case of disabled Domain Reload, reset static members before entering Play Mode.
	[RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.SubsystemRegistration )]
	private static void InitOnPlayMode()
	{
		s_EverInitialized = false;
		s_instance = null;
	}
#endif

	protected virtual void Awake()
	{
		LoadPersistedGameStats();

		// Only one instance of SteamManager at a time!
		if ( s_instance != null )
		{
			Destroy( gameObject );
			return;
		}
		s_instance = this;

		if ( s_EverInitialized )
		{
			// This is almost always an error.
			// The most common case where this happens is when SteamManager gets destroyed because of Application.Quit(),
			// and then some Steamworks code in some other OnDestroy gets called afterwards, creating a new SteamManager.
			// You should never call Steamworks functions in OnDestroy, always prefer OnDisable if possible.
			throw new System.Exception( "Tried to Initialize the SteamAPI twice in one session!" );
		}

		// We want our SteamManager Instance to persist across scenes.
		DontDestroyOnLoad( gameObject );

		if ( !Packsize.Test() )
		{
			Debug.LogError( "[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", this );
		}

		if ( !DllCheck.Test() )
		{
			Debug.LogError( "[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", this );
		}

		try
		{
			// If Steam is not running or the game wasn't started through Steam, SteamAPI_RestartAppIfNecessary starts the
			// Steam client and also launches this game again if the User owns it. This can act as a rudimentary form of DRM.

			if ( SteamAPI.RestartAppIfNecessary( ( AppId_t )4745240 ) )
			{
#if UNITY_STANDALONE
				Application.Quit();
#endif
				return;
			}
		}
		catch ( System.DllNotFoundException e )
		{ // We catch this exception here, as it will be the first occurrence of it.
			Debug.LogError( "[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the README for more details.\n" + e, this );

#if UNITY_STANDALONE
			Application.Quit();
#endif
			return;
		}

		// Initializes the Steamworks API.
		// If this returns false then this indicates one of the following conditions:
		// [*] The Steam client isn't running. A running Steam client is required to provide implementations of the various Steamworks interfaces.
		// [*] The Steam client couldn't determine the App ID of game. If you're running your application from the executable or debugger directly then you must have a [code-inline]steam_appid.txt[/code-inline] in your game directory next to the executable, with your app ID in it and nothing else. Steam will look for this file in the current working directory. If you are running your executable from a different directory you may need to relocate the [code-inline]steam_appid.txt[/code-inline] file.
		// [*] Your application is not running under the same OS user context as the Steam client, such as a different user or administration access level.
		// [*] Ensure that you own a license for the App ID on the currently active Steam account. Your game must show up in your Steam library.
		// [*] Your App ID is not completely set up, i.e. in Release State: Unavailable, or it's missing default packages.
		// Valve's documentation for this is located here:
		// https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown
		m_bInitialized = SteamAPI.Init();
		if ( !m_bInitialized )
		{
			Debug.LogWarning( "[Steamworks.NET] SteamAPI_Init() failed.", this );

#if UNITY_STANDALONE
			Application.Quit();
#endif

			return;
		}

		s_EverInitialized = true;

		userStatsReceivedResult = Callback<UserStatsReceived_t>.Create( OnStatsReceived );
		CSteamID localPlayerID = SteamUser.GetSteamID();
		SteamUserStats.RequestUserStats( localPlayerID );

		Runtime.Events.RequestGlobalSave.Subscribe( this, OnRequestGlobalSave );
	}

	void OnRequestGlobalSave( Runtime.Events.RequestGlobalSave e )
	{
		if ( !m_bInitialized )
			return;

		bool anyDirty = false;
		foreach ( var key in cachedGameStats.Keys.ToArray() )
		{
			var value = cachedGameStats[key];
			if ( value.dirty )
			{
				SteamUserStats.SetStat( key.ToString(), value.value );
				cachedGameStats[key] = (value.value, false, value.timestamp, value.fromSteam);
				PersistGameStat( key, value.value, value.timestamp );
				anyDirty = true;
			}
		}

		if ( anyDirty )
		{
			PlayerPrefs.Save();
			SteamUserStats.StoreStats();
		}
	}

	private void OnStatsReceived( UserStatsReceived_t pCallback )
	{
		// Ensure the data returned belongs to your game and matches the local player
		if ( pCallback.m_nGameID == ( ulong )SteamUtils.GetAppID() && pCallback.m_eResult == EResult.k_EResultOK )
		{
			statsReady = true;
			Debug.Log( "Local player stats successfully verified. Ready for leaderboard upload!" );
		}
		else
		{
			Debug.LogError( $"Failed to fetch stats. Error code: {pCallback.m_eResult}" );
		}
	}

	// This should only ever get called on first load and after an Assembly reload, You should never Disable the Steamworks Manager yourself.
	protected virtual void OnEnable()
	{
		if ( s_instance == null )
		{
			s_instance = this;
		}

		if ( !m_bInitialized )
		{
			return;
		}

		if ( m_SteamAPIWarningMessageHook == null )
		{
			// Set up our callback to receive warning messages from Steam.
			// You must launch with "-debug_steamapi" in the launch args to receive warnings.
			m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t( SteamAPIDebugTextHook );
			SteamClient.SetWarningMessageHook( m_SteamAPIWarningMessageHook );
		}

		SignInWithSteam();
	}

	// OnApplicationQuit gets called too early to shutdown the SteamAPI.
	// Because the SteamManager should be persistent and never disabled or destroyed we can shutdown the SteamAPI here.
	// Thus it is not recommended to perform any Steamworks work in other OnDestroy functions as the order of execution can not be garenteed upon Shutdown. Prefer OnDisable().
	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( s_instance != this )
		{
			return;
		}

		s_instance = null;

		if ( !m_bInitialized )
		{
			return;
		}

		leaderboardFindResult?.Dispose();
		leaderboardFindResult = null;
		leaderboardUploadResult?.Dispose();
		leaderboardUploadResult = null;
		leaderboardHandles.Clear();

		SteamAPI.Shutdown();
	}

	string identity = "unityauthenticationservice";

	void SignInWithSteam()
	{
		// It's not necessary to add event handlers if they are
		// already hooked up.
		// Callback.Create return value must be assigned to a
		// member variable to prevent the GC from cleaning it up.
		// Create the callback to receive events when the session ticket
		// is ready to use in the web API.
		// See GetAuthSessionTicket document for details.
		m_AuthTicketForWebApiResponseCallback = Callback<GetTicketForWebApiResponse_t>.Create( OnAuthCallback );

		SteamUser.GetAuthTicketForWebApi( identity );
	}

	void OnAuthCallback( GetTicketForWebApiResponse_t callback )
	{
		m_SessionTicket = BitConverter.ToString( callback.m_rgubTicket ).Replace( "-", string.Empty );
		m_AuthTicketForWebApiResponseCallback.Dispose();
		m_AuthTicketForWebApiResponseCallback = null;
		Debug.Log( "Steam Login success. Session Ticket: " + m_SessionTicket );

		Task.Run( () => SignInWithSteamAsync( m_SessionTicket ) );
	}

	async Task SignInWithSteamAsync( string ticket )
	{
		try
		{
			await AuthenticationService.Instance.SignInWithSteamAsync( ticket, identity );
			Debug.Log( "SignIn is successful." );
		}
		catch ( AuthenticationException ex )
		{
			// Compare error code to AuthenticationErrorCodes
			// Notify the player with the proper error message
			Debug.LogException( ex );
		}
		catch ( RequestFailedException ex )
		{
			// Compare error code to CommonErrorCodes
			// Notify the player with the proper error message
			Debug.LogException( ex );
		}
	}

	protected virtual void Update()
	{
		if ( !m_bInitialized )
			return;

		// Run Steam client callbacks
		SteamAPI.RunCallbacks();
	}

#else
	public static bool Initialized
	{
		get
		{
			return false;
		}
	}
#endif // !DISABLESTEAMWORKS

	public void CompleteAchievement( Schema.AchievementType achievementType, bool forceUpdate = false )
	{
#if !DISABLESTEAMWORKS
		if ( !m_bInitialized || achievementType == Schema.AchievementType.None )
			return;


		if ( !forceUpdate )
			if ( cachedAchievements.Contains( achievementType ) )
				return;

		SteamUserStats.SetAchievement( achievementType.ToString() );
		SteamUserStats.StoreStats();
		cachedAchievements.Add( achievementType );
#endif
	}

	public void UpdateGameStat( Schema.GameStatType gameStatType, int value, bool forceUpdate = false, bool sendImmediate = false )
	{
#if !DISABLESTEAMWORKS
		if ( !m_bInitialized || gameStatType == Schema.GameStatType.None )
			return;

		if ( !forceUpdate )
			if ( cachedGameStats.TryGetValue( gameStatType, out var cachedValue ) && cachedValue.value >= value )
				return;

		var timestamp = DateTime.UtcNow;
		if ( sendImmediate )
		{
			SteamUserStats.SetStat( gameStatType.ToString(), value );
			SteamUserStats.StoreStats();
			PersistGameStat( gameStatType, value, timestamp );
		}
		cachedGameStats[gameStatType] = (value, !sendImmediate, timestamp, false);
#endif
	}

	public bool TryGetGameStat( Schema.GameStatType gameStatType, out int value, out bool isCached )
	{
		value = 0;
		isCached = false;
		if ( gameStatType == Schema.GameStatType.None )
			return false;

		if ( cachedGameStats.TryGetValue( gameStatType, out var cachedValue ) && IsFresh( cachedValue.timestamp ) )
		{
			value = cachedValue.value;
			isCached = !cachedValue.fromSteam;
			return true;
		}

#if !DISABLESTEAMWORKS
		if ( !m_bInitialized || !statsReady )
			return TryGetStaleCachedGameStat( gameStatType, out value );

		if ( SteamUserStats.GetStat( gameStatType.ToString(), out value ) )
		{
			var timestamp = DateTime.UtcNow;
			cachedGameStats[gameStatType] = (value, false, timestamp, true);
			PersistGameStat( gameStatType, value, timestamp );
			return true;
		}
#endif

		return TryGetStaleCachedGameStat( gameStatType, out value );
	}

	public bool AreGameStatsReady
	{
		get
		{
#if !DISABLESTEAMWORKS
			return m_bInitialized && statsReady;
#else
			return false;
#endif
		}
	}

	bool TryGetStaleCachedGameStat( Schema.GameStatType gameStatType, out int value )
	{
		if ( cachedGameStats.TryGetValue( gameStatType, out var cachedValue ) )
		{
			value = cachedValue.value;
			return true;
		}

		value = 0;
		return false;
	}

	static bool IsFresh( DateTime timestamp )
	{
		var age = DateTime.UtcNow - timestamp;
		return timestamp != default && age >= TimeSpan.Zero && age <= GameStatsCacheLifetime;
	}

	void LoadPersistedGameStats()
	{
		foreach ( Schema.GameStatType gameStatType in Enum.GetValues( typeof( Schema.GameStatType ) ) )
		{
			if ( gameStatType == Schema.GameStatType.None )
				continue;

			string prefix = GameStatsCacheKeyPrefix + gameStatType;
			if ( !PlayerPrefs.HasKey( prefix + ".Value" ) || !PlayerPrefs.HasKey( prefix + ".Timestamp" ) )
				continue;

			if ( !long.TryParse( PlayerPrefs.GetString( prefix + ".Timestamp" ), out var ticks ) )
				continue;

			try
			{
				var timestamp = new DateTime( ticks, DateTimeKind.Utc );
				cachedGameStats[gameStatType] = (PlayerPrefs.GetInt( prefix + ".Value" ), false, timestamp, false);
			}
			catch ( ArgumentOutOfRangeException )
			{
				// Ignore invalid persisted timestamps.
			}
		}
	}

	void PersistGameStat( Schema.GameStatType gameStatType, int value, DateTime timestamp )
	{
		string prefix = GameStatsCacheKeyPrefix + gameStatType;
		PlayerPrefs.SetInt( prefix + ".Value", value );
		PlayerPrefs.SetString( prefix + ".Timestamp", timestamp.ToUniversalTime().Ticks.ToString() );
	}

	public void UpdateLeaderboard( int depth, int seed, int level, bool isHardcore, TimeSpan timePlayed )
	{
#if !DISABLESTEAMWORKS
		if ( !m_bInitialized )
			return;

		string leaderboardName = isHardcore ? "HardcoreHighscores" : "DefaultHighscores";

		if ( leaderboardHandles.TryGetValue( leaderboardName, out var handle ) )
		{
			if ( depth <= handle.score )
				return;
			handle.score = Math.Max( handle.score, depth );
			if ( handle.handle.HasValue )
				UploadLeaderboardScore( handle.handle.Value, depth, ConstructDetails( seed, level, timePlayed ) );
			return;
		}

		leaderboardHandles[leaderboardName] = (0, null);

		var call = SteamUserStats.FindLeaderboard( leaderboardName );
		leaderboardFindResult = new CallResult<LeaderboardFindResult_t>();
		leaderboardFindResult.Set( call, ( data, ioFailure ) =>
		{
			if ( data.m_bLeaderboardFound == 0 || ioFailure )
			{
				Debug.LogError( "Failed to find leaderboard: " + leaderboardName );
				return;
			}
			var entry = leaderboardHandles[leaderboardName];
			entry.handle = data.m_hSteamLeaderboard;
			leaderboardHandles[leaderboardName] = entry;
			UploadLeaderboardScore( data.m_hSteamLeaderboard, depth, ConstructDetails( seed, level, timePlayed ) );
		} );
#endif
	}

	public const int NumDetails = 3;

	int[] ConstructDetails( int seed, int level, TimeSpan timePlayed )
	{
		return new int[NumDetails]
		{
			seed,
			level,
			(int)timePlayed.TotalSeconds
		};
	}

	public void DeconstructDetails( int[] details, out int seed, out int level, out TimeSpan timePlayed )
	{
		seed = details.Length > 0 ? details[0] : 0;
		level = details.Length > 1 ? details[1] : 0;
		timePlayed = details.Length > 2 ? TimeSpan.FromSeconds( details[2] ) : TimeSpan.Zero;
	}

	void UploadLeaderboardScore( SteamLeaderboard_t leaderboard, int score, int[] details = null )
	{
		if ( score < 200 )
			return;

		var call = SteamUserStats.UploadLeaderboardScore(
			leaderboard,
			ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
			score,
			details,
			details?.Length ?? 0 );

		Debug.Log( $"Steam leaderboard upload requested. Call: {call.m_SteamAPICall}, leaderboard: {SteamUserStats.GetLeaderboardName( leaderboard )}, score: {score}" );

		leaderboardUploadResult ??= new CallResult<LeaderboardScoreUploaded_t>();
		leaderboardUploadResult.Set( call, ( data, ioFailure ) =>
		{
			if ( ioFailure )
			{
				Debug.LogError( $"Steam leaderboard upload failed due to an IO failure. Call: {call.m_SteamAPICall}, score: {score}" );
				return;
			}

			Debug.Log(
				$"Steam leaderboard upload result. Success: {data.m_bSuccess != 0}, " +
				$"score changed: {data.m_bScoreChanged != 0}, " +
				$"score: {data.m_nScore}, " +
				$"call: {call.m_SteamAPICall}" );
		} );
	}
}