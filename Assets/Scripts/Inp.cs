using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Small input wrapper so the project works with both the old and the new Unity input system.
public static class Inp
{
    public static float Vertical()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return 0f;
        float v = 0f;
        if (k.wKey.isPressed || k.upArrowKey.isPressed) v += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed) v -= 1f;
        return v;
#else
        return Input.GetAxisRaw("Vertical");
#endif
    }

    public static float Horizontal()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return 0f;
        float h = 0f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) h += 1f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) h -= 1f;
        return h;
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }

    public static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }

    // n = 0, 1, 2 for keys 1, 2, 3
    public static bool ScenarioKeyPressed(int n)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;
        if (n == 0) return k.digit1Key.wasPressedThisFrame;
        if (n == 1) return k.digit2Key.wasPressedThisFrame;
        if (n == 2) return k.digit3Key.wasPressedThisFrame;
        return false;
#else
        return Input.GetKeyDown(KeyCode.Alpha1 + n);
#endif
    }

    public static bool CameraTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.C);
#endif
    }
}
