using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public struct MeshConstructionData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;
}

public enum BlockType
{
    Air,
    Dirt,
    Grass,
    Stone,
    WoodPlank,
    Path,
    StoneBrick,
    MossyStoneBrick
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ChunkTerrainGenerator : MonoBehaviour
{
    [Header("Chunk Settings")]
    [SerializeField] private int ChunkWidth = 50;
    private const int ChunkHeight = 200;
    private const int AtlasColumns = 3;
    private const int AtlasRows = 3;
    private const float AtlasTileInset = 0.001f;
    private const int DungeonRoomWidth = 9;
    private const int DungeonRoomHeight = 5;
    private const int DungeonCorridorWidth = 3;
    private const int DungeonCorridorHeight = 3;
    private const int DungeonRoomSeparation = 2;
    private const int VillageRoadWidth = 3;
    private const int VillageFoundationDepth = 3;

    [Header("Terrain Noise")]
    [SerializeField] private int seed;
    [SerializeField, Min(0)] private int baseTerrainHeight = 100;
    [SerializeField, Min(0)] private float terrainAmplitudeDirt = 20.0f;
    [SerializeField] private float terrainAmplitudeStone = 20.0f;
    [SerializeField] private float noiseScale = 0.05f;

    [Header("River")]
    [SerializeField] private bool generateRiver = true;
    [SerializeField, Min(1)] private int riverDepth = 5;
    [SerializeField, Range(0.001f, 0.5f)] private float riverWidth = 0.06f;
    [SerializeField, Range(0.001f, 0.5f)] private float riverBankWidth = 0.12f;
    [SerializeField, Min(0.001f)] private float riverNoiseScale = 0.015f;
    [SerializeField, Min(0.001f)] private float riverWarpNoiseScale = 0.008f;
    [SerializeField, Min(0.0f)] private float riverWarpStrength = 18.0f;

    [Header("Large Cave")]
    [SerializeField] private bool generateLargeCave = true;
    [SerializeField, Min(1)] private int minimumCaveY = 5;
    [SerializeField] private float caveCenterY = 55.0f;
    [SerializeField, Range(0.1f, 0.6f)] private float caveHorizontalRadiusFraction = 0.4f;
    [SerializeField, Min(1.0f)] private float caveVerticalRadius = 25.0f;
    [SerializeField, Min(0.001f)] private float caveShapeNoiseScale = 0.025f;
    [SerializeField, Range(0.0f, 1.0f)] private float caveShapeNoiseStrength = 0.35f;
    [SerializeField, Min(0.001f)] private float caveDetailNoiseScale = 0.08f;
    [SerializeField, Range(0.0f, 0.5f)] private float caveDetailNoiseStrength = 0.08f;
    [SerializeField, Min(0.001f)] private float caveWarpNoiseScale = 0.018f;
    [SerializeField, Min(0.0f)] private float caveWarpStrength = 8.0f;
    [SerializeField, Range(-0.5f, 0.5f)] private float caveThreshold = 0.05f;

    [Header("Dungeon")]
    [SerializeField] private bool generateDungeon = true;
    [SerializeField, Min(1)] private int dungeonRoomCount = 8;
    [SerializeField, Min(0)] private int dungeonFloorY = 40;
    [SerializeField, Range(0.0f, 1.0f)] private float dungeonMossBrickChance = 0.15f;

    [Header("Village")]
    [SerializeField] private bool generateVillage = true;
    [SerializeField, Range(1, 8)] private int villageBuildingCount = 6;
    [SerializeField, Min(1)] private int villageCandidateCount = 24;
    [SerializeField, Min(0)] private int villageBuildingSpacing = 2;

    [Header("Water")]
    [SerializeField] private GameObject waterPrefab;
    [SerializeField] private int waterHeight = 100;
    private const float waterHorizontalInset = 0.05f;
    private const float waterVerticalOffset = 0.05f;

    public bool IsGenerating { get; private set; }

    private GameObject waterInstance;

    private static readonly Vector3Int[] NeighborDirections =
    {
        Vector3Int.left,
        Vector3Int.right,
        Vector3Int.down,
        Vector3Int.up,
        Vector3Int.back,
        Vector3Int.forward
    };

    private static readonly Vector3[,] FaceVertices =
    {
        {
            new Vector3(0, 0, 1), new Vector3(0, 1, 1),
            new Vector3(0, 1, 0), new Vector3(0, 0, 0)
        },
        {
            new Vector3(1, 0, 0), new Vector3(1, 1, 0),
            new Vector3(1, 1, 1), new Vector3(1, 0, 1)
        },
        {
            new Vector3(0, 0, 1), new Vector3(0, 0, 0),
            new Vector3(1, 0, 0), new Vector3(1, 0, 1)
        },
        {
            new Vector3(0, 1, 0), new Vector3(0, 1, 1),
            new Vector3(1, 1, 1), new Vector3(1, 1, 0)
        },
        {
            new Vector3(0, 0, 0), new Vector3(0, 1, 0),
            new Vector3(1, 1, 0), new Vector3(1, 0, 0)
        },
        {
            new Vector3(1, 0, 1), new Vector3(1, 1, 1),
            new Vector3(0, 1, 1), new Vector3(0, 0, 1)
        }
    };

    private static readonly Vector2[] FaceUvs =
    {
        new Vector2(0, 0),
        new Vector2(0, 1),
        new Vector2(1, 1),
        new Vector2(1, 0)
    };

    // Tile coordinates use Unity's bottom-left UV origin
    // Face order is left, right, down, up, back, forward
    private static readonly Vector2Int[,] AtlasTiles =
    {
        {
            new Vector2Int(1, 2), new Vector2Int(1, 2), new Vector2Int(1, 2),
            new Vector2Int(1, 2), new Vector2Int(1, 2), new Vector2Int(1, 2)
        },
        {
            new Vector2Int(1, 2), new Vector2Int(1, 2), new Vector2Int(1, 2),
            new Vector2Int(1, 2), new Vector2Int(1, 2), new Vector2Int(1, 2)
        },
        {
            new Vector2Int(2, 2), new Vector2Int(2, 2), new Vector2Int(1, 2),
            new Vector2Int(0, 1), new Vector2Int(2, 2), new Vector2Int(2, 2)
        },
        {
            new Vector2Int(0, 2), new Vector2Int(0, 2), new Vector2Int(0, 2),
            new Vector2Int(0, 2), new Vector2Int(0, 2), new Vector2Int(0, 2)
        },
        {
            new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0),
            new Vector2Int(0, 0), new Vector2Int(0, 0), new Vector2Int(0, 0)
        },
        {
            new Vector2Int(1, 1), new Vector2Int(1, 1), new Vector2Int(1, 2),
            new Vector2Int(2, 1), new Vector2Int(1, 1), new Vector2Int(1, 1)
        },
        {
            new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(1, 0),
            new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(1, 0)
        },
        {
            new Vector2Int(2, 0), new Vector2Int(2, 0), new Vector2Int(2, 0),
            new Vector2Int(2, 0), new Vector2Int(2, 0), new Vector2Int(2, 0)
        }
    };

    private static readonly VillageBuildingTemplate VillageHouseTemplate =
        VillageBuildingTemplates.PlankHouse;

    private void Start()
    {
        GenerateChunk();
    }

    private BlockType[,,] GenerateBlockMap()
    {
        BlockType[,,] blocks = new BlockType[ChunkWidth, ChunkHeight, ChunkWidth];
        int[,] surfaceHeights = new int[ChunkWidth, ChunkWidth];
        float[,] riverInfluences = new float[ChunkWidth, ChunkWidth];
        System.Random seedRandom = new System.Random(seed);
        float noiseOffsetX = seedRandom.Next(-100000, 100001);
        float noiseOffsetZ = seedRandom.Next(-100000, 100001);
        int riverSeed = unchecked(seed * 1103515245 + 12345);
        int riverWarpSeedX = unchecked(riverSeed + 1301);
        int riverWarpSeedZ = unchecked(riverSeed + 2903);
        int riverBedHeight = Mathf.Clamp(waterHeight - riverDepth, 0, ChunkHeight - 1);

        // Base chunk
        for (int x = 0; x < ChunkWidth; x++)
        {
            for (int z = 0; z < ChunkWidth; z++)
            {
                float terrainNoise = Mathf.PerlinNoise(
                    noiseOffsetX + x * noiseScale,
                    noiseOffsetZ + z * noiseScale
                );
                int stoneHeight = Mathf.Clamp(
                    Mathf.RoundToInt(
                        baseTerrainHeight +
                        terrainNoise * terrainAmplitudeStone
                    ),
                    0,
                    ChunkHeight - 1
                );
                int dirtHeight = Mathf.Clamp(
                    Mathf.RoundToInt(
                        stoneHeight +
                        terrainNoise * terrainAmplitudeDirt
                    ),
                    0,
                    ChunkHeight - 1
                );
                float riverInfluence = 0.0f;

                if (generateRiver)
                {
                    float sampleX = x + 0.5f;
                    float sampleZ = z + 0.5f;
                    float riverWarpX = SampleValueNoise3D(
                        sampleX * riverWarpNoiseScale,
                        17.31f,
                        sampleZ * riverWarpNoiseScale,
                        riverWarpSeedX
                    ) * riverWarpStrength;
                    float riverWarpZ = SampleValueNoise3D(
                        sampleX * riverWarpNoiseScale,
                        31.73f,
                        sampleZ * riverWarpNoiseScale,
                        riverWarpSeedZ
                    ) * riverWarpStrength;
                    float riverDistance = Mathf.Abs(SampleValueNoise3D(
                        (sampleX + riverWarpX) * riverNoiseScale,
                        53.19f,
                        (sampleZ + riverWarpZ) * riverNoiseScale,
                        riverSeed
                    ));
                    float bankBlend = Mathf.Clamp01(
                        (riverDistance - riverWidth) / Mathf.Max(0.0001f, riverBankWidth)
                    );
                    bankBlend = bankBlend * bankBlend * (3.0f - 2.0f * bankBlend);
                    riverInfluence = 1.0f - bankBlend;
                    int carvedHeight = Mathf.RoundToInt(
                        dirtHeight + (riverBedHeight - dirtHeight) * riverInfluence
                    );
                    dirtHeight = Mathf.Min(dirtHeight, carvedHeight);
                    stoneHeight = Mathf.Min(stoneHeight, dirtHeight);
                }

                riverInfluences[x, z] = riverInfluence;
                surfaceHeights[x, z] = dirtHeight;

                for (int y = 0; y <= dirtHeight; y++)
                {
                    blocks[x, y, z] = y <= stoneHeight ? BlockType.Stone : BlockType.Dirt;
                }

                if (riverInfluence > 0.0f)
                {
                    blocks[x, dirtHeight, z] = BlockType.Dirt;
                }

            }
        }

        // Cave
        if (generateLargeCave)
        {
            int caveSeed = unchecked(seed * 73428767 + 912367421);
            System.Random caveParameterRandom = new System.Random(caveSeed);
            float caveCenterX = 0.5f + (float)caveParameterRandom.NextDouble() * (ChunkWidth - 1.0f);
            float caveCenterZ = 0.5f + (float)caveParameterRandom.NextDouble() * (ChunkWidth - 1.0f);
            float caveHorizontalRadius = ChunkWidth * caveHorizontalRadiusFraction;
            int warpSeedX = unchecked(caveSeed + 101);
            int warpSeedY = unchecked(caveSeed + 307);
            int warpSeedZ = unchecked(caveSeed + 503);
            int shapeSeed = unchecked(caveSeed + 701);
            int detailSeed = unchecked(caveSeed + 907);

            for (int x = 0; x < ChunkWidth; x++)
            {
                for (int z = 0; z < ChunkWidth; z++)
                {
                    for (int y = minimumCaveY; y <= surfaceHeights[x, z]; y++)
                    {
                        float sampleX = x + 0.5f;
                        float sampleY = y + 0.5f;
                        float sampleZ = z + 0.5f;
                        float coreX = (sampleX - caveCenterX) / caveHorizontalRadius;
                        float coreY = (sampleY - caveCenterY) / caveVerticalRadius;
                        float coreZ = (sampleZ - caveCenterZ) / caveHorizontalRadius;
                        float coreDistanceSquared = coreX * coreX + coreY * coreY + coreZ * coreZ;
                        float warpX = SampleValueNoise3D(
                            sampleX * caveWarpNoiseScale,
                            sampleY * caveWarpNoiseScale,
                            sampleZ * caveWarpNoiseScale,
                            warpSeedX
                        ) * caveWarpStrength;
                        float warpY = SampleValueNoise3D(
                            sampleX * caveWarpNoiseScale,
                            sampleY * caveWarpNoiseScale,
                            sampleZ * caveWarpNoiseScale,
                            warpSeedY
                        ) * caveWarpStrength * 0.35f;
                        float warpZ = SampleValueNoise3D(
                            sampleX * caveWarpNoiseScale,
                            sampleY * caveWarpNoiseScale,
                            sampleZ * caveWarpNoiseScale,
                            warpSeedZ
                        ) * caveWarpStrength;
                        float warpedX = sampleX + warpX;
                        float warpedY = sampleY + warpY;
                        float warpedZ = sampleZ + warpZ;
                        float normalizedX = (warpedX - caveCenterX) / caveHorizontalRadius;
                        float normalizedY = (warpedY - caveCenterY) / caveVerticalRadius;
                        float normalizedZ = (warpedZ - caveCenterZ) / caveHorizontalRadius;
                        float warpedDistanceSquared =
                            normalizedX * normalizedX +
                            normalizedY * normalizedY +
                            normalizedZ * normalizedZ;
                        float caveDensity =
                            1.0f - warpedDistanceSquared +
                            SampleValueNoise3D(
                                warpedX * caveShapeNoiseScale,
                                warpedY * caveShapeNoiseScale,
                                warpedZ * caveShapeNoiseScale,
                                shapeSeed
                            ) * caveShapeNoiseStrength +
                            SampleValueNoise3D(
                                warpedX * caveDetailNoiseScale,
                                warpedY * caveDetailNoiseScale,
                                warpedZ * caveDetailNoiseScale,
                                detailSeed
                            ) * caveDetailNoiseStrength;

                        bool insideGuaranteedCore = coreDistanceSquared < 0.45f;
                        bool insideDistortedBoundary =
                            warpedDistanceSquared < 1.6f &&
                            caveDensity > caveThreshold;

                        if (insideGuaranteedCore || insideDistortedBoundary)
                        {
                            blocks[x, y, z] = BlockType.Air;
                        }
                    }
                }
            }
        }

        // Dungeon
        if (generateDungeon)
        {
            int clampedDungeonFloorY = Mathf.Clamp(
                dungeonFloorY,
                0,
                ChunkHeight - DungeonRoomHeight - 2
            );
            int dungeonSeed = unchecked(seed * 1664525 + 1013904223);
            System.Random dungeonRandom = new System.Random(dungeonSeed);
            System.Random dungeonMossRandom = new System.Random(
                unchecked(dungeonSeed + 1597334677)
            );
            List<BoundsInt> dungeonRooms = new List<BoundsInt>();
            int placementAttempts = 0;
            int maximumPlacementAttempts = dungeonRoomCount * 50;

            while (
                dungeonRooms.Count < dungeonRoomCount &&
                placementAttempts < maximumPlacementAttempts
            )
            {
                placementAttempts++;
                int roomCenterX = dungeonRandom.Next(0, ChunkWidth);
                int roomCenterZ = dungeonRandom.Next(0, ChunkWidth);
                BoundsInt candidateRoom = new BoundsInt(
                    roomCenterX - DungeonRoomWidth / 2,
                    clampedDungeonFloorY + 1,
                    roomCenterZ - DungeonRoomWidth / 2,
                    DungeonRoomWidth,
                    DungeonRoomHeight,
                    DungeonRoomWidth
                );
                bool overlapsExistingRoom = false;

                for (int roomIndex = 0; roomIndex < dungeonRooms.Count; roomIndex++)
                {
                    BoundsInt existingRoom = dungeonRooms[roomIndex];

                    if (
                        candidateRoom.xMin < existingRoom.xMax + DungeonRoomSeparation &&
                        candidateRoom.xMax + DungeonRoomSeparation > existingRoom.xMin &&
                        candidateRoom.zMin < existingRoom.zMax + DungeonRoomSeparation &&
                        candidateRoom.zMax + DungeonRoomSeparation > existingRoom.zMin
                    )
                    {
                        overlapsExistingRoom = true;
                        break;
                    }
                }

                if (!overlapsExistingRoom)
                {
                    bool carvesAtLeastOneBlock = false;
                    int candidateStartX = Mathf.Max(0, candidateRoom.xMin);
                    int candidateEndX = Mathf.Min(ChunkWidth, candidateRoom.xMax);
                    int candidateStartY = Mathf.Max(0, candidateRoom.yMin);
                    int candidateEndY = Mathf.Min(ChunkHeight, candidateRoom.yMax);
                    int candidateStartZ = Mathf.Max(0, candidateRoom.zMin);
                    int candidateEndZ = Mathf.Min(ChunkWidth, candidateRoom.zMax);

                    for (
                        int x = candidateStartX;
                        x < candidateEndX && !carvesAtLeastOneBlock;
                        x++
                    )
                    {
                        for (
                            int y = candidateStartY;
                            y < candidateEndY && !carvesAtLeastOneBlock;
                            y++
                        )
                        {
                            for (int z = candidateStartZ; z < candidateEndZ; z++)
                            {
                                if (blocks[x, y, z] != BlockType.Air)
                                {
                                    carvesAtLeastOneBlock = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (carvesAtLeastOneBlock)
                    {
                        dungeonRooms.Add(candidateRoom);
                    }
                }
            }

            for (int roomIndex = 0; roomIndex < dungeonRooms.Count; roomIndex++)
            {
                BuildDungeonRoom(
                    blocks,
                    dungeonRooms[roomIndex],
                    clampedDungeonFloorY,
                    dungeonMossRandom,
                    dungeonMossBrickChance
                );
            }

            if (dungeonRooms.Count > 1)
            {
                bool[] connectedRooms = new bool[dungeonRooms.Count];
                List<Vector2Int> dungeonConnections = new List<Vector2Int>();
                connectedRooms[0] = true;

                for (int connectedCount = 1; connectedCount < dungeonRooms.Count; connectedCount++)
                {
                    int bestConnectedRoom = -1;
                    int bestUnconnectedRoom = -1;
                    int bestDistanceSquared = int.MaxValue;

                    for (int connectedRoom = 0; connectedRoom < dungeonRooms.Count; connectedRoom++)
                    {
                        if (!connectedRooms[connectedRoom])
                        {
                            continue;
                        }

                        int connectedCenterX = dungeonRooms[connectedRoom].xMin + DungeonRoomWidth / 2;
                        int connectedCenterZ = dungeonRooms[connectedRoom].zMin + DungeonRoomWidth / 2;

                        for (int unconnectedRoom = 0; unconnectedRoom < dungeonRooms.Count; unconnectedRoom++)
                        {
                            if (connectedRooms[unconnectedRoom])
                            {
                                continue;
                            }

                            int unconnectedCenterX =
                                dungeonRooms[unconnectedRoom].xMin + DungeonRoomWidth / 2;
                            int unconnectedCenterZ =
                                dungeonRooms[unconnectedRoom].zMin + DungeonRoomWidth / 2;
                            int deltaX = connectedCenterX - unconnectedCenterX;
                            int deltaZ = connectedCenterZ - unconnectedCenterZ;
                            int distanceSquared = deltaX * deltaX + deltaZ * deltaZ;

                            if (distanceSquared < bestDistanceSquared)
                            {
                                bestDistanceSquared = distanceSquared;
                                bestConnectedRoom = connectedRoom;
                                bestUnconnectedRoom = unconnectedRoom;
                            }
                        }
                    }

                    if (bestUnconnectedRoom < 0)
                    {
                        break;
                    }

                    connectedRooms[bestUnconnectedRoom] = true;
                    dungeonConnections.Add(new Vector2Int(bestConnectedRoom, bestUnconnectedRoom));
                }

                for (
                    int connectionIndex = 0;
                    connectionIndex < dungeonConnections.Count;
                    connectionIndex++
                )
                {
                    BoundsInt startRoom = dungeonRooms[dungeonConnections[connectionIndex].x];
                    BoundsInt endRoom = dungeonRooms[dungeonConnections[connectionIndex].y];
                    Vector3Int start = new Vector3Int(
                        startRoom.xMin + DungeonRoomWidth / 2,
                        clampedDungeonFloorY,
                        startRoom.zMin + DungeonRoomWidth / 2
                    );
                    Vector3Int end = new Vector3Int(
                        endRoom.xMin + DungeonRoomWidth / 2,
                        clampedDungeonFloorY,
                        endRoom.zMin + DungeonRoomWidth / 2
                    );
                    Vector3Int corner;

                    if (dungeonRandom.Next(0, 2) == 0)
                    {
                        corner = new Vector3Int(end.x, clampedDungeonFloorY, start.z);
                    }
                    else
                    {
                        corner = new Vector3Int(start.x, clampedDungeonFloorY, end.z);
                    }

                    CarveDungeonCorridor(
                        blocks,
                        start,
                        corner,
                        dungeonRooms,
                        dungeonMossRandom,
                        dungeonMossBrickChance
                    );
                    CarveDungeonCorridor(
                        blocks,
                        corner,
                        end,
                        dungeonRooms,
                        dungeonMossRandom,
                        dungeonMossBrickChance
                    );
                }
            }
        }

        if (generateVillage)
        {
            int villageSeed = unchecked(seed * 22695477 + 1);
            System.Random villageRandom = new System.Random(villageSeed);
            int siteMargin = Mathf.Min(24, Mathf.Max(0, (ChunkWidth - 1) / 2));
            int minimumSiteCoordinate = siteMargin;
            int maximumSiteCoordinate = ChunkWidth - siteMargin;

            if (maximumSiteCoordinate <= minimumSiteCoordinate)
            {
                minimumSiteCoordinate = 0;
                maximumSiteCoordinate = ChunkWidth;
            }

            int villageSiteX = ChunkWidth / 2;
            int villageSiteZ = ChunkWidth / 2;
            float bestSiteScore = float.NegativeInfinity;

            for (int candidateIndex = 0; candidateIndex < villageCandidateCount; candidateIndex++)
            {
                int candidateX = villageRandom.Next(minimumSiteCoordinate, maximumSiteCoordinate);
                int candidateZ = villageRandom.Next(minimumSiteCoordinate, maximumSiteCoordinate);
                int sampleStartX = Mathf.Max(0, candidateX - 10);
                int sampleEndX = Mathf.Min(ChunkWidth, candidateX + 11);
                int sampleStartZ = Mathf.Max(0, candidateZ - 10);
                int sampleEndZ = Mathf.Min(ChunkWidth, candidateZ + 11);
                int minimumHeight = int.MaxValue;
                int maximumHeight = int.MinValue;
                int underwaterColumns = 0;
                float nearbyRiverInfluence = 0.0f;

                for (int x = sampleStartX; x < sampleEndX; x++)
                {
                    for (int z = sampleStartZ; z < sampleEndZ; z++)
                    {
                        int columnHeight = surfaceHeights[x, z];
                        minimumHeight = Mathf.Min(minimumHeight, columnHeight);
                        maximumHeight = Mathf.Max(maximumHeight, columnHeight);
                        if (columnHeight < waterHeight)
                        {
                            underwaterColumns++;
                        }

                        nearbyRiverInfluence = Mathf.Max(
                            nearbyRiverInfluence,
                            riverInfluences[x, z]
                        );
                    }
                }

                float siteScore =
                    -(maximumHeight - minimumHeight) * 20.0f -
                    underwaterColumns * 3.0f -
                    riverInfluences[candidateX, candidateZ] * 100.0f +
                    nearbyRiverInfluence * 5.0f;

                if (siteScore > bestSiteScore)
                {
                    bestSiteScore = siteScore;
                    villageSiteX = candidateX;
                    villageSiteZ = candidateZ;
                }
            }

            int roadHalfWidth = VillageRoadWidth / 2;
            villageSiteX = Mathf.Clamp(
                villageSiteX,
                roadHalfWidth,
                ChunkWidth - roadHalfWidth - 1
            );
            villageSiteZ = Mathf.Clamp(
                villageSiteZ,
                roadHalfWidth,
                ChunkWidth - roadHalfWidth - 1
            );
            bool mainRoadAlongX = villageRandom.Next(0, 2) == 0;
            Vector2Int villageCenter =
                new Vector2Int(villageSiteX, villageSiteZ);
            List<Vector2Int> buildingOrigins = new List<Vector2Int>();
            List<Vector2Int> buildingServicePoints = new List<Vector2Int>();
            List<int> buildingRotations = new List<int>();
            List<VillageBuildingTemplate> buildingTemplates =
                new List<VillageBuildingTemplate>();
            List<BoundsInt> buildingFootprints = new List<BoundsInt>();
            List<Vector2Int> branchRoadStarts = new List<Vector2Int>();
            List<Vector2Int> branchRoadEnds = new List<Vector2Int>();
            int branchSeparation =
                VillageHouseTemplate.Depth + villageBuildingSpacing * 2;
            int branchLength = branchSeparation;
            int sideBuildingDistance =
                roadHalfWidth +
                villageBuildingSpacing +
                (VillageHouseTemplate.Width + 1) / 2;
            int centerMainCoordinate =
                mainRoadAlongX ? villageCenter.x : villageCenter.y;
            int firstBranchOffset = (branchSeparation + 1) / 2;
            int firstMainDirection = villageRandom.Next(0, 2) == 0 ? -1 : 1;
            int maximumBranchAttempts = Mathf.Max(4, villageBuildingCount * 4);

            for (
                int branchAttempt = 0;
                branchAttempt < maximumBranchAttempts &&
                buildingOrigins.Count < villageBuildingCount;
                branchAttempt++
            )
            {
                int mainDirection = branchAttempt % 2 == 0 ?
                    firstMainDirection :
                    -firstMainDirection;
                int distanceRing = branchAttempt / 2;
                int branchStation =
                    centerMainCoordinate +
                    mainDirection *
                    (firstBranchOffset + distanceRing * branchSeparation);

                if (
                    branchStation < roadHalfWidth ||
                    branchStation >= ChunkWidth - roadHalfWidth
                )
                {
                    continue;
                }

                int firstBranchSide =
                    villageRandom.Next(0, 2) == 0 ? -1 : 1;

                for (
                    int branchSideAttempt = 0;
                    branchSideAttempt < 2;
                    branchSideAttempt++
                )
                {
                    int branchSide = branchSideAttempt == 0 ?
                        firstBranchSide :
                        -firstBranchSide;
                    Vector2Int roadStart;
                    Vector2Int branchDirection;
                    Vector2Int mainPositiveDirection;

                    if (mainRoadAlongX)
                    {
                        roadStart = new Vector2Int(
                            branchStation,
                            villageCenter.y
                        );
                        branchDirection = branchSide > 0 ?
                            Vector2Int.up :
                            Vector2Int.down;
                        mainPositiveDirection = Vector2Int.right;
                    }
                    else
                    {
                        roadStart = new Vector2Int(
                            villageCenter.x,
                            branchStation
                        );
                        branchDirection = branchSide > 0 ?
                            Vector2Int.right :
                            Vector2Int.left;
                        mainPositiveDirection = Vector2Int.up;
                    }

                    Vector2Int roadEnd =
                        roadStart + branchDirection * branchLength;
                    int buildingsBeforeBranch = buildingOrigins.Count;

                    TryPlanVillageBuilding(
                        roadEnd,
                        -branchDirection,
                        VillageHouseTemplate,
                        ChunkWidth,
                        villageBuildingSpacing,
                        surfaceHeights,
                        riverInfluences,
                        waterHeight,
                        buildingOrigins,
                        buildingServicePoints,
                        buildingRotations,
                        buildingTemplates,
                        buildingFootprints
                    );

                    if (buildingOrigins.Count < villageBuildingCount)
                    {
                        Vector2Int sideServicePoint =
                            roadStart +
                            branchDirection * sideBuildingDistance;
                        Vector2Int firstSideDirection =
                            villageRandom.Next(0, 2) == 0 ?
                            mainPositiveDirection :
                            -mainPositiveDirection;
                        bool placedSideBuilding = TryPlanVillageBuilding(
                            sideServicePoint,
                            -firstSideDirection,
                            VillageHouseTemplate,
                            ChunkWidth,
                            villageBuildingSpacing,
                            surfaceHeights,
                            riverInfluences,
                            waterHeight,
                            buildingOrigins,
                            buildingServicePoints,
                            buildingRotations,
                            buildingTemplates,
                            buildingFootprints
                        );

                        if (!placedSideBuilding)
                        {
                            TryPlanVillageBuilding(
                                sideServicePoint,
                                firstSideDirection,
                                VillageHouseTemplate,
                                ChunkWidth,
                                villageBuildingSpacing,
                                surfaceHeights,
                                riverInfluences,
                                waterHeight,
                                buildingOrigins,
                                buildingServicePoints,
                                buildingRotations,
                                buildingTemplates,
                                buildingFootprints
                            );
                        }
                    }

                    if (buildingOrigins.Count > buildingsBeforeBranch)
                    {
                        branchRoadStarts.Add(roadStart);
                        branchRoadEnds.Add(roadEnd);
                        break;
                    }
                }
            }

            int buildingsToPlace = buildingOrigins.Count;
            List<int> buildingTargetYs = new List<int>();

            if (buildingsToPlace > 0)
            {
                int minimumMainCoordinate = centerMainCoordinate;
                int maximumMainCoordinate = centerMainCoordinate;

                for (
                    int branchIndex = 0;
                    branchIndex < branchRoadStarts.Count;
                    branchIndex++
                )
                {
                    int branchCoordinate = mainRoadAlongX ?
                        branchRoadStarts[branchIndex].x :
                        branchRoadStarts[branchIndex].y;
                    minimumMainCoordinate = Mathf.Min(
                        minimumMainCoordinate,
                        branchCoordinate
                    );
                    maximumMainCoordinate = Mathf.Max(
                        maximumMainCoordinate,
                        branchCoordinate
                    );
                }

                Vector2Int mainRoadStart = mainRoadAlongX ?
                    new Vector2Int(minimumMainCoordinate, villageCenter.y) :
                    new Vector2Int(villageCenter.x, minimumMainCoordinate);
                Vector2Int mainRoadEnd = mainRoadAlongX ?
                    new Vector2Int(maximumMainCoordinate, villageCenter.y) :
                    new Vector2Int(villageCenter.x, maximumMainCoordinate);
                Dictionary<Vector2Int, int> villageRoadHeights =
                    new Dictionary<Vector2Int, int>();

                TerraformVillageRoad(
                    blocks,
                    surfaceHeights,
                    riverInfluences,
                    mainRoadStart,
                    villageCenter,
                    mainRoadEnd,
                    waterHeight,
                    -1,
                    villageRoadHeights
                );

                for (
                    int branchIndex = 0;
                    branchIndex < branchRoadStarts.Count;
                    branchIndex++
                )
                {
                    Vector2Int roadStart = branchRoadStarts[branchIndex];

                    TerraformVillageRoad(
                        blocks,
                        surfaceHeights,
                        riverInfluences,
                        roadStart,
                        roadStart,
                        branchRoadEnds[branchIndex],
                        waterHeight,
                        villageRoadHeights[roadStart],
                        villageRoadHeights
                    );
                }

                for (
                    int buildingIndex = 0;
                    buildingIndex < buildingsToPlace;
                    buildingIndex++
                )
                {
                    int roadHeight = villageRoadHeights[
                        buildingServicePoints[buildingIndex]
                    ];
                    int minimumBaseHeight = GetVillageBuildingMinimumBaseHeight(
                        surfaceHeights,
                        buildingTemplates[buildingIndex],
                        buildingOrigins[buildingIndex],
                        buildingRotations[buildingIndex],
                        roadHeight
                    );
                    buildingTargetYs.Add(
                        Mathf.Clamp(
                            Mathf.Max(roadHeight, minimumBaseHeight),
                            0,
                            ChunkHeight -
                                buildingTemplates[buildingIndex].Height -
                                1
                        )
                    );
                }
            }

            for (int buildingIndex = 0; buildingIndex < buildingsToPlace; buildingIndex++)
            {
                int rotation = buildingRotations[buildingIndex];
                VillageBuildingTemplate buildingTemplate =
                    buildingTemplates[buildingIndex];
                Vector2Int origin = buildingOrigins[buildingIndex];

                PrepareVillageBuildingFoundation(
                    blocks,
                    buildingTemplate,
                    origin,
                    buildingTargetYs[buildingIndex],
                    rotation
                );
                PlaceVillageBuilding(
                    blocks,
                    buildingTemplate,
                    origin,
                    buildingTargetYs[buildingIndex],
                    rotation
                );
            }
        }

        for (int x = 0; x < ChunkWidth; x++)
        {
            for (int z = 0; z < ChunkWidth; z++)
            {
                for (int y = ChunkHeight - 1; y >= 0; y--)
                {
                    if (
                        blocks[x, y, z] == BlockType.Dirt &&
                        y >= waterHeight &&
                        (
                            y == ChunkHeight - 1 ||
                            blocks[x, y + 1, z] == BlockType.Air
                        )
                    )
                    {
                        blocks[x, y, z] = BlockType.Grass;
                    }
                }
            }
        }

        return blocks;
    }

    private static Vector2Int RotateVillageTemplateCoordinate(
        int localX,
        int localZ,
        int rotation,
        int templateWidth,
        int templateDepth
    )
    {
        switch (rotation & 3)
        {
            case 0:
                return new Vector2Int(localX, localZ);
            case 1:
                return new Vector2Int(templateDepth - 1 - localZ, localX);
            case 2:
                return new Vector2Int(
                    templateWidth - 1 - localX,
                    templateDepth - 1 - localZ
                );
            default:
                return new Vector2Int(localZ, templateWidth - 1 - localX);
        }
    }

    private static bool TryPlanVillageBuilding(
        Vector2Int servicePoint,
        Vector2Int outwardDirection,
        VillageBuildingTemplate buildingTemplate,
        int chunkWidth,
        int buildingSpacing,
        int[,] surfaceHeights,
        float[,] riverInfluences,
        int waterHeight,
        List<Vector2Int> buildingOrigins,
        List<Vector2Int> buildingServicePoints,
        List<int> buildingRotations,
        List<VillageBuildingTemplate> buildingTemplates,
        List<BoundsInt> buildingFootprints
    )
    {
        int rotation;

        if (outwardDirection == Vector2Int.down)
        {
            rotation = 0;
        }
        else if (outwardDirection == Vector2Int.right)
        {
            rotation = 1;
        }
        else if (outwardDirection == Vector2Int.up)
        {
            rotation = 2;
        }
        else
        {
            rotation = 3;
        }

        Vector2Int entrance = servicePoint - outwardDirection * 3;
        Vector2Int rotatedDoor = RotateVillageTemplateCoordinate(
            buildingTemplate.Width / 2,
            0,
            rotation,
            buildingTemplate.Width,
            buildingTemplate.Depth
        );
        Vector2Int buildingOrigin = entrance - rotatedDoor;
        int rotatedWidth = rotation % 2 == 0 ?
            buildingTemplate.Width :
            buildingTemplate.Depth;
        int rotatedDepth = rotation % 2 == 0 ?
            buildingTemplate.Depth :
            buildingTemplate.Width;
        BoundsInt buildingFootprint = new BoundsInt(
            buildingOrigin.x,
            0,
            buildingOrigin.y,
            rotatedWidth,
            1,
            rotatedDepth
        );

        if (
            buildingFootprint.xMin < 0 ||
            buildingFootprint.xMax > chunkWidth ||
            buildingFootprint.zMin < 0 ||
            buildingFootprint.zMax > chunkWidth
        )
        {
            return false;
        }

        for (int x = buildingFootprint.xMin; x < buildingFootprint.xMax; x++)
        {
            for (int z = buildingFootprint.zMin; z < buildingFootprint.zMax; z++)
            {
                if (
                    surfaceHeights[x, z] < waterHeight ||
                    riverInfluences[x, z] > 0.5f
                )
                {
                    return false;
                }
            }
        }

        for (
            int footprintIndex = 0;
            footprintIndex < buildingFootprints.Count;
            footprintIndex++
        )
        {
            BoundsInt existingFootprint = buildingFootprints[footprintIndex];

            if (
                buildingFootprint.xMin <
                    existingFootprint.xMax + buildingSpacing &&
                buildingFootprint.xMax + buildingSpacing >
                    existingFootprint.xMin &&
                buildingFootprint.zMin <
                    existingFootprint.zMax + buildingSpacing &&
                buildingFootprint.zMax + buildingSpacing >
                    existingFootprint.zMin
            )
            {
                return false;
            }
        }

        buildingOrigins.Add(buildingOrigin);
        buildingServicePoints.Add(servicePoint);
        buildingRotations.Add(rotation);
        buildingTemplates.Add(buildingTemplate);
        buildingFootprints.Add(buildingFootprint);
        return true;
    }

    private static int GetVillageBuildingMinimumBaseHeight(
        int[,] surfaceHeights,
        VillageBuildingTemplate buildingTemplate,
        Vector2Int origin,
        int rotation,
        int fallbackHeight
    )
    {
        int minimumBaseHeight = int.MaxValue;

        for (int localZ = 0; localZ < buildingTemplate.Depth; localZ++)
        {
            for (int localX = 0; localX < buildingTemplate.Width; localX++)
            {
                VillageBuildingCell floorCell =
                    buildingTemplate.GetCell(localX, 0, localZ);

                if (
                    !floorCell.ReplacesBlock ||
                    floorCell.Block == BlockType.Air
                )
                {
                    continue;
                }

                Vector2Int rotatedCoordinate = RotateVillageTemplateCoordinate(
                    localX,
                    localZ,
                    rotation,
                    buildingTemplate.Width,
                    buildingTemplate.Depth
                );
                int worldX = origin.x + rotatedCoordinate.x;
                int worldZ = origin.y + rotatedCoordinate.y;
                minimumBaseHeight = Mathf.Min(
                    minimumBaseHeight,
                    surfaceHeights[worldX, worldZ]
                );
            }
        }

        return minimumBaseHeight == int.MaxValue ?
            fallbackHeight :
            minimumBaseHeight;
    }

    private static void PrepareVillageBuildingFoundation(
        BlockType[,,] blocks,
        VillageBuildingTemplate buildingTemplate,
        Vector2Int origin,
        int baseY,
        int rotation
    )
    {
        for (int localZ = 0; localZ < buildingTemplate.Depth; localZ++)
        {
            for (int localX = 0; localX < buildingTemplate.Width; localX++)
            {
                VillageBuildingCell floorCell =
                    buildingTemplate.GetCell(localX, 0, localZ);

                if (!floorCell.ReplacesBlock)
                {
                    continue;
                }

                Vector2Int rotatedCoordinate = RotateVillageTemplateCoordinate(
                    localX,
                    localZ,
                    rotation,
                    buildingTemplate.Width,
                    buildingTemplate.Depth
                );
                int worldX = origin.x + rotatedCoordinate.x;
                int worldZ = origin.y + rotatedCoordinate.y;

                if (
                    worldX < 0 || worldX >= blocks.GetLength(0) ||
                    worldZ < 0 || worldZ >= blocks.GetLength(2)
                )
                {
                    continue;
                }

                if (floorCell.Block == BlockType.Air)
                {
                    continue;
                }

                for (int y = baseY - 1; y >= 0; y--)
                {
                    if (blocks[worldX, y, worldZ] != BlockType.Air)
                    {
                        break;
                    }

                    blocks[worldX, y, worldZ] = BlockType.Dirt;
                }
            }
        }
    }

    private static int TerraformVillageRoad(
        BlockType[,,] blocks,
        int[,] surfaceHeights,
        float[,] riverInfluences,
        Vector2Int start,
        Vector2Int corner,
        Vector2Int end,
        int waterHeight,
        int forcedStartHeight,
        Dictionary<Vector2Int, int> generatedCenterlineHeights
    )
    {
        List<Vector2Int> centerline = new List<Vector2Int>();
        AppendVillageRoadSegment(centerline, start, corner, true);
        AppendVillageRoadSegment(centerline, corner, end, false);

        int[] roadHeights = new int[centerline.Count];
        bool[] waterLevelAnchors = new bool[centerline.Count];

        for (int pointIndex = 0; pointIndex < centerline.Count; pointIndex++)
        {
            Vector2Int point = centerline[pointIndex];
            int sampleX = Mathf.Clamp(point.x, 0, blocks.GetLength(0) - 1);
            int sampleZ = Mathf.Clamp(point.y, 0, blocks.GetLength(2) - 1);
            int targetHeight = surfaceHeights[sampleX, sampleZ];

            if (
                riverInfluences[sampleX, sampleZ] > 0.5f &&
                surfaceHeights[sampleX, sampleZ] < waterHeight
            )
            {
                targetHeight = waterHeight;
                waterLevelAnchors[pointIndex] = true;
            }

            roadHeights[pointIndex] = targetHeight;
        }

        for (int anchorIndex = 0; anchorIndex < roadHeights.Length; anchorIndex++)
        {
            if (!waterLevelAnchors[anchorIndex])
            {
                continue;
            }

            for (int pointIndex = 0; pointIndex < roadHeights.Length; pointIndex++)
            {
                roadHeights[pointIndex] = Mathf.Min(
                    roadHeights[pointIndex],
                    waterHeight + Mathf.Abs(pointIndex - anchorIndex)
                );
            }
        }

        if (forcedStartHeight >= 0)
        {
            roadHeights[0] = Mathf.Clamp(
                forcedStartHeight,
                0,
                blocks.GetLength(1) - 1
            );
        }

        for (int pointIndex = 1; pointIndex < roadHeights.Length; pointIndex++)
        {
            roadHeights[pointIndex] = Mathf.Clamp(
                roadHeights[pointIndex],
                roadHeights[pointIndex - 1] - 1,
                roadHeights[pointIndex - 1] + 1
            );
        }

        if (forcedStartHeight < 0)
        {
            for (
                int pointIndex = roadHeights.Length - 2;
                pointIndex >= 0;
                pointIndex--
            )
            {
                roadHeights[pointIndex] = Mathf.Clamp(
                    roadHeights[pointIndex],
                    roadHeights[pointIndex + 1] - 1,
                    roadHeights[pointIndex + 1] + 1
                );
            }
        }

        int halfWidth = VillageRoadWidth / 2;

        for (int pointIndex = 0; pointIndex < centerline.Count; pointIndex++)
        {
            Vector2Int point = centerline[pointIndex];

            if (generatedCenterlineHeights != null)
            {
                generatedCenterlineHeights[point] = roadHeights[pointIndex];
            }

            for (int offsetX = -halfWidth; offsetX <= halfWidth; offsetX++)
            {
                for (int offsetZ = -halfWidth; offsetZ <= halfWidth; offsetZ++)
                {
                    TerraformVillageRoadColumn(
                        blocks,
                        surfaceHeights,
                        riverInfluences,
                        point.x + offsetX,
                        point.y + offsetZ,
                        roadHeights[pointIndex]
                    );
                }
            }
        }

        return roadHeights[roadHeights.Length - 1];
    }

    private static void AppendVillageRoadSegment(
        List<Vector2Int> centerline,
        Vector2Int start,
        Vector2Int end,
        bool includeStart
    )
    {
        int deltaX = System.Math.Sign(end.x - start.x);
        int deltaZ = System.Math.Sign(end.y - start.y);
        int stepCount = Mathf.Max(
            Mathf.Abs(end.x - start.x),
            Mathf.Abs(end.y - start.y)
        );
        int firstStep = includeStart ? 0 : 1;

        for (int step = firstStep; step <= stepCount; step++)
        {
            centerline.Add(
                new Vector2Int(
                    start.x + deltaX * step,
                    start.y + deltaZ * step
                )
            );
        }
    }

    private static void TerraformVillageRoadColumn(
        BlockType[,,] blocks,
        int[,] surfaceHeights,
        float[,] riverInfluences,
        int x,
        int z,
        int roadY
    )
    {
        if (
            x < 0 || x >= blocks.GetLength(0) ||
            z < 0 || z >= blocks.GetLength(2)
        )
        {
            return;
        }

        roadY = Mathf.Clamp(roadY, 0, blocks.GetLength(1) - 1);
        bool isUnsupportedRiverCrossing =
            riverInfluences[x, z] > 0.5f &&
            surfaceHeights[x, z] < roadY;

        for (int y = roadY + 1; y < blocks.GetLength(1); y++)
        {
            blocks[x, y, z] = BlockType.Air;
        }

        if (!isUnsupportedRiverCrossing)
        {
            for (
                int y = Mathf.Max(0, roadY - VillageFoundationDepth);
                y < roadY;
                y++
            )
            {
                if (blocks[x, y, z] == BlockType.Air)
                {
                    blocks[x, y, z] = BlockType.Stone;
                }
            }
        }

        blocks[x, roadY, z] =
            isUnsupportedRiverCrossing ?
            BlockType.WoodPlank :
            BlockType.Path;
    }

    private static void PlaceVillageBuilding(
        BlockType[,,] blocks,
        VillageBuildingTemplate buildingTemplate,
        Vector2Int origin,
        int baseY,
        int rotation
    )
    {
        for (int localY = 0; localY < buildingTemplate.Height; localY++)
        {
            int worldY = baseY + localY;

            if (worldY < 0 || worldY >= blocks.GetLength(1))
            {
                continue;
            }

            for (int localZ = 0; localZ < buildingTemplate.Depth; localZ++)
            {
                for (int localX = 0; localX < buildingTemplate.Width; localX++)
                {
                    VillageBuildingCell buildingCell =
                        buildingTemplate.GetCell(localX, localY, localZ);

                    if (!buildingCell.ReplacesBlock)
                    {
                        continue;
                    }

                    Vector2Int rotatedCoordinate = RotateVillageTemplateCoordinate(
                        localX,
                        localZ,
                        rotation,
                        buildingTemplate.Width,
                        buildingTemplate.Depth
                    );
                    int worldX = origin.x + rotatedCoordinate.x;
                    int worldZ = origin.y + rotatedCoordinate.y;

                    if (
                        worldX < 0 || worldX >= blocks.GetLength(0) ||
                        worldZ < 0 || worldZ >= blocks.GetLength(2)
                    )
                    {
                        continue;
                    }

                    blocks[worldX, worldY, worldZ] = buildingCell.Block;
                }
            }
        }
    }

    private static void BuildDungeonRoom(
        BlockType[,,] blocks,
        BoundsInt room,
        int floorY,
        System.Random mossRandom,
        float mossChance
    )
    {
        int roomStartX = Mathf.Max(0, room.xMin);
        int roomEndX = Mathf.Min(blocks.GetLength(0), room.xMax);
        int roomStartZ = Mathf.Max(0, room.zMin);
        int roomEndZ = Mathf.Min(blocks.GetLength(2), room.zMax);
        int roofY = Mathf.Min(
            floorY + DungeonRoomHeight + 1,
            blocks.GetLength(1) - 1
        );

        for (int x = roomStartX; x < roomEndX; x++)
        {
            for (int z = roomStartZ; z < roomEndZ; z++)
            {
                for (int y = floorY; y <= roofY; y++)
                {
                    bool isRoomShell =
                        y == floorY ||
                        y == roofY ||
                        x == room.xMin ||
                        x == room.xMax - 1 ||
                        z == room.zMin ||
                        z == room.zMax - 1;

                    if (isRoomShell)
                    {
                        bool isUnsupportedFloor =
                            y == floorY &&
                            floorY > 0 &&
                            blocks[x, floorY - 1, z] == BlockType.Air;

                        if (isUnsupportedFloor)
                        {
                            blocks[x, y, z] = BlockType.WoodPlank;
                        }
                        else
                        {
                            blocks[x, y, z] =
                                mossRandom.NextDouble() < mossChance ?
                                BlockType.MossyStoneBrick :
                                BlockType.StoneBrick;
                        }
                    }
                    else
                    {
                        blocks[x, y, z] = BlockType.Air;
                    }
                }
            }
        }
    }

    private static void CarveDungeonCorridor(
        BlockType[,,] blocks,
        Vector3Int start,
        Vector3Int end,
        List<BoundsInt> dungeonRooms,
        System.Random mossRandom,
        float mossChance
    )
    {
        int halfWidth = DungeonCorridorWidth / 2;
        int minimumX;
        int maximumX;
        int minimumZ;
        int maximumZ;

        if (start.x != end.x)
        {
            minimumX = Mathf.Min(start.x, end.x) - halfWidth;
            maximumX = Mathf.Max(start.x, end.x) + halfWidth + 1;
            minimumZ = start.z - halfWidth;
            maximumZ = start.z + halfWidth + 1;
        }
        else
        {
            minimumX = start.x - halfWidth;
            maximumX = start.x + halfWidth + 1;
            minimumZ = Mathf.Min(start.z, end.z) - halfWidth;
            maximumZ = Mathf.Max(start.z, end.z) + halfWidth + 1;
        }

        minimumX = Mathf.Max(0, minimumX);
        maximumX = Mathf.Min(blocks.GetLength(0), maximumX);
        minimumZ = Mathf.Max(0, minimumZ);
        maximumZ = Mathf.Min(blocks.GetLength(2), maximumZ);
        int floorY = Mathf.Clamp(start.y, 0, blocks.GetLength(1) - 1);
        int ceilingY = Mathf.Min(
            floorY + DungeonCorridorHeight,
            blocks.GetLength(1) - 1
        );

        for (int x = minimumX; x < maximumX; x++)
        {
            for (int z = minimumZ; z < maximumZ; z++)
            {
                bool insideDungeonRoom = false;

                for (int roomIndex = 0; roomIndex < dungeonRooms.Count; roomIndex++)
                {
                    BoundsInt room = dungeonRooms[roomIndex];

                    if (
                        x >= room.xMin && x < room.xMax &&
                        z >= room.zMin && z < room.zMax
                    )
                    {
                        insideDungeonRoom = true;
                        break;
                    }
                }

                if (!insideDungeonRoom)
                {
                    bool isUnsupportedFloor =
                        floorY > 0 &&
                        blocks[x, floorY - 1, z] == BlockType.Air;

                    if (isUnsupportedFloor)
                    {
                        blocks[x, floorY, z] = BlockType.WoodPlank;
                    }
                    else
                    {
                        blocks[x, floorY, z] =
                            mossRandom.NextDouble() < mossChance ?
                            BlockType.MossyStoneBrick :
                            BlockType.StoneBrick;
                    }
                }

                for (int y = floorY + 1; y <= ceilingY; y++)
                {
                    blocks[x, y, z] = BlockType.Air;
                }
            }
        }
    }

    private static float SampleValueNoise3D(float x, float y, float z, int noiseSeed)
    {
        int x0 = (int)System.Math.Floor(x);
        int y0 = (int)System.Math.Floor(y);
        int z0 = (int)System.Math.Floor(z);
        int x1 = x0 + 1;
        int y1 = y0 + 1;
        int z1 = z0 + 1;
        float blendX = Fade(x - x0);
        float blendY = Fade(y - y0);
        float blendZ = Fade(z - z0);
        float lowerFront = Lerp(
            HashToSignedFloat(x0, y0, z0, noiseSeed),
            HashToSignedFloat(x1, y0, z0, noiseSeed),
            blendX
        );
        float lowerBack = Lerp(
            HashToSignedFloat(x0, y0, z1, noiseSeed),
            HashToSignedFloat(x1, y0, z1, noiseSeed),
            blendX
        );
        float upperFront = Lerp(
            HashToSignedFloat(x0, y1, z0, noiseSeed),
            HashToSignedFloat(x1, y1, z0, noiseSeed),
            blendX
        );
        float upperBack = Lerp(
            HashToSignedFloat(x0, y1, z1, noiseSeed),
            HashToSignedFloat(x1, y1, z1, noiseSeed),
            blendX
        );

        return Lerp(
            Lerp(lowerFront, lowerBack, blendZ),
            Lerp(upperFront, upperBack, blendZ),
            blendY
        );
    }

    private static float HashToSignedFloat(int x, int y, int z, int noiseSeed)
    {
        uint hash;

        unchecked
        {
            hash = (uint)noiseSeed;
            hash ^= (uint)x * 374761393u;
            hash = (hash << 13) | (hash >> 19);
            hash ^= (uint)y * 668265263u;
            hash = (hash << 11) | (hash >> 21);
            hash ^= (uint)z * 2246822519u;
            hash *= 3266489917u;
            hash ^= hash >> 16;
        }

        return (hash & 0x00ffffffu) / 8388607.5f - 1.0f;
    }

    private static float Fade(float value)
    {
        return value * value * value * (value * (value * 6.0f - 15.0f) + 10.0f);
    }

    private static float Lerp(float start, float end, float amount)
    {
        return start + (end - start) * amount;
    }

    private static MeshConstructionData ConstructMeshData(BlockType[,,] blocks)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int chunkSizeX = blocks.GetLength(0);
        int chunkSizeY = blocks.GetLength(1);
        int chunkSizeZ = blocks.GetLength(2);

        for (int y = 0; y < chunkSizeY; y++)
        {
            for (int x = 0; x < chunkSizeX; x++)
            {
                for (int z = 0; z < chunkSizeZ; z++)
                {
                    if (blocks[x, y, z] == BlockType.Air)
                    {
                        continue;
                    }

                    for (int face = 0; face < NeighborDirections.Length; face++)
                    {
                        int neighborX = x + NeighborDirections[face].x;
                        int neighborY = y + NeighborDirections[face].y;
                        int neighborZ = z + NeighborDirections[face].z;

                        bool neighborIsOutside =
                            neighborX < 0 || neighborX >= chunkSizeX ||
                            neighborY < 0 || neighborY >= chunkSizeY ||
                            neighborZ < 0 || neighborZ >= chunkSizeZ;

                        if (!neighborIsOutside && blocks[neighborX, neighborY, neighborZ] != BlockType.Air)
                        {
                            continue;
                        }

                        int firstVertex = vertices.Count;
                        Vector3 blockPosition = new Vector3(x, y, z);
                        Vector2Int atlasTile = AtlasTiles[(int)blocks[x, y, z], face];

                        for (int corner = 0; corner < 4; corner++)
                        {
                            Vector2 faceUv = FaceUvs[corner];
                            vertices.Add(blockPosition + FaceVertices[face, corner]);
                            uvs.Add(new Vector2(
                                (
                                    atlasTile.x + AtlasTileInset +
                                    faceUv.x * (1.0f - 2.0f * AtlasTileInset)
                                ) / AtlasColumns,
                                (
                                    atlasTile.y + AtlasTileInset +
                                    faceUv.y * (1.0f - 2.0f * AtlasTileInset)
                                ) / AtlasRows
                            ));
                        }

                        triangles.Add(firstVertex);
                        triangles.Add(firstVertex + 1);
                        triangles.Add(firstVertex + 2);
                        triangles.Add(firstVertex);
                        triangles.Add(firstVertex + 2);
                        triangles.Add(firstVertex + 3);
                    }
                }
            }
        }

        return new MeshConstructionData
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            uvs = uvs.ToArray()
        };
    }

    private void ApplyMesh(MeshConstructionData data)
    {
        Mesh mesh = new Mesh
        {
            name = "Generated Stone Chunk",
            hideFlags = HideFlags.DontSave,
            indexFormat = IndexFormat.UInt32,
            vertices = data.vertices,
            triangles = data.triangles,
            uv = data.uvs
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh previousMesh = meshFilter.sharedMesh;
        meshFilter.sharedMesh = mesh;

        if (previousMesh != null && (previousMesh.hideFlags & HideFlags.DontSave) != 0)
        {
            if (Application.isPlaying)
            {
                Destroy(previousMesh);
            }
            else
            {
                DestroyImmediate(previousMesh);
            }
        }
    }

    public async void GenerateChunk()
    {
        if (IsGenerating)
        {
            return;
        }

        IsGenerating = true;

        try
        {
            BlockType[,,] blocks = GenerateBlockMap();
            ApplyMesh(await Task.Run(() => ConstructMeshData(blocks)));

            if (waterPrefab != null)
            {
                if (waterInstance != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(waterInstance);
                    }
                    else
                    {
                        DestroyImmediate(waterInstance);
                    }
                }

                float waterDepth = Mathf.Max(0.01f, waterHeight - baseTerrainHeight);
                float waterWidth = Mathf.Max(0.01f, ChunkWidth - 2.0f * waterHorizontalInset);
                waterInstance = Instantiate(waterPrefab, transform);
                waterInstance.hideFlags = HideFlags.DontSave;
                waterInstance.transform.localPosition = new Vector3(
                    ChunkWidth * 0.5f,
                    waterHeight + 1.0f - waterDepth * 0.5f - waterVerticalOffset,
                    ChunkWidth * 0.5f
                );
                waterInstance.transform.localRotation = Quaternion.identity;
                waterInstance.transform.localScale = new Vector3(waterWidth, waterDepth, waterWidth);
            }
        }
        finally
        {
            IsGenerating = false;
        }
    }
}
