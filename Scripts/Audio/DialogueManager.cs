using System;
using System.Collections.Generic;
using Runtime.Game;
using Schema;
using Schema.Story;

namespace Runtime.Audio
{
	// Listens to the player controller's dialogue requests and manages queuing, chaining, and interruption of dialogue audio.
	// Will omit global events for when the dialogue or lore is actually palyed
	public class DialogueManager : Memory.MonoSingleton<DialogueManager>
	{
		SfxManager.AudioInstance currentAudioInstance;
		Utility.FunctionTimer queuedDialogTimer;
		Queue<Schema.Story.DialogueSchema> queuedDialogues = new();

		public Audio.SfxManager.AudioInstance RequestDialogue( Schema.AudioType dialogue, bool ignoreAlreadyPlayedChecks = false, bool queueIfCurrentlyPlaying = true )
		{
			if ( dialogue.GetSchema() is Schema.Story.DialogueSchema dialogueSchema )
				return RequestDialogue( dialogueSchema, ignoreAlreadyPlayedChecks, queueIfCurrentlyPlaying );
			else
				UnityEngine.Debug.LogWarning( $"Tried to play dialogue but provided AudioType was not a DialogueSchema: {dialogue}" );
			return null;
		}

		public Audio.SfxManager.AudioInstance RequestDialogue( Schema.Story.DialogueSchema dialogue, bool ignoreAlreadyPlayedChecks = false, bool queueIfCurrentlyPlaying = true )
			=> OnDialogueRequest( dialogue, ignoreAlreadyPlayedChecks, queueIfCurrentlyPlaying );

		public Audio.SfxManager.AudioInstance RequestLore( Schema.Story.LoreSchema lore, int page )
		 	=> OnLoreRequest( lore, page );


		Audio.SfxManager.AudioInstance OnDialogueRequest( Schema.Story.DialogueSchema dialogue, bool ignoreAlreadyPlayedChecks, bool queueIfCurrentlyPlaying = true )
		{
			if ( dialogue == null )
				return null;

			var discoverySave = GlobalConstantsHandler.RuntimeConstants.discoverySave;
			var originalDialogue = dialogue;

			if ( SfxManager.Instance.IsOnCooldown( dialogue ) )
				return null;

			if ( DataManager.Instance.GroupedAudioClips.TryGetValue( dialogue, out var group ) )
			{
				if ( ignoreAlreadyPlayedChecks || dialogue.isSimpleDialogue )
				{
					dialogue = group[UnityEngine.Random.Range( 0, group.Count )] as Schema.Story.DialogueSchema;
				}
				else
				{
					var selector = new WeightedSelector<DialogueSchema>();
					foreach ( var groupDialogue in group )
						if ( groupDialogue is Schema.Story.DialogueSchema groupDialogueSchema )
							if ( !discoverySave.HasDialogueBeenHeard( groupDialogueSchema ) )
								selector.AddItem( groupDialogueSchema, 1 );
					dialogue = selector.GetResult();
				}
			}
			else
			{
				if ( !ignoreAlreadyPlayedChecks && discoverySave.HasDialogueBeenHeard( dialogue ) )
					return null;
			}

			if ( dialogue == null )
				return null;

			if ( queuedDialogues.Contains( dialogue ) )
				return null;

			if ( !queueIfCurrentlyPlaying && queuedDialogTimer != null )
				return null;

			//if ( currentAudioInstance != null )
			//	Debug.Log( $"Current audio instance: IsPlaying {currentAudioInstance.IsAudioPlaying()} - isPlaying: {currentAudioInstance.playing} - source.isPlaying: {currentAudioInstance.source.isPlaying} - fadingin: {currentAudioInstance.fadingIn} - fadingOut: {currentAudioInstance.fadingOut} - data: {currentAudioInstance.data.name}" );

			// Already playing dialogue, queue this one
			if ( currentAudioInstance != null &&
				currentAudioInstance.IsAudioPlaying( ignoreFadingOut: true ) &&
				currentAudioInstance.data is Schema.Story.DialogueSchema )
			{
				if ( dialogue == currentAudioInstance.data || dialogue.isSimpleDialogue || !queueIfCurrentlyPlaying )
					return null;

				if ( !queuedDialogues.Contains( dialogue ) )
				{
					queuedDialogues.Enqueue( dialogue );
					if ( queuedDialogues.Count == 1 )
						currentAudioInstance.onFinished += OnNarratorFinished;
				}

				return null;
			}

			var dialogueAudio = PlayDialogueAudio( dialogue, ignoreAlreadyPlayedChecks );
			Runtime.Events.DialoguePlayed.Trigger( new Runtime.Events.DialoguePlayed { dialogue = dialogue, audioInstance = dialogueAudio } );
			SfxManager.Instance.RecordCooldown( originalDialogue );

			return dialogueAudio;
		}

		Audio.SfxManager.AudioInstance OnLoreRequest( Schema.Story.LoreSchema lore, int index )
		{
			if ( lore == null || index < 0 || index >= lore.entries.Count )
				return null;
			var loreAudio = PlayDialogueAudio( lore.entries[index]?.audioData );
			Runtime.Events.LorePlayed.Trigger( new Runtime.Events.LorePlayed { lore = lore, loreIndex = index, audioInstance = loreAudio } );
			return loreAudio;
		}

		Audio.SfxManager.AudioInstance PlayDialogueAudio( Schema.Audio.AudioDataSchema audio, bool ignoreDialogueAlreadyPlayedChecks = false )
		{
			if ( audio == null )
				return null;

			if ( currentAudioInstance != null &&
				currentAudioInstance.data == audio &&
				currentAudioInstance.IsAudioPlaying() )
				return null;

			bool playNarratorInterruptAudio = false;
			float startDelaySec = GlobalConstantsHandler.FTUEConstants.loreReadDelay;
			if ( currentAudioInstance != null &&
				currentAudioInstance.source != null &&
				currentAudioInstance.source.isPlaying )
			{
				currentAudioInstance.onFinished -= OnAudioFinished;
				currentAudioInstance.onChained -= OnDialogueChained;

				if ( currentAudioInstance.data is Schema.Story.DialogueSchema schema &&
					audio is not Schema.Story.DialogueSchema &&
					!schema.isSimpleDialogue &&
					Utility.DefaultRng.Roll( GlobalConstantsHandler.FTUEConstants.narratorRespondToInterruptionChance ) )
				{
					playNarratorInterruptAudio = true;
				}

				Audio.SfxManager.Stop( currentAudioInstance, GlobalConstantsHandler.FTUEConstants.loreFadeoutTimeSec );
				startDelaySec = GlobalConstantsHandler.FTUEConstants.loreFadeoutTimeSec;
			}

			audio.startDelaySec = startDelaySec;

			if ( audio is Schema.Story.DialogueSchema dialogue )
			{
				// We handle dialogue audio through discoverySave to manage already-played checks, etc. Would be nice to unify with the lore audio system later.
				var discoverySave = GlobalConstantsHandler.RuntimeConstants.discoverySave;
				currentAudioInstance = discoverySave.PlayDialogue( dialogue, ignoreDialogueAlreadyPlayedChecks );
			}
			else
			{
				currentAudioInstance = Audio.SfxManager.Instance.PlayDialogue( audio );
			}

			if ( currentAudioInstance == null )
				return null;

			currentAudioInstance.onFinished += OnAudioFinished;
			currentAudioInstance.onChained += OnDialogueChained;

			if ( playNarratorInterruptAudio )
			{
				currentAudioInstance.onFinished += OnAudioInterrupted;
				currentAudioInstance.onCancelled += OnAudioInterrupted;
			}

			return currentAudioInstance;
		}

		private void OnAudioFinished( Audio.SfxManager.AudioInstance instance )
		{
			if ( instance.data.automaticChainTo == null )
			{
				instance.onFinished -= OnAudioFinished;
				instance.onChained -= OnDialogueChained;
				currentAudioInstance = null;
				Runtime.Events.DialogueOrLoreFinished.Trigger( new Runtime.Events.DialogueOrLoreFinished { instance = instance } );
			}
		}

		private void OnAudioInterrupted( Audio.SfxManager.AudioInstance instance )
		{
			if ( instance.data.automaticChainTo == null )
			{
				Utility.FunctionTimer.CreateTimer( GlobalConstantsHandler.FTUEConstants.queueDialogueDelaySec, () =>
				{
					GlobalConstantsHandler.FTUEConstants.interruptedDialogues.RandomShuffle();
					var discoverySave = GlobalConstantsHandler.RuntimeConstants.discoverySave;

					foreach ( var dialogue in GlobalConstantsHandler.FTUEConstants.interruptedDialogues )
					{
						if ( !discoverySave.HasDialogueBeenHeard( dialogue ) )
						{
							OnDialogueRequest( dialogue, ignoreAlreadyPlayedChecks: true );
							break;
						}
					}
				} );
			}

			instance.onFinished -= OnAudioInterrupted;
			instance.onCancelled -= OnAudioInterrupted;
		}

		private void OnDialogueChained( Audio.SfxManager.AudioInstance instance )
		{
			if ( instance.data is Schema.Story.DialogueSchema dialogue )
				Runtime.Events.DialoguePlayed.Trigger( new Runtime.Events.DialoguePlayed { dialogue = dialogue, audioInstance = instance } );
		}

		private void OnNarratorFinished( Audio.SfxManager.AudioInstance instance )
		{
			if ( instance.data.automaticChainTo != null )
				return;

			if ( queuedDialogTimer != null )
				return;

			queuedDialogTimer = Utility.FunctionTimer.CreateTimer( GlobalConstantsHandler.FTUEConstants.queueDialogueDelaySec, () =>
			{
				if ( queuedDialogues.Count > 0 )
				{
					var nextDialogue = queuedDialogues.Dequeue();
					OnDialogueRequest( nextDialogue, ignoreAlreadyPlayedChecks: false );

					if ( queuedDialogues.Count == 0 )
						instance.onFinished -= OnNarratorFinished;
				}

				queuedDialogTimer = null;
			} );
		}

		public void Stop()
		{
			if ( currentAudioInstance != null )
			{
				if ( currentAudioInstance.data is Schema.Story.DialogueSchema dialogue )
				{
					GlobalConstantsHandler.RuntimeConstants.discoverySave.MarkDialogueHeard( dialogue );
				}

				Audio.SfxManager.Stop( currentAudioInstance, GlobalConstantsHandler.FTUEConstants.loreFadeoutTimeSec, playNextInChain: true );

				if ( !currentAudioInstance.betweenChains )
				{
					currentAudioInstance.onFinished -= OnAudioFinished;
					currentAudioInstance.onChained -= OnDialogueChained;
					currentAudioInstance = null;
				}
			}
		}

		public void CancelQueuedDialogues()
		{
			if ( queuedDialogTimer != null )
			{
				queuedDialogTimer.Stop();
				queuedDialogTimer = null;
			}
			queuedDialogues.Clear();
		}
	}
}
