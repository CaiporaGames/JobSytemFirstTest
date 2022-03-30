
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;


public class WaveGenerator : MonoBehaviour
{
    [Header("Wave Parameters")]
    public float waveScale;
    public float waveOffsetSpeed;
    public float waveHeight;

    [Header("References and Prefabs")]
    public MeshFilter waterMeshFilter;
    private Mesh waterMesh;

    //These arrays are responsible to bring the code from to the job system.
    NativeArray<Vector3> waterVertices;
    NativeArray<Vector3> waterNormals;


    JobHandle meshModificationJobHandle; // 1 This JobHandle serves three primary functions:    Scheduling a job correctly.
    //Making the main thread wait for a job’s completion.Adding dependencies. Dependencies ensure that a job only starts after another job completes.This prevents two jobs from changing the same data at the same time.It segments the logical flow of your game.

        UpdateMeshJob meshModificationJob; // 2 Reference an UpdateMeshJob so the entire class can access it

    //Initialization code for the waves
    private void Start()
    {
        waterMesh = waterMeshFilter.mesh;

        waterMesh.MarkDynamic(); // 1 Unity can optimize sending vertex changes from the CPU to the GPU.

        waterVertices =
        new NativeArray<Vector3>(waterMesh.vertices, Allocator.Persistent); // 2 You initialize waterVertices with the vertices of the waterMesh. You also assign a persistent allocator.

        waterNormals =
        new NativeArray<Vector3>(waterMesh.normals, Allocator.Persistent);// Allocator.Persistent = This ensures that you don’t have to re-initialize the NativeArray each time the job finishes.

    }

    private void Update()
    {
        // 1 You initialize the UpdateMeshJob with all the variables required for the job.
        meshModificationJob = new UpdateMeshJob()
        {
            vertices = waterVertices,
            normals = waterNormals,
            offsetSpeed = waveOffsetSpeed,
            time = Time.time,
            scale = waveScale,
            height = waveHeight 
        };

        // 2 The IJobParallelFor’s Schedule() requires the length of the loop and the batch size. The batch size determines how many segments to divide the work into.
        meshModificationJobHandle =
        meshModificationJob.Schedule(waterVertices.Length, 64);

    }

    private void LateUpdate()
    {
        // 1 Ensures the completion of the job because you can’t get the result of the vertices inside the job before it completes.
        meshModificationJobHandle.Complete();

        // 2 Unity allows you to directly set the vertices of a mesh from a job. This is a new improvement that eliminates copying the data back and forth between threads.
        waterMesh.SetVertices(meshModificationJob.vertices);

        // 3 You have to recalculate the normals of the mesh so that the lighting interacts with the deformed mesh correctly.
        waterMesh.RecalculateNormals();

    }

    private void OnDestroy()
    {
        waterVertices.Dispose();
        waterNormals.Dispose();
    }

    [BurstCompile]
    private struct UpdateMeshJob : IJobParallelFor
    {
        // 1 public NativeArray to read and write vertex data between the job and the main thread.
        public NativeArray<Vector3> vertices;

        // 2 you only want to read the data from the main thread.
        [ReadOnly]
        public NativeArray<Vector3> normals;

        // 3 These variables control how the Perlin noise function acts. The main thread passes them in.
        public float offsetSpeed;
        public float scale;
        public float height;

        // 4 Note that you cannot access statics such as Time.time within a job. Instead, you pass them in as variables during the job’s initialization.
        public float time;




        private float Noise(float x, float y)
        {
            float2 pos = math.float2(x, y);
            return noise.snoise(pos);
        }

        public void Execute(int i)
        {
            // 1 You ensure the wave only affects the vertices facing upwards. This excludes the base of the water.
            if (normals[i].z > 0f)
            {
                // 2 Here, you get a reference to the current vertex.
                var vertex = vertices[i];

                // 3 You sample Perlin noise with scaling and offset transformations.
                float noiseValue =
                Noise(vertex.x * scale + offsetSpeed * time, vertex.y * scale +
                offsetSpeed * time);

                // 4 Finally, you apply the value of the current vertex within the vertices.
                vertices[i] =
                new Vector3(vertex.x, vertex.y, noiseValue * height + 0.3f);
            }

        }
    }



}