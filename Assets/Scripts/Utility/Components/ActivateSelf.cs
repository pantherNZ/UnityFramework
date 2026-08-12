using UnityEngine;

public class ActivateSelf : MonoBehaviour
{
    public float autoActivateAfterSeconds;

    private void Start()
    {
        gameObject.SetActive( false );
        if ( autoActivateAfterSeconds > 0 )
            Utility.FunctionTimer.CreateTimer( autoActivateAfterSeconds, () => { if ( this != null && gameObject != null ) gameObject.SetActive( true ); } );
    }
}