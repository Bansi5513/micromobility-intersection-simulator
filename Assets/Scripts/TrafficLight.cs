using System.Collections.Generic;
using UnityEngine;

public enum LightState { Green, Yellow, Red }

[System.Serializable]
public class SignalHead
{
    public Renderer red, yellow, green;
    public bool eastWest;   // which phase this head shows
}

// One signal controller per intersection. East-west and north-south alternate:
// green, yellow, all red. Each intersection starts at its own point in the cycle.
public class TrafficLight : MonoBehaviour
{
    public static readonly List<TrafficLight> All = new List<TrafficLight>();
    public static float GreenTime = 40f;   // set by the scenario

    public Vector3 center;
    public float yellowTime = 3f;
    public float allRedTime = 1.5f;
    public float startOffset;
    public SignalHead[] heads;

    float t;
    int lastEW = -1, lastNS = -1;

    static readonly Color RedOn = new Color(1f, 0.1f, 0.1f), RedOff = new Color(0.20f, 0.03f, 0.03f);
    static readonly Color YelOn = new Color(1f, 0.85f, 0.1f), YelOff = new Color(0.22f, 0.18f, 0.03f);
    static readonly Color GrnOn = new Color(0.1f, 1f, 0.3f), GrnOff = new Color(0.03f, 0.20f, 0.06f);

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    void Start() { t = startOffset; }

    LightState Phase(float ts)
    {
        if (ts < GreenTime) return LightState.Green;
        if (ts < GreenTime + yellowTime) return LightState.Yellow;
        return LightState.Red;
    }

    public LightState GetState(bool eastWest)
    {
        float half = GreenTime + yellowTime + allRedTime;
        float total = 2f * half;
        float ts = eastWest ? t % total : (t + half) % total;
        return Phase(ts);
    }

    void Update()
    {
        t += Time.deltaTime;
        int ew = (int)GetState(true), ns = (int)GetState(false);
        if (ew == lastEW && ns == lastNS) return;
        lastEW = ew; lastNS = ns;
        if (heads == null) return;
        foreach (var h in heads)
        {
            LightState s = h.eastWest ? (LightState)ew : (LightState)ns;
            Set(h.red, s == LightState.Red ? RedOn : RedOff);
            Set(h.yellow, s == LightState.Yellow ? YelOn : YelOff);
            Set(h.green, s == LightState.Green ? GrnOn : GrnOff);
        }
    }

    static void Set(Renderer r, Color c) { if (r != null) r.material.color = c; }
}
