using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plane : MonoBehaviour
{
    public Vector3 normal => transform.up;
    public Vector3 right => transform.right;
    public Vector3 forward => transform.forward;
    public Vector3 position => transform.position;
    public Vector2 size => new Vector2(transform.localScale.x * 10f, transform.localScale.z * 10f);

    public bool ground = false;

}
