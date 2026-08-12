using System;
using Runtime.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
	public class Toast
	{
		public enum CloseMode
		{
			Timeout,
			TimeoutOrClick,
		}

		static string lastToastMessage = "";
		static float lastToastTime = 0;

		public static void Prompt( VisualElement root, string text )
		{
			root = Utility.UI.Head( root );
			if ( Time.fixedTime - lastToastTime < 1.0f && text == lastToastMessage )
				return;

			lastToastMessage = text;
			lastToastTime = Time.fixedTime;

			var label = new Label();
			label.text = text;
			label.styleSheets.Add( Resources.Load<StyleSheet>( "UI/USS/Generic" ) );
			label.AddToClassList( "CenteredLabel" );
			Custom( root, label, CloseMode.TimeoutOrClick );
		}

		public static void Custom( VisualElement root, VisualElement visualElement, CloseMode closeMode = CloseMode.Timeout )
		{
			var popupController = new ToastController( visualElement, closeMode );
			popupController.root = root;
			root.Add( popupController.selfView );
		}

		private Toast() { }
	}

	internal class ToastController
	{
		internal VisualElement root;
		internal VisualElement selfView;
		public bool playAudio = true;
		IVisualElementScheduledItem exitSchedule;

		internal ToastController( VisualElement childView, Toast.CloseMode closeMode )
		{
			var popupTemplate = Runtime.Game.GlobalConstantsHandler.UIConstants.helper.toastTemplate;
			selfView = popupTemplate.Instantiate();
			selfView.pickingMode = PickingMode.Ignore;
			selfView.style.position = Position.Absolute;
			selfView.style.left = 0;
			selfView.style.right = 0;
			selfView.style.top = 0;
			selfView.style.bottom = 0;
			selfView.userData = this;

			var frame = selfView.Q<VisualElement>( "Frame" );
			if ( closeMode == Toast.CloseMode.TimeoutOrClick )
				frame.RegisterCallback<ClickEvent>( evt => ExitToast() );
			frame.Add( childView );
			frame.schedule.Execute( () => frame.AddToClassList( "Toast_End" ) ).StartingIn( 3500 );
			exitSchedule = selfView.schedule.Execute( ExitToast ).StartingIn( 5500 );

			if ( playAudio )
				SfxManager.Instance.PlayUI( Schema.AudioType.ButtonPressGenericClose );
		}

		public virtual void ExitToast()
		{
			exitSchedule.Pause();
			root.Remove( selfView );
		}
	}
}
