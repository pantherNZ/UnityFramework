namespace Schema
{
	public enum TagType
	{
		AreaOfEffect = 0,
		CannotBeDamaged = 30,
		CannotConsumeEnergy = 31,
		CannotDropLoot = 32,
		Channel = 44,
		ChannelInfinite = 60,
		ChannelFocus = 45,
		Damage = 2,
		DamageDivine = 3,
		DamageDivineChaos = 4,
		DamageDivineDecay = 5,
		DamageDivineVoid = 6,
		DamageElemental = 7,
		DamageElementalFire = 8,
		DamageElementalCold = 9,
		DamageElementalLightning = 10,
		DamageSkillLaser = 11,
		DamageSkillNova = 12,
		DamageSkillProjectile = 13,
		Duration = 14,
		IgnoreStorageRequirements = 33,
		InertiaRelease = 54,
		LaserFluctuate = 77,
		LaserFocus = 40,
		LaserPierce = 75,
		LaserPierceBendable = 74,
		LaserPierceChain = 76,
		LaserPiercePrism = 80,
		LaserBurst = 79,
		LaserSplitStream = 41,
		LaserStationary = 72,
		LaserSustain = 78,
		LaserSweep = 34,
		LaserThrow = 73,
		Melee = 15,
		Mining = 16,
		Movement = 28,
		MovementTarget = 29,
		NovaAmplify = 69,
		NovaCascade = 56,
		NovaCascadeReverse = 57,
		NovaChannel = 62,
		NovaChannelGrow = 63,
		NovaChannelPaint = 64,
		NovaChannelShrink = 65,
		NovaDissipationNo = 67,
		NovaDissipationSingleTarget = 68,
		NovaDual = 66,
		NovaExpandOnRepeat = 55,
		NovaLingering = 59,
		NovaLingeringCreep = 70,
		NovaProject = 58,
		OnHitAppliedField = 49,
		OnHitAppliedLaser = 50,
		OnHitAppliedNova = 51,
		OnHitAppliedProjectile = 52,
		ProjectileAligned = 35,
		ProjectileFar = 46,
		ProjectileFork = 53,
		ProjectileHoming = 42,
		ProjectileNear = 47,
		ProjectileNova = 36,
		ProjectileOrb = 37,
		ProjectilePayload = 38,
		ProjectileReturning = 43,
		ProjectileSequenced = 39,
		ProjectileSwirling = 48,
		ProxyAutomated = 61,
		ProxyGrenade = 23,
		ProxyManual = 24,
		ProxyTurret = 25,
		ProxyTurretDrone = 26,
		ProxyVehicle = 27,
		SkillCostFree = 71,


		// Availables
		//MiningDysprosium = 17,
		//MiningLanthanum = 18,
		//MiningNeodymium = 19,
		//MiningPromethium = 20,
		//MiningOre = 21,
		//MiningTerrain = 22,

		CurrentStatCount = 81,
	}

	public abstract class TagUtility
	{
		public static string FormatTag( TagType type )
		{
			switch ( type )
			{
				case TagType.DamageElemental:
					return "Elemental";
				case TagType.DamageElementalFire:
					return "Fire";
				case TagType.DamageElementalCold:
					return "Cold";
				case TagType.DamageElementalLightning:
					return "Lightning";
				case TagType.DamageDivine:
					return "Divine";
				case TagType.DamageDivineChaos:
					return "Chaos";
				case TagType.DamageDivineDecay:
					return "Decay";
				case TagType.DamageDivineVoid:
					return "Void";
			}
			return type.ToString();
		}
	}
}
