using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Runtime.Game
{
	public class VisualRemoteController : MonoBehaviour
	{
		[SerializeField] List<ParticleSystem> particleSystems;

		void OnEnable()
		{
			foreach ( var ps in particleSystems )
				ps.Play();
		}

		void OnDisable()
		{
			foreach ( var ps in particleSystems )
				ps.Stop();
		}
	}
}
