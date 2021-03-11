using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test script to explore different finite burn strategies to reach a target point at the specified radius.
/// 
/// Basic idea:
/// - determine deltaV to perform a Hohmann transfer to specified radius
/// - do burn with the dV spread out over the burn time, stop when the OP reports the required radius as the
///   major axis
/// - report the phase offset from the desired position and the total deltaV required and time required
/// 
/// Initially assume that the direction of the dV should be perpendicular to the radius line to the Earth
///  Alternatives:
///  - maintain the initial heading
///  
/// Currently assuming there is not mass change as the rocket fires. Add in later.
/// </summary>
public class FiniteBurn : MonoBehaviour
{
    public enum SteeringMode { FIXED, PERPENDICULAR, TANGENT };

    public enum BurnMode {  APPLY_IMPULSE, ENGINE};

    public BurnMode burnMode;

    private RocketEngine rocketEngine;

    public SteeringMode steeringMode = SteeringMode.PERPENDICULAR;

    public OrbitUniversal targetOrbit; 

    // burn time is in scaled seconds, not game time
    public float burnTimeSeconds = 600.0f;

    private float burnTimeGameSecs; 

    public NBody spaceship;

    //! Spaceship on impulse Hohmann transfer (for orbit comparison purposes)
    public NBody spaceshipImpulse;

    public NBody earth;

    private GravityEngine ge;

    private float targetRadius;

    private float impulseMagnitude;

    private OrbitUniversal orbitU;
    private OrbitPredictor orbitPredictor;

    // Start is called before the first frame update
    void Start()
    {
        ge = GravityEngine.Instance();

        burnTimeGameSecs = (float) GravityScaler.WorldSecsToPhysTime(burnTimeSeconds);
        Debug.LogFormat("Burn time in game secs={0}", burnTimeGameSecs);

        orbitPredictor = spaceship.GetComponentInChildren<OrbitPredictor>();
        orbitU = orbitPredictor.GetOrbitUniversal();

        targetRadius = (float) targetOrbit.GetMajorAxis();

        rocketEngine = spaceship.GetComponent<RocketEngine>();
        //ge.AddGEStartCallback(GEStarted);
    }

    public void GEStarted()
    {
        // when this is in GEStarted() get an error from OP not inited yet...TODO
        TransferShip transferShip = spaceship.GetComponent<TransferShip>();
        transferShip.Init();
        List<Maneuver> manuevers = transferShip.GetManeuvers();
        float numImpulses = Mathf.Max(burnTimeGameSecs / Time.fixedDeltaTime, 1.0f);
        impulseMagnitude = manuevers[0].dV / numImpulses;
        Debug.LogFormat("Transfer impulse dV={0} burn impulse={1} numImpulse={2}",
            manuevers[0].dV, impulseMagnitude, numImpulses);
        origDir = ge.GetVelocity(spaceship).normalized;
        startTime = ge.GetPhysicalTime();
        if (burnMode == BurnMode.ENGINE) {
            rocketEngine.SetEngine(true);
        }
        // Do impulse transfer for "control" ship
        transferShip = spaceshipImpulse.GetComponent<TransferShip>();
        transferShip.Init();
        transferShip.DoTransfer(null);
    }

    private int frameCount = 0;
    private bool done;
    private float startTime;
    private float totalImpulse = 0;
    private Vector3 origDir = Vector3.zero;

    // Update is called once per frame
    void FixedUpdate()
    {
        if (done)
            return;

        if (frameCount == 1) {
            // when this is in GEStarted() get an error from OP not inited yet...TODO
            TransferShip transferShip = spaceship.GetComponent<TransferShip>();
            transferShip.Init();
            List<Maneuver> manuevers = transferShip.GetManeuvers();
            float numImpulses = Mathf.Max( burnTimeGameSecs / Time.fixedDeltaTime, 1.0f);
            impulseMagnitude = manuevers[0].dV/numImpulses;
            Debug.LogFormat("Transfer impulse dV={0} burn impulse={1} numImpulse={2}", 
                manuevers[0].dV, impulseMagnitude, numImpulses);
            origDir = ge.GetVelocity(spaceship).normalized;
            startTime = ge.GetPhysicalTime();
            if (burnMode == BurnMode.ENGINE) {
                rocketEngine.SetEngine(true);
            }
            // Do impulse transfer for "control" ship
            transferShip = spaceshipImpulse.GetComponent<TransferShip>();
            transferShip.Init();
            transferShip.DoTransfer(null);
        }
        if (frameCount >= 1) {
            // check if orbit has reached targetRadius. Allow a slight fundge in case it get s near to avoid
            // an extra impulse when burnTime near zero
            orbitPredictor.UpdateOrbitU();
            // Debug.LogFormat("apogee={0}", orbitU.GetApogee());
            double apogee = orbitU.GetApogee();
            if (orbitU.eccentricity > 1.0f) {
                Debug.LogWarning("Orbit became hyperbolic");
                done = true;
            } else if ( apogee > (targetRadius - 1f))  {
                float dt = ge.GetPhysicalTime() - startTime;
                Debug.LogFormat("Reached target orbit. Game time={0} burn time={1} total impulse={2} phaseOffset={3}",
                    dt,
                    GravityScaler.GetWorldTimeSeconds(dt), 
                    totalImpulse,
                    orbitU.omega_lc
                    ); ;
                done = true;
            } else {

                Vector3 burnDir = Vector3.zero;
                switch (steeringMode) {
                    case SteeringMode.TANGENT:
                        burnDir = ge.GetVelocity(spaceship).normalized;
                        break;

                    case SteeringMode.PERPENDICULAR:
                        Vector3 radialDir = ge.GetPhysicsPosition(spaceship).normalized;
                        burnDir = Vector3.Cross(Vector3.forward, radialDir);
                        break;

                    case SteeringMode.FIXED:
                        burnDir = origDir;
                        break;
                }
                if (burnMode == BurnMode.APPLY_IMPULSE) {
                    // apply impulse
                    ge.ApplyImpulse(spaceship, impulseMagnitude * burnDir);
                    totalImpulse += impulseMagnitude;
                } else {
                    rocketEngine.SetThrustAxis(-burnDir);
                }
            }
            if (done && (burnMode == BurnMode.ENGINE)) {
                rocketEngine.SetEngine(false);
            }
        }
        frameCount++;
    }
}
