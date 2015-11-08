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

	private enum Directions {Front, Right, Back, Left, None};
	
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

		// We always draw the initial tile first and grow from there
		DropTerrainAt(0, 0);

		// Spread is the amount of surrounding terrains to draw

		// Why is this called so often?

		for(int i=-spread;i<=spread;i++){
			for(int j=-spread;j<=spread;j++){

				if(i == 0 && j == 0){
				//	break;
				}

				DropTerrainAt(currentTerrainID[0] + i, currentTerrainID[1] + j);
				dbgString += (currentTerrainID[0] + i) + "," + (currentTerrainID[1] + j) + "\n";

			}
		}

		Debug.Log(dbgString);

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
		if(terrainUsage.ContainsKey(i, j) && terrainUsage[i,j] != -1){
			// Tile mapped, use it.
		}
		// If terrain doesn't exist, drop it.
		else{
			doUpdate = true;

			terrainUsage[i,j] = FindNextAvailableTerrainID();
			if(terrainUsage[i,j] == -1) Debug.LogError("No more tiles, failing...");
		}

		if(terrainUsageData.ContainsKey(i,j)){
			// Restore the data for this tile
		}
		else{
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
	
	TerrainData CreateNewTerrainData(){

		TerrainData tData = new TerrainData();
		CopyTerrainDataFromTo(referenceTerrain.terrainData, ref tData);
		return tData;

	}
	
	void ResetTouch(){
		touchedTiles.SetAll(false);
	}
	
	int CountOnes(BitArray arr){
		int count = 0;
		for(int i=0;i<arr.Length;i++)
		{
			if(arr[i])
				count++;
		}
		return count;
	}
	
	void ReclaimTiles(){
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
	
	void ActivateUsedTile(int i, int j){
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

	bool TileExists(int i, int j){
		return (terrainUsage.ContainsKey(i, j) && terrainUsage[i,j] != -1);
	}

	void UpdateTerrain(int i, int j){

		Terrain terrainBlock = terrainBuffer [terrainUsage [i, j]];

		// 0,0 is original tile, 0,1 is front, -1,0 is left, 1,0 is right
		terrainBlock.name = "Tile " + i + " " + j;

		Debug.Log("Updating: " + terrainBlock.name);

		int xResolution = terrainBlock.terrainData.heightmapWidth;
		int zResolution = terrainBlock.terrainData.heightmapHeight;

		float[][] borders = new float[4][]{ new float[]{0},new float[]{0},new float[]{0},new float[]{0}};
	


		/*

		Make sure there is some AI work being done here!


		*/


		// Should call on ALL border tiles to get the array of border values, which are used to create the new terrain



		//if(i == currentTerrainID[0] && j == currentTerrainID[1] ){
		if (TileExists(i, j-1)){
			// Call on the tile behind this one
			borders[2] = ReturnBorder(i, j-1, Directions.Back);
		} 
		if (TileExists(i, j+1)){
			// Call on the tile to the front of this one
			borders[0] = ReturnBorder(i, j+1, Directions.Front);
		}
		if (TileExists(i+1, j)){
			// Call on the tile to the right of this one
			borders[1] = ReturnBorder(i+1, j, Directions.Right);
		} 
		if (TileExists(i-1, j)){
			// Call on the tile to the left of this one
			borders[3] = ReturnBorder(i-1, j, Directions.Left);
		}

		// Loop over every point on the terrain and adjust the height accordingly
		terrainBlock.terrainData.SetHeights(0, 0, ReturnHeightmap(borders, xResolution, zResolution));


		// Also update the collider
		TerrainCollider tc = terrainBlock.gameObject.GetComponent<TerrainCollider>();
		tc.terrainData = terrainBlock.terrainData;

	}

	float[,] ReturnHeightmap(float[][] borders, int x, int y){


		float xFactor, yFactor;
		float borderChange = 0;
		float heightFactor = 600.0f;
		float heightVal = 0;
		float[,] heights = new float[ (int)x, (int)y ];
		bool[] hasBorder = {false,false,false,false};
		float[] randHeights = new float[4];
		float[] borderHeights = new float[4];

	

		// Given the borders, fill in the rest of the height map accordingly



		// TODO: Figure out how to add some randomness while still keeping borders correct



		// Need to define them here so they stay the same!

		if(borders[0].Length > 1)
			hasBorder[0] = true;
		else
			randHeights[0] = Random.Range(0.0f, 0.2f);

		if(borders[1].Length > 1)
			hasBorder[1] = true;
		else
			randHeights[1] = Random.Range(0.0f, 0.2f);

		if(borders[2].Length > 1)
			hasBorder[2] = true;
		else
			randHeights[2] = Random.Range(0.0f, 0.2f);

		if(borders[3].Length > 1)
			hasBorder[3] = true;
		else
			randHeights[3] = Random.Range(0.0f, 0.2f);


		// Moving from left to right
		for (int My = 0; My < y; My++) {


			// Moving from back to front
			for (int Mx = 0; Mx < x; Mx++) {
				
				borderChange = 0;
				xFactor = 0;
				yFactor = 0;


				borderHeights[0] = hasBorder[0] ? borders[0][My] : randHeights[0];
				borderHeights[1] = hasBorder[1] ? borders[1][Mx] : randHeights[1];
				borderHeights[2] = hasBorder[2] ? borders[2][My] : randHeights[2];
				borderHeights[3] = hasBorder[3] ? borders[3][Mx] : randHeights[3];

				// Bottom, top, etc means I'm GETTING my border from that direction

				//(float)(My/y)

				// Top and bottom
				xFactor = Mathf.Lerp( borderHeights[2], borderHeights[0], (float)Mx/x );

				// Left and right borders
				yFactor = Mathf.Lerp( borderHeights[3], borderHeights[1], (float)My/y );


				//xFactor += (borderChange * heightFactor) + (x - Mx);
				
				heightVal = xFactor + yFactor;
				
				heights[Mx, My] = (float)((float)heightVal);



			}

		}

		return heights;

	}

	float[] ReturnBorder(int i, int j, Directions dir){

		Terrain thisTile = terrainBuffer [terrainUsage [i, j]];
		List<float> retBorder = new List<float>();
		//nextTile.name = "Back Tile";

		float[,] heights = thisTile.terrainData.GetHeights(0, 0, tileWidth, tileHeight);

		if(dir == Directions.Back){
			for (int z = 0; z < tileWidth; z++){
				retBorder.Add(heights[tileWidth-1, z]);
			}
		} else if(dir == Directions.Front){
			for (int z = 0; z < tileWidth; z++){
				retBorder.Add(heights[0, z]);
			}
		}
		else if(dir == Directions.Right){
			for (int x = 0; x < tileHeight; x++){
				retBorder.Add(heights[x, 0]);
			}
		} else if(dir == Directions.Left){
			for (int x = 0; x < tileHeight; x++){
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





