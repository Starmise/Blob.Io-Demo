using UnityEngine;

/// <summary>
/// Virtual joystick writes here; <see cref="PlayerController"/> reads when active.
/// </summary>
public static class MobileMoveInput
{
    public static Vector2 Axis { get; private set; }
    public static bool IsAxisActive => Axis.sqrMagnitude > 0.0025f;

    public static void SetAxis(Vector2 a) => Axis = a;
    public static void Clear() => Axis = Vector2.zero;
}
