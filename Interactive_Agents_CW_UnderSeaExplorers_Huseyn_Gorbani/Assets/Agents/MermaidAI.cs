using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NPBehave;
using UnityMovementAI;
using URandom = UnityEngine.Random;

// similar code to SharkAI
public class MermaidAI : MonoBehaviour
{
    public GameObject treasureChest;
    public GameObject diver;
    public GameObject shark;
    public float distractRadius = 10f;
    public float wanderChangeTargetTime = 2f;
    public Root behaviorTree;

    private SteeringBasics steeringBasics;
    private WallAvoidance wallAvoidance;
    private Vector3 targetPosition;
    private float timeToChangeTarget;
    private float health = 100f;

    void Start()
    {
        steeringBasics = GetComponent<SteeringBasics>();
        wallAvoidance = GetComponent<WallAvoidance>();

        // setting color
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = new Color(255f / 255f, 0f / 255f, 228f / 255f); // purple color 

        }

        behaviorTree = new Root(
            new Service(0.5f, () => { UpdateBlackboard(); },
                new Selector(
                    // Behaviour: Dead when health is 0
                    new BlackboardCondition("mermaidHealth", Operator.IS_SMALLER_OR_EQUAL, 0f, Stops.IMMEDIATE_RESTART,
                        new Action(() => Die())
                    ),

                    // Behaviour: Distract the diver by interposing between the diver and the shark so that diver (stranger) gets in a danger and get killed. 
                    new BlackboardCondition("distractDiver", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Action(() => DistractDiver())
                    ),

                    // Behaviour: Move randomly around the treasure chest
                    new Action(() => MoveRandomly())
                )
            )
        );

        behaviorTree.Start();
        behaviorTree.Blackboard["mermaidHealth"] = health;
    }

    private void UpdateBlackboard()
    {
        float distanceToDiver = Vector3.Distance(transform.position, diver.transform.position);
        float distanceToShark = Vector3.Distance(transform.position, shark.transform.position);

        behaviorTree.Blackboard["distractDiver"] = distanceToDiver < 1.5f * distanceToShark; // until the distance of mermaid to diver is 1.5 times bigger than it is to shark, distract
        behaviorTree.Blackboard["mermaidHealth"] = health;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("SeaMine"))
        {
            health = 0; // mermaid dies with straightaway 
            behaviorTree.Blackboard["mermaidHealth"] = health;
        }
    }

    private void Die()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.black;
        }
        else
        {
            Debug.LogWarning("MeshRenderer component not found on the mermaid or its child objects.");
        }
    }


    private void DistractDiver()
    {
        MovementAIRigidbody diverRigidbody = diver.GetComponent<MovementAIRigidbody>();
        MovementAIRigidbody sharkRigidbody = shark.GetComponent<MovementAIRigidbody>();
        Vector3 acceleration = steeringBasics.Interpose(diverRigidbody, sharkRigidbody);

        // wall avoidance
        Vector3 wallAvoidanceAccel = wallAvoidance.GetSteering();
        if (wallAvoidanceAccel.magnitude > 0)
        {
            acceleration = wallAvoidanceAccel;
        }

        steeringBasics.Steer(acceleration);
        steeringBasics.LookWhereYoureGoing();
    }

    private void MoveRandomly()
    {
        timeToChangeTarget -= Time.deltaTime;

        if (timeToChangeTarget <= 0f)
        {
            targetPosition = treasureChest.transform.position + URandom.insideUnitSphere * distractRadius;
            targetPosition.y = transform.position.y;
            timeToChangeTarget = wanderChangeTargetTime;
        }

        Vector3 acceleration = steeringBasics.Arrive(targetPosition);

        // wall avoidance
        Vector3 wallAvoidanceAccel = wallAvoidance.GetSteering();
        if (wallAvoidanceAccel.magnitude > 0)
        {
            acceleration = wallAvoidanceAccel;
        }

        steeringBasics.Steer(acceleration);
        steeringBasics.LookWhereYoureGoing();
    }

    // similar to method in DiverAI and SharkAI, resseting color and health for Mermaid. This function is called from UnderwaterCaveGenerator.cs script
    public void ResetColorAndHealth()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = new Color(255f / 255f, 0f / 255f, 228f / 255f); // purple color 
            health = 100;
        }
        else
        {
            Debug.LogWarning("MeshRenderer component not found on the agent or its child objects.");
        }
    }

}