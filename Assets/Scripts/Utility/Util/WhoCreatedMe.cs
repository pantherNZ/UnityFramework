using UnityEngine;

public class WhoCreatedMe : MonoBehaviour
{
	void OnEnable()
	{
		Debug.Log("My name is " + gameObject.name);
	}
}
