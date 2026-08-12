
using System.Collections;
using UnityEngine;

public class CoroutineRunner : Memory.MonoSingleton<CoroutineRunner>
{
    public static void RunCoroutine( IEnumerator coroutine )
    {
        if ( !HasInstance )
        {
            var newGo = new GameObject( "CoroutineRunner" );
            newGo.AddComponent<CoroutineRunner>();
        }

        var instance = CoroutineRunner.Instance;
        instance.StartCoroutine( instance.MonitorRunning( coroutine ) );
    }

    IEnumerator MonitorRunning( IEnumerator coroutine )
    {
        while ( coroutine.MoveNext() )
        {
            yield return coroutine.Current;
        }
    }
}