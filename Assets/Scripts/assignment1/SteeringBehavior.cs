using UnityEngine;
using System.Collections.Generic;
using TMPro;


public class SteeringBehavior : MonoBehaviour
{
    public Vector3 target;
    public KinematicBehavior kinematic;
    public List<Vector3> path;
    // you can use this label to show debug information,
    // like the distance to the (next) target
    public TextMeshProUGUI label;

    bool isReversing = false;
    bool isEscaping = false;
    Vector3 escapeStart;
    float escapeDistance = 3f;
    Vector3 reverseOrigin;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        kinematic = GetComponent<KinematicBehavior>();
        target = transform.position;
        path = null;
        EventBus.OnSetMap += SetMap;
    }

    // Update is called once per frame
    void Update()
    {
        // Assignment 1: If a single target was set, move to that target
        //                If a path was set, follow that path ("tightly")

        // you can use kinematic.SetDesiredSpeed(...) and kinematic.SetDesiredRotationalVelocity(...)
        //    to "request" acceleration/decceleration to a target speed/rotational velocity

         // If following a path (waypoints)
         // if angle is too steep, use backing up technique
         // drive far away, then figure the angle
         // decide whether to overshoot or undershoot during pathfinding -- notice this in reflectio
        

        if (path != null && path.Count > 0)
        {
            while (path.Count > 0)
            {
                float waypointDistance = (path[0] - transform.position).magnitude;

                // Lookahead for tight turns
                bool shouldAdvance = waypointDistance < 2f;

                if (path.Count >= 2 && waypointDistance < 5f) // consider "cutting" early if needed
                {
                    Vector3 toCurrent = (path[0] - transform.position).normalized;
                    Vector3 toNext = (path[1] - path[0]).normalized;

                    float turnAngle = Vector3.Angle(toCurrent, toNext);

                    if (turnAngle < 25f)
                    {
                        // Small angle — go ahead and skip early
                        shouldAdvance = true;
                    }
                }

                if (shouldAdvance)
                {
                    Debug.Log("Reached (or skipped) waypoint: " + path[0]);
                    path.RemoveAt(0);
                }

                else
                {
                    break;
                }
            }

            if (path.Count == 0)
            {
                kinematic.SetDesiredSpeed(0f);
                kinematic.SetDesiredRotationalVelocity(0f);
                return;
            }

            //Only happens if there's still a point left
            target = path[0];
            Vector3 direction = target - transform.position;
            float distance = direction.magnitude;

            Debug.Log("Next waypoint: " + target);
            if (label != null)
                label.text = "Waypoint dist: " + distance.ToString("F2");

            MoveToward(direction);

        }

        // If a single target is set
        else if ((target - transform.position).magnitude > 1f)
        {
            Vector3 direction = target - transform.position;
            float distance = direction.magnitude;

            if (label != null)
                label.text = "Distance: " + distance.ToString("F2");

            MoveToward(direction);
        }
        else
        {
            // Stop if we're at the target
            kinematic.SetDesiredSpeed(0f);
            kinematic.SetDesiredRotationalVelocity(0f);
        }
    }

    void MoveToward(Vector3 direction) {
        direction.y = 0;
        float distance = direction.magnitude;

        float arrivalRadius = 2f;
        float slowingRadius = 10f;
        float targetSpeed = 10f;

        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        float absAngle = Mathf.Abs(angle);

        // Thresholds
        float closeThreshold = 20f; // Lowered to reduce reverse frequency
        float alignThreshold = 60f;
        float alignmentCompleteThreshold = 18f;

        float speed = 0f;
        float rotationSpeed = 0f;

        // --- Dynamic reverseExitDistance based on target distance ---
        float minReverseExit = 1f;
        float maxReverseExit = 10f;
        float maxDistanceForScaling = 20f;
        float t = Mathf.Clamp01(distance / maxDistanceForScaling);
        float reverseExitDistance = Mathf.Lerp(minReverseExit, maxReverseExit, t);


        if (isEscaping)
        {
            float moved = Vector3.Distance(transform.position, escapeStart);
            if (moved < escapeDistance)
            {
                kinematic.SetDesiredSpeed(4f);
                kinematic.SetDesiredRotationalVelocity(angle > 0 ? 60f : -60f);
                Debug.Log("Escaping forward to create space");
                return;
            }
            else
            {
                isEscaping = false;
                Debug.Log("Escape complete, resuming normal steering");
            }
        }

        // Trigger reversing if close and misaligned
        if (!isReversing && distance < closeThreshold && absAngle > alignThreshold) {
            if (!Physics.Raycast(transform.position, -transform.forward, 2f))
            {
                isReversing = true;
                reverseOrigin = transform.position;
            }
            else
            {
                Debug.Log("Blocked from reversing, switching to escape forward mode");
                isEscaping = true;
                escapeStart = transform.position;

                // Begin slight forward motion
                kinematic.SetDesiredSpeed(4f);
                kinematic.SetDesiredRotationalVelocity(angle > 0 ? 60f : -60f);
                return;
            }
        }

        // Handle reversing
        if (isReversing) {
            float reverseDistance = Vector3.Distance(transform.position, reverseOrigin);

            if (reverseDistance < reverseExitDistance) {
                // Check if we are blocked while reversing
                if (Physics.Raycast(transform.position, -transform.forward, 1f))
                {
                    Debug.Log("Reversing blocked by wall, switching to forward to create space");
                    isReversing = false;

                    // Move forward slightly
                    speed = 4f;
                    rotationSpeed = angle > 0 ? 60f : -60f;

                    kinematic.SetDesiredSpeed(speed);
                    kinematic.SetDesiredRotationalVelocity(rotationSpeed);
                    return;
                }

                Vector3 reverseDirection = -direction.normalized;
                float reverseAngle = Vector3.SignedAngle(transform.forward, reverseDirection, Vector3.up);

                speed = -8f;
                rotationSpeed = reverseAngle > 0 ? 120f : -120f;

                Debug.DrawLine(transform.position, transform.position + reverseDirection * 5f, Color.blue);
                kinematic.SetDesiredSpeed(speed);
                kinematic.SetDesiredRotationalVelocity(rotationSpeed);
                return;
            } else {
                isReversing = false;
            }
        }

        if (distance > arrivalRadius) {
            // Only slow down if we’re on the last waypoint of the path (or not on a path)
            bool isFinalPathPoint = (path == null || path.Count == 1);

            speed = (!isFinalPathPoint || distance >= slowingRadius)
                ? targetSpeed
                : Mathf.Lerp(0, targetSpeed, distance / slowingRadius);

            if (absAngle > alignmentCompleteThreshold) {
                rotationSpeed = angle > 0 ? 120f : -120f;
            } else {
                rotationSpeed = 0f;
            }
        }

        kinematic.SetDesiredSpeed(speed);
        kinematic.SetDesiredRotationalVelocity(rotationSpeed);

        // Debug visuals
        Debug.DrawLine(transform.position, transform.position + direction.normalized * 5f, Color.red);
        Debug.Log("Moving toward: " + direction + " | Distance: " + distance);
    }



    public void SetTarget(Vector3 target)
    {
        this.target = target;
        EventBus.ShowTarget(target);
    }

    public void SetPath(List<Vector3> path)
    {
        if (path == null)
        {
            Debug.LogWarning("SetPath was called with a null path!");
            return;
        }
        Debug.Log("Path received with count: " + path.Count);
        this.path = path;
    }

    public void SetMap(List<Wall> outline)
    {
        this.path = null;
        this.target = transform.position;
    }
}