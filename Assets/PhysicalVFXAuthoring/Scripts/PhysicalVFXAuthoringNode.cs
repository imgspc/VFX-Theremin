using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicalVFXAuthoringNode : MonoBehaviour {

    [SerializeField]
    Color nodeColor = Color.white;

    void OnDrawGizmos () {
        Gizmos.color = nodeColor;
        Gizmos.DrawSphere (transform.position, 0.01f);
    }

}