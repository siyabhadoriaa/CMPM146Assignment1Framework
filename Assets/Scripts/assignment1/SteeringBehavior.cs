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
            target = path[0];
            Vector3 direction = target - transform.position;
            float distance = direction.magnitude;

            if (label != null)
                label.text = "Waypoint dist: " + distance.ToString("F2");

            if (distance < 1f)
            {
                path.RemoveAt(0);
                if (path.Count == 0)
                {
                    kinematic.SetDesiredSpeed(0f);
                    kinematic.SetDesiredRotationalVelocity(0f);
                }
                return;
            }

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

        float arrivalRadius = 1f;
        float slowingRadius = 10f;
        float targetSpeed = 10f;

        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        float absAngle = Mathf.Abs(angle);

        // --- Tweak these thresholds ---
        float closeThreshold = 15f;
        float alignThreshold = 60f; // Angle at which we say "you're facing too far off"
        float reverseExitDistance = 15f;
        float alignmentCompleteThreshold = 18f;

        float speed = 0f;
        float rotationSpeed = 0f;

        if (!isReversing && distance < closeThreshold && absAngle > alignThreshold) {
            isReversing = true;
            reverseOrigin = transform.position; // Record starting point
        }

        if (isReversing) {
            float reverseDistance = Vector3.Distance(transform.position, reverseOrigin);

            if (reverseDistance < reverseExitDistance) {
                // Keep backing up and turning toward the target
                Vector3 reverseDirection = -direction.normalized;
                float reverseAngle = Vector3.SignedAngle(transform.forward, reverseDirection, Vector3.up);

                speed = -3f;
                rotationSpeed = reverseAngle > 0 ? 120f : -120f;

                Debug.DrawLine(transform.position, transform.position + reverseDirection * 5f, Color.blue);
                kinematic.SetDesiredSpeed(speed);
                kinematic.SetDesiredRotationalVelocity(rotationSpeed);
                return;
            } else {
                // Stop reversing, go back to normal movement
                isReversing = false;
            }
        }
        // Once aligned (angle is small enough), move normally
        if (distance > arrivalRadius) {
            speed = (distance < slowingRadius)
                ? Mathf.Lerp(0, targetSpeed, distance / slowingRadius)
                : targetSpeed;

            if (absAngle > alignmentCompleteThreshold) {
                rotationSpeed = angle > 0 ? 120f : -120f;
            } else {
                rotationSpeed = 0f;
            }
        } else {
            speed = 0f;
            rotationSpeed = 0f;
        }

        kinematic.SetDesiredSpeed(speed);
        kinematic.SetDesiredRotationalVelocity(rotationSpeed);

        // Visual debug
        Debug.DrawLine(transform.position, transform.position + direction.normalized * 5, Color.red);
    }



    public void SetTarget(Vector3 target)
    {
        this.target = target;
        EventBus.ShowTarget(target);
    }

    public void SetPath(List<Vector3> path)
    {
        this.path = path;
    }

    public void SetMap(List<Wall> outline)
    {
        this.path = null;
        this.target = transform.position;
    }
}
