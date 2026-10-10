using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class Scenario
{
    public string name;
    public string description;
    public bool hookCar;              // spawn the right-turning car
    public float hookCarStartOffset;  // start position relative to the hook intersection (m along the road)
    public float hookCarSpeed;        // its cruise speed (m/s)
    public float triggerOffset;       // rider position (relative to the intersection) that releases the car
    public int traffic;               // background traffic: 0 none, 1 light, 2 normal, 3 heavy
    public float greenTime;           // green duration for every signal (s)
}

[System.Serializable]
class ScenarioList { public Scenario[] scenarios; }

// Loads Resources/scenarios.json, applies the selected scenario, runs the game rules and draws the HUD.
public class ScenarioManager : MonoBehaviour
{
    enum RunState { Riding, OffRoad, Crashed }

    public static int Current = 0;

    public CarAI hookCar;
    public CarAI[] ambientCars;
    public ScooterController scooter;
    public DataLogger logger;
    public Vector3 hookCenter;          // intersection where the right hook happens
    public bool endOnCollision = true;

    Scenario[] list;
    Scenario s;
    bool released;
    RunState state = RunState.Riding;
    float flashUntil;
    readonly Dictionary<TrafficLight, bool> inside = new Dictionary<TrafficLight, bool>();

    void Start()
    {
        var file = Resources.Load<TextAsset>("scenarios");
        list = JsonUtility.FromJson<ScenarioList>(file.text).scenarios;
        Current = Mathf.Clamp(Current, 0, list.Length - 1);
        s = list[Current];

        TrafficLight.GreenTime = s.greenTime;
        foreach (var car in ambientCars)
            car.gameObject.SetActive(car.slot < s.traffic);

        if (s.hookCar)
        {
            hookCar.path[0] = new Vector3(hookCenter.x + s.hookCarStartOffset, 0f, hookCar.path[0].z);
            hookCar.cruiseSpeed = s.hookCarSpeed;
            hookCar.transform.position = hookCar.path[0];
            hookCar.transform.rotation = Quaternion.LookRotation(Vector3.right);
        }
        logger.scenarioName = s.name;
        logger.endX = hookCenter.x + 60f;
        logger.endZ = hookCenter.z;
    }

    void Update()
    {
        if (Inp.RestartPressed()) { Reload(); return; }
        for (int i = 0; i < list.Length && i < 3; i++)
            if (Inp.ScenarioKeyPressed(i)) { Current = i; Reload(); return; }

        if (state != RunState.Riding) return;
        Vector3 p = scooter.transform.position;

        if (s.hookCar && !released && p.x >= hookCenter.x + s.triggerOffset && Mathf.Abs(p.z - hookCenter.z) < 20f)
        {
            released = true;
            hookCar.gameObject.SetActive(true);
        }

        if (!City.OnRoad(p)) { End(RunState.OffRoad, "off_road"); return; }
        if (endOnCollision && logger.Collision) { End(RunState.Crashed, "collision"); return; }
        CheckRedLights(p);
    }

    void CheckRedLights(Vector3 p)
    {
        foreach (var tl in TrafficLight.All)
        {
            Vector3 c = tl.center;
            bool now = Mathf.Abs(p.x - c.x) < City.HalfWidth && Mathf.Abs(p.z - c.z) < City.HalfWidth;
            bool was;
            inside.TryGetValue(tl, out was);
            if (now && !was && scooter.SpeedKmh > 2f)
            {
                Vector3 f = scooter.transform.forward;
                bool ew = Mathf.Abs(f.x) > Mathf.Abs(f.z);
                if (tl.GetState(ew) == LightState.Red)
                {
                    logger.RedLights++;
                    flashUntil = Time.time + 2.5f;
                }
            }
            inside[tl] = now;
        }
    }

    void End(RunState st, string reason)
    {
        state = st;
        scooter.Frozen = true;
        logger.EndRun(reason);
    }

    void Reload()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (s == null) return;
        int fs = Mathf.Max(14, Screen.height / 45);
        float lh = fs * 1.5f;
        var label = new GUIStyle(GUI.skin.label) { fontSize = fs };
        label.normal.textColor = Color.white;

        GUI.Box(new Rect(10, 10, fs * 26f, lh * 6f + 12f), "");
        GUI.Label(new Rect(20, 14, fs * 25f, lh), "Scenario " + (Current + 1) + ": " + s.name, label);
        GUI.Label(new Rect(20, 14 + lh, fs * 25f, lh), "Speed " + scooter.SpeedKmh.ToString("0") + " km/h    TTC " +
                  (logger.CurrentTTC >= 99f ? "-" : logger.CurrentTTC.ToString("0.0") + " s"), label);
        GUI.Label(new Rect(20, 14 + lh * 2, fs * 25f, lh), "Min TTC " + (logger.MinTTC >= 99f ? "-" : logger.MinTTC.ToString("0.0") + " s") +
                  "    " + logger.Status(), label);
        GUI.Label(new Rect(20, 14 + lh * 3, fs * 25f, lh), "Red lights run: " + logger.RedLights +
                  (logger.Done && state == RunState.Riding ? "    RUN SAVED" : ""), label);
        GUI.Label(new Rect(20, 14 + lh * 4, fs * 25f, lh), "W/S speed  A/D steer  C camera", label);
        GUI.Label(new Rect(20, 14 + lh * 5, fs * 25f, lh), "R restart   1 / 2 / 3 scenario", label);

        if (Time.time < flashUntil && state == RunState.Riding)
        {
            var flash = new GUIStyle(GUI.skin.label) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            flash.normal.textColor = new Color(1f, 0.25f, 0.2f);
            GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, fs * 3f), "RED LIGHT!", flash);
        }

        if (state != RunState.Riding)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var big = new GUIStyle(GUI.skin.label) { fontSize = fs * 3, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            big.normal.textColor = new Color(1f, 0.3f, 0.25f);
            var small = new GUIStyle(GUI.skin.label) { fontSize = fs * 3 / 2, alignment = TextAnchor.MiddleCenter };
            small.normal.textColor = Color.white;

            string title = state == RunState.OffRoad ? "YOU WENT OFF THE ROAD" : "COLLISION";
            GUI.Label(new Rect(0, Screen.height * 0.30f, Screen.width, fs * 4f), title, big);
            GUI.Label(new Rect(0, Screen.height * 0.30f + fs * 4f, Screen.width, fs * 3f), "GAME OVER", big);
            GUI.Label(new Rect(0, Screen.height * 0.30f + fs * 8f, Screen.width, fs * 2.5f), "Press R to restart", small);
        }
    }
}
