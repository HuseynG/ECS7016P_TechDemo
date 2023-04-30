using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NPBehave;
using UnityMovementAI;
using URandom = UnityEngine.Random;

public class SharkAI : MonoBehaviour
{
    public GameObject treasureChest;
    public GameObject diver;
    public float protectionRadius = 10f;
    public float wanderChangeTargetTime = 2f;
    public Root behaviorTree;

    private SteeringBasics steeringBasics;
    private WallAvoidance wallAvoidance;
    private Pursue pursue;
    private Vector3 targetPosition;
    private float timeToChangeTarget;
    private float health = 200f;

    private List<GameObject> collidedMines = new List<GameObject>();


    void Start()
    {
        steeringBasics = GetComponent<SteeringBasics>();
        pursue = GetComponent<Pursue>();
        wallAvoidance = GetComponent<WallAvoidance>();

        // setting color
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.blue;
        }
        // creating a behaviour tree node to control the agent's decision-making process
        behaviorTree = new Root(
            new Service(0.5f, () => { UpdateBlackboard(); },
                new Selector(

                    // Behaviour: Stop when health is 0. Shark is dead/defeated
                    new BlackboardCondition("sharkHealth", Operator.IS_SMALLER_OR_EQUAL, 0f, Stops.IMMEDIATE_RESTART,
                        new Selector(
                            new Action(() => Die())
                        )
                    ),

                    // Behaviour: Chasing and and trying to Killing
                    new BlackboardCondition("chaseDiver", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Action(() => ChaseAndKillDiver())
                    ),

                    // Behaviour: Protecting treasure chest (randomly swimming nearby)
                    new Action(() => ProtectTreasureChest())
                )
            )
        );
        // Starting the behaviour tree
        behaviorTree.Start();
    }

    private void UpdateBlackboard()
    {
        float distanceToTreasure = Vector3.Distance(transform.position, treasureChest.transform.position);
        float distanceToDiver = Vector3.Distance(transform.position, diver.transform.position);

        // true if 1.5 times of distance to diver is more than current distance to treasure chest
        behaviorTree.Blackboard["chaseDiver"] = distanceToTreasure < (1.5f * distanceToDiver);
        behaviorTree.Blackboard["sharkHealth"] = health;
        behaviorTree.Blackboard["collidedMines"] = collidedMines;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("SeaMine"))
        {
            // checking if the mine has not been collided with before
            if (!collidedMines.Contains(collision.gameObject))
            {
                DecreaseHealth(100); // when colliding with a sea mine
                Debug.Log("Shark's health: " + health);
                collidedMines.Add(collision.gameObject); // adding the mine to the list of collided mines
                behaviorTree.Blackboard["collidedMines"] = collidedMines;
            }
        }
    }

    private void ChaseAndKillDiver()
    {
        MovementAIRigidbody diverRigidbody = diver.GetComponent<MovementAIRigidbody>();
        Vector3 acceleration = pursue.GetSteering(diverRigidbody);

        // wall avoidance to chose whether to pursue or avoid wall.
        Vector3 wallAvoidanceAccel = wallAvoidance.GetSteering();
        if (wallAvoidanceAccel.magnitude > 0)
        {
            acceleration = wallAvoidanceAccel;
        }

        steeringBasics.Steer(acceleration);
        steeringBasics.LookWhereYoureGoing();
    }

    // method to wander around (guarding) treasure chest
    private void ProtectTreasureChest()
    {
        // decresing the time to change the target position
        timeToChangeTarget -= Time.deltaTime;

        // if the time to change target is zero or less
        if (timeToChangeTarget <= 0f)
        {
            // setting a new random target position within the protection radius around the treasure chest
            targetPosition = treasureChest.transform.position + URandom.insideUnitSphere * protectionRadius;
            targetPosition.y = transform.position.y; // keeping the same y position as the shark (height)
            timeToChangeTarget = wanderChangeTargetTime; // resetting the timer for changing the target positon
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

    // similar to method in DiverAI, resseting color and health for Shark. This function is called from UnderwaterCaveGenerator.cs script
    public void ResetColorAndHealth()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.blue;
            health = 200f;
            // also reseting the list of collided mines

            behaviorTree.Blackboard["collidedMines"] = new List<GameObject>();
        }
        else
        {
            Debug.LogWarning("MeshRenderer component not found on the agent or its child objects.");
        }
    }


    public void DecreaseHealth(float amount)
    {
        health -= amount;
    }

    // when health is 0 due to collision with sea mines, shark stops and turns black (similar to the diver)
    private void Die()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.black;
        }
        else
        {
            Debug.LogWarning("MeshRenderer component not found on the shark or its child objects.");
        }
    }
}
