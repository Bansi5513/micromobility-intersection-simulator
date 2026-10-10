using System.Collections.Generic;
using UnityEngine;

// Waypoint-following car. Slows down for corners (per-waypoint speed limits).
// Background traffic (obeyLight = true) also stops at red and yellow lights and keeps its distance to the car ahead.
public class CarAI : MonoBehaviour
{
    public static readonly List<CarAI> All = new List<CarAI>();

    public List<Vector3> path = new List<Vector3>();
    public List<float> limits = new List<float>();   // max speed on arrival at each waypoint (99 = no limit)
    public float cruiseSpeed = 11f;
    public float accel = 3f;
    public float brake = 4.5f;
    public bool obeyLight = true;
    public bool loop = true;
    public float startAlong = 0f;     // start this far along the first leg
    public int slot;                  // 0..2, used to set traffic density
    public float frontOffset = 2.3f;  // center to front bumper
    public BoxCollider Body;

    public Vector3 Velocity { get; private set; }

    int target = 1;
    float speed;
    Vector3 heading;
    bool eastWest;
    float laneCoord;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); Velocity = Vector3.zero; }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void Start()
    {
        speed = cruiseSpeed;
        if (path.Count < 2) return;
        heading = Flat(path[1] - path[0]).normalized;
        eastWest = Mathf.Abs(heading.x) > Mathf.Abs(heading.z);
        laneCoord = eastWest ? path[0].z : path[0].x;
        transform.position = path[0] + heading * startAlong;
        transform.rotation = Quaternion.LookRotation(heading);
    }

    void Update()
    {
        if (path.Count < 2) return;
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;

        Vector3 to = Flat(path[target] - pos);
        float dist = to.magnitude;
        if (dist < 1.2f)
        {
            target++;
            if (target >= path.Count)
            {
                if (loop) { transform.position = path[0]; target = 1; speed = cruiseSpeed * 0.5f; }
                else gameObject.SetActive(false);
                return;
            }
            to = Flat(path[target] - pos);
            dist = to.magnitude;
        }
        Vector3 dir = to.normalized;

        // Speed profile: be at the waypoint's speed limit when we get there
        float next = target < limits.Count ? limits[target] : 99f;
        float goal = Mathf.Min(cruiseSpeed, Mathf.Sqrt(next * next + 2f * brake * dist));

        if (obeyLight)
        {
            // Nearest signal ahead on this road
            TrafficLight best = null;
            float bestD = 60f;
            foreach (var tl in TrafficLight.All)
            {
                float lateral = eastWest ? Mathf.Abs(tl.center.z - laneCoord) : Mathf.Abs(tl.center.x - laneCoord);
                if (lateral > 4f) continue;
                float d = Vector3.Dot(tl.center - pos, heading) - City.StopDistance - frontOffset;
                if (d > -0.5f && d < bestD) { bestD = d; best = tl; }
            }
            if (best != null)
            {
                LightState st = best.GetState(eastWest);
                if (st != LightState.Green)
                {
                    bool canStop = bestD >= speed * speed / (2f * brake);
                    if (st == LightState.Red || canStop)
                    {
                        float lim = Mathf.Sqrt(2f * brake * Mathf.Max(0f, bestD));
                        goal = Mathf.Min(goal, lim < 0.3f ? 0f : lim);
                    }
                }
            }

            // Keep a gap to the car ahead in the same lane
            foreach (var o in All)
            {
                if (o == this) continue;
                if (Vector3.Dot(o.heading, heading) < 0.9f) continue;
                if (Mathf.Abs(o.laneCoord - laneCoord) > 1f) continue;
                float ahead = Vector3.Dot(o.transform.position - pos, heading);
                if (ahead <= 0f || ahead > 40f) continue;
                float gap = ahead - 4.8f;
                float lim = Mathf.Sqrt(o.speed * o.speed + 2f * brake * Mathf.Max(0f, gap - 2f));
                goal = Mathf.Min(goal, lim);
            }
        }

        speed = Mathf.MoveTowards(speed, goal, (goal > speed ? accel : brake * 1.5f) * dt);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 150f * dt);
        Velocity = dir * speed;
        transform.position += Velocity * dt;
    }
}
