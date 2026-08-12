using UnityEngine.UIElements;
using System.Collections.Generic;

namespace UI
{
	public class LoadingScreenController
	{
		VisualElement rootVisualElement;
		List<Utility.UI.RepeatTransition> barRepeaters = new();
		bool visible;
		bool animationActive;

		public void Bind( VisualElement rootVisualElement )
		{
			this.rootVisualElement = rootVisualElement;
			rootVisualElement.RegisterCallback<GeometryChangedEvent>( OnGeometryChanged );

			if ( visible )
				Show();
			else
				Hide();
		}

		private void OnGeometryChanged( GeometryChangedEvent evt )
		{
			OnVisibleChanged();
		}

		public void Show()
		{
			visible = true;
			if ( rootVisualElement != null )
			{
				rootVisualElement.visible = true;
				OnVisibleChanged();
			}
		}

		public void Hide()
		{
			visible = false;
			if ( rootVisualElement != null )
			{
				rootVisualElement.visible = false;
				OnVisibleChanged();
			}
		}

		private void OnVisibleChanged()
		{
			if ( barRepeaters.Count == 0 )
				SetupBars();

			if ( rootVisualElement.visible && !animationActive )
			{
				animationActive = true;
				foreach ( var barRepeater in barRepeaters )
					barRepeater.Activate();
			}
			else if ( !rootVisualElement.visible && animationActive )
			{
				animationActive = false;
				foreach ( var barRepeater in barRepeaters )
					barRepeater.Deactivate();
			}
		}

		void SetupBars()
		{
			for ( int i = 1; i <= 6; ++i )
			{
				var barElement = rootVisualElement.Q<VisualElement>( $"Bar{i}" );
				if ( barElement == null )
					break;

				Utility.UI.RepeatTransition barRepeater = new( barElement, "LoadingBarAnimation" );
				barRepeaters.Add( barRepeater );
			}
		}
	}
}
