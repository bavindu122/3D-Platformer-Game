using System.Linq;
using UnityEngine;

public static class AnimatorExtensions
{
    public static bool HasParameterOfType(this Animator animator, string name, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        return animator.parameters.Any(p => p.name == name && p.type == type);
    }
}
