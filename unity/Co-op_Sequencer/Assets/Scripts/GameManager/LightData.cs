using UnityEngine;

public class LightData : MonoBehaviour
{
    // Arrays holding all volume and cone light objects in the scene
    [SerializeField] internal GameObject[] volLights;
    [SerializeField] internal GameObject[] coneLights;

    // Cached renderer references for performance (avoid repeated GetComponent calls)
    private Renderer[] volRenderers;
    private Renderer[] coneRenderers;

    // Shader property IDs (faster than using string property names every frame)
    private static readonly int strengthID = Shader.PropertyToID("_Strengt");
    private static readonly int colorGradientID = Shader.PropertyToID("_ColorGradient");
    private static readonly int opacityID = Shader.PropertyToID("_opacity");
    private static readonly int transperencyOverlay = Shader.PropertyToID("_transperentsy");
    private static readonly int gradientOverlay = Shader.PropertyToID("_ColorGradient");

    void Start()
    {
        // Find all light objects by tag at scene start
        volLights = GameObject.FindGameObjectsWithTag("Light");
        coneLights = GameObject.FindGameObjectsWithTag("ConeLight");

        // Initialize renderer arrays to match object arrays
        volRenderers = new Renderer[volLights.Length];
        coneRenderers = new Renderer[coneLights.Length];

        // Cache Renderer components for volume lights
        for (int i = 0; i < volLights.Length; i++)
            volRenderers[i] = volLights[i].GetComponent<Renderer>();

        // Cache Renderer components for cone lights
        for (int i = 0; i < coneLights.Length; i++)
            coneRenderers[i] = coneLights[i].GetComponent<Renderer>();
    }

    // Applies runtime shader parameter values to all lights
    public void ApplyValues(float strength, float transparency, float gradient, float overlay, float gradientOverlays)
    {
        // Apply settings to cone lights
        foreach (var r in coneRenderers)
        {
            if (r == null) continue;

            // Set light strength on material shader
            r.material.SetFloat(strengthID, strength);

            // Clamp strength to avoid extreme values
            strength = Mathf.Clamp(strength, 0.0032f, 0.02f);

            // Apply overlay intensity values
            r.material.SetFloat(transperencyOverlay, overlay);
            r.material.SetFloat(gradientOverlay, gradientOverlays);

        }

        // Apply settings to volume lights
        foreach (var r in volRenderers)
        {
            if (r == null) continue;

            // Update shader gradient value
            r.material.SetFloat(colorGradientID, gradient);

            // Update transparency/opacity
            r.material.SetFloat(opacityID, transparency);

            // Clamp transparency to keep visuals stable
            transparency = Mathf.Clamp(transparency, 0.1f, 0.5f);
        }
    }
}
