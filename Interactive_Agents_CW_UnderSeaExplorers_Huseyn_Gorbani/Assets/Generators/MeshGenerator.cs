using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
// To achieve this code the video series and github link below (i.e., provided by Sebastian Lague) was studied utilised partially
// Link to YouTube Playlist: https://www.youtube.com/playlist?list=PLFt_AvWsXl0eZgMK_DT5_biRkWXftAOf9
// Link to GitHub Repo: https://github.com/SebLague/Procedural-Cave-Generation
// This code essentially converts the generated map in UnderwaterCaveGenerator script to
// Mesh while utlising marching square process to make the map more appealing.
public class MeshGenerator : MonoBehaviour
{

	public CellGrid cellGrid;
	List<Vector3> vertices;
	List<int> triangles;

	// generating mesh based on the given grid and cell size
	public void GenerateMesh(int[,] map, float cellSize = 1f, Vector3 rotation = default(Vector3), Vector3 position = default(Vector3), float depth = 0)
	{
		CreateCellGrid(map, cellSize);
		InitializeVertexAndTriangleLists();
		ProcessGridCells();
		CreateAndAssignMesh(rotation, position, depth);
	}

	// creating CellGrid object using the given map and cell size
	private void CreateCellGrid(int[,] map, float cellSize)
	{
		cellGrid = new CellGrid(map, cellSize);
	}

	// initialising vertex and triangle lists
	private void InitializeVertexAndTriangleLists()
	{
		vertices = new List<Vector3>();
		triangles = new List<int>();
	}

	// processing each cell in the grid to triangulate them
	private void ProcessGridCells()
	{
		for (int x = 0; x < cellGrid.cells.GetLength(0); x++)
		{
			for (int y = 0; y < cellGrid.cells.GetLength(1); y++)
			{
				TriangulateCell(cellGrid.cells[x, y]);
			}
		}
	}

	private void CreateAndAssignMesh(Vector3 rotation, Vector3 position, float depth)
	{
		ExtrudeMesh(depth);
		Mesh mesh = new Mesh
		{
			vertices = vertices.ToArray(),
			triangles = triangles.ToArray()
		};
		mesh.RecalculateNormals();

		MeshFilter meshFilter = GetComponent<MeshFilter>();
		MeshCollider meshCollider = GetComponent<MeshCollider>();

		meshFilter.mesh = mesh;
		meshCollider.sharedMesh = mesh;

		// setting the rotation for the mesh
		transform.eulerAngles = rotation;
		transform.position = position;
	}


	private void ExtrudeMesh(float depth)
	{
		int originalVertexCount = vertices.Count;
		for (int i = 0; i < originalVertexCount; i++)
		{
			Vector3 vertex = vertices[i];
			Vector3 extrudedVertex = new Vector3(vertex.x, vertex.y - depth, vertex.z);
			vertices.Add(extrudedVertex);
		}

		int originalTriangleCount = triangles.Count;
		for (int i = 0; i < originalTriangleCount; i += 3)
		{
			int a = triangles[i];
			int b = triangles[i + 1];
			int c = triangles[i + 2];

			int a2 = a + originalVertexCount;
			int b2 = b + originalVertexCount;
			int c2 = c + originalVertexCount;

			// adding back face triangles
			triangles.Add(c2);
			triangles.Add(b2);
			triangles.Add(a2);

			// adding side face triangles
			triangles.AddRange(new int[] { a, b, b2, a, b2, a2 });
			triangles.AddRange(new int[] { b, c, c2, b, c2, b2 });
			triangles.AddRange(new int[] { c, a, a2, c, a2, c2 });
		}
	}

	//triangulating a cell based on its config
	void TriangulateCell(Cell cell)
	{
		switch (cell.configuration)
		{
			case 0:
				break;

			// 1 points:
			case 1:
				MeshFromPoints(cell.centreBottom, cell.bottomLeft, cell.centreLeft);
				break;
			case 2:
				MeshFromPoints(cell.centreRight, cell.bottomRight, cell.centreBottom);
				break;
			case 4:
				MeshFromPoints(cell.centreTop, cell.topRight, cell.centreRight);
				break;
			case 8:
				MeshFromPoints(cell.topLeft, cell.centreTop, cell.centreLeft);
				break;

			// 2 points:
			case 3:
				MeshFromPoints(cell.centreRight, cell.bottomRight, cell.bottomLeft, cell.centreLeft);
				break;
			case 6:
				MeshFromPoints(cell.centreTop, cell.topRight, cell.bottomRight, cell.centreBottom);
				break;
			case 9:
				MeshFromPoints(cell.topLeft, cell.centreTop, cell.centreBottom, cell.bottomLeft);
				break;
			case 12:
				MeshFromPoints(cell.topLeft, cell.topRight, cell.centreRight, cell.centreLeft);
				break;
			case 5:
				MeshFromPoints(cell.centreTop, cell.topRight, cell.centreRight, cell.centreBottom, cell.bottomLeft, cell.centreLeft);
				break;
			case 10:
				MeshFromPoints(cell.topLeft, cell.centreTop, cell.centreRight, cell.bottomRight, cell.centreBottom, cell.centreLeft);
				break;

			// 3 point:
			case 7:
				MeshFromPoints(cell.centreTop, cell.topRight, cell.bottomRight, cell.bottomLeft, cell.centreLeft);
				break;
			case 11:
				MeshFromPoints(cell.topLeft, cell.centreTop, cell.centreRight, cell.bottomRight, cell.bottomLeft);
				break;
			case 13:
				MeshFromPoints(cell.topLeft, cell.topRight, cell.centreRight, cell.centreBottom, cell.bottomLeft);
				break;
			case 14:
				MeshFromPoints(cell.topLeft, cell.topRight, cell.bottomRight, cell.centreBottom, cell.centreLeft);
				break;

			// 4 point:
			case 15:
				MeshFromPoints(cell.topLeft, cell.topRight, cell.bottomRight, cell.bottomLeft);
				break;
		}

	}

	// generating mesh from given points
	private void MeshFromPoints(params Node[] points)
	{
		AssignVertices(points);

		for (int i = 2; i < points.Length; i++)
		{
			CreateTriangle(points[0], points[i - 1], points[i]);
		}
	}

	// assigning verices to the points if not already assigned
	void AssignVertices(Node[] points)
	{
		for (int i = 0; i < points.Length; i++)
		{
			if (points[i].vertexIndex == -1)
			{
				points[i].vertexIndex = vertices.Count;
				vertices.Add(points[i].position);
			}
		}
	}

	// creating a triangle using the given nodes
	void CreateTriangle(Node a, Node b, Node c)
	{
		triangles.Add(a.vertexIndex);
		triangles.Add(b.vertexIndex);
		triangles.Add(c.vertexIndex);
	}

	// class to store the grid of cells
	public class CellGrid
	{
		public Cell[,] cells;

		public CellGrid(int[,] map, float cellSize)
		{
			int nodeCountX = map.GetLength(0);
			int nodeCountY = map.GetLength(1);
			float mapWidth = nodeCountX * cellSize;
			float mapHeight = nodeCountY * cellSize;

			ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

			for (int x = 0; x < nodeCountX; x++)
			{
				for (int y = 0; y < nodeCountY; y++)
				{
					Vector3 pos = new Vector3(-mapWidth / 2 + x * cellSize + cellSize / 2, 0, -mapHeight / 2 + y * cellSize + cellSize / 2);
					controlNodes[x, y] = new ControlNode(pos, map[x, y] == 1, cellSize);
				}
			}

			cells = new Cell[nodeCountX - 1, nodeCountY - 1];
			for (int x = 0; x < nodeCountX - 1; x++)
			{
				for (int y = 0; y < nodeCountY - 1; y++)
				{
					cells[x, y] = new Cell(controlNodes[x, y + 1], controlNodes[x + 1, y + 1], controlNodes[x + 1, y], controlNodes[x, y]);
				}
			}

		}
	}

	// class representing a single cell in a the grid
	public class Cell
	{
		public ControlNode topLeft, topRight, bottomRight, bottomLeft;
		public Node centreTop, centreRight, centreBottom, centreLeft;
		public int configuration;

		public Cell(ControlNode _topLeft, ControlNode _topRight, ControlNode _bottomRight, ControlNode _bottomLeft)
		{
			topLeft = _topLeft;
			topRight = _topRight;
			bottomRight = _bottomRight;
			bottomLeft = _bottomLeft;

			centreTop = topLeft.right;
			centreRight = bottomRight.above;
			centreBottom = bottomLeft.right;
			centreLeft = bottomLeft.above;

			configuration = (topLeft.active ? 8 : 0) + (topRight.active ? 4 : 0) + (bottomRight.active ? 2 : 0) + (bottomLeft.active ? 1 : 0);

		}

	}

	// class representing a single central node as explained in the YouTube Video
	public class Node
	{
		public Vector3 position { get; set; }
		public int vertexIndex { get; set; } = -1;

		public Node(Vector3 _pos)
		{
			position = _pos;
		}
	}

	// corner nodes as it is also explained in the YouTube Video Mentioned above
	public class ControlNode : Node
	{

		public bool active { get; set; }
		public Node above { get; set; }
		public Node right { get; set; }

		public ControlNode(Vector3 _pos, bool _active, float cellSize) : base(_pos)
		{
			active = _active;
			above = new Node(position + Vector3.forward * cellSize / 2f);
			right = new Node(position + Vector3.right * cellSize / 2f);
		}

	}
}