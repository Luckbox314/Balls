using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

public class PhysicsSimulation : MonoBehaviour
{
    [Header("General Settings")]
    private int numberOfBalls = 20000;
    public float gravity = -9.81f;

    [Header("Balls")]
    public float ballRadius = 0.2f;
    public Vector3 initialVelocityRange = new Vector3(2f, 2f, 2f);

    [Header("Grid Settings")]
    public float cellSize = 0.5f;

    [Header("Room Walls")]
    public float leftX = -10f;
    public float rightX = 10f;
    public float backZ = -10f;
    public float frontZ = 10f;
    public float groundPlaneY = 0f;
    public float restitution = 0.9f;
    private int numberOfPlanes;

    private NativeArray<Vector3> positions;
    private NativeArray<Vector3> velocities;
    private NativeArray<Vector3> velocityDeltas;
    private NativeArray<float> radii;

    private Plane[] planes; 
    private NativeArray<Vector3> planeNormals;
    private NativeArray<Vector3> planeRights;
    private NativeArray<Vector3> planeForwards;
    private NativeArray<Vector3> planePositions;
    private NativeArray<Vector2> planeSizes;

    private NativeMultiHashMap<int, int> grid;

    private Matrix4x4[] ballPosMatrices;
    public InstancedBallRenderer instancedRenderer;

    private void Start()
    {
        numberOfBalls = GlobalSimulationSettings.numberOfBalls;
        positions = new NativeArray<Vector3>(numberOfBalls, Allocator.Persistent);
        velocities = new NativeArray<Vector3>(numberOfBalls, Allocator.Persistent);
        velocityDeltas = new NativeArray<Vector3>(numberOfBalls, Allocator.Persistent);
        radii = new NativeArray<float>(numberOfBalls, Allocator.Persistent);

        for (int i = 0; i < numberOfBalls; i++)
        {
            positions[i] = new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(0f, 20f),
                Random.Range(-5f, 5f)
            );

            velocities[i] = new Vector3(
                Random.Range(-initialVelocityRange.x, initialVelocityRange.x),
                Random.Range(-initialVelocityRange.y, initialVelocityRange.y),
                Random.Range(-initialVelocityRange.z, initialVelocityRange.z)
            );

            radii[i] = ballRadius;
        }

        ballPosMatrices = new Matrix4x4[numberOfBalls];

        grid = new NativeMultiHashMap<int, int>(numberOfBalls * 4, Allocator.Persistent);

        planes = FindObjectsOfType<Plane>();
        numberOfPlanes = planes.Length;
        planeNormals = new NativeArray<Vector3>(numberOfPlanes, Allocator.Persistent);
        planeRights = new NativeArray<Vector3>(numberOfPlanes, Allocator.Persistent);
        planeForwards = new NativeArray<Vector3>(numberOfPlanes, Allocator.Persistent);
        planePositions = new NativeArray<Vector3>(numberOfPlanes, Allocator.Persistent);
        planeSizes = new NativeArray<Vector2>(numberOfPlanes, Allocator.Persistent);
        for (int i = 0; i < numberOfPlanes; i++)
        {
            planeNormals[i] = planes[i].normal;
            planeRights[i] = planes[i].right;
            planeForwards[i] = planes[i].forward;
            planePositions[i] = planes[i].position;
            planeSizes[i] = planes[i].size;
        }

    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < numberOfPlanes; i++)
        {
            planeNormals[i] = planes[i].normal;
            planeRights[i] = planes[i].right;
            planeForwards[i] = planes[i].forward;
            planePositions[i] = planes[i].position;
            planeSizes[i] = planes[i].size;
        }
        SimulateBallPhysics(dt);
    }

    private void Update()
    {
        // TODO: Interpolation
        for (int i = 0; i < numberOfBalls; i++)
        {
            
            ballPosMatrices[i] = Matrix4x4.TRS(
                positions[i],
                Quaternion.identity,
                Vector3.one * (ballRadius * 2f)
            );
        }

        // render
        instancedRenderer.UpdateBatches(ballPosMatrices, numberOfBalls);
    }

    private void SimulateBallPhysics(float deltaTime)
    {
        // 1) UpdatePositionsJob (gravity + movement + wall collisions)
        // 2) BuildGridJob
        // 3) CollisionsJob (grid-based ball-ball collisions)
        // 4) (Optional) Copy data back to CPU for rendering

        var initialMoveAndCollideJob = new InitialMoveAndCollideJob
        {
            positions = positions,
            velocities = velocities,
            radii = radii,
            deltaTime = deltaTime,
            gravity = gravity,
            planeNormals = planeNormals,
            planeRights = planeRights,
            planeForwards = planeForwards,
            planePositions = planePositions,
            planeSizes = planeSizes,
            numberOfPlanes  = numberOfPlanes,
            wallRestitution = restitution,
        };
        JobHandle initialMoveAndCollideHandle = initialMoveAndCollideJob.Schedule(numberOfBalls, 64);

        initialMoveAndCollideHandle.Complete();


        // Update the Grid
        grid.Clear();
        var buildGridJob = new BuildGridJob
        {
            positions = positions,
            cellSize = cellSize,
            grid = grid.AsParallelWriter()
        };
        JobHandle buildGridHandle = buildGridJob.Schedule(numberOfBalls, 64, initialMoveAndCollideHandle);
        buildGridHandle.Complete();


        // Calculate ball to ball collisions (aproximation)
        var ballCollisionJob = new BallCollisionsJob
        {
            positions = positions,
            velocities = velocities,
            radii = radii,
            cellSize = cellSize,
            grid = grid,
            velocityDeltas = velocityDeltas,
            restitution = restitution
        };
        JobHandle ballCollisionHandle = ballCollisionJob.Schedule(numberOfBalls, 64, buildGridHandle);
        ballCollisionHandle.Complete();


        // Update velocities after collisions
        var updateVelocitiesJob = new UpdateVelocitiesJob
        {
            velocities = velocities,
            velocityDeltas = velocityDeltas
        };

        JobHandle updateVelocitiesHandle = updateVelocitiesJob.Schedule(numberOfBalls, 64, ballCollisionHandle);

        updateVelocitiesHandle.Complete();
    }

    void OnDestroy()
    {
        if (positions.IsCreated) positions.Dispose();
        if (velocities.IsCreated) velocities.Dispose();
        if (velocityDeltas.IsCreated) velocityDeltas.Dispose();
        if (radii.IsCreated) radii.Dispose();

        if (planeNormals.IsCreated) planeNormals.Dispose();
        if (planeForwards.IsCreated) planeForwards.Dispose();
        if (planeRights.IsCreated) planeRights.Dispose();
        if (planePositions.IsCreated) planePositions.Dispose();
        if (planeSizes.IsCreated) planeSizes.Dispose();

        if (grid.IsCreated) grid.Dispose();
    }
}

// --------------------------------------------------------------------------------------
//  JOB #1: UPDATE POSITIONS AND COLLIDE WITH MAIN OBJECTS
// --------------------------------------------------------------------------------------
[BurstCompile]
struct InitialMoveAndCollideJob : IJobParallelFor
{
    public NativeArray<Vector3> positions;
    public NativeArray<Vector3> velocities;
    [ReadOnly] public NativeArray<float> radii;
    [ReadOnly] public NativeArray<Vector3> planeNormals;
    [ReadOnly] public NativeArray<Vector3> planeRights;
    [ReadOnly] public NativeArray<Vector3> planeForwards;
    [ReadOnly] public NativeArray<Vector3> planePositions;
    [ReadOnly] public NativeArray<Vector2> planeSizes;
    [ReadOnly] public int numberOfPlanes;

    public float deltaTime;
    public float gravity;

    public float wallRestitution;

    public void Execute(int index)
    {
        // Gravity
        Vector3 vel = velocities[index];
        vel.y += gravity * deltaTime;

        // Update position
        Vector3 pos = positions[index] + vel * deltaTime;

        float r = radii[index];

        // Collide with Planes
        for (int j = 0; j < numberOfPlanes; j++)
        {
            Vector3 planeNormal = planeNormals[j];
            Vector3 planeRight = planeRights[j];
            Vector3 planeForward = planeForwards[j];
            Vector3 planePos = planePositions[j];
            Vector2 planeSize = planeSizes[j];
            

            float distance = Vector3.Dot(pos - planePos, planeNormal);

            if (Mathf.Abs(distance) < r)
            {
                Vector3 projectedPos = pos - distance * planeNormal;
                Vector3 localPoint = projectedPos - planePos;

                if (Mathf.Abs(Vector3.Dot(localPoint, planeRight)) <= planeSize.x * 0.5f &&
                    Mathf.Abs(Vector3.Dot(localPoint, planeForward)) <= planeSize.y * 0.5f)
                {
                    float penetrationDepth = r - Mathf.Abs(distance);
                    Vector3 correction = planeNormal * penetrationDepth * Mathf.Sign(distance);

                    pos += correction;
                    vel = Vector3.Reflect(vel, planeNormal) * wallRestitution;
                }
            } else if (planeNormal.y > 0.5f && distance < r)
            {
                // ensure they don't go trough the ground
                Vector3 projectedPos = pos - distance * planeNormal;
                Vector3 localPoint = projectedPos - planePos;

                if (Mathf.Abs(Vector3.Dot(localPoint, planeRight)) <= planeSize.x * 0.5f &&
                    Mathf.Abs(Vector3.Dot(localPoint, planeForward)) <= planeSize.y * 0.5f)
                {
                    float penetrationDepth = r - distance;
                    Vector3 correction = planeNormal * penetrationDepth;

                    pos += correction;
                    vel = Vector3.Reflect(vel, planeNormal) * wallRestitution;
                }
            }
        }

        // Write back
        velocities[index] = vel;
        positions[index] = pos;
    }
}

// --------------------------------------------------------------------------------------
//  JOB #2: BUILD GRID
//  For each ball, compute its cell and add (cellHash -> ballIndex)
// --------------------------------------------------------------------------------------
[BurstCompile]
struct BuildGridJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector3> positions;
    public float cellSize;

    public NativeMultiHashMap<int, int>.ParallelWriter grid;

    public void Execute(int index)
    {
        Vector3 pos = positions[index];
        int cx = Mathf.FloorToInt(pos.x / cellSize);
        int cy = Mathf.FloorToInt(pos.y / cellSize);
        int cz = Mathf.FloorToInt(pos.z / cellSize);

        int hash = HashCoords(cx, cy, cz);
        grid.Add(hash, index);
    }

    // A simple hash for cell coords (cx, cy, cz).
    private int HashCoords(int x, int y, int z)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + y;
            hash = hash * 31 + z;
            return hash;
        }
    }
}


// --------------------------------------------------------------------------------------
//  JOB #3: COLLISIONS BETWEEN BALLS
//  For each ball, find which cell it's in + neighbor cells => check collisions
// --------------------------------------------------------------------------------------
[BurstCompile]
struct BallCollisionsJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<Vector3> positions;
    [ReadOnly] public NativeArray<float> radii;
    [ReadOnly] public NativeArray<Vector3> velocities;
    [ReadOnly] public NativeMultiHashMap<int, int> grid;
    public NativeArray<Vector3> velocityDeltas;

    public float restitution;
    public float cellSize;

    public void Execute(int index)
    {
        Vector3 posA = positions[index];
        float rA = radii[index];


        // figure out the cell coords
        int cx = Mathf.FloorToInt(posA.x / cellSize);
        int cy = Mathf.FloorToInt(posA.y / cellSize);
        int cz = Mathf.FloorToInt(posA.z / cellSize);

        for (int nx = cx - 1; nx <= cx + 1; nx++)
        {
            for (int ny = cy - 1; ny <= cy + 1; ny++)
            {
                for (int nz = cz - 1; nz <= cz + 1; nz++)
                {
                    int hash = HashCoords(nx, ny, nz);

                    NativeMultiHashMapIterator<int> it;
                    int otherIndex;
                    // iterate all ball indices in that cell
                    if (grid.TryGetFirstValue(hash, out otherIndex, out it))
                    {
                        do
                        {
                            if (otherIndex != index)
                            {
                                // check collision
                                Vector3 posB = positions[otherIndex];
                                float rB = radii[otherIndex];
                                Vector3 diff = posA - posB;
                                float distSq = diff.sqrMagnitude;
                                float radSum = (rA + rB);
                                if (distSq < radSum * radSum)
                                {
                                    // compute collision response impulse for ball A
                                    float dist = Mathf.Sqrt(distSq);
                                    if (dist > float.MinValue)
                                    {
                                        Vector3 normal = diff / dist;
                                        Vector3 velA = velocities[index];
                                        Vector3 velB = velocities[otherIndex];

                                        Vector3 relVel = velA - velB;
                                        float relSpeedAlongNormal = Vector3.Dot(relVel, normal);
                                        if (relSpeedAlongNormal < 0f)
                                        {
                                            float impulseMag = Mathf.Max(-(1.0f + restitution) * relSpeedAlongNormal / 2f, radSum - dist);
                                            Vector3 impulse = impulseMag * normal;

                                            // We'll add this impulse to ball A's velocity
                                            velocityDeltas[index] += impulse;
                                        }
                                    }
                                }
                            }
                        } while (grid.TryGetNextValue(out otherIndex, ref it));
                    }
                }
            }
        }
    }

    private int HashCoords(int x, int y, int z)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + x;
            hash = hash * 31 + y;
            hash = hash * 31 + z;
            return hash;
        }
    }
}

// --------------------------------------------------------------------------------------
//  JOB #4: UPDATE VELOCITIES 
// --------------------------------------------------------------------------------------
[BurstCompile] 
struct UpdateVelocitiesJob : IJobParallelFor
{
    public NativeArray<Vector3> velocityDeltas;
    public NativeArray<Vector3> velocities;

    public void Execute(int index)
    {
        velocities[index] += velocityDeltas[index];
        velocityDeltas[index] = Vector3.zero;
    }
}