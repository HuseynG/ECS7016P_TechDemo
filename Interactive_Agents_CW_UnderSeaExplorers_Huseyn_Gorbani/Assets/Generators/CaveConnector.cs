using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// To achieve this code the video series and github link below (i.e., provided by Sebastian Lague) was studied utilised partially
// Link to YouTube Playlist: https://www.youtube.com/playlist?list=PLFt_AvWsXl0eZgMK_DT5_biRkWXftAOf9
// Link to GitHub Repo: https://github.com/SebLague/Procedural-Cave-Generation
// This code essentially converts connects isolated zones (rooms) to each other.
public class CaveConnector : MonoBehaviour
{
    public UnderwaterCaveGenerator caveGenerator;
    public MeshGenerator meshGenerator;

    // defining Room class with a list of tiles and a center
    public class Room
    {
        public List<Vector2Int> tiles;
        public Vector2 center;

        // takes a list of tiles and calculates their center
        public Room(List<Vector2Int> tiles)
        {
            this.tiles = tiles;
            CalculateCenter();
        }

        // calculating the center of the room by averaging the tile positions
        private void CalculateCenter()
        {

            Vector2 sum = Vector2.zero;
            foreach (Vector2Int tile in tiles)
            {
                sum += tile;
            }
            center = sum / tiles.Count;
        }
    }

    // identifying and sorting rooms using flood fill algorithm
    public List<Room> IdentifyAndSortRooms(int[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);
        bool[,] visited = new bool[width, height];
        List<Room> rooms = new List<Room>();

        // iterating through the map and calling FloodFill on unvisited water tiles
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!visited[x, y] && map[x, y] == 0)
                {
                    List<Vector2Int> roomTiles = new List<Vector2Int>();
                    FloodFill(map, visited, x, y, roomTiles);
                    rooms.Add(new Room(roomTiles));
                }
            }
        }

        // sorting rooms by size in descending order
        rooms.Sort((a, b) => b.tiles.Count.CompareTo(a.tiles.Count));
        return rooms;
    }


    // Flood Fill algorithm to identify room tiles
    private void FloodFill(int[,] map, bool[,] visited, int x, int y, List<Vector2Int> roomTiles)
    {
        // checking for visited tiles ot out of bounds
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        if (x < 0 || x >= width || y < 0 || y >= height) return;
        if (visited[x, y] || map[x, y] == 1) return;

        visited[x, y] = true;
        roomTiles.Add(new Vector2Int(x, y));

        // recursive flood fill calls for neighboring tiles
        FloodFill(map, visited, x + 1, y, roomTiles);
        FloodFill(map, visited, x - 1, y, roomTiles);
        FloodFill(map, visited, x, y + 1, roomTiles);
        FloodFill(map, visited, x, y - 1, roomTiles);
    }

    // finding the closet room to current room
    public Room FindClosestRoom(Room room, List<Room> rooms)
    {
        Room closestRoom = null;
        float closestDistance = float.MaxValue;

        // iterating through the list of available rooms to find the closet one
        foreach (Room otherRoom in rooms)
        {
            if (otherRoom == room) continue;

            float distance = Vector2.Distance(room.center, otherRoom.center);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestRoom = otherRoom;
            }
        }

        return closestRoom;
    }

    // creating a corridor between two points in the grid
    private int[,] CreateCorridor(int[,] grid, Vector2Int startPoint, Vector2Int endPoint)
    {
        int corridorWidth = 3; // width of the corridor

        while (startPoint != endPoint)
        {
            if (startPoint.x < endPoint.x)
            {
                startPoint.x++;
            }
            else if (startPoint.x > endPoint.x)
            {
                startPoint.x--;
            }
            else if (startPoint.y < endPoint.y)
            {
                startPoint.y++;
            }
            else if (startPoint.y > endPoint.y)
            {
                startPoint.y--;
            }

            // setting the grid tiles to water (0) to create the corridor
            // so, looping through a square area around the startPoint with the side length of corridorWidth
            for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
            {
                for (int j = -corridorWidth / 2; j <= corridorWidth / 2; j++)
                {
                    int x = startPoint.x + i;
                    int y = startPoint.y + j;

                    // before updating the grid we check for out of bounds
                    if (x >= 0 && x < grid.GetLength(0) && y >= 0 && y < grid.GetLength(1))
                    {
                        grid[x, y] = 0;
                    }
                }
            }
        }

        return grid;
    }

    // connecting rooms using corridors
    public int[,] ConnectRooms(int[,] grid, List<Room> rooms)
    {
        // looping trough list of rooms to connect each of them to its closest one
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Room roomA = rooms[i];
            Room roomB = FindClosestRoom(roomA, rooms);

            // converting (with rounding) the room centers to Vecto2Int to avoid float
            Vector2Int startPoint = new Vector2Int(Mathf.RoundToInt(roomA.center.x), Mathf.RoundToInt(roomA.center.y));
            Vector2Int endPoint = new Vector2Int(Mathf.RoundToInt(roomB.center.x), Mathf.RoundToInt(roomB.center.y));

            // connecting roomA and roomB using corridors
            grid = CreateCorridor(grid, startPoint, endPoint);
        }

        return grid;
    }

}
