using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class LightningFlash : MonoBehaviour
{
    [Tooltip("Reference to the light that will flash")]
    public Light lightningLight;
    
    [Tooltip("Minimum time between flickers")]
    public float minFlickerTime = 0.1f;
    
    [Tooltip("Maximum time between flickers")]
    public float maxFlickerTime = 0.4f;
    
    [Tooltip("Minimum intensity multiplier (as percentage of original)")]
    [Range(1f, 100f)]
    public float minIntensityMultiplier = 1f;
    
    [Tooltip("Maximum intensity multiplier (as percentage of original)")]
    [Range(1f, 100f)]
    public float maxIntensityMultiplier = 8f;
    
    [Tooltip("Number of flashes to perform")]
    [Range(1, 10)]
    public int numberOfFlashes = 3;
    
    private float originalIntensity;
    private Coroutine flashRoutine;
    
    // Unity Event that can be invoked from a Timeline Signal Receiver
    public UnityEvent onLightningTriggered;
    
    private void Awake()
    {
        // Store the original intensity on startup
        if (lightningLight == null)
            lightningLight = GetComponent<Light>();
            
        if (lightningLight != null)
            originalIntensity = lightningLight.intensity;
            
        // Initialize the event if needed
        if (onLightningTriggered == null)
            onLightningTriggered = new UnityEvent();
    }
    
    // This method can be called from a Timeline Signal Receiver
    public void TriggerLightningFlash()
    {
        // If there's already a flash happening, stop it
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
            
        // Start a new flash routine
        flashRoutine = StartCoroutine(FlashRoutine());
        
        // Invoke the event for any listeners
        onLightningTriggered.Invoke();
    }
    
    private IEnumerator FlashRoutine()
    {
        // Store the current intensity in case it was changed since startup
        originalIntensity = lightningLight.intensity;
        
        // Perform the specified number of flashes
        for (int i = 0; i < numberOfFlashes; i++)
        {
            // Increase to peak intensity (random within range)
            float peakMultiplier = Random.Range(minIntensityMultiplier, maxIntensityMultiplier);
            lightningLight.intensity = originalIntensity * peakMultiplier;
            
            // Hold at peak for a short random time
            yield return new WaitForSeconds(Random.Range(minFlickerTime * 0.5f, maxFlickerTime * 0.5f));
            
            // Return to original intensity
            lightningLight.intensity = originalIntensity;
            
            // Wait before next flash if this isn't the last one
            if (i < numberOfFlashes - 1)
                yield return new WaitForSeconds(Random.Range(minFlickerTime, maxFlickerTime));
        }
        
        // Ensure we end with the original intensity
        lightningLight.intensity = originalIntensity;
        flashRoutine = null;
    }
}
