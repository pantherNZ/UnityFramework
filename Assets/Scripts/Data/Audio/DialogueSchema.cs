using Runtime.Game;
using UnityEngine;

namespace Schema.Story
{
	[CreateAssetMenu(fileName = "DialogueSchema", menuName = "NQ/Dialogue")]
	public class DialogueSchema : Audio.AudioDataSchema
	{
		[TextArea(3, 10)]
		public string text;
		[Tooltip("If true, we don't mark them as played, don't interrupt or queue")]
		public bool isSimpleDialogue = false;
		[Range(0.0f, 1.0f)]
		public float markAsHeardAfterPercentPlayed = 0.5f;
	}
}
