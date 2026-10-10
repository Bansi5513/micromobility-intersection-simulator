using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// Logs the rider's state and the conflict with the nearest car ten times per second.
// One CSV per run plus one row per run in runs.csv, saved in <project folder>/SimLogs.
//
// TTC (time to collision) = distance to the nearest point of the car / closing speed
// along the line between rider and car. Near miss = TTC below nearMissTTC while closer than nearMissDist.
public class DataLogger : MonoBehaviour
{
    public ScooterController scooter;
    public string scenarioName = "";
    public float logInterval = 0.1f;
    public float endX = 0f;             // the run counts as completed past this x ...
    public float endZ = 0f;             // ... while still on the road at this z
    public float nearMissTTC = 1.5f;    // seconds
    public float nearMissDist = 5f;     // meters
    public float collisionDist = 0.5f;  // meters
    public int RedLights;               // red light violations, counted by ScenarioManager

    public float CurrentTTC { get; private set; }
    public float MinTTC { get; private set; }
    public float MinDist { get; private set; }
    public bool NearMiss { get; private set; }
    public bool Collision { get; private set; }
    public bool Done { get { return done; } }

    StringBuilder sb = new StringBuilder();
    float elapsed, timer, maxSpeed;
    bool done;
    string endReason = "restarted";

    void Start()
    {
        CurrentTTC = 99f; MinTTC = 99f; MinDist = 99f;
        sb.AppendLine("time_s,x_m,z_m,speed_kmh,nearest_car_dist_m,ttc_s,near_miss,collision");
    }

    void Update()
    {
        if (scooter == null) return;

        float dist, ttc;
        Measure(out dist, out ttc);
        CurrentTTC = ttc;
        if (dist < collisionDist) Collision = true;
        if (done) return;

        elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        MinTTC = Mathf.Min(MinTTC, ttc);
        MinDist = Mathf.Min(MinDist, dist);
        bool near = ttc < nearMissTTC && dist < nearMissDist;
        if (near) NearMiss = true;
        maxSpeed = Mathf.Max(maxSpeed, scooter.SpeedKmh);

        Vector3 p = scooter.transform.position;
        if (timer >= logInterval)
        {
            timer = 0f;
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0:0.00},{1:0.00},{2:0.00},{3:0.0},{4:0.00},{5:0.00},{6},{7}",
                elapsed, p.x, p.z, scooter.SpeedKmh, dist, ttc, near ? 1 : 0, dist < collisionDist ? 1 : 0));
        }

        if (p.x > endX && Mathf.Abs(p.z - endZ) < 15f) EndRun("completed");
    }

    void Measure(out float dist, out float ttc)
    {
        dist = 99f; ttc = 99f;
        Vector3 p = scooter.transform.position + Vector3.up * 0.8f;
        foreach (var c in CarAI.All)
        {
            if (c == null || c.Body == null) continue;
            Vector3 cp = c.Body.ClosestPoint(p);
            Vector3 rel = cp - p;
            float d = rel.magnitude;
            if (d < dist) dist = d;
            float closing = d > 0.001f ? Vector3.Dot(scooter.Velocity - c.Velocity, rel / d) : 99f;
            if (closing > 0.3f) ttc = Mathf.Min(ttc, d / closing);
        }
    }

    public string Status()
    {
        if (Collision) return "COLLISION";
        if (NearMiss) return "NEAR MISS";
        return "clear";
    }

    // reason: completed, collision, off_road or restarted
    public void EndRun(string reason)
    {
        if (done) return;
        done = true;
        endReason = reason;
        if (elapsed < 2f) return;

        string dir = Path.Combine(Application.dataPath, "..", "SimLogs");
        Directory.CreateDirectory(dir);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string file = "run_" + stamp + "_s" + (ScenarioManager.Current + 1) + ".csv";
        File.WriteAllText(Path.Combine(dir, file), sb.ToString());

        string summary = Path.Combine(dir, "runs.csv");
        if (!File.Exists(summary))
            File.WriteAllText(summary, "run_file,scenario,min_ttc_s,min_dist_m,near_miss,collision,red_lights,max_speed_kmh,duration_s,end_reason\n");
        File.AppendAllText(summary, string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2:0.00},{3:0.00},{4},{5},{6},{7:0.0},{8:0.0},{9}\n",
            file, scenarioName, MinTTC, MinDist, NearMiss ? 1 : 0, Collision ? 1 : 0, RedLights, maxSpeed, elapsed, endReason));
        Debug.Log("Run saved (" + endReason + "): " + Path.GetFullPath(Path.Combine(dir, file)));
    }

    void OnDestroy() { EndRun("restarted"); }
}
