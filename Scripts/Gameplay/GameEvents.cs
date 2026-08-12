using System;
using UnityEngine.InputSystem;

namespace Runtime.Events
{
	// Player
	public class PlayerBoundSkill : BaseEvent<PlayerBoundSkill> { public InputAction action; }
	public class PlayerUnboundSkill : BaseEvent<PlayerUnboundSkill> { public int oldSlot; }
	public class PlayerJoinedGame : BaseEvent<PlayerJoinedGame> { public Game.PlayerController player; }
	public class PlayerLeftGame : BaseEvent<PlayerLeftGame> { public Game.PlayerController player; }
	public class PlayerCombatStatusChanged : BaseEvent<PlayerCombatStatusChanged> { public Game.PlayerController player; public bool isInCombat; }

	// Terrain
	public class WorldLoadComplete : BaseEvent<WorldLoadComplete> { }

	public class EnteredSpawnArea : BaseEvent<EnteredSpawnArea> { public Game.Outpost outpost; public Game.PlayerController player; }
	public class ExitedSpawnArea : BaseEvent<ExitedSpawnArea> { public Game.Outpost outpost; public Game.PlayerController player; }

	public class ChunkLoaded : BaseEvent<ChunkLoaded> { public Terrain.Chunk chunk; }
	public class ChunkUnloaded : BaseEvent<ChunkUnloaded> { public Terrain.Chunk chunk; }
	public class ChunksLoaded : BaseEvent<ChunksLoaded> { }
	public class SourceChunkUpdated : BaseEvent<SourceChunkUpdated> { public Terrain.Chunk chunk; }

	// World
	public class MonsterSpawnedEvent : BaseEvent<MonsterSpawnedEvent> { public Monsters.Monster monster; }
	public class MonsterDepawnedEvent : BaseEvent<MonsterDepawnedEvent> { public Monsters.Monster monster; }

	// General
	public class SceneChanging : BaseEvent<SceneChanging> { public Game.GameSceneManager.SceneData newScene; public Game.GameSceneManager.SceneData oldScene; }
	public class RequestGlobalSave : BaseEvent<RequestGlobalSave> { }

	public class ItemSpawned : BaseEvent<ItemSpawned> { public Game.GroundItem itemObject; }
	public class ItemPickedUp : BaseEvent<ItemPickedUp> { public Game.PlayerController player; public Game.GroundItem itemObject; }

	public class PopupOpened : BaseEvent<PopupOpened> { public Schema.FTUE.SlideShowSchema slideShow; }
	public class PopupClosed : BaseEvent<PopupClosed> { public Schema.FTUE.SlideShowSchema slideShow; }

	public class HudHovered : BaseEvent<HudHovered> { public bool hovered; }
	public class RequestCameraShake : BaseEvent<RequestCameraShake> { public Utility.ShakeParams? shakeParams; }

	// Game State
	public class GamePaused : BaseEvent<GamePaused> { public bool showPauseUi; }
	public class GameResumed : BaseEvent<GameResumed> { }
	public class SettingsModified : BaseEvent<SettingsModified> { }

	// Audio
	public class LorePlayed : BaseEvent<LorePlayed> { public Schema.Story.LoreSchema lore; public int loreIndex; public Audio.SfxManager.AudioInstance audioInstance; }
	public class DialoguePlayed : BaseEvent<DialoguePlayed> { public Schema.Story.DialogueSchema dialogue; public Audio.SfxManager.AudioInstance audioInstance; }
	public class DialogueOrLoreFinished : BaseEvent<DialogueOrLoreFinished> { public Audio.SfxManager.AudioInstance instance; }
}
