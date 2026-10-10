using UnityEngine;

// Shared layout of the city grid. Roads run along x and z at the coordinates in Roads.
public static class City
{
    public const float HalfWidth = 7f;        // half of the road width (14 m roads)
    public const float Extent = 258f;         // roads run from -Extent to +Extent
    public const float StopDistance = 11.5f;  // stop line distance from the intersection center
    public static readonly float[] Roads = { -200f, -100f, 0f, 100f, 200f };

    // True while p is on paved road (any road strip, including intersections).
    public static bool OnRoad(Vector3 p, float margin = 0.3f)
    {
        if (Mathf.Abs(p.x) > Extent || Mathf.Abs(p.z) > Extent) return false;
        float hw = HalfWidth + margin;
        foreach (float r in Roads)
            if (Mathf.Abs(p.z - r) <= hw || Mathf.Abs(p.x - r) <= hw) return true;
        return false;
    }
}
