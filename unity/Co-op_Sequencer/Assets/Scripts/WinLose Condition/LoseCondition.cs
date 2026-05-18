using System.Collections;
using UnityEngine;

public class LoseCondition : MonoBehaviour
{
    [SerializeField] private AudioClip scratchPad;
    [SerializeField] private GameObject[] lights;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        GameObject[] coneLights = GameObject.FindGameObjectsWithTag("ConeLight");
        GameObject[] volLights = GameObject.FindGameObjectsWithTag("Light");

        GameObject[] allLights = new GameObject[lights.Length + coneLights.Length + volLights.Length];

        lights.CopyTo(allLights, 0);
        coneLights.CopyTo(allLights, lights.Length);
        volLights.CopyTo(allLights, lights.Length + coneLights.Length);

        lights = allLights;
    }

    public void TriggerLoseCondition()
    {
        audioSource.PlayOneShot(scratchPad);
        StartCoroutine(FlashLights());
    }

    IEnumerator FlashLights()
    {
        while (true)
        {
            foreach (var light in lights)
            {
                if (light != null)
                    light.SetActive(false);
            }
            yield return new WaitForSeconds(0.2f);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            TriggerLoseCondition();
        }
    }
}
