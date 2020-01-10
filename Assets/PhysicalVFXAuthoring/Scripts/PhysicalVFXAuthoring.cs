using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PhysicalVFXAuthoring : MonoBehaviour
{

    [Header("General Settings")]

    [SerializeField]
    ParticleSystem affectedParticleSystem;

    [Tooltip("Recorder nodes to track movement. These are probaby your fingertips of the dominant hand")]
    [SerializeField]
    Transform[] nodes;

    [Tooltip("How long does the tool record data. This is the final length of the particle system.")]
    [SerializeField]
    float recordingLength = 5f;

    [Tooltip("Preroll time before the tool start the recording process. You will be warned in the console.")]
    [SerializeField]
    int preRollSeconds = 4;

    [Tooltip("Time interval between frames where positional data is recorded. You shouldn't really change this.")]
    [SerializeField]
    float recorderTimeInterval = 0.2f;

    [Tooltip("The node index that informs scale of the particle. On a finger, 2 is most likely the middle finger.")]
    [SerializeField]
    int sizeNodeIndex = 2;

    [Tooltip("How many slices of the recorded data will be used to generate the particle system curves.")]
    [SerializeField]
    int interpolationSlices = 3;

    [Tooltip("Simulation scale applied to the various forces")]
    [SerializeField]
    float simulationScale = 10f;


    [Space(10)]
    [Header("Recorder Settings")]

    [SerializeField]
    bool velocityOverLifetime = true;
    [SerializeField]
    bool sizeOverLifetime = true;
    [Tooltip("Size Scale")]
    [SerializeField]
    float sizeOverLifetimeScale = 1f;
    [SerializeField]
    bool noiseStrength = true;
    [SerializeField]
    float noiseScale = 1f;

    // Internal Data 

    public List<Vector3> averagePositions = new List<Vector3>();
    public List<Vector3> averageVelocities = new List<Vector3>();
    public List<float> averageExpansions = new List<float>();

    void Start()
    {

        var mainModule = affectedParticleSystem.main;
        mainModule.startLifetime = recordingLength;

        InvokeRepeating("PreRoll", 0.5f, 1f);
    }

    void PreRoll()
    {
        preRollSeconds--;
        Debug.Log(preRollSeconds + "!");
        if (preRollSeconds == 0)
        {
            StartRecording();
        }
    }

    void StartRecording()
    {
        CancelInvoke("PreRoll");
        Debug.Log("Started Recording Physical VFX Authoring Data!");
        InvokeRepeating("RecordFrame", 0f, recorderTimeInterval);
    }

    void StopRecording()
    {
        CancelInvoke("RecordFrame");
        Debug.Log("Stopped Recording Physical VFX Authoring Data!");

        ComputeData();
    }

    void RecordFrame()
    {
        recordingLength -= recorderTimeInterval;

        // First we compute base node positions
        Vector3[] nodePositions = new Vector3[nodes.Length];
        for (int i = 0; i < nodes.Length; i++)
        {
            nodePositions[i] = nodes[i].position;
        }

        // Average position
        Vector3 averageNodePosition = Vector3.zero;
        for (int i = 0; i < nodePositions.Length; i++)
        {
            averageNodePosition += nodePositions[i];
        }
        averagePositions.Add(averageNodePosition / (nodes.Length - 1));

        // Average velocity
        if (averageVelocities.Count != 0)
        {
            averageVelocities.Add(averagePositions[averagePositions.Count - 1] - averagePositions[averagePositions.Count - 2]);
        }
        else
        {
            averageVelocities.Add(Vector3.zero);
        }

        // average size
        averageExpansions.Add(Vector3.Distance(nodePositions[0], nodePositions[sizeNodeIndex]));

        if (recordingLength <= 0f)
        {
            StopRecording();
        }
    }

    // This is where we actually generate the particle system from the recorded averages.
    void ComputeData()
    {

        // Here are the various curves that can be affected.
        AnimationCurve volCurveX = new AnimationCurve();
        AnimationCurve volCurveY = new AnimationCurve();
        AnimationCurve volCurveZ = new AnimationCurve();
        AnimationCurve sizeCurve = new AnimationCurve();
        AnimationCurve noiseCurve = new AnimationCurve();


        var velociyLifetime = affectedParticleSystem.velocityOverLifetime;
        var sizeLifeTime = affectedParticleSystem.sizeOverLifetime;
        var noiseModule = affectedParticleSystem.noise;

        if (velocityOverLifetime)
        {
            velociyLifetime.enabled = true;
        }

        if (sizeOverLifetime)
        {
            sizeLifeTime.enabled = true;
        }

        if (noiseStrength)
        {
            noiseModule.enabled = true;
            noiseModule.frequency = 12f;
        }

        // As per unity documentation, particle system modules are interfaces and do not need to be reassigned.

        for (int i = 0; i < interpolationSlices; i++)
        {

            // Depending on the interpolationSlices, we will sample a number of frames that will be used to build the curves of the particle system.
            int sampledFrame = Mathf.Clamp(averagePositions.Count / interpolationSlices * i, 1, averagePositions.Count - 1);
            float relativePosition = Mathf.Clamp((1f / interpolationSlices) * i, 0f, 1f);

            if (velocityOverLifetime)
            {
                volCurveX.AddKey(relativePosition, averageVelocities[sampledFrame].x * simulationScale);
                volCurveY.AddKey(relativePosition, averageVelocities[sampledFrame].y * simulationScale);
                volCurveZ.AddKey(relativePosition, averageVelocities[sampledFrame].z * simulationScale);
            }

            if (sizeOverLifetime)
            {
                sizeCurve.AddKey(relativePosition, averageExpansions[sampledFrame] * sizeOverLifetimeScale);
            }

            if (noiseStrength)
            {
                Vector3 noiseStrengthValue = averagePositions[(Mathf.Clamp(sampledFrame + 1, 0, averagePositions.Count - 1)) - Mathf.Clamp(sampledFrame - 1, 0, averagePositions.Count - 1)];
                noiseCurve.AddKey(relativePosition, (noiseStrengthValue.y + noiseStrengthValue.x + noiseStrengthValue.z) * noiseScale);
            }

        }

        if (velocityOverLifetime)
        {
            // We assign the edited curves once done
            velociyLifetime.x = new ParticleSystem.MinMaxCurve(1f, volCurveX);
            velociyLifetime.y = new ParticleSystem.MinMaxCurve(1f, volCurveY);
            velociyLifetime.z = new ParticleSystem.MinMaxCurve(1f, volCurveZ);
        }

        if (sizeOverLifetime)
        {
            sizeLifeTime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        }

        if (noiseStrength)
        {
            noiseModule.strength = new ParticleSystem.MinMaxCurve(1f, noiseCurve);
        }

    }

}