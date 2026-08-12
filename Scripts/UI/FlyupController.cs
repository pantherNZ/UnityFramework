using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
	class FlyupController
	{
		public enum Style
		{
			CenterShowcase,
			FadeToDestination,
		}
		VisualElement head;
		VisualElement root;
		VisualElement inners;
		VisualElement origin;
		VisualElement target;
		IVisualElementScheduledItem update;
		Style style;
		float startTime;
		Vector2 p1;
		Vector2 p2;
		Vector2 pControl;
		const float firstLegDuration = 0.2f;
		const float centerHoldDuration = 0.4f;
		float secondLegDuration = 0.5f;
		float totalDuration;
		bool trajectoryBuilt = false;

		static public void Create( VisualElement origin, VisualElement target, Func<VisualElement> innersGenerator, Style style = Style.CenterShowcase )
		{
			if ( origin == null || target == null || innersGenerator == null )
				return;

			var inners = innersGenerator();
			if ( inners == null )
				return;

			var flyupElement = Runtime.Game.GlobalConstantsHandler.UIConstants.generic.flyupTemplate.Instantiate();
			flyupElement.pickingMode = PickingMode.Ignore;
			var container = flyupElement.Q<VisualElement>( "Container" );
			container.Add( inners );

			new FlyupController( flyupElement, origin, target, style );
		}

		private FlyupController( VisualElement inners, VisualElement origin, VisualElement target, Style style )
		{
			this.origin = origin;
			this.target = target;
			this.inners = inners;
			this.style = style;

			head = Utility.UI.Head( origin );
			if ( head == null )
				return;

			root = new VisualElement();
			root.style.position = Position.Absolute;
			root.style.left = 0;
			root.style.right = 0;
			root.style.top = 0;
			root.style.bottom = 0;
			root.pickingMode = PickingMode.Ignore;
			root.userData = this;

			this.inners.style.position = Position.Absolute;
			this.inners.pickingMode = PickingMode.Ignore;

			root.Add( this.inners );
			head.Add( root );

			if ( style == Style.FadeToDestination )
				secondLegDuration *= 2f;
			totalDuration = firstLegDuration + centerHoldDuration + secondLegDuration;
			inners.style.opacity = 0;

			startTime = Time.unscaledTime;
			if ( style == Style.FadeToDestination )
				startTime -= firstLegDuration + centerHoldDuration;
			update = root.schedule.Execute( Tick ).Every( 15 );

			BuildTrajectory();
			UpdatePosition( 0 );
		}

		void BuildTrajectory()
		{
			if ( trajectoryBuilt )
				return;

			var targetBound = target.contentContainer.worldBound;

			p1 = new Vector2( Screen.width * 0.5f, Screen.height * 0.3f );
			p2 = targetBound.center;
			pControl = BuildControlPoint( p1, p2 );

			if ( float.IsNaN( p1.x ) || float.IsNaN( p1.y ) || float.IsNaN( p2.x ) || float.IsNaN( p2.y ) || float.IsNaN( pControl.x ) || float.IsNaN( pControl.y ) )
				return;

			trajectoryBuilt = true;
		}

		void Tick()
		{
			if ( origin == null || target == null || root == null || root.parent == null )
			{
				Exit();
				return;
			}

			if ( Utility.UI.Head( origin ) != head )
			{
				Exit();
				return;
			}

			BuildTrajectory();

			var elapsed = Time.unscaledTime - startTime;
			UpdatePosition( elapsed );

			if ( elapsed >= totalDuration )
				Exit();
		}

		void UpdatePosition( float elapsed )
		{
			if ( !trajectoryBuilt )
				return;

			var point = p1;
			var scale = 1.0f;
			var fadeIn = 1.0f;
			var fadeOut = 1.0f;

			if ( elapsed <= firstLegDuration )
			{
				var tFirst = Mathf.Clamp01( elapsed / firstLegDuration );
				fadeIn = EaseInOutCubic( tFirst );
			}
			else if ( elapsed <= firstLegDuration + centerHoldDuration )
			{
				point = p1;
			}
			else
			{
				var secondElapsed = elapsed - firstLegDuration - centerHoldDuration;
				var tSecond = Mathf.Clamp01( secondElapsed / secondLegDuration );
				var easedSecond = EaseInOutCubic( tSecond );
				point = EvaluateQuadraticBezier( p1, pControl, p2, easedSecond );
				if ( style == Style.FadeToDestination )
					fadeIn = EaseInOutCubic( tSecond );
				else
				{
					var progress = Mathf.Clamp01( elapsed / totalDuration );
					fadeOut = 1.0f; Mathf.Clamp01( 1.0f - Mathf.Max( 0.0f, progress - 0.8f ) / 0.2f );
				}
			}

			inners.style.left = new StyleLength( new Length( point.x, LengthUnit.Pixel ) );
			inners.style.top = new StyleLength( new Length( point.y, LengthUnit.Pixel ) );

			inners.style.scale = new StyleScale( new Scale( new Vector3( scale, scale, 1.0f ) ) );
			inners.style.opacity = Mathf.Clamp01( fadeIn * fadeOut );
		}

		Vector2 BuildControlPoint( Vector2 start, Vector2 end )
		{
			var toTarget = end - start;
			var distance = toTarget.magnitude;
			if ( distance <= 0.001f )
				return start;

			var direction = toTarget / distance;
			var angleOffset = UnityEngine.Random.Range( -25.0f, 25.0f );
			var startDirection = Rotate( direction, angleOffset );
			var controlDistance = distance * 0.35f;
			return start + startDirection * controlDistance;
		}

		Vector2 Rotate( Vector2 v, float degrees )
		{
			var radians = degrees * Mathf.Deg2Rad;
			var sin = Mathf.Sin( radians );
			var cos = Mathf.Cos( radians );
			return new Vector2(
				v.x * cos - v.y * sin,
				v.x * sin + v.y * cos
			);
		}

		Vector2 EvaluateQuadraticBezier( Vector2 a, Vector2 b, Vector2 c, float t )
		{
			var u = 1.0f - t;
			return ( u * u * a ) + ( 2.0f * u * t * b ) + ( t * t * c );
		}

		float EaseInOutCubic( float t )
		{
			if ( t < 0.5f )
				return 4.0f * t * t * t;

			var d = -2.0f * t + 2.0f;
			return 1.0f - ( d * d * d ) / 2.0f;
		}

		void Exit()
		{
			update?.Pause();
			update = null;
			root?.RemoveFromHierarchy();
		}
	}
}
