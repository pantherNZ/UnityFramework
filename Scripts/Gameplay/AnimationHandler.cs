using System;
using System.Collections;
using System.Collections.Generic;
using Schema.Animation;
using Schema.Combat.Visual;
using UnityEngine;

namespace Runtime.Animation
{
	public struct AnimationEventParams
	{
		public AnimationType type;
		public string eventName;
	}

	public struct AnimationCancelledParams
	{
		public AnimationType cancelledType;
		public AnimationType newAnimType;
	}

	public class QueuedAnimation
	{
		public AnimationData animation;
		public float? speed;
		public float? duration;
	}

	[RequireComponent( typeof( Animator ) )]
	public class AnimationHandler : MonoBehaviour
	{
		public bool playDeathAnimation = true;
		public AnimationMap animations;

		// Events
		public event Action DispatchDamageEvent;
		public event Action<AnimationEventParams> AnimationEvent;
		public event Action<AnimationData> AnimationStartEvent;
		public event Action<AnimationData> AnimationCompleteEvent;
		public event Action<AnimationData> AnimationLoopedEvent;
		public event Action<AnimationCancelledParams> AnimationCancelledEvent;

		// Components
		Animator animator;
		Pathfinding.IAstarAI agent;

		// Animation
		AnimationData currentAnimation;
		int currentChainCount;
		QueuedAnimation queuedAnimation;
		public AnimationData CurrentAnimation => currentAnimation;
		public AnimationData QueuedAnimation => queuedAnimation?.animation;
		public Animator Animator => animator;
		static readonly int sideVelocityAnimHash = Animator.StringToHash( "SideVelocity" );
		static readonly int forwardVelocityAnimHash = Animator.StringToHash( "ForwardVelocity" );
		Vector3? movementVelocityOverride;

		Dictionary<ParticleVisualSchema, List<ParticleVisualSchema.Runtime>> attachedParticles = new();
		bool isDead;

		private void Awake()
		{
			animator = GetComponent<Animator>();
			agent = GetComponent<Pathfinding.IAstarAI>();
			var health = GetComponent<Game.Health>();

			if ( health != null )
				health.DeathEvent += Health_DeathEvent;
		}

		public float CurrentAnimationNormalizedTime => animator.GetCurrentAnimatorStateInfo( 0 ).normalizedTime;
		public float CurrentAnimationElapsedTime => CurrentAnimationNormalizedTime * animator.GetCurrentAnimatorStateInfo( 0 ).length;

		// Add end events, looping events & store length & other data into the animation data info for later use
		void SetupAnimationData()
		{
			animator = GetComponent<Animator>();
			animations.SetupRuntimeController( animator.runtimeAnimatorController );
		}

		void Start()
		{
			if ( animations == null )
			{
				Debug.LogError( $"AnimationHandler: animations data is null: {gameObject.name}" );
				return;
			}

			SetupAnimationData();

			if ( animations.startingAnimation.HasValue )
				PlayAnimation( animations.startingAnimation.Value );
			else
				animator.StopPlayback();

			AnimationCompleteEvent += OnAnimationComplete;

			StartCoroutine( CleanupEffects() );
		}

		static WaitForSeconds cleanupTimer = new( 1.0f );

		IEnumerator CleanupEffects()
		{
			while ( true )
			{
				foreach ( var x in attachedParticles )
					x.Value.RemoveAll( x => x == null || x.destroyed );
				attachedParticles.RemoveAll( x => x.Key == null || x.Value.Count == 0 );
				yield return cleanupTimer;
			}
		}

		private void OnAnimationComplete( AnimationData data )
		{
			if ( isDead )
				return;

			if ( currentAnimation != data )
				return;

			if ( animator.IsInTransition( 0 ) )
				return;

			if ( QueuedAnimation != null )
			{
				if ( queuedAnimation.duration.HasValue )
					PlayAnimationForDuration( queuedAnimation.animation.animType, queuedAnimation.duration.Value, true );
				else
					PlayAnimation( queuedAnimation.animation.animType, queuedAnimation.speed.Value, true );
				queuedAnimation = null;
			}
			else if ( currentAnimation != null
				&& animations.defaultAnimation.HasValue
				&& !currentAnimation.isLooping
				&& currentChainCount == 0 )
			{
				PlayAnimation( animations.defaultAnimation.Value );
			}
			else
			{
				currentChainCount = Mathf.Max( 0, currentChainCount - 1 );
			}
		}

		public void SetAnimations( AnimationMap data )
		{
			if ( animations == data )
				return;

			animations = data;

			SetupAnimationData();
		}

		private void Update()
		{
			if ( currentAnimation != null &&
				currentAnimation.animType == AnimationType.MovementBlendTree &&
				agent != null &&
			 	movementVelocityOverride == null )
			{
				var velocityLocal = transform.InverseTransformDirection( agent.velocity );
				animator.SetFloat( sideVelocityAnimHash, agent.isStopped ? 0.0f : velocityLocal.x );
				animator.SetFloat( forwardVelocityAnimHash, agent.isStopped ? 0.0f : velocityLocal.z );
				animator.speed = velocityLocal.magnitude * animations.animationMovementSpeedModifier;
			}
		}

		public AnimationData GetAnimationData( AnimationType animation )
			=> animations.GetRandomAnimationData( animation );

		public AnimationData GetAnimationData( Motion animation )
			=> animations.GetAnimationData( animation );

		public float GetAnimationLengthSec( AnimationType animation )
			=> GetAnimationData( animation )?.length ?? 0.0f;

		public bool? IsLoopingAnim( AnimationType animation )
			=> GetAnimationData( animation )?.isLooping;

		public void OverrideVelocityAnimData( float velocityX, float velocityZ )
		{
			movementVelocityOverride = new Vector3( velocityX, 0.0f, velocityZ );
			animator.SetFloat( sideVelocityAnimHash, velocityX );
			animator.SetFloat( forwardVelocityAnimHash, velocityZ );
			animator.speed = movementVelocityOverride.Value.magnitude * animations.animationMovementSpeedModifier;
		}

		public void ResetVelocityAnimData()
		{
			movementVelocityOverride = null;
		}

		public void PlayAnimationForDuration( Motion animation, float timeSec = 1.0f, bool force = false )
		{
			if ( !force && currentAnimation != null && currentAnimation.animationOrBlendTree == animation )
				return;
			var animationData = animations.GetAnimationData( animation );
			PlayAnimationForDurationInternal( animationData, timeSec, force );
		}

		public void PlayAnimationForDuration( AnimationType animation, float timeSec = 1.0f, bool force = false )
		{
			if ( !force && currentAnimation != null && currentAnimation.animType == animation )
				return;
			var animationData = animations.GetRandomAnimationData( animation );
			PlayAnimationForDurationInternal( animationData, timeSec, force );
		}

		void PlayAnimationForDurationInternal( AnimationData animationData, float timeSec = 1.0f, bool force = false )
		{
			if ( animationData == null )
			{
				Debug.LogError( $"PlayAnimationForDuration failed due to not finding the animation data: {animationData}, {gameObject.name}" );
				return;
			}

			if ( animationData.isBlendTree )
			{
				Debug.LogError( $"PlayAnimationForDuration cannot be used on blend trees: {animationData}, {gameObject.name}" );
				return;
			}

			if ( animationData.length == 0.0f )
			{
				Debug.LogError( $"PlayAnimationForDuration failed due to animationData length value of 0 from data: {animationData}, {gameObject.name}" );
				animationData.length = 1.0f;
			}

			float speed = animationData.length / timeSec;
			if ( speed == 0.0f )
			{
				Debug.LogError( $"PlayAnimationForDuration failed due to speed value of 0 from data: {animationData}, {gameObject.name}" );
				speed = 1.0f;
			}

			PlayAnimationInternal( animationData, speed );
		}

		public bool HasAnimation( AnimationType animation )
		{
			var data = animations.GetAnimationDataList( animation );
			return data != null && !data.data.IsEmpty();
		}

		public void PlayAnimation( AnimationType animation, float speed = 1.0f, bool force = false )
		{
			if ( !force && currentAnimation != null && currentAnimation.animType == animation )
				return;
			var animationData = animations.GetRandomAnimationData( animation );
			PlayAnimationInternal( animation, animationData, speed );
		}

		public void PlayAnimation( Motion animation, float speed = 1.0f, bool force = false )
		{
			if ( !force && currentAnimation != null && currentAnimation.animationOrBlendTree == animation )
				return;
			var animationData = animations.GetAnimationData( animation );
			PlayAnimationInternal( animationData, speed );
		}

		public void QueueAnimation( AnimationType animation, float speed = 1.0f )
		{
			var animData = animations.GetRandomAnimationData( animation );

			if ( animData == null )
			{
				Debug.LogError( $"QueueAnimation failed due to monster not having animation: {animation}, {gameObject.name}" );
				return;
			}

			QueueAnimationInternal( animData, speed, duration: null );
		}

		public void QueueAnimation( Motion animation, float speed = 1.0f )
		{
			var animData = animations.GetAnimationData( animation );
			QueueAnimationInternal( animData, speed, duration: null );
		}

		public void QueueAnimationWithDuration( AnimationType animation, float durationSec = 1.0f )
		{
			var animData = animations.GetRandomAnimationData( animation );

			if ( animData == null )
			{
				Debug.LogError( $"QueueAnimation failed due to monster not having animation: {animation}, {gameObject.name}" );
				return;
			}

			QueueAnimationInternal( animData, speed: null, duration: durationSec );
		}

		public void QueueAnimationWithDuration( Motion animation, float durationSec = 1.0f )
		{
			var animData = animations.GetAnimationData( animation );
			QueueAnimationInternal( animData, speed: null, duration: durationSec );
		}

		void QueueAnimationInternal( AnimationData animationData, float? speed, float? duration )
		{
			if ( animationData == null )
				return;

			queuedAnimation = new QueuedAnimation()
			{
				animation = animationData,
				speed = speed,
				duration = duration,
			};
		}

		void PlayAnimationInternal( AnimationType animation, AnimationData animationData, float speed = 1.0f )
		{
			if ( animationData == null )
			{
				Debug.LogError( $"PlayAnimation failed due to monster not having animation: {animation}, {gameObject.name}" );
				return;
			}

			PlayAnimationInternal( animationData, speed );
		}

		void PlayAnimationInternal( AnimationData animationData, float speed = 1.0f )
		{
			if ( animationData == null )
				return;

			if ( currentAnimation != null )
			{
				AnimationCancelledEvent?.Invoke( new AnimationCancelledParams()
				{
					cancelledType = currentAnimation.animType,
					newAnimType = animationData.animType
				} );
			}

			queuedAnimation = null;
			animationData.isLooping |= animationData.isLooping || animationData.animType == AnimationType.MovementBlendTree;
			currentAnimation = animationData;
			currentChainCount = currentAnimation.chainCount;
			animator.CrossFadeInFixedTime( animationData.animationHash, animations.animationTransitionTimeSec );
			animator.speed = speed * animationData.speedMultiplier;

			AnimationStartEvent?.Invoke( currentAnimation );
		}

		private void Health_DeathEvent( Events.DeathEventArgs _ )
		{
			if ( playDeathAnimation )
				PlayAnimation( AnimationType.Death );
			isDead = true;
		}

		AnimationData GetEventAnimationData( AnimationEvent animationEvent )
		{
			if ( animations != null && animationEvent?.objectReferenceParameter is AnimationClip clip && clip != null )
				return animations.GetAnimationData( clip ) ?? currentAnimation;
			return currentAnimation;
		}

		void HandleAnimEvent( string eventParam, AnimationData animationData )
		{
			if ( animationData == null )
			{
				var animatorinfo = animator.GetCurrentAnimatorClipInfo( 0 );
				var currentAnim = animatorinfo.Length > 0 ? animatorinfo[0].clip.name : "unknown";
				Debug.LogError( $"AnimEvent called without resolvable animation source: anim: {currentAnim}, event: {eventParam}, obj: {gameObject.name}" );
				animator.StopPlayback();
				return;
			}

			var eventAnimationType = animationData?.animType ?? AnimationType.INVALID;

			AnimationEvent?.Invoke( new AnimationEventParams()
			{
				eventName = eventParam,
				type = eventAnimationType
			} );

			if ( eventParam.ToLower() == "dispatchdamage" )
				DispatchDamageEvent?.Invoke();
			else if ( eventParam.ToLower() == "animationend" )
				AnimationCompleteEvent?.Invoke( animationData );
			else if ( eventParam.ToLower() == "animationloop" )
				AnimationLoopedEvent?.Invoke( animationData );
		}

		// Animation event
		public void AnimEvent( string eventParam )
		{
			HandleAnimEvent( eventParam, currentAnimation );
		}

		// Animation event
		public void AnimEventWithSource( AnimationEvent animationEvent )
		{
			if ( animationEvent == null )
				return;

			var eventAnimationData = GetEventAnimationData( animationEvent );
			HandleAnimEvent( animationEvent.stringParameter, eventAnimationData );
		}

		// Animation event
		public void PlayAudio( Schema.Audio.AudioDataSchema audio )
		{
			Audio.SfxManager.Instance.Play( audio, transform.position );
		}

		// Animation event
		public void StopAudio( Schema.Audio.AudioDataSchema audio )
		{
			Audio.SfxManager.Instance.Stop( audio );
		}

		// Animation event
		public void PlayParticle( ParticleVisualAsset particle )
		{
			attachedParticles.GetOrAdd( particle.effect ).Add( particle.effect.Build( transform ) as ParticleVisualSchema.Runtime );
		}

		// Animation event
		public void StopParticle( ParticleVisualAsset particle )
		{
			if ( attachedParticles.TryGetValue( particle.effect, out var runtimes ) )
			{
				foreach ( var runtime in runtimes )
					runtime.Destroy();
				attachedParticles.Remove( particle.effect );
			}
		}
	}
}
