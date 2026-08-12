using System.Collections;
using Runtime.Game;
using Schema.Audio;
using UnityEngine;

namespace Runtime.Audio
{
    [DisallowMultipleComponent]
    public class AudioComponent : MonoBehaviour
    {
        public enum PlaybackType
        {
            World,
            UI,
            Dialogue,
        }

        public AudioDataSchema asset;
        public PlaybackType playbackType;
        public float delaySec;

        private IEnumerator Start()
        {
            if ( asset == null )
                yield break;

            yield return new WaitUntil( () => SfxManager.Instance != null && GameSceneManager.Instance != null && !GameSceneManager.Instance.IsLoading );

            if ( delaySec > 0.0f )
                yield return new WaitForSeconds( delaySec );

            if ( !isActiveAndEnabled || asset == null || SfxManager.Instance == null )
                yield break;

            switch ( playbackType )
            {
                case PlaybackType.UI:
                    SfxManager.Instance.PlayUI( asset );
                    break;

                case PlaybackType.Dialogue:
                    SfxManager.Instance.PlayDialogue( asset );
                    break;

                default:
                    SfxManager.Instance.Play( asset, transform.position );
                    break;
            }
        }
    }
}