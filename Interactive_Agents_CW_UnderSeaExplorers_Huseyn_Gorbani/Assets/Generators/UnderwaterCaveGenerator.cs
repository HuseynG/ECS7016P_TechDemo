using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using NPBehave;
using URandom = UnityEngine.Random;

public class UnderwaterCaveGenerator : MonoBehaviour
{
    public int width = 192;
    public int height = 108;
    public int fillPercent = 43;
    public int simulationSteps = 20;
    public int seed;
    public bool usingRandomSeed;
    private int[,] grid;
    private bool isGridReady = false;
    public UnityEvent OnCaveGenerationCompleted;
    public Tilemap soilTilemap;
    public Tilemap waterTilemap;

    public MeshGenerator meshGenerator;
    public MeshGenerator waterMeshGenerator;

    // nav mesh generator
    public NavMeshSurface navMeshSurface;

    // cave connector
    public CaveConnector caveConnector;

    //public GameObject treasureChest;
    public GameObject treasureChest;
    public GameObject diver;
    public GameObject shark;
    public GameObject mermaid;

    // cutom tile
    private Tile CreateTile(Color color)
    {
        // creating texture with the specified color
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();

        // creating new sprite using the texture
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);

        // creating new tile with the sprite
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;

        return tile;
    }

    // to initialise the defualt cave (random)
    private void InitialiseGrid(bool usingRandomSeed)
    {
        if (usingRandomSeed) { seed = System.Guid.NewGuid().GetHashCode(); }

        System.Random pseudoRan = new System.Random(seed); // setting seed value

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // edges of screen should be soil
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1) { grid[x, y] = 1; }
                else
                {
                    // if generated number is less than fillPercent it is soil,
                    // otherwise it is water
                    grid[x, y] = (pseudoRan.Next(0, 100) < fillPercent) ? 1 : 0;
                }
            }
        }
    }

    //counting neihgbouring cells
    private int CountNeighbouringSoils(int gridX, int gridY)
    {
        int soilCount = 0;
        for (int x = gridX - 1; x <= gridX + 1; x++)
        {
            for (int y = gridY - 1; y <= gridY + 1; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    if (x != gridX || y != gridY) { soilCount += grid[x, y]; }
                }
                else
                {
                    soilCount++;
                }
            }
        }
        return soilCount;
    }

    //for cellular automata rules
    private void SimulateStep()
    {
        int[,] newGrid = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int numOfNeighbouringCells = CountNeighbouringSoils(x, y);

                if (grid[x, y] == 0 && numOfNeighbouringCells > 4)
                {
                    newGrid[x, y] = 1;
                }
                else if (grid[x, y] == 1 && numOfNeighbouringCells < 4)
                {
                    newGrid[x, y] = 0;
                }
                else
                {
                    newGrid[x, y] = grid[x, y];
                }
            }
        }

        grid = newGrid;
    }

    // generate levels and assigning tiles
    private void GenerateLevel(Tile waterTile, Tile soilTile)
    {
        soilTilemap.ClearAllTiles();
        waterTilemap.ClearAllTiles();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);

                if (grid[x, y] == 0)
                {
                    waterTilemap.SetTile(position, waterTile);
                }
                else
                {
                    soilTilemap.SetTile(position, soilTile);
                }
            }
        }
    }

    public void GenerateCave(bool usingRandomSeed)
    {
        // defining colors for tiles
        Tile waterTile = CreateTile(new Color(153 / 255f, 255 / 255f, 255 / 255f));
        Tile soilTile = CreateTile(new Color(102 / 255f, 51 / 255f, 0 / 255f)); // Brown color

        // creating a grid
        grid = new int[width, height];

        // intialising grid
        InitialiseGrid(usingRandomSeed);

        for (int i = 0; i < simulationSteps; i++)
        {
            // cellular automat in action
            SimulateStep();
        }
        // grid as cave
        GenerateLevel(waterTile, soilTile);
    }

    public void ResizeMap(float percentage)
    {
        float width_ = (float)width;
        float height_ = (float)height;

        width_ *= (1f + percentage);
        height_ *= (1f + percentage);

        height = (int)height_;
        width = (int)width_;
    }

    public void GenerateCaveWithMesh(bool usingRandomSeed)
    {
        // to reasize the map after each generation
        ResizeMap(0.04f);

        GenerateCave(usingRandomSeed);

        // indentifying and sorting rooms
        List<CaveConnector.Room> rooms = caveConnector.IdentifyAndSortRooms(grid);

        // updating grids, after connecting rooms
        grid = caveConnector.ConnectRooms(grid, rooms);

        Vector3 rotation = new Vector3(0, 0, 0);
        Vector3 position = new Vector3(0, 0, 0);
        // soil
        meshGenerator.GenerateMesh(GetCaveGrid(), 1f, rotation, position, -7);
        // water
        GenerateWaterMesh(); // Call the new method here

        BakeNavMesh();

        PlacePrefabs();

        isGridReady = true;
        // Invoke the event
        OnCaveGenerationCompleted?.Invoke();
    }

    public int[,] GetCaveGrid()
    {
        return grid;
    }

    public void GenerateWaterMesh()
    {
        int[,] waterGrid = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                waterGrid[x, y] = grid[x, y] == 0 ? 1 : 0;
            }
        }

        Vector3 waterRotation = new Vector3(0, 0, 0);
        Vector3 waterPosition = new Vector3(0, 0, -1);
        waterMeshGenerator.GenerateMesh(waterGrid, 1f, waterRotation, waterPosition, 0);
    }

    public void BakeNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.size = GetLevelBounds();
            navMeshSurface.BuildNavMesh();
        }
        else
        {
            Debug.LogError("NavMeshSurface reference not set in UnderwaterCaveGenerator.");
        }
    }

    public Vector3 GetLevelBounds()
    {
        return new Vector3(width, height, 10); // setting the z value to a reasonable depth for level
    }

    public void PlacePrefabs()
    {

        Vector3 chestPosition = GetRandomPointOnNavMesh(); // this will place the chest at a random position
        Vector3 sharkPosition = GetRandomPointOnNavMesh(minDistancePercentageFromReference: 5f, maxDistancePercentageFromReference: 10f, referencePosition: chestPosition); // Place shark within 5-10% of map size from the chest
        Vector3 diverPosition = GetRandomPointOnNavMesh(minDistancePercentageFromReference: 100f, maxDistancePercentageFromReference: 120f, referencePosition: chestPosition);
        Vector3 mermaidPosition = GetRandomPointOnNavMesh(minDistancePercentageFromReference: 100f, maxDistancePercentageFromReference: 120f, referencePosition: chestPosition);

        chestPosition.y += 6.5f; // to fix the positioning of chest
        treasureChest.transform.position = chestPosition;
        diver.transform.position = diverPosition;
        shark.transform.position = sharkPosition;
        mermaid.transform.position = mermaidPosition;
    }

    public void RegenerateCave()
    {
        GenerateCaveWithMesh(usingRandomSeed);
    }

    private Vector3 GetRandomPointOnNavMesh(float? minDistancePercentageFromReference = null, float? maxDistancePercentageFromReference = null, Vector3? referencePosition = null)
    {
        Vector3 randomPosition = Vector3.zero;
        bool validPositionFound = false;

        while (!validPositionFound)
        {
            // generating random point within the bounds of the cave
            Vector3 randomPoint = new Vector3(URandom.Range(1, width - 1), 0, URandom.Range(1, height - 1));

            // calculating the world position of the random point
            NavMeshHit hit;

            if (NavMesh.SamplePosition(randomPoint, out hit, 100.0f, NavMesh.AllAreas))
            {
                randomPosition = hit.position;

                if (referencePosition.HasValue && minDistancePercentageFromReference.HasValue && maxDistancePercentageFromReference.HasValue)
                {
                    float distance = Vector3.Distance(randomPosition, referencePosition.Value);

                    // calcualting minimum and maximum distances based on percentage
                    float minDistance = Mathf.Min(width, height) * minDistancePercentageFromReference.Value / 100f;
                    float maxDistance = Mathf.Min(width, height) * maxDistancePercentageFromReference.Value / 100f;

                    if (distance >= minDistance && distance <= maxDistance)
                    {
                        validPositionFound = true;
                    }
                }
                else
                {
                    validPositionFound = true;
                }
            }
        }

        return randomPosition;
    }

    public List<Vector3> GetSoilPositions()
    {
        List<Vector3> soilPositions = new List<Vector3>();

        //Debug.Log($"isGridReady: {isGridReady}"); // would add this line to check the value of isGridReady

        if (isGridReady)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] == 1)
                    {
                        soilPositions.Add(new Vector3((3 * x) - (width * 3 / 2), 0, (3 * y) - (height * 3 / 2))); ///??
                    }
                }
            }
        }
        //Debug.Log($"Generated {soilPositions.Count} soil positions.");
        return soilPositions;
    }

    private void Awake()
    {
        // to set the fps rate to 60hz
#if UNITY_EDITOR
        QualitySettings.vSyncCount = 0;  // disabling VSync 
        Application.targetFrameRate = 60;
#endif
    }

    void Start()
    {
        // if mesh generator does not exist try finding
        if (meshGenerator == null)
        {
            meshGenerator = FindObjectOfType<MeshGenerator>();
        }

        // if it is still null try components
        if (meshGenerator == null)
        {
            meshGenerator = GetComponent<MeshGenerator>();
        }

        // if it it still null return error
        if (meshGenerator == null)
        {
            Debug.LogError("MeshGenerator component not found on the same GameObject.");
            return;
        }

        // start generating cave and meshes
        GenerateCaveWithMesh(usingRandomSeed);
    }

    private void RestartAgentsAndDeleteMines()
    {
        if (diver != null)
        {
            // getting the DiverAI component from the diver GameObject
            DiverAI diverAI = diver.GetComponent<DiverAI>();
            SharkAI sharkAI = shark.GetComponent<SharkAI>();
            MermaidAI mermaidAI = mermaid.GetComponent<MermaidAI>();


            // calling the new method to reset the color and health of the agents
            diverAI.ResetColorAndHealth();
            sharkAI.ResetColorAndHealth();
            mermaidAI.ResetColorAndHealth();
            diverAI.ResetMines();

        }
        else
        {
            Debug.LogError("Agent GameObject is not assigned.");
        }
    }

    // to restart whole game, Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GenerateCaveWithMesh(usingRandomSeed);

            // reset agents and delete any exisiting mines
            RestartAgentsAndDeleteMines();

        }
    }


}