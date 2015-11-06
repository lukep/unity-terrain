using UnityEngine;
using System.Collections;
//using Utility;
using HelixToolkit.Wpf;
using System.Collections.Generic;

public class TerrainManager : MonoBehaviour {
	
	public GameObject playerGameObject;
	public Terrain referenceTerrain;
	public int TERRAIN_BUFFER_COUNT = 50;
	public int spread = 1;
	
	private int[] currentTerrainID;
	private Terrain[] terrainBuffer;
	private DoubleKeyDictionary<int, int, int> terrainUsage;
	private DoubleKeyDictionary<int, int, TerrainData> terrainUsageData;
	private BitArray usedTiles;
	private BitArray touchedTiles;
	private Vector3 referencePosition;
	private Vector2 referenceSize;
	private Quaternion referenceRotation;

	private int tileWidth;
	private int tileHeight;

	private enum Directions {Front, Right, Back, Left};
	
	// Use this for initialization
	void Start () {
		currentTerrainID = new int[2];
		terrainBuffer = new Terrain[TERRAIN_BUFFER_COUNT];
		terrainUsage = new DoubleKeyDictionary<int, int, int>();
		terrainUsageData = new DoubleKeyDictionary<int, int, TerrainData>();
		usedTiles = new BitArray(TERRAIN_BUFFER_COUNT, false);
		touchedTiles = new BitArray(TERRAIN_BUFFER_COUNT, false);
		
		referencePosition = referenceTerrain.transform.position;
		referenceRotation = referenceTerrain.transform.rotation;
		referenceSize = new Vector2(referenceTerrain.terrainData.size.x, referenceTerrain.terrainData.size.z);

		tileWidth = referenceTerrain.terrainData.heightmapWidth;
		tileHeight = referenceTerrain.terrainData.heightmapHeight;
		
		for(int i=0; i<TERRAIN_BUFFER_COUNT; i++)
		{
			TerrainData tData = new TerrainData();
			CopyTerrainDataFromTo(referenceTerrain.terrainData, ref tData);
			terrainBuffer[i] = Terrain.CreateTerrainGameObject(tData).GetComponent<Terrain>();
			terrainBuffer[i].gameObject.SetActive(false);
		}
	}
	
	// Update is called once per frame
	void Update () {
		ResetTouch();
		Vector3 warpPosition = playerGameObject.transform.position;
		TerrainIDFromPosition(ref currentTerrainID, ref warpPosition);
		
		string dbgString = "";
		dbgString = "CurrentID : " + currentTerrainID[0] + ", " + currentTerrainID[1] + "\n\n";




		// Starts behind the player?

		// Spread is the amount of surrounding terrains to draw

		// Why is this called so often?

		for(int i=-spread;i<=spread;i++)
		{
			for(int j=-spread;j<=spread;j++)
			{	
				DropTerrainAt(currentTerrainID[0] + i, currentTerrainID[1] + j);
				dbgString += (currentTerrainID[0] + i) + "," + (currentTerrainID[1] + j) + "\n";
			}
		}

		//Debug.Log(dbgString);

		// TODO: Reactivate this when needed
		//ReclaimTiles();
	}
	
	void TerrainIDFromPosition(ref int[] currentTerrainID, ref Vector3 position)
	{
		currentTerrainID[0] = Mathf.RoundToInt((position.x - referencePosition.x )/ referenceSize.x);
		currentTerrainID[1] = Mathf.RoundToInt((position.z - referencePosition.z )/ referenceSize.y);
	}
	
	void DropTerrainAt(int i, int j)
	{
		bool doUpdate = false;
		// Check if terrain exists, if it does, activate it.
		if(terrainUsage.ContainsKey(i, j) && terrainUsage[i,j] != -1)
		{
			// Tile mapped, use it.
		}
		// If terrain doesn't exist, drop it.
		else
		{
			doUpdate = true;

			terrainUsage[i,j] = FindNextAvailableTerrainID();
			if(terrainUsage[i,j] == -1) Debug.LogError("No more tiles, failing...");
		}

		if(terrainUsageData.ContainsKey(i,j))
		{
			// Restore the data for this tile
		}
		else
		{
			doUpdate = true;

			// Create a new data object
			Debug.Log("Calling CreateNewTerrainData at " + i + " " + j);
			terrainUsageData[i,j] = CreateNewTerrainData();
		}

		// No problems, time to activate the tile!

		// TODO: Should the edit happen here?


		if(doUpdate){
			ActivateUsedTile(i, j);
			usedTiles[terrainUsage[i,j]] = true;
			touchedTiles[terrainUsage[i,j]] = true;
		}

	}
	
	TerrainData CreateNewTerrainData()
	{

		TerrainData tData = new TerrainData();
		CopyTerrainDataFromTo(referenceTerrain.terrainData, ref tData);
		return tData;

	}
	
	void ResetTouch()
	{
		touchedTiles.SetAll(false);
	}
	
	int CountOnes(BitArray arr)
	{
		int count = 0;
		for(int i=0;i<arr.Length;i++)
		{
			if(arr[i])
				count++;
		}
		return count;
	}
	
	void ReclaimTiles()
	{
		if(CountOnes(usedTiles) > ((spread*2 + 1)*(spread*2 + 1)))
		{
			for(int i=0;i<usedTiles.Length;i++)
			{
				if(usedTiles[i] && !touchedTiles[i])
				{
					usedTiles[i] = false;
					terrainBuffer[i].gameObject.SetActive(false);
				}
			}
		}
	}
	
	void ActivateUsedTile(int i, int j)
	{
		terrainBuffer[terrainUsage[i, j]].gameObject.transform.position = 
									new Vector3(  	referencePosition.x + i * referenceSize.x,
													referencePosition.y,
													referencePosition.z + j * referenceSize.y);
		terrainBuffer[terrainUsage[i, j]].gameObject.transform.rotation = referenceRotation;
		terrainBuffer[terrainUsage[i, j]].gameObject.SetActive(true);
		
		terrainBuffer[terrainUsage[i, j]].terrainData = terrainUsageData[i, j];


		// The original terrain should stay the same
		if( !(i==0 && j==0) ){

			UpdateTerrain (i, j);

		}

	}

	void UpdateTerrain(int i, int j){

		Terrain terrainBlock = terrainBuffer [terrainUsage [i, j]];

		// 0,0 is original tile, 0,1 is front, -1,0 is left, 1,0 is right
		terrainBlock.name = "Tile " + i + " " + j;

		Debug.Log("Updating: " + terrainBlock.name);

		int xResolution = terrainBlock.terrainData.heightmapWidth;
		int zResolution = terrainBlock.terrainData.heightmapHeight;
		
		Vector2 terrainSize = new Vector2 (xResolution, zResolution);
		float heightVal = 0;
		float xFactor, yFactor;
		int midPoint = (int)terrainSize.x / 2;

		float heightFactor = 600.0f;
		
		float[,] heights = new float[ (int)terrainSize.x, (int)terrainSize.y ];  //terrainBlock.terrainData.GetHeights(0, 0, (int)terrainSize.x, (int)terrainSize.y);

		//float[,] thisStartHeight = new float[,]{{0}};
		float[] thisStartHeight = new float[xResolution];


		/*

		Make sure there is some AI work being done here!


		*/


		// Look around this tile to alter its heightmap



		// Test with just the terrain in FRONT of the player
		if(i == currentTerrainID[0] && j == currentTerrainID[1] ){

			thisStartHeight = ReturnBorder(i, j-1, Directions.Back);
			//Debug.Log("START HEIGHT: " + i + " " + j + " " + thisStartHeight[70] + " " + thisStartHeight[80]);

		} else if (i == currentTerrainID[0]+1 && j == currentTerrainID[1]-1){

			// TODO: Alter the tile to the right!

			//thisStartHeight = ReturnBorder(i+1, j, Directions.Right);

		}

		// Loop over every point on the terrain and adjust the height
		for (int My = 0; My < terrainSize.y; My++) {

			// Set the Starting X and Y values to the same as the neighboring values

			for (int Mx = 0; Mx < terrainSize.x; Mx++) {

				if(Mx < midPoint){
					xFactor = Mx/2 + (thisStartHeight[My] * heightFactor);
				} else {
					xFactor = Mx/2 + (thisStartHeight[My] * heightFactor);
					//xFactor = midPoint - (Mx - midPoint) + frontHeights[0,0];
				}

				if(My < midPoint)
					yFactor = My + (thisStartHeight[My] * heightFactor);
				else
					yFactor = My + (thisStartHeight[My] * heightFactor);  //midPoint - (My - midPoint);

				heightVal = xFactor; // + yFactor;


				heights[Mx, My] = (float)((float)heightVal / heightFactor);

			}

		}

		terrainBlock.terrainData.SetHeights(0, 0, heights);


		// Also update the collider?
		TerrainCollider tc = terrainBlock.gameObject.GetComponent<TerrainCollider>();
		tc.terrainData = terrainBlock.terrainData;

		//terrainBlock.terrainData.RefreshPrototypes();

	}

	float[] ReturnBorder(int i, int j, Directions dir){

		Terrain thisTile = terrainBuffer [terrainUsage [i, j]];
		List<float> retBorder = new List<float>();
		//nextTile.name = "Back Tile";

		float[,] heights = thisTile.terrainData.GetHeights(0, 0, tileWidth, tileHeight);

		if(dir == Directions.Back){
			for (int z = 0; z < tileWidth; z++){
				retBorder.Add(heights[tileHeight-1, z]);
			}
		} else if(dir == Directions.Right){
			for (int x = 0; x < tileWidth; x++){
				retBorder.Add(heights[x, tileHeight-1]);
			}
		}

		return retBorder.ToArray();

	}
	
	int FindNextAvailableTerrainID()
	{
		for(int i=0;i<usedTiles.Length;i++)
			if(!usedTiles[i]) return i;
		return -1;	
	}	
	
	void CopyTerrainDataFromTo(TerrainData tDataFrom, ref TerrainData tDataTo)
	{
		//TerrainCollider tc = terrainBlock.gameObject.GetComponent<TerrainCollider>();
		//tc.terrainData = terrainBlock.terrainData;

		tDataTo.SetDetailResolution(tDataFrom.detailResolution, 8);
		tDataTo.heightmapResolution = tDataFrom.heightmapResolution;
		tDataTo.alphamapResolution = tDataFrom.alphamapResolution;
		tDataTo.baseMapResolution = tDataFrom.baseMapResolution;
		tDataTo.size = tDataFrom.size;
		tDataTo.splatPrototypes = tDataFrom.splatPrototypes;

		float[,] heights = tDataFrom.GetHeights(0, 0, tileWidth, tileHeight);
		
		tDataTo.SetHeights(0,0, heights);

	}
}





