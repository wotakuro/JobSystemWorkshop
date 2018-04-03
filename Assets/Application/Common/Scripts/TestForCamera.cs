using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestForCamera : MonoBehaviour {
    public float rotTime = 20.0f;
    public float distance = 20.0f;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        float angleP = (Time.timeSinceLevelLoad) * Mathf.PI / rotTime + Mathf.PI;
        transform.position = new Vector3(Mathf.Sin(angleP) * distance, transform.position.y, Mathf.Cos(angleP) * distance);
        transform.LookAt(new Vector3(0, 0, 0));
	}
}
