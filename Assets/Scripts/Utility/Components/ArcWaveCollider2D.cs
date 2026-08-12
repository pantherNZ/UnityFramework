using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions.Must;

[RequireComponent( typeof( Collider ) )]
public class ArcWaveCollider : MonoBehaviour
{
    public float expansionSpeed = 8f;
    public float maxRadius = 12f;
    public float thickness = 1.5f;
    public float directionDeg = 0f;
    public float arcDeg = 90f;
    public LayerMask hitMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

    [SerializeField, Min( 1 )]
    int maxOverlapColliders = 128;

    float currentRadius;
    Collider[] overlapBuffer;
    readonly HashSet<Collider> activeOverlaps = new();
    readonly HashSet<Collider> frameOverlaps = new();
    readonly List<Collider> removals = new();

    void Awake()
    {
        currentRadius = 0f;
        overlapBuffer = new Collider[Mathf.Max( 1, maxOverlapColliders )];
    }

    void OnEnable()
    {
        currentRadius = 0f;
    }

    void OnDisable()
    {
        foreach ( var collider in activeOverlaps )
            DispatchTriggerExit( collider );

        activeOverlaps.Clear();
        frameOverlaps.Clear();
        removals.Clear();
    }

    void FixedUpdate()
    {
        currentRadius += expansionSpeed * Time.fixedDeltaTime;
        if ( currentRadius > maxRadius )
        {
            enabled = false;
            return;
        }

        RebuildOverlaps();
    }

    void RebuildOverlaps()
    {
        frameOverlaps.Clear();

        int overlapCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            currentRadius * transform.lossyScale.y,
            overlapBuffer,
            hitMask,
            triggerInteraction );

        for ( int i = 0; i < overlapCount; i++ )
        {
            var other = overlapBuffer[i];
            if ( other == null )
                continue;

            if ( !IsLayerInHitMask( other.gameObject.layer ) )
                continue;

            if ( !IsInsideWaveArc( other ) )
                continue;

            frameOverlaps.Add( other );
        }

        foreach ( var collider in frameOverlaps )
        {
            if ( activeOverlaps.Add( collider ) )
                DispatchTriggerEnter( collider );

            DispatchTriggerStay( collider );
        }

        removals.Clear();
        foreach ( var collider in activeOverlaps )
        {
            if ( frameOverlaps.Contains( collider ) )
                continue;

            removals.Add( collider );
        }

        for ( int i = 0; i < removals.Count; i++ )
        {
            var collider = removals[i];
            activeOverlaps.Remove( collider );
            DispatchTriggerExit( collider );
        }
    }

    void DispatchTriggerEnter( Collider other )
    {
        SendMessage( "OnTriggerEnter", other, SendMessageOptions.DontRequireReceiver );
    }

    void DispatchTriggerStay( Collider other )
    {
        SendMessage( "OnTriggerStay", other, SendMessageOptions.DontRequireReceiver );
    }

    void DispatchTriggerExit( Collider other )
    {
        SendMessage( "OnTriggerExit", other, SendMessageOptions.DontRequireReceiver );
    }

    bool IsLayerInHitMask( int layer )
    {
        return ( hitMask.value & ( 1 << layer ) ) != 0;
    }

    bool IsInsideWaveArc( Collider other )
    {
        float innerRadius = Mathf.Max( 0f, currentRadius - thickness ) * transform.lossyScale.y;

        Vector3 closest = other.transform.position;
        Vector3 offset = closest - transform.position;
        offset.y = 0f;

        float distance = offset.magnitude;
        if ( distance < 0.0001f )
        {
            offset = other.transform.position - transform.position;
            offset.y = 0f;
            distance = offset.magnitude;
        }

        if ( distance < innerRadius || distance > currentRadius * transform.lossyScale.y )
            return false;

        float clampedArc = Mathf.Clamp( arcDeg, 0f, 360f );
        if ( clampedArc >= 359.9f )
            return true;

        if ( offset.sqrMagnitude < 0.0001f )
            return true;

        Vector3 direction = Vector3.forward.RotateY( directionDeg + transform.eulerAngles.y );
        float signedAngle = Vector3.SignedAngle( direction, offset.normalized, Vector3.up );
        return Mathf.Abs( signedAngle ) <= clampedArc * 0.5f;
    }
}