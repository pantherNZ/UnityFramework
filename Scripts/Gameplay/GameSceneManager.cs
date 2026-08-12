using System;
using System.Collections;
using System.Collections.Generic;
using Runtime.Events;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Runtime.Game
{
	public class LoadHook
	{
		public event Action OnFinished;
		public void Finished() { OnFinished.Invoke(); }
	}

	[Serializable]
	[DisallowMultipleComponent]
	public class GameSceneManager : MonoEventReceiver
	{
		public enum SceneType
		{
			Game,
			Outpost,
			Test,
			MainMenu,
			DummyTargets
		}

		// Singleton
		static GameSceneManager sceneManager;
		public static GameSceneManager Instance { get => sceneManager; }

		[Serializable]
		public struct SceneData
		{
			public SceneReference scene;
			public bool showCursor;
			public Schema.Audio.AudioDataSchema onLoadAudio;
			public SceneType sceneType;
		};

		[SerializeField] SceneData gameScene = new();
		[SerializeField] SceneData outpostScene = new();
		[SerializeField] SceneData testScene = new();
		[SerializeField] SceneData dummyScene = new();
		[SerializeField] SceneData worldGenScene = new();
		[SerializeField] SceneData mainMenuScene = new();
		// Make sure to add any new scenes to the allScenes list in Awake()

		UnityEngine.UIElements.UIDocument loadingScreenUI = null;
		UI.LoadingScreenController loadingScreenController = new();

		private List<SceneData> allScenes;
		private ReadWriteProperty<bool> currentSceneLoaded = new();
		private SceneData currentScene;

		public SceneReference CurrentScene => currentScene.scene;
		public SceneType CurrentSceneType => currentScene.sceneType;

		private List<LoadHook> activeLoadingHooks = new();
		private ReadWriteProperty<bool> hasActiveLoadingHooks = new();
		private bool? cursorVisibilityOverride;
		public Property<bool> isLoadingProp;
		public bool IsLoading => isLoadingProp.value;
		public float TimeSinceSceneLoad { get; private set; }

		float _lastInputActivityTime;
		bool _hasAppFocus = true;
		bool _worldLoadComplete;


#if UNITY_EDITOR
		[NonSerialized]
		public static string DebugOverrideGameScene;
#endif

		protected void Awake()
		{
			if ( sceneManager != null && sceneManager != this )
			{
				Debug.LogError( "Multiple scene managers found!" );
				Destroy( gameObject );
				return;
			}

			isLoadingProp = new OrProperty( currentSceneLoaded.Inverse(), hasActiveLoadingHooks );

			sceneManager = this;

			allScenes = new()
			{
				gameScene,
				outpostScene,
				testScene,
				worldGenScene,
				mainMenuScene,
				dummyScene,
			};

			RecheckCursorVisibility();

			loadingScreenUI = GetComponent<UnityEngine.UIElements.UIDocument>();
			SceneManager.sceneLoaded += SceneManager_sceneLoaded;
		}

		private void Start()
		{
			loadingScreenController.Bind( loadingScreenUI.rootVisualElement );

			if ( !activeLoadingHooks.IsEmpty() )
				loadingScreenController.Show();
			else if ( _worldLoadComplete )
				loadingScreenController.Hide();

			if ( IngameDebugConsole.DebugLogManager.Instance != null )
				IngameDebugConsole.DebugLogManager.Instance.OnLogWindowHidden += OnLogWindowHidden;
		}

		private void FixedUpdate()
		{
			RecheckCursorVisibility();

			if ( currentSceneLoaded.value )
				TimeSinceSceneLoad += Time.fixedDeltaTime;
			else
				TimeSinceSceneLoad = 0.0f;

		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			SceneManager.sceneLoaded -= SceneManager_sceneLoaded;

			if ( IngameDebugConsole.DebugLogManager.Instance != null )
				IngameDebugConsole.DebugLogManager.Instance.OnLogWindowHidden -= OnLogWindowHidden;
		}

		void OnLogWindowHidden()
		{
			if ( !InOutpostScene() &&
				!InMainMenuScene() &&
				PauseManager.Instance != null &&
				!PauseManager.Instance.IsPaused )
				Cursor.visible = false;
		}

		public LoadHook PushLoadHook()
		{
			var newHook = new LoadHook();
			newHook.OnFinished += () =>
			{
				activeLoadingHooks.Remove( newHook );
				hasActiveLoadingHooks.SetValue( !activeLoadingHooks.IsEmpty() );
				CheckLoadingFinish();
			};
			activeLoadingHooks.Add( newHook );
			hasActiveLoadingHooks.SetValue( !activeLoadingHooks.IsEmpty() );
			if ( loadingScreenUI != null && loadingScreenUI.rootVisualElement != null )
				loadingScreenController.Show();
			return newHook;
		}

		private void SceneManager_sceneLoaded( Scene arg0, LoadSceneMode arg1 )
		{
			foreach ( var scene in allScenes )
			{
				if ( SceneManager.GetActiveScene().path == scene.scene )
				{
					currentScene = scene;
					break;
				}
			}

			currentSceneLoaded.SetValue( true );

			CheckLoadingFinish();
			RecheckCursorVisibility();
		}

		private void CheckLoadingFinish()
		{
			if ( currentSceneLoaded.value &&
				activeLoadingHooks.IsEmpty() )
			{
				loadingScreenController.Hide();
				WorldLoadComplete.Trigger( new WorldLoadComplete() );
				_worldLoadComplete = true;

				if ( Audio.SfxManager.Instance != null && currentScene.onLoadAudio != null )
					Utility.FunctionTimer.CreateTimer( 0.01f, () => Audio.SfxManager.Instance.PlayUI( currentScene.onLoadAudio ) );
			}
		}

		private void OnApplicationFocus( bool focus )
		{
			_hasAppFocus = focus;
			RecheckCursorVisibility();
		}

		void RecheckCursorVisibility()
		{
			if ( cursorVisibilityOverride.HasValue )
			{
				Cursor.visible = cursorVisibilityOverride.Value;
				return;
			}

			if ( currentScene.scene != null )
			{
				Cursor.visible = currentScene.showCursor;
				return;
			}

			Cursor.visible = false;
		}

		public void LoadMainMenuScene()
		{
			Application.targetFrameRate = Settings.MenuFpsLimit;
			LoadScene( mainMenuScene );
		}

		public void LoadOutpostScene()
		{
#if UNITY_EDITOR
			// Debug editor code to override the game scene to we don't load the game from the outpost when in test world etc.
			DebugOverrideGameScene = SceneManager.GetActiveScene().path;
#endif

			Screen.SetResolution( 1920, 1080, Screen.fullScreen );
			Application.targetFrameRate = Settings.MenuFpsLimit;
			LoadScene( outpostScene );
		}

		public void LoadGameScene()
		{
#if UNITY_EDITOR
			if ( DebugOverrideGameScene != null )
				if ( LoadScene( DebugOverrideGameScene ) )
					return;
#endif
			Screen.SetResolution( Settings.Resolution.x, Settings.Resolution.y, Settings.Fullscreen );
			Application.targetFrameRate = Settings.GameFpsLimit;
			LoadScene( gameScene );
		}

#if UNITY_EDITOR
		private bool LoadScene( string scene )
		{
			foreach ( var sceneData in allScenes )
			{
				if ( scene == sceneData.scene.ScenePath )
				{
					LoadScene( sceneData );
					return true;
				}
			}

			Debug.LogError( $"Failed to load to scene: {scene}, you probably need to add it to the GameSceneManager" );
			return false;
		}
#endif

		private void LoadScene( SceneData scene )
		{
			if ( CurrentScene != null && CurrentScene == scene.scene )
				return;

			TimeSinceSceneLoad = 0.0f;
			currentScene = scene;
			currentSceneLoaded.SetValue( false );
			Cursor.visible = scene.showCursor;
			Events.SceneChanging.Trigger( new() { newScene = scene, oldScene = currentScene } );
			StartCoroutine( LoadSceneAsync( scene.scene ) );
			if ( loadingScreenUI.rootVisualElement != null )
				loadingScreenController.Show();
		}

		private IEnumerator LoadSceneAsync( SceneReference scene )
		{
			var asyncOp = SceneManager.LoadSceneAsync( scene );
			while ( !asyncOp.isDone )
				yield return null;
		}

		public bool InTestWorldScene() =>
			( testScene.scene != null && SceneManager.GetActiveScene().path == testScene.scene ) ||
			( dummyScene.scene != null && SceneManager.GetActiveScene().path == dummyScene.scene );
		public bool InOutpostScene() => outpostScene.scene != null && SceneManager.GetActiveScene().path == outpostScene.scene;
		public bool InMainMenuScene() => mainMenuScene.scene != null && SceneManager.GetActiveScene().path == mainMenuScene.scene;
		public bool InGameScene()
		{
#if UNITY_EDITOR
			if ( DebugOverrideGameScene != null )
				return SceneManager.GetActiveScene().path == DebugOverrideGameScene;
#endif
			return gameScene.scene != null && SceneManager.GetActiveScene().path == gameScene.scene;
		}

		public void OverrideCursorVisibility( bool visible )
		{
			if ( cursorVisibilityOverride.HasValue && cursorVisibilityOverride.Value == visible )
				return;
			cursorVisibilityOverride = visible;
			RecheckCursorVisibility();
		}

		public void ResetCursorVisibility()
		{
			cursorVisibilityOverride = null;
			RecheckCursorVisibility();
		}
	}
}
