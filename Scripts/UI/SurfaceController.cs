using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace UI
{
	public class SurfaceController
	{
		public abstract class CanvasSizer
		{
			public abstract void Apply( VisualElement canvas );
		}

		public class CanvasSizerFixed : CanvasSizer
		{
			public Vector2 size;

			public CanvasSizerFixed( Vector2 size )
			{
				this.size = size;
			}

			public override void Apply( VisualElement canvas )
			{
				canvas.style.width = size.x;
				canvas.style.height = size.y;
			}
		}

		public class CanvasSizerParentWidthRatio : CanvasSizer
		{
			VisualElement parent;
			public Vector2 ratio;

			public CanvasSizerParentWidthRatio( VisualElement parent, Vector2 ratio )
			{
				this.parent = parent;
				this.ratio = ratio;
			}

			public override void Apply( VisualElement canvas )
			{
				canvas.style.width = parent.resolvedStyle.width * ratio.x;
				canvas.style.height = parent.resolvedStyle.width * ratio.y;
			}
		}

		public class CanvasSizerParentHeightRatio : CanvasSizer
		{
			VisualElement parent;
			public Vector2 ratio;

			public CanvasSizerParentHeightRatio( VisualElement parent, Vector2 ratio )
			{
				this.parent = parent;
				this.ratio = ratio;
			}

			public override void Apply( VisualElement canvas )
			{
				canvas.style.width = parent.resolvedStyle.height * ratio.x;
				canvas.style.height = parent.resolvedStyle.height * ratio.y;
			}
		}

		VisualElement surface;
		VisualElement canvas;
		CanvasSizer canvasSizer;
		float zoom = 0;
		int zoomMaxRange = 5;
		int zoomMinRange = -5;
		float zoomDenominator = 2.5f;
		Vector2 mousePositionMove = new();
		Vector2? mousePositionDown;
		Vector2 canvasPositionDown;
		Vector2 canvasPositionTarget;
		float positionCloseInFactorPercent = 50.0f;
		float positionMinSpeed = 1.0f;
		float panSpeed = 20.0f;
		float zoomTarget = 1;
		Vector2 zoomTargetPosition;
		float zoomCloseInFactorPercent = 50.0f;
		float zoomMinSpeed = 0.01f;
		InputAction zoomAction;
		InputAction moveAction;
		bool inputActionsBound;
		float heldZoomDelta;
		public bool restrictZoomToCanvasSize = true;

		public void SetBinding( VisualElement rootElement )
		{
			surface = rootElement;
			surface.userData = this;
			canvas = rootElement.Q<VisualElement>( "SurfaceCanvas" );
			UnityEngine.Debug.Assert( canvas != null, "SurfaceCanvas not found" );
			surface.RegisterCallback<MouseDownEvent>( OnMouseDown, TrickleDown.TrickleDown );
			surface.RegisterCallback<MouseUpEvent>( OnMouseUp, TrickleDown.TrickleDown );
			surface.RegisterCallback<MouseMoveEvent>( OnMouseMove, TrickleDown.TrickleDown );
			surface.RegisterCallback<WheelEvent>( OnWheel, TrickleDown.TrickleDown );
			surface.RegisterCallback<DetachFromPanelEvent>( OnDetachFromPanel );

			zoomAction = InputSystem.actions.FindAction( "UI/ScrollWheel" );
			moveAction = InputSystem.actions.FindAction( "UI/Look" );
			BindInputActions();
		}

		public void SetData( CanvasSizer canvasSizer, Vector2 startPosition, float zoom )
		{
			this.canvasSizer = canvasSizer;
			SetCanvasPosition( startPosition );
			this.zoom = zoom;
			zoomTarget = Mathf.Pow( 2.0f, zoom / zoomDenominator );
			SetCanvasScale( zoomTarget );
			surface.schedule.Execute( Update ).Every( 1000 / 60 );
		}

		public Vector2 GetPosition() => GetCanvasPosition();

		public float GetZoom()
		{
			return zoom;
		}

		public float GetScale() => GetCanvasScale();

		public Vector2 GetCursorPosition()
		{
			Vector2 surfaceSize = new( surface.resolvedStyle.width, surface.resolvedStyle.height );
			if ( float.IsNaN( surfaceSize.x ) || float.IsNaN( surfaceSize.y ) )
				return Vector2.zero;
			var cursor = mousePositionMove - surfaceSize / 2 - GetCanvasPosition();
			cursor.y = -cursor.y;
			return cursor;
		}

		void Update()
		{
			// Keep cursor position fresh even when no mouse move event is fired.
			mousePositionMove = GetSurfaceMousePosition();

			canvasSizer.Apply( canvas );
			Vector2 surfaceSize = new( surface.resolvedStyle.width, surface.resolvedStyle.height );
			Vector2 canvasSize = new( canvas.resolvedStyle.width, canvas.resolvedStyle.height );
			if ( float.IsNaN( surfaceSize.x ) || float.IsNaN( surfaceSize.y ) )
				return;
			if ( float.IsNaN( canvasSize.x ) || float.IsNaN( canvasSize.y ) )
				return;
			if ( canvasSize.x == 0 || canvasSize.y == 0 )
				return;

			if ( mousePositionDown == null )
			{
				canvasPositionDown = GetCanvasPosition();
				canvasPositionTarget = GetCanvasPosition();
			}

			// Poll moveAction every frame so held gamepad sticks pan continuously
			if ( moveAction != null && InputTypeTracker.Instance.currentType == InputTypeTracker.InputType.Gamepad )
			{
				var moveDelta = moveAction.ReadValue<Vector2>();
				if ( moveDelta != Vector2.zero )
				{
					canvasPositionDown = GetCanvasPosition();
					moveDelta.x = -moveDelta.x;
					canvasPositionTarget = canvasPositionDown + moveDelta * panSpeed;
				}
			}

			// Apply zoom every frame while zoom input is held (gamepad triggers or mouse wheel)
			if ( !Mathf.Approximately( heldZoomDelta, 0f ) )
			{
				if ( IsMouseOverSurface( mousePositionMove ) )
					ApplyZoomDelta( heldZoomDelta, mousePositionMove );
			}

			var currentPos = GetCanvasPosition();
			var delta = canvasPositionTarget - currentPos;
			if ( delta != Vector2.zero )
			{

				delta *= positionCloseInFactorPercent / 100.0f;
				if ( delta.magnitude > positionMinSpeed )
					SetCanvasPosition( currentPos + delta );
				else
					SetCanvasPosition( canvasPositionTarget );
			}

			if ( restrictZoomToCanvasSize )
			{
				var minZoom = Mathf.Min( 1.0f, Mathf.Max( surfaceSize.x / canvasSize.x, surfaceSize.y / canvasSize.y ) );
				if ( zoomTarget < minZoom )
				{
					zoomTarget = minZoom;
					zoom = Mathf.RoundToInt( zoomDenominator * Mathf.Log( zoomTarget, 2.0f ) );
					SetCanvasScale( zoomTarget );
				}
			}

			var currentScale = GetCanvasScale();
			if ( !Mathf.Approximately( zoomTarget, currentScale ) )
			{
				var previousScale = currentScale;
				var deltaZoom = ( zoomTarget - currentScale ) * ( zoomCloseInFactorPercent / 100.0f );
				var newScale = Mathf.Abs( deltaZoom ) > zoomMinSpeed ? currentScale + deltaZoom : zoomTarget;
				SetCanvasScale( newScale );
				var zoomOffset = zoomTargetPosition - GetCanvasPosition();
				zoomOffset *= newScale / previousScale;
				zoomOffset -= zoomTargetPosition;
				SetCanvasPosition( new Vector2( -zoomOffset.x, -zoomOffset.y ) );
			}

			currentPos = GetCanvasPosition();
			currentScale = GetCanvasScale();
			var topLeft = currentPos - canvasSize * currentScale / 2 + surfaceSize / 2;
			var bottomRight = currentPos + canvasSize * currentScale / 2 - surfaceSize / 2;
			if ( canvasSize.x * currentScale < surfaceSize.x )
				SetCanvasPosition( new Vector2( 0, currentPos.y ) );
			else if ( topLeft.x > 0 )
				SetCanvasPosition( new Vector2( currentPos.x - topLeft.x, currentPos.y ) );
			else if ( bottomRight.x < 0 )
				SetCanvasPosition( new Vector2( currentPos.x - bottomRight.x, currentPos.y ) );

			currentPos = GetCanvasPosition();
			// Recompute bounds after potential X correction
			topLeft = currentPos - canvasSize * currentScale / 2 + surfaceSize / 2;
			bottomRight = currentPos + canvasSize * currentScale / 2 - surfaceSize / 2;
			if ( canvasSize.y * currentScale < surfaceSize.y )
				SetCanvasPosition( new Vector2( currentPos.x, 0 ) );
			else if ( topLeft.y > 0 )
				SetCanvasPosition( new Vector2( currentPos.x, currentPos.y - topLeft.y ) );
			else if ( bottomRight.y < 0 )
				SetCanvasPosition( new Vector2( currentPos.x, currentPos.y - bottomRight.y ) );
		}

		void OnMouseDown( MouseDownEvent evt )
		{
			if ( evt.button == ( int )MouseButton.LeftMouse )
			{
				// Don't start a drag if the click originated on a map pin
				var target = evt.target as VisualElement;
				var current = target;
				while ( current != null )
				{
					if ( current.userData is UI.Outpost.Gate.MapPinController )
						return;
					current = current.parent;
				}

				mousePositionDown = GetSurfaceLocalMousePosition( evt.mousePosition );
				canvasPositionDown = GetCanvasPosition();
				surface.CaptureMouse();
			}
		}

		void OnMouseUp( MouseUpEvent evt )
		{
			if ( evt.button == ( int )MouseButton.LeftMouse )
			{
				mousePositionDown = null;
				surface.ReleaseMouse();
			}
		}

		void OnMouseMove( MouseMoveEvent evt )
		{
			mousePositionMove = GetSurfaceLocalMousePosition( evt.mousePosition );
			if ( mousePositionDown != null )
			{
				Vector2 delta = mousePositionMove - mousePositionDown.Value;
				canvasPositionTarget = canvasPositionDown + delta;
			}
		}

		void OnWheel( WheelEvent evt )
		{
			mousePositionMove = GetSurfaceLocalMousePosition( evt.mousePosition );
		}

		void BindInputActions()
		{
			if ( inputActionsBound )
				return;

			if ( zoomAction != null )
			{
				zoomAction.started += OnZoomStarted;
				zoomAction.canceled += OnZoomCanceled;
				zoomAction.Enable();
			}

			if ( moveAction != null )
				moveAction.Enable();

			inputActionsBound = true;
		}

		void UnbindInputActions()
		{
			if ( !inputActionsBound )
				return;

			if ( zoomAction != null )
			{
				zoomAction.started -= OnZoomStarted;
				zoomAction.canceled -= OnZoomCanceled;
			}

			inputActionsBound = false;
		}

		void OnDetachFromPanel( DetachFromPanelEvent evt )
		{
			UnbindInputActions();
		}

		// Set heldZoomDelta when zoom input starts (gamepad trigger pressed or scroll wheel moved)
		void OnZoomStarted( InputAction.CallbackContext ctx )
		{
			heldZoomDelta = ctx.ReadValue<Vector2>().y;
		}

		// Clear heldZoomDelta when zoom input is released
		void OnZoomCanceled( InputAction.CallbackContext ctx )
		{
			heldZoomDelta = 0f;
		}

		void ApplyZoomDelta( float deltaY, Vector2 localMousePosition )
		{
			Vector2 surfaceSize = new( surface.resolvedStyle.width, surface.resolvedStyle.height );
			Vector2 canvasSize = new( canvas.resolvedStyle.width, canvas.resolvedStyle.height );
			if ( float.IsNaN( surfaceSize.x ) || float.IsNaN( surfaceSize.y ) )
				return;
			if ( float.IsNaN( canvasSize.x ) || float.IsNaN( canvasSize.y ) )
				return;
			if ( canvasSize.x == 0 || canvasSize.y == 0 )
				return;

			var steps = Mathf.Clamp( deltaY, -10, 10 );
			zoom += steps;
			zoom = Mathf.Clamp( zoom, zoomMinRange, zoomMaxRange );
			zoomTarget = Mathf.Pow( 2.0f, zoom / zoomDenominator );
			zoomTargetPosition = localMousePosition - surfaceSize / 2;

			if ( restrictZoomToCanvasSize )
			{
				var minZoom = Mathf.Max( surfaceSize.x / canvasSize.x, surfaceSize.y / canvasSize.y );
				if ( zoomTarget < minZoom )
				{
					zoomTarget = minZoom;
					zoom = Mathf.RoundToInt( zoomDenominator * Mathf.Log( zoomTarget, 2.0f ) );
				}
			}
		}

		Vector2 GetSurfaceMousePosition()
		{
			if ( surface == null || surface.panel == null || Mouse.current == null )
				return mousePositionMove;

			var screenPos = Mouse.current.position.ReadValue();
			screenPos.y = Screen.height - screenPos.y; // Invert Y to match UI coordinates
			var panelPos = RuntimePanelUtils.ScreenToPanel( surface.panel, screenPos );
			return GetSurfaceLocalMousePosition( panelPos );
		}

		Vector2 GetSurfaceLocalMousePosition( Vector2 panelMousePosition )
		{
			if ( surface == null || surface.panel == null )
				return panelMousePosition;

			return surface.panel.visualTree.ChangeCoordinatesTo( surface, panelMousePosition );
		}

		bool IsMouseOverSurface( Vector2 localMousePosition )
		{
			var width = surface.resolvedStyle.width;
			var height = surface.resolvedStyle.height;
			if ( float.IsNaN( width ) || float.IsNaN( height ) )
				return false;
			return localMousePosition.x >= 0f && localMousePosition.y >= 0f && localMousePosition.x <= width && localMousePosition.y <= height;
		}

		// Helpers to read/write position and scale using non-obsolete UI Toolkit APIs
		Vector2 GetCanvasPosition()
		{
			var t = canvas.resolvedStyle.translate;
			return new Vector2( t.x, t.y );
		}

		void SetCanvasPosition( Vector2 pos )
		{
			canvas.style.translate = new Translate( new Length( pos.x, LengthUnit.Pixel ), new Length( pos.y, LengthUnit.Pixel ), 0f );
		}

		float GetCanvasScale()
		{
			var s = canvas.resolvedStyle.scale;
			return s.value.x;
		}

		void SetCanvasScale( float scale )
		{
			canvas.style.scale = new Scale( new Vector3( scale, scale, 1f ) );
		}
	}
}
