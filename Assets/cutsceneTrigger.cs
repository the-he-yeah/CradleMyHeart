using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;
using StarterAssets;

public class cutsceneTrigger : MonoBehaviour
{
    [Header("Timeline")]
    [SerializeField] private PlayableDirector endGameTimeline;
    
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineCamera boatCamera;

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private FirstPersonController playerController; // Your player controller script

    private bool hasTriggered = false;

    private void Start()
    {
        // Ensure boat camera starts disabled
        if (boatCamera != null)
        {
            boatCamera.Priority = 0;
        }
        
        // Ensure player camera starts as main camera
        if (playerCamera != null)
        {
            playerCamera.Priority = 10;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if it's the player and hasn't been triggered yet
        if (other.CompareTag("Player") && !hasTriggered)
        {
            StartEndGameSequence();
        }
    }

    private void StartEndGameSequence()
    {
        hasTriggered = true;

        // Disable player movement
        if (playerController != null)
        {
            playerController.enabled = false;
            playerController.gameObject.SetActive(false);
        }

        // Switch cameras
        if (playerCamera != null && boatCamera != null)
        {
            playerCamera.Priority = 0;
            boatCamera.Priority = 10;
        }

        // Start the timeline
        if (endGameTimeline != null)
        {
            endGameTimeline.Play();
            endGameTimeline.stopped += OnTimelineComplete;
        }

        // Log telemetry event
        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.LogEvent("session", "win");
        }

    }

    private void OnTimelineComplete(PlayableDirector director)
    {
        // Clean up the event subscription
        endGameTimeline.stopped -= OnTimelineComplete;

        // Show win screen
        if (gameManager != null)
        {
            gameManager.ShowGameWin();
        }
    }

    private void OnDisable()
    {
        // Clean up in case the object is disabled before timeline completes
        if (endGameTimeline != null)
        {
            endGameTimeline.stopped -= OnTimelineComplete;
        }
    }
}