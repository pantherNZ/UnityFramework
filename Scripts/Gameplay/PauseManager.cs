using System;
using System.Diagnostics.Contracts;
using BehaviorDesigner.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Runtime.Game
{
    public class PauseManager : MonoBehaviour
    {
        // Singleton
        static PauseManager pauseManager;
        public static PauseManager Instance => pauseManager;

        private int pauseCount = 0;
        private ReadWriteProperty<bool> _isPaused = new();
        public Property<bool> IsPaused => _isPaused;

        // TODOa: this pause action lives here for now, but once we have a real "pause menu" (resume/settings/exit_game kinda thang) we will move it there
        private InputAction _pauseAction;
        private PauseLock? _playerPauseLock;

        private void Awake()
        {
            if ( pauseManager != null && pauseManager != this )
            {
                Debug.LogError( "Multiple pause managers found!" );
                Destroy( gameObject );
                return;
            }

            pauseManager = this;

            _pauseAction = InputSystem.actions.FindAction( "Gameplay/Pause" );

            _pauseAction.performed += ctx => PlayerPausePressed();
        }

        // TODOa: not every part of the codebase respects the game being paused during gameplay right now
        private void PlayerPausePressed()
        {
            if ( _playerPauseLock.HasValue )
            {
                _playerPauseLock.Value.Dispose();
                _playerPauseLock = null;
            }
            else
            {
                _playerPauseLock = PushPause( true );
            }
        }

        public void ReleasePlayerPause()
        {
            if ( _playerPauseLock.HasValue )
            {
                _playerPauseLock.Value.Dispose();
                _playerPauseLock = null;
            }
        }

        public struct PauseLock : IDisposable
        {
            private PauseManager pm;

            // showPauseScreen is ignored if the game is already paused
            public PauseLock( PauseManager pm, bool showPauseScreen )
            {
                this.pm = pm;
                pm.PushPauseInternal( showPauseScreen );
            }

            public void Dispose()
            {
                if ( pm )
                {
                    pm.PopPauseInternal();
                    pm = null;
                }
            }
        }

        [Pure] public PauseLock PushPause( bool showPauseScreen ) => new( this, showPauseScreen );

        void PushPauseInternal( bool showPauseScreen )
        {
            if ( pauseCount == 0 )
            {
                Time.timeScale = 0.0f;
                _isPaused.SetValue( true );
                if ( InputTypeTracker.Instance.currentType == InputTypeTracker.InputType.KeyboardMouse )
                    GameSceneManager.Instance.OverrideCursorVisibility( true );
                if ( BehaviorManager.instance != null )
                    BehaviorManager.instance.enabled = false;
                Runtime.Events.GamePaused.Trigger( new Events.GamePaused() { showPauseUi = showPauseScreen } );
            }

            pauseCount++;
        }

        void PopPauseInternal()
        {
            pauseCount--;

            if ( pauseCount == 0 )
            {
                Time.timeScale = 1.0f;
                _isPaused.SetValue( false );
                if ( BehaviorManager.instance != null )
                    BehaviorManager.instance.enabled = true;
                Events.GameResumed.Trigger( new Events.GameResumed() { } );
            }
        }
    }
}
