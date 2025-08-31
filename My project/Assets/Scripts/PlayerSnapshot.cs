using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AnimatorFrame
{
    public int fullPathHash;
    public float normalizedTime;
    public float speedParam;
    public bool groundedParam;
    public bool landingParam;
}

[System.Serializable]
public class PlayerSnapshot
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public List<AnimatorFrame> animators = new List<AnimatorFrame>(8);
}
