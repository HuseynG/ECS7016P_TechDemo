using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NPBehave;
using UnityMovementAI;
using URandom = UnityEngine.Random;

public class DiverAI : MonoBehaviour
{
    public GameObject treasureChest;
    public GameObject seaMinePrefab;
    public GameObject shark;
    public GameObject mermaid;
    public CustomSpawner customSpawner;
    public Root behaviorTree;

    private SteeringBasics steeringBasics;
    private LinePath path;
    private FollowPath followPath;
    private WallAvoidance wallAvoidance;
    private Hide hide;
    private Evade evade;
    private Pursue pursue;

    private Material originalMaterial;
    private List<GameObject> instantiatedSeaMines = new List<GameObject>();
    private int availableMines = 5;
    private bool canSetSeaMine = true;
    private bool isTakingDetour;
    private bool isHiding = false;
    private float minePlacementDistanceThreshold = 20f;
    private float mermaidVisibilityTreshold = 10f;
    private float timeStuck;
    private float stuckThreshold = 4f;
    private float maxHideDuration = 3.0f;
    private float hideStartTime;
    private float health = 100f;
    private Vector3 temporaryDestination;
    private Vector3 previousPosition;
    private Vector3 detourStartPosition;
    private AudioSource audioSourceShark;

    public static event TreasureFoundDelegate OnTreasureFound;
    public delegate void TreasureFoundDelegate();

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        steeringBasics = GetComponent<SteeringBasics>();
        followPath = GetComponent<FollowPath>();
        wallAvoidance = GetComponent<WallAvoidance>();
        hide = GetComponent<Hide>();
        evade = GetComponent<Evade>();
        audioSourceShark = GetComponent<AudioSource>();
        pursue = GetComponent<Pursue>();
    }

    void Start()
    {
        SetupDiverAppearance();
        SetupBehaviorTree();

        // starting the behaviour tree and registering a callback function
        behaviorTree.Start();
        OnTreasureFound += HandleTreasureFound;
    }

    private void SetupDiverAppearance()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material.color = Color.green;
    }

    private void SetupBehaviorTree()
    {
        // creating a behaviour tree node to control the agent's decision-making process
        behaviorTree = new Root(
            new Service(0.5f, () => { UpdateBlackboard(); UpdateHidingDuration(); },
                new Selector(

                    // Behaviour: Stop when health is 0. Game Over!
                    new BlackboardCondition("agentHealth", Operator.IS_SMALLER_OR_EQUAL, 0f, Stops.IMMEDIATE_RESTART,
                        new Selector(
                            new Action(() => StopGameOver())
                        )
                    ),

                    // Behaviour: Running from the shark and placing sea mines when too close
                    new BlackboardCondition("seeShark", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Selector(
                            new BlackboardCondition("sharkTooClose", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                                new Sequence(
                                    new Action(() => PlaceSeaMines()),
                                    new Action(() => EvadeShark())
                                )
                            ),
                        new Selector(
                            new BlackboardCondition("hideDurationReached", Operator.IS_EQUAL, false, Stops.NONE,
                                new Action(() => HideFromShark())
                            ),
                            new Action(() => EvadeShark())
                        )

                        )
                    ),

                    // Behaviour: Getting distracted by mermaid when close its close
                    new BlackboardCondition("isMermaidClose", Operator.IS_EQUAL, true, Stops.IMMEDIATE_RESTART,
                        new Action(() => GetDistractedByMermaid())
                    ),

                    // Behaviour: Seeking the treasure chest
                    new Sequence(
                        new Action(() => FindTreasureChest()),
                        new Action(() => SetPathToTreasureChest()),
                        new Action(() => FollowPathToTreasureChestWithWallAvoidance())
                    )
                )
            )
        );
    }

    private void UpdateBlackboard()
    {
        behaviorTree.Blackboard["agentHealth"] = health;
        behaviorTree.Blackboard["seeShark"] = IsChasedByShark();
        behaviorTree.Blackboard["sharkTooClose"] = IsSharkTooClose();
        behaviorTree.Blackboard["isMermaidClose"] = IsMermaidClose();
    }

    private bool IsMermaidClose()
    {
        // checking if the diver is distracted by a mermaid
        float distanceToMermaid = Vector3.Distance(transform.position, mermaid.transform.position);
        return distanceToMermaid <= mermaidVisibilityTreshold; // true if within a certain treshold
    }

    private void GetDistractedByMermaid()
    {
        if (mermaid != null && wallAvoidance != null)
        {
            Vector3 accel = HandleStuck(mermaid.transform.position, 0.5f, false);
            steeringBasics.Steer(accel);
            steeringBasics.LookWhereYoureGoing();
        }
        else
        {
            Debug.LogError("Mermaid or WallAvoidance component is missing or not assigned.");
        }

        previousPosition = transform.position;
    }


    private bool IsChasedByShark()
    {
        // calculating the direction from the shark to the diver
        Vector3 directionToDiver = (transform.position - shark.transform.position).normalized;
        // calculating the angle between the shark's forward direction and the direction to the diver
        float angle = Vector3.Angle(shark.transform.forward, directionToDiver);

        // checking if the angle is within a certain threshold
        if (angle < 45f)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool IsSharkTooClose()
    {
        SharkAI sharkAI = shark.GetComponent<SharkAI>();
        float sharkHealth = sharkAI.behaviorTree.Blackboard.Get<float>("sharkHealth");
        if (sharkHealth <= 0f) // if shark is dead
        {
            return false;
        }
        float distanceToShark = Vector3.Distance(transform.position, shark.transform.position);
        return distanceToShark <= minePlacementDistanceThreshold; //true if within a certain treshold
    }

    // function to place mines
    private void PlaceSeaMines()
    {
        SharkAI sharkAI = shark.GetComponent<SharkAI>();
        float sharkHealth = sharkAI.behaviorTree.Blackboard.Get<float>("sharkHealth");
        if (!canSetSeaMine || sharkHealth <= 0f) return; // Can set mines now or not? If false, no mine is set.
        if (availableMines > 0)
        {
            // instantiating a sea mine prefab and place it at the diver's position
            if (seaMinePrefab != null)
            {
                Vector3 currentPosition_ = transform.position;
                float newY = currentPosition_.y + 4.5f; // just modifying y axis, so that mines are properly set on the ground

                GameObject seaMine = Instantiate(seaMinePrefab, new Vector3(currentPosition_.x, newY, currentPosition_.z), Quaternion.identity);
                instantiatedSeaMines.Add(seaMine);
                audioSourceShark.Play(); // sound effect
            }
            else
            {
                Debug.LogError("Sea mine prefab is missing or destroyed. Please make sure it is assigned in the DiverAI script.");
            }

            availableMines--; // deducting from available mines
        }
        // starting SeaMineCooldown coroutine to prevent the agent from setting sea mines too frequently.
        StartCoroutine(SeaMineCooldown());
    }

    IEnumerator SeaMineCooldown()
    {
        canSetSeaMine = false;
        yield return new WaitForSeconds(2f); // Wait for 2 seconds
        canSetSeaMine = true;
    }

    public void ResetColorAndHealth()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = Color.green;
            health = 100;
        }
        else
        {
            Debug.LogWarning("MeshRenderer component not found on the agent or its child objects.");
        }
    }


    public void ResetMines()
    {
        // destroying all instantiated sea mines
        foreach (GameObject seaMine in instantiatedSeaMines)
        {
            Destroy(seaMine);
        }
        // clearning the instantiated sea mines list
        instantiatedSeaMines.Clear();
        // resetting the available mines count
        availableMines = 5;

    }

    private void EvadeShark()
    {
        SharkAI sharkAI = shark.GetComponent<SharkAI>();
        float sharkHealth = sharkAI.behaviorTree.Blackboard.Get<float>("sharkHealth");
        if (evade != null && sharkHealth <= 0f)
        {
            MovementAIRigidbody sharkRigidbody = shark.GetComponent<MovementAIRigidbody>();
            Vector3 acceleration = evade.GetSteering(sharkRigidbody);
            isHiding = false;
            steeringBasics.Steer(acceleration);
            steeringBasics.LookWhereYoureGoing();
        }
    }

    public void HideFromShark()
    {
        if (customSpawner != null)
        {
            List<GameObject> spawnedObstacles = customSpawner.spawnedObstacles;
            List<MovementAIRigidbody> obstaclesRigidbodies = new List<MovementAIRigidbody>();

            // adding all obsticles to the map
            foreach (GameObject obstacle in spawnedObstacles)
            {
                MovementAIRigidbody obstacleRigidbody = obstacle.GetComponent<MovementAIRigidbody>();
                obstaclesRigidbodies.Add(obstacleRigidbody);
            }

            MovementAIRigidbody sharkRigidbody = shark.GetComponent<MovementAIRigidbody>();

            Vector3 hidePosition;
            Vector3 hideAccel = hide.GetSteering(sharkRigidbody, obstaclesRigidbodies, out hidePosition);
            Vector3 accel = wallAvoidance.GetSteering(hidePosition - transform.position);

            if (accel.magnitude < 0.005f)
            {
                accel = hideAccel;
            }

            isHiding = true;
            hideStartTime = Time.time; // hide start time, this is for not to hide forever.

            steeringBasics.Steer(accel);
            steeringBasics.LookWhereYoureGoing();

        }
        else
        {
            Debug.LogError("CustomSpawner reference not set in DiverAI.");
        }
    }

    // updating hiding durarion to blackboard (NPBehave)
    private void UpdateHidingDuration()
    {
        if (isHiding && Time.time - hideStartTime > maxHideDuration)
        {
            behaviorTree.Blackboard["hideDurationReached"] = true;
        }
        else
        {
            behaviorTree.Blackboard["hideDurationReached"] = false;
        }
    }

    void Update()
    {
    }

    // for tresure chest position
    private void FindTreasureChest()
    {
        if (treasureChest != null)
        {
            behaviorTree.Blackboard["chestPosition"] = treasureChest.transform.position;
        }
    }

    private void SetPathToTreasureChest()
    {
        Vector3 chestPosition = behaviorTree.Blackboard.Get<Vector3>("chestPosition");
        Vector3 midPoint = (transform.position + chestPosition) / 2;
        Vector3[] waypoints = new Vector3[] { transform.position, midPoint, chestPosition };
        path = new LinePath(waypoints);
        path.CalcDistances(); // calculating the distance
    }

    // This is an important part. Basically, this method makes an agent in a game follow a path to a treasure chest while avoiding walls.
    // It calculates the acceleration based on the agent's position and the treasure chest's position, and also checks if the agent is taking a detour or not.
    // If it is, it calculates the temporary destination and checks if the agent has reached 50% of the distance to the detour start position.
    // If the agent is stuck, it chooses a temporary destination and updates the agent's position and acceleration based on the wall avoidance acceleration.
    // It also updates the agent's previous position.

    private void FollowPathToTreasureChestWithWallAvoidance()
    {
        if (path != null && followPath != null && wallAvoidance != null)
        {
            Vector3 accel = HandleStuck(treasureChest.transform.position, 0.5f, true);
            steeringBasics.Steer(accel);
            steeringBasics.LookWhereYoureGoing();
        }
        else
        {
            Debug.LogError("Path, FollowPath, or WallAvoidance component is missing or not assigned.");
        }

        previousPosition = transform.position;
    }


    private Vector3 HandleStuck(Vector3 targetPosition, float threshold = 0.5f, bool isTreasureChest = false)
    {
        Vector3 accel;
        Vector3 wallAvoidanceAcceleration = wallAvoidance.GetSteering();
        // if wall avoidance acceleration is low, follow the path or take a detour
        if (wallAvoidanceAcceleration.magnitude < 0.5f)
        {
            // if taking a detour, arrive at the temporary destination
            if (isTakingDetour)
            {
                accel = steeringBasics.Arrive(temporaryDestination);
                float distanceToDestination = Vector3.Distance(transform.position, temporaryDestination);
                float totalDetourDistance = Vector3.Distance(temporaryDestination, detourStartPosition);
                if (distanceToDestination < totalDetourDistance * threshold)
                {
                    isTakingDetour = false;
                }
            }
            // if not taking detour, then follow the path to the tresure chest?
            else
            {
                float distanceToChest = Vector3.Distance(transform.position, treasureChest.transform.position);
                if (isTreasureChest || (distanceToChest < 30f))
                {
                    accel = followPath.GetSteering(path);
                    if (distanceToChest < 20f)
                    {
                        OnTreasureFound?.Invoke();
                    }
                }
                else
                {
                    accel = steeringBasics.Arrive(targetPosition);
                }
            }
        }
        else
        {
            // calculating distance moved to check if the agent is stuck
            float distanceMoved = Vector3.Distance(transform.position, previousPosition);

            if (distanceMoved <= 7f)
            {
                timeStuck += Time.deltaTime;
            }
            else
            {
                timeStuck = 0;
            }

            if (timeStuck >= stuckThreshold)
            {
                isTakingDetour = true;
                detourStartPosition = transform.position;
                temporaryDestination = ChooseTemporaryDestination();
                timeStuck = 0;
            }

            accel = wallAvoidanceAcceleration;
        }

        return accel;
    }

    private void HandleTreasureFound()
    {
        UnderwaterCaveGenerator underwaterCaveGenerator = FindObjectOfType<UnderwaterCaveGenerator>();
        if (underwaterCaveGenerator != null)
        {
            underwaterCaveGenerator.RegenerateCave();
            List<GameObject> agents = new List<GameObject> { treasureChest };
            underwaterCaveGenerator.PlacePrefabs();
        }
        ResetMines();
        ResetColorAndHealth();

        // when tresure found shark should be reset as well
        SharkAI sharkAI = shark.GetComponent<SharkAI>();
        sharkAI.ResetColorAndHealth();

        // when tresure found shark should be reset as well
        MermaidAI mermaidAI = mermaid.GetComponent<MermaidAI>();
        mermaidAI.ResetColorAndHealth();
    }

    private Vector3 ChooseTemporaryDestination()
    {
        // calculating the direction and distance of the detour
        Vector3 direction = Quaternion.Euler(0, URandom.Range(30, 60), 0) * transform.forward;
        float distance = URandom.Range(20f, 50f); // desired detour distance range

        // calculating the temporary destination
        Vector3 temporaryDestination = transform.position + (direction * distance);

        return temporaryDestination;
    }

    private void OnDestroy()
    {
        OnTreasureFound -= HandleTreasureFound;
    }

    public void DecreaseHealth(float amount)
    {
        health -= amount;
    }

    // decreasing agent health when colliding with shark
    private void OnCollisionEnter(Collision collision)
    {
        SharkAI sharkAI = shark.GetComponent<SharkAI>();
        float sharkHealth = sharkAI.behaviorTree.Blackboard.Get<float>("sharkHealth");


        if (collision.gameObject.CompareTag("Shark") && (sharkHealth > 0f))
        {
            DecreaseHealth(10);
            Debug.Log("Diver's health: " + health);
        }
    }

    private void StopGameOver()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
                meshRenderer.material.color = Color.black;
        }
        else
        {
            Debug.LogWarning("MeshRenderer component not found on the agent or its child objects.");
        }
    }


}
