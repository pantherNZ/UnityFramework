
using System;
using Runtime.Combat;
using Schema;

namespace Runtime.Events
{
	public class StatChangedEventArgs : EventArgs
	{
		public Schema.StatType type;
		public float newValue;
		public float delta;
	}

	public class StatSourceAddedEventArgs : EventArgs
	{
		public StatsDataSource source;
	}

	public class HealthChangedEventArgs : EventArgs
	{
		public Game.Health healthComponent;
		public float newHealth;
		public float oldHealth;
		public float delta;
		public float lifeDelta;
		public float barrierDelta;
	}

	public class DeathEventArgs : EventArgs
	{
		public Game.Health healthComponent;
		public float delta;
		public float lifeDelta;
		public float barrierDelta;
	}

	public class TerrainDamagedArgs
	{
		public Terrain.TerrainDamageable terrainObject;
		public CombatContext context;
		public Terrain.ModifyResult result;
	}
}
