using UnityEngine;

// Rider controller for the e-scooter. W/S = accelerate/brake, A/D = steer, C = change camera.
public class ScooterController : MonoBehaviour
{
    public float maxSpeed = 7f;        // m/s (about 25 km/h)
    public float acceleration = 2.2f;  // m/s^2
    public float brakeDecel = 6f;      // m/s^2
    public float coastDecel = 0.6f;    // m/s^2
    public float turnRate = 55f;       // degrees per second at speed

    public Transform cam;              // rider camera (child of the scooter)
    public GameObject riderBody;       // visible only in the chase camera
    public bool Frozen;                // set when the run is over

    public Vector3 Velocity { get; private set; }
    public float SpeedKmh { get { return speed * 3.6f; } }

    float speed;
    bool chase;

    void Start() { ApplyCamera(); }

    void ApplyCamera()
    {
        if (cam != null)
        {
            cam.localPosition = chase ? new Vector3(0f, 2.6f, -5f) : new Vector3(0f, 1.5f, -0.15f);
            cam.localRotation = Quaternion.Euler(chase ? 14f : 6f, 0f, 0f);
        }
        if (riderBody != null) riderBody.SetActive(chase);
    }

    void Update()
    {
        if (Inp.CameraTogglePressed()) { chase = !chase; ApplyCamera(); }

        if (Frozen)
        {
            speed = 0f;
            Velocity = Vector3.zero;
            return;
        }

        float v = Inp.Vertical();
        float h = Inp.Horizontal();

        if (v > 0.1f) speed += acceleration * Time.deltaTime;
        else if (v < -0.1f) speed -= brakeDecel * Time.deltaTime;
        else speed -= coastDecel * Time.deltaTime;
        speed = Mathf.Clamp(speed, 0f, maxSpeed);

        float steerFactor = Mathf.Clamp01(speed / 2.5f);   // no steering while standing still
        transform.Rotate(0f, h * turnRate * steerFactor * Time.deltaTime, 0f);

        Velocity = transform.forward * speed;
        transform.position += Velocity * Time.deltaTime;
    }
}
