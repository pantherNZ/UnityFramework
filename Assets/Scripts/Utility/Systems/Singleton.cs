using System;
using UnityEngine;

namespace Memory
{
    abstract public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static object _lock = new object();

        public static bool HasInstance => _instance != null;
        public static T Instance
        {
            get
            {
                lock ( _lock )
                {
                    if ( _instance == null )
                    {
                        Debug.Log
                        (
                            "Singleton instance of " + typeof( T ) +
                            " is trying to be accessed, but it wasn't initialized first. " +
                            "Make sure to add an instance of " + typeof( T ) + " in the scene before " +
                            " trying to access it."
                        );
                    }

                    return _instance;
                }
            }
        }

        public void Awake()
        {
            if ( _instance != null && _instance != this )
            {
                Debug.LogError( "Multiple singletons found! " + typeof( T ) );
                Destroy( gameObject );
                return;
            }

            _instance = GetComponent<T>();
        }
    }

    public abstract class Singleton<T> : IDisposable where T : Singleton<T>
    {
        private static T _instance;
        private static readonly object _lock = new object();

        public static bool HasInstance => _instance != null;
        public static T Instance
        {
            get
            {
                lock ( _lock )
                {
                    if ( _instance == null )
                    {
                        Debug.LogError(
                            "Singleton instance of " + typeof( T ) +
                            " is trying to be accessed, but it wasn't initialized first. " +
                            "Make sure to create an instance of " + typeof( T ) + " before " +
                            "trying to access it." );
                    }

                    return _instance;
                }
            }
        }

        protected Singleton()
        {
            lock ( _lock )
            {
                if ( _instance != null )
                {
                    Debug.LogError( "Multiple singletons found! " + typeof( T ) );
                    return;
                }

                _instance = ( T )( object )this;
            }
        }

        public virtual void Dispose()
        {
            lock ( _lock )
            {
                if ( _instance == this )
                {
                    _instance = null;
                }
            }
        }
    }


    public class SingletonEventListener<T> : MonoEventReceiver where T : MonoBehaviour
    {
        private static T _instance;
        private static object _lock = new object();

        public static T Instance
        {
            get
            {
                lock ( _lock )
                {
                    if ( _instance == null )
                    {
                        Debug.Log
                        (
                            "Singleton instance of " + typeof( T ) +
                            " is trying to be accessed, but it wasn't initialized first. " +
                            "Make sure to add an instance of " + typeof( T ) + " in the scene before " +
                            " trying to access it."
                        );
                    }

                    return _instance;
                }
            }
        }

        public void Awake()
        {
            if ( _instance != null && _instance != this )
            {
                Debug.LogError( "Multiple singletons found! " + typeof( T ) );
                Destroy( gameObject );
                return;
            }

            _instance = GetComponent<T>();
        }
    }
}