using UnityEngine;

public class LightData : MonoBehaviour
{
    [SerializeField] internal GameObject[] volLights;
    [SerializeField] internal GameObject[] coneLights;

    private Renderer[] volRenderers;
    private Renderer[] coneRenderers;

    private static readonly int strengthID = Shader.PropertyToID("_Strengt");
    private static readonly int colorGradientID = Shader.PropertyToID("_ColorGradient");
    private static readonly int opacityID = Shader.PropertyToID("_opacity");
    private static readonly int transperencyOverlay = Shader.PropertyToID("_transperentsy");
    private static readonly int gradientOverlay = Shader.PropertyToID("_ColorGradient");

    void Start()
    {
        volLights = GameObject.FindGameObjectsWithTag("Light");
        coneLights = GameObject.FindGameObjectsWithTag("ConeLight");

        volRenderers = new Renderer[volLights.Length];
        coneRenderers = new Renderer[coneLights.Length];

        for (int i = 0; i < volLights.Length; i++)
            volRenderers[i] = volLights[i].GetComponent<Renderer>();

        for (int i = 0; i < coneLights.Length; i++)
            coneRenderers[i] = coneLights[i].GetComponent<Renderer>();
    }

    public void ApplyValues(float strength, float transparency, float gradient, float overlay, float gradientOverlays)
    {
        // Clamp once for all parameters to ensure they stay within expected ranges
        strength = Mathf.Clamp(strength, 0.0032f, 0.02f);
        transparency = Mathf.Clamp(transparency, 0.1f, 0.5f);

        foreach (var r in coneRenderers)
        {
            if (r == null || r.materials.Length == 0) continue;

            Material mat = r.materials[0];

            mat.SetFloat(strengthID, strength);
            mat.SetFloat(transperencyOverlay, overlay);
            mat.SetFloat(gradientOverlay, gradientOverlays);
        }

        foreach (var r in volRenderers)
        {
            if (r == null || r.materials.Length == 0) continue;

            Material mat = r.materials[0];

            mat.SetFloat(colorGradientID, gradient);
            mat.SetFloat(opacityID, transparency);
        }
    }
}
