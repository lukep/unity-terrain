using UnityEngine;
using System.Collections;
using HelixToolkit.Wpf;
using System.Collections.Generic;

public class TerrainManager : MonoBehaviour {
	
	public GameObject playerGameObject;
	public GameObject sphereGameObject;
	public Terrain referenceTerrain;
	public int TERRAIN_BUFFER_COUNT = 50;
	public int spread = 1;
	public int treeLow = 5;
	public int treeHigh = 20;

	public int hillLow = 2;
	public int hillHigh = 8;
	public int hillSizeLow = 8;
	public int hillSizeHigh = 30;

	private int xMoves = 0;
	private int yMoves = 0;
	private bool blockWay = false;
	private Vector3 lastPos;
	private int updateCount = 0;
	private int[] currentTerrainID;
	private int[] previousTerrainID;
	private int[] tempTerrainID;
	private Terrain[] terrainBuffer;
	private DoubleKeyDictionary<int, int, int> terrainUsage;
	private DoubleKeyDictionary<int, int, TerrainData> terrainUsageData;
	private DoubleKeyDictionary<int, int, int> hillFactor;
	private BitArray usedTiles;
	private BitArray touchedTiles;
	private Vector3 referencePosition;
	private Vector2 referenceSize;
	private Quaternion referenceRotation;

	private int treeBase;
	private int tileWidth;
	private int tileHeight;

	private List<TreeInstance> TreeInstances;

	// The order that tiles are dropped
	private int[][] tileOrder = new int[8][]{ 
		new int[]{0,1}, new int[]{1,0}, new int[]{0,-1}, new int[]{-1,0},
		new int[]{1,1}, new int[]{1,-1}, new int[]{-1,-1}, new int[]{-1,1} };

	private enum Directions {Front, Right, Back, Left, None};

	
	// Use this for initialization
	void Start () {
		currentTerrainID = new int[2]{0,0};
		previousTerrainID = new int[2]{0,0};
		tempTerrainID = new int[2]{0,0};
		terrainBuffer = new Terrain[TERRAIN_BUFFER_COUNT];
		terrainUsage = new DoubleKeyDictionary<int, int, int>();
		terrainUsageData = new DoubleKeyDictionary<int, int, TerrainData>();

		hillFactor = new DoubleKeyDictionary<int, int, int>();

		usedTiles = new BitArray(TERRAIN_BUFFER_COUNT, false);
		touchedTiles = new BitArray(TERRAIN_BUFFER_COUNT, false);
		
		referencePosition = referenceTerrain.transform.position;
		referenceRotation = referenceTerrain.transform.rotation;
		referenceSize = new Vector2(referenceTerrain.terrainData.size.x, referenceTerrain.terrainData.size.z);

		treeBase = referenceTerrain.terrainData.treeInstanceCount;

		tileWidth = referenceTerrain.terrainData.heightmapWidth;
		tileHeight = referenceTerrain.terrainData.heightmapHeight;
		
		for(int i=0; i<TERRAIN_BUFFER_COUNT; i++){
			TerrainData tData = new TerrainData();
			CopyTerrainDataFromTo(referenceTerrain.terrainData, ref tData);
			terrainBuffer[i] = Terrain.CreateTerrainGameObject(tData).GetComponent<Terrain>();
			terrainBuffer[i].gameObject.SetActive(false);
		}

	}

	void CopyArrayFromTo(ref int[] fromArray, ref int[] toArray ){
		toArray[0] = fromArray[0];
		toArray[1] = fromArray[1];
	}
	
	// Update is called once per frame
	void Update () {
		ResetTouch();
		Vector3 warpPosition = playerGameObject.transform.position;

		CopyArrayFromTo(ref currentTerrainID, ref tempTerrainID);

		TerrainIDFromPosition(ref currentTerrainID, ref warpPosition);

		string dbgString = "";

		if( currentTerrainID[0] != tempTerrainID[0] || currentTerrainID[1] != tempTerrainID[1] ){

			// Moved to a new tile
			CopyArrayFromTo(ref tempTerrainID, ref previousTerrainID);


			if(currentTerrainID[0] == previousTerrainID[0]){
				yMoves++;
				xMoves = 0;
			}
			else if(currentTerrainID[1] == previousTerrainID[1]){
				xMoves++;
				yMoves = 0;
			}

			if( xMoves + yMoves > 2 ){
				blockWay = true;
			}


		}


		//dbgString = "CurrentID : " + currentTerrainID[0] + ", " + currentTerrainID[1] + "\n\n";



		// We always draw the initial tile first and grow from there
		DropTerrainAt(0, 0);

		// Spread is the amount of surrounding terrains to draw

		for(int i = 0; i < spread * 8; i++){

			//for(int j = -spread; j <= spread; j++){

				//if(i != 0 && j != 0){
			DropTerrainAt(currentTerrainID[0] + tileOrder[i][0], currentTerrainID[1] + tileOrder[i][1]);
			dbgString += "NEW TILE: " + currentTerrainID[0] + tileOrder[i][0] + " " + currentTerrainID[1] + tileOrder[i][1] + "\n";
				//} else { dbgString += "Skipping original terrain"; }

			//}

		}

		updateCount++;
		if(updateCount == 1){
			lastPos = warpPosition;
		}
		else if(updateCount > 400){

			// Check position of player after some frames, if they haven't moved very far, activate something

			updateCount = 0;

			float distance = Vector3.Distance (warpPosition, lastPos);
			//Debug.Log("DISTANCE: " + distance);
			
			if(distance < 200){

				Vector3 spherePos = warpPosition;
				Vector3 lookDir = playerGameObject.transform.forward;
				lookDir.Normalize();

				// Add a "boulder"
				spherePos.x += (lookDir.x * 50);
				spherePos.z += (lookDir.y * 50);
				spherePos.y += lookDir.z + 25;
				
				//Debug.Log(spherePos);
				//Debug.Log(warpPosition);
				
				Instantiate(sphereGameObject, spherePos, Quaternion.identity);

			}

		}


		//Debug.Log(dbgString);

		// TODO: Reactivate this when needed
		//ReclaimTiles();
	}
	
	void TerrainIDFromPosition(ref int[] currentTerrainID, ref Vector3 position){
		currentTerrainID[0] = Mathf.RoundToInt((position.x - referencePosition.x )/ referenceSize.x);
		currentTerrainID[1] = Mathf.RoundToInt((position.z - referencePosition.z )/ referenceSize.y);
	}
	
	void DropTerrainAt(int i, int j){
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
			//Debug.Log("Calling CreateNewTerrainData at " + i + " " + j);
			terrainUsageData[i,j] = CreateNewTerrainData();
		}

		// No problems, time to activate the tile!
		if(doUpdate){

			hillFactor[i,j] = Random.Range(hillLow,hillHigh);

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
		if(CountOnes(usedTiles) > ((spread*2 + 1)*(spread*2 + 1))){
			for(int i=0;i<usedTiles.Length;i++){
				if(usedTiles[i] && !touchedTiles[i]){
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
		} else {
			//Smooth(terrainBuffer[terrainUsage[i, j]]);
		}

		//UpdateTerrain (i, j);

	}

	bool TileExists(int i, int j){
		return (terrainUsage.ContainsKey(i, j) && terrainUsage[i,j] != -1);
	}

	void UpdateTerrain(int i, int j){

		Terrain terrainBlock = terrainBuffer [terrainUsage [i, j]];

		// 0,0 is original tile, 0,1 is front, -1,0 is left, 1,0 is right
		terrainBlock.name = "Tile " + i + " " + j;

		//Debug.Log("Updating: " + terrainBlock.name);

		int xResolution = terrainBlock.terrainData.heightmapWidth;
		int zResolution = terrainBlock.terrainData.heightmapHeight;

		int previousTileX = previousTerrainID[0];
		int previousTileY = previousTerrainID[1];

		float[][] borders = new float[4][]{ new float[]{0},new float[]{0},new float[]{0},new float[]{0}};
	
		/*

		Make sure there is some AI work being done here!


		*/

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


		// Check the last terrain ID that was walked on to udpate values
		int newHillFactor = hillFactor[ previousTileX, previousTileY ];


		if(newHillFactor > 3)
			newHillFactor++;
		else
			newHillFactor = Mathf.Max(1, newHillFactor--);


		if(blockWay){
			blockWay = false;
			newHillFactor += 10;
			Debug.Log("BIG HILLS");
		}


		//Debug.Log("Last Tile: " + previousTileX + ", " + previousTileY + " New HILL: " + newHillFactor + " Old Hill: " + hillFactor[ previousTileX, previousTileY ] );
		//Debug.Log("This Tile: " + i + ", " + j);

		hillFactor[ i, j ] = newHillFactor;

		// Loop over every point on the terrain and adjust the height accordingly
		terrainBlock.terrainData.SetHeights(0, 0, ReturnHeightmap(borders, xResolution, zResolution, newHillFactor, terrainBlock.GetPosition()));


		//Smooth(terrainBlock);

	
		int prevTreeCount = terrainBuffer [terrainUsage [previousTileX, previousTileY]].terrainData.treeInstanceCount;
		int treeChange = Random.Range(treeLow, treeHigh);

		int newTreeCount = prevTreeCount + ( prevTreeCount >= treeBase ? treeChange : -treeChange );

		//Debug.Log("Trees, Prev, New: " + prevTreeCount + " " + newTreeCount);

		newTreeCount = Mathf.Min(newTreeCount, 0);

		UpdateTrees(ref terrainBlock, newTreeCount);


		// Also update the collider
		TerrainCollider tc = terrainBlock.gameObject.GetComponent<TerrainCollider>();
		tc.terrainData = terrainBlock.terrainData;


		// TODO: Need to update surrounding tiles now?
		if(TileExists(i,j-1)){
			//UpdateTerrain(i,j-1);
		}

	}

	void UpdateTrees(ref Terrain terrain, int newTreeCount){

		TreeInstances = new List<TreeInstance>(terrain.terrainData.treeInstances);
		
		//Debug.Log("TREES");
		//Debug.Log(td.treeInstances[0]);

		TreeInstance newTree = terrain.terrainData.treeInstances[0];

		for (int i = 0; i < newTreeCount; i++){
			//Debug.Log(newTree.position.x);
			newTree.position.Set( Random.Range(0.0f,0.9f), 0.0f, Random.Range(0.0f,0.9f) );
			TreeInstances.Add(newTree);
		}

		terrain.terrainData.treeInstances = TreeInstances.ToArray();

	}

	int GetTreeCount(TerrainData td){
		return td.treeInstanceCount;
	}

	float[,] ReturnHeightmap(float[][] borders, int x, int y, int hillFactor, Vector3 terrainPos){

		float xFactor, yFactor;
		float heightVal = 0;
		float[,] heights = new float[ (int)x, (int)y ];
		bool[] hasBorder = {false,false,false,false};
		float[] randHeights = new float[4];
		float[] borderHeights = new float[4];

		float randLow = 0.2f;
		float randHigh = 0.4f;
	

		// Given the borders, fill in the rest of the height map accordingly




		// TODO: Figure out how to add some randomness while still keeping borders correct

		// Transform the incoming border?


	
		// Need to define them here so they stay the same!

		if(borders[0].Length > 1)
			hasBorder[0] = true;
		else
			randHeights[0] = Random.Range(randLow, randHigh);

		if(borders[1].Length > 1)
			hasBorder[1] = true;
		else
			randHeights[1] = Random.Range(randLow, randHigh);

		if(borders[2].Length > 1)
			hasBorder[2] = true;
		else
			randHeights[2] = Random.Range(randLow, randHigh);

		if(borders[3].Length > 1)
			hasBorder[3] = true;
		else
			randHeights[3] = Random.Range(randLow, randHigh);


		// hasBorders is accurate!
		//Debug.Log("hasborders: " + hasBorder[0] + hasBorder[1] + hasBorder[2] + hasBorder[3]);



		// Moving from left to right
		for (int My = 0; My < y; My++) {

			// Moving from back to front
			for (int Mx = 0; Mx < x; Mx++) {

				xFactor = 0;
				yFactor = 0;



				// Bottom, top, etc means I'm GETTING my border from that direction


				// Top is affected by left and right

				// Right is affected by top and bottom

				// Bottom is affected by left and right

				// Left is affected by top and bottom

							
				// Top
				if(hasBorder[0]){

					// If there exists a top border

					borderHeights[0] = borders[0][My];

					// If there is no opposite border given, make it up!
					if(!hasBorder[1] && !hasBorder[2] && !hasBorder[3]){
						//borderHeights[2] = randHeights[2];
					}
					

				}

				// Right
				if(hasBorder[1]){

					borderHeights[1] = borders[1][Mx];

					// If there is no opposite border given, make it up!
					if(!hasBorder[0] && !hasBorder[2] && !hasBorder[3]){
						//borderHeights[3] = randHeights[3];
					}

				}

				// Bottom: The tile "below" this one exists
				if(hasBorder[2]){

					// If there exists a bottom border

					borderHeights[2] = borders[2][My];

					if(!hasBorder[0] && !hasBorder[1] && !hasBorder[3]){
						//borderHeights[0] = randHeights[0];
					}

				}

				// Left
				if(hasBorder[3]){

					borderHeights[3] = borders[3][Mx];

					if(!hasBorder[0] && !hasBorder[1] && !hasBorder[2]){
						//borderHeights[1] = randHeights[1];
					}
				}
			
				// Overall problem: values being added from both "sweeps"", doubles up heights

			

				// Top and bottom
				xFactor = Mathf.Lerp( borderHeights[2], borderHeights[0], (float)Mx/x );

				// Left and right borders
				yFactor = Mathf.Lerp( borderHeights[3], borderHeights[1], (float)My/y );


				// (float)Mx/x + (float)My/y is correct for right borders

				// (float)My/y + (float)Mx/x for left borders


				// NOW this needs to be lerped over?


				//heightVal = Mathf.Lerp( xFactor, yFactor, lerpFactor);

				// Works for the initial tiles!
				//heightVal = xFactor + yFactor;





				// Has a right border
				//var lerpFactor = (float)Mx/x; // hasBorder[3] ? (float)My/y + (float)Mx/x : (float)Mx/x + (float)My/y;

				if(yFactor > 0 && xFactor > 0){
					heightVal = Mathf.Lerp( xFactor, yFactor, 0.5f );
						//+ Mathf.Lerp( yFactor, xFactor, (float)Mx/x );
				} else {
					heightVal = xFactor + yFactor;
				}

				
				heights[Mx, My] = (float)((float)heightVal);

			}

		}


		while(hillFactor > 0){
			hillFactor--;

			AddHill(x-1, y-1, heights, terrainPos);
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

	private void AddHill(int xMax, int yMax, float[,] heights, Vector3 spherePos){


		int size = Random.Range(hillSizeLow, hillSizeHigh);


		int x = Random.Range(size, xMax-size);
		int y = Random.Range(size, yMax-size);

		float height;
		//float maxHeight = 0;
		
		for(int i = x-size; i < x+size; i++){

			for(int j = y-size; j < y+size; j++){

				height = Mathf.Pow(size, 2) - ( Mathf.Pow((y-j),2) + Mathf.Pow((x-i),2) );
				
				if(height<0)
					height=0;

				if( i < xMax && j < yMax )
					heights[i,j] += (height / 6000.0f);

				//if(i == x && j == y)
				//	maxHeight = heights[i,j] * 100;
				
			}
		}
	}

	private void Smooth(Terrain terrain){
		
		float[,] height = terrain.terrainData.GetHeights(0, 0, terrain.terrainData.heightmapWidth,
		                                                 terrain.terrainData.heightmapHeight);
		float k = 0.5f;
		/* Rows, left to right */
		for (int x = 1; x < terrain.terrainData.heightmapWidth; x++)
			for (int z = 0; z < terrain.terrainData.heightmapHeight; z++)
				height[x, z] = height[x - 1, z] * (1 - k) +
					height[x, z] * k;
		
		/* Rows, right to left*/
		for (int x = terrain.terrainData.heightmapWidth - 2; x < -1; x--)
			for (int z = 0; z < terrain.terrainData.heightmapHeight; z++)
				height[x, z] = height[x + 1, z] * (1 - k) +
					height[x, z] * k;
		
		/* Columns, bottom to top */
		for (int x = 0; x < terrain.terrainData.heightmapWidth; x++)
			for (int z = 1; z < terrain.terrainData.heightmapHeight; z++)
				height[x, z] = height[x, z - 1] * (1 - k) +
					height[x, z] * k;
		
		/* Columns, top to bottom */
		for (int x = 0; x < terrain.terrainData.heightmapWidth; x++)
			for (int z = terrain.terrainData.heightmapHeight; z < -1; z--)
				height[x, z] = height[x, z + 1] * (1 - k) +
					height[x, z] * k;
		
		terrain.terrainData.SetHeights(0, 0, height);
	}
	
	int FindNextAvailableTerrainID(){
		for(int i=0;i<usedTiles.Length;i++)
			if(!usedTiles[i]) return i;
		return -1;	
	}	
	
	void CopyTerrainDataFromTo(TerrainData tDataFrom, ref TerrainData tDataTo){
		//TerrainCollider tc = terrainBlock.gameObject.GetComponent<TerrainCollider>();
		//tc.terrainData = terrainBlock.terrainData;

		tDataTo.SetDetailResolution(tDataFrom.detailResolution, 8);
		tDataTo.heightmapResolution = tDataFrom.heightmapResolution;
		tDataTo.alphamapResolution = tDataFrom.alphamapResolution;
		tDataTo.baseMapResolution = tDataFrom.baseMapResolution;
		tDataTo.size = tDataFrom.size;
		tDataTo.splatPrototypes = tDataFrom.splatPrototypes;
		tDataTo.treePrototypes = tDataFrom.treePrototypes;
		tDataTo.treeInstances = tDataFrom.treeInstances;

		float[,] heights = tDataFrom.GetHeights(0, 0, tileWidth, tileHeight);
		
		tDataTo.SetHeights(0,0, heights);

	}
}





