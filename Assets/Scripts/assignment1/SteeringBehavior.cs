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

 void MoveToward(Vector3 direction)
    {
        direction.y = 0;
        float distance = direction.magnitude;

        float slowingRadius = 10f;
        float targetSpeed = 10f;

        float speed = (distance < slowingRadius)
            ? Mathf.Lerp(0, targetSpeed, distance / slowingRadius)
            : targetSpeed;

        kinematic.SetDesiredSpeed(speed);

        float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
        float rotationSpeed = 0f;

        if (Mathf.Abs(angle) > 5f)
            rotationSpeed = angle > 0 ? 30f : -30f;

        kinematic.SetDesiredRotationalVelocity(rotationSpeed);

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
