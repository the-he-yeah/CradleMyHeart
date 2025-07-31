using UnityEngine;
using UnityEngine.Playables;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayableDirector enemyTimeline;
    [SerializeField] private GameObject enemyObject;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UDPReceiver heartbeatReceiver;

    [Header("Trigger Zones")]
    [SerializeField] private GameObject triggerZoneObject; // Zone where spawning can occur

    [Header("Control Settings")]
    [SerializeField] private bool ignoreHeartRate = false; // When enabled, always spawn when player enters trigger zone
    [SerializeField] private bool showDebugLogs = true; // Toggle for debug logs

    [Header("Heartrate Settings")]
    [SerializeField] private int eventTriggerRange = 5; // Offset from baseline for triggering events
    [SerializeField] private int runningAvgCount = 10; // Number of samples to average
    [SerializeField] private float heartRateCheckInterval = 0.5f; // How often to check heartrate
    [SerializeField] private float targetPercentageOfBaseline = 0f; // Percentage increase/decrease from baseline (e.g., 0.1 = +10%)
    [SerializeField] private float heartRateLoggingInterval = 2.0f;
    [SerializeField] private bool isMainHeartRateLogger = false;

    [Header("Wwise States")]
    [SerializeField] private AK.Wwise.State relaxState; // State for when heart rate is above target
    [SerializeField] private AK.Wwise.State moderateState; // State for when heart rate is at target
    [SerializeField] private AK.Wwise.State exciteState; // State for when heart rate is below target
    [SerializeField] private AK.Wwise.State noState; // State for when in menu

    [Header("Respawn")]
    [SerializeField] private float respawnCooldownValue = 30f; // Time to wait before allowing respawn

    private bool enemyIsActive = false;
    private static float respawnTimer = 0f; // Static to be shared across instances
    private static float respawnCooldown = 30f; // Static to be shared across instances
    private static bool isInCooldown = false; // Static to be shared across instances
    private static bool isTimerRunning = false; // Static to be shared across instances
    private static bool hasTriggeredTimeline = false; // Static to be shared across instances

    // State management
    private enum HeartRateState { Relax, Moderate, Excite, None }
    private HeartRateState currentState = HeartRateState.Moderate;
    private HeartRateState previousStateBeforePause = HeartRateState.Moderate;

    // Runtime variables
    private bool isPlayerInTriggerZone = false;
    private float targetHeartRate = 0;
    private Queue<int> heartRateSamples = new Queue<int>();
    private float runningHeartRateAverage = 0f;
    private float heartRateLogTimer = 0f;
    private bool heartRateMonitoringStarted = false;

    private void Start()
    {
        // Validate components
        ValidateComponents();
        
        // Set up zone identifiers on existing zone objects
        SetupZoneIdentifiers();
        
        // Set the global respawn cooldown value from this instance
        respawnCooldown = respawnCooldownValue;

        if (!isTimerRunning)
        {
            isInCooldown = true;
            respawnTimer = 0f;
            isTimerRunning = true;
            
            if (showDebugLogs)
            {
                Debug.Log("Starting with cooldown active - will be ready to spawn after initial cooldown");
            }
        }
        else if (showDebugLogs)
        {
            Debug.Log($"Using existing cooldown timer: {respawnTimer:F1}/{respawnCooldown:F1} seconds");
        }

        // Check if game is in menu
        bool isInMenu = gameManager != null && !gameManager.IsGameActive();

        // Handle menu state first, regardless of experimental/control mode
        if (isInMenu)
        {
            SetAudioState(HeartRateState.None);
            currentState = HeartRateState.None;
            previousStateBeforePause = HeartRateState.Moderate; // Set default state for when game starts
            
            if (showDebugLogs)
            {
                Debug.Log("Game in menu - Setting audio state to NONE");
            }
        }
        else
        {
            // If game is already active at start, pre-fill the queue
            PreFillHeartRateQueue();

            // Configure for the active mode when not in menu
            if (ignoreHeartRate)
            {
                if (showDebugLogs)
                {
                    Debug.Log("====== CONTROL MODE ACTIVE ======");
                    Debug.Log("Timeline will play when player enters trigger zone, ignoring heart rate");
                }
                
                // Make sure moderate state is active for control condition
                currentState = HeartRateState.Moderate;
                SetAudioState(HeartRateState.Moderate);
            }
            else
            {
                if (showDebugLogs)
                {
                    Debug.Log("====== EXPERIMENTAL MODE ACTIVE ======");
                    Debug.Log("Timeline will play based on heart rate conditions");
                }
                
                // Use moderate state as default for experimental mode too
                currentState = HeartRateState.Moderate;
                SetAudioState(HeartRateState.Moderate);
            }
            
            // Start heartrate monitoring
            StartCoroutine(MonitorHeartRate());
            heartRateMonitoringStarted = true; // Mark monitoring as started
            if (showDebugLogs)
            {
                Debug.Log("MonitorHeartRate coroutine initialized in Start()");
            }
        }
    }

    private void Update()
    {
        // Only update the timer if it's running
        if (isTimerRunning)
        {
            respawnTimer += Time.deltaTime;
            
            // Check if cooldown is complete
            if (respawnTimer >= respawnCooldown)
            {
                // Cooldown complete - ready for next spawn
                isInCooldown = false;
                isTimerRunning = false;
                hasTriggeredTimeline = false;
                
                // In control mode, return to moderate state when cooldown is complete
                if (ignoreHeartRate)
                {
                    SetAudioState(HeartRateState.Moderate);
                    currentState = HeartRateState.Moderate;
                    
                    if (showDebugLogs)
                    {
                        Debug.Log("CONTROL MODE: Cooldown complete - returning to MODERATE state");
                    }
                }
                
                if (showDebugLogs)
                {
                    Debug.Log("Respawn cooldown complete - ready to spawn again");
                }
            }
        }
    }

    // Method to be called by the GameManager when game becomes active
    public void OnGameBecameActive()
    {
        // Pre-fill heart rate queue now that game is active
        PreFillHeartRateQueue();
        
        // Set the appropriate audio state based on the mode
        if (ignoreHeartRate)
        {
            // In control mode, use moderate state
            currentState = HeartRateState.Moderate;
            SetAudioState(HeartRateState.Moderate);
            
            if (showDebugLogs)
            {
                Debug.Log("Game became active (Control Mode) - Setting audio state to MODERATE");
            }
        }
        else
        {
            // In experimental mode, update state based on heart rate
            UpdateHeartRateState();
            
            if (showDebugLogs)
            {
                Debug.Log("Game became active (Experimental Mode) - Updating heart rate state");
            }
        }
        
        // Start heart rate monitoring if not already started
        if (!heartRateMonitoringStarted)
        {
            if (showDebugLogs)
            {
                Debug.Log("Starting MonitorHeartRate coroutine...");
            }
            
            StartCoroutine(MonitorHeartRate());
            heartRateMonitoringStarted = true;
            
            if (showDebugLogs)
            {
                Debug.Log("MonitorHeartRate coroutine initialized successfully");
            }
        }
        else if (showDebugLogs)
        {
            Debug.Log("MonitorHeartRate coroutine already running");
        }
        
        if (showDebugLogs)
        {
            Debug.Log("Game became active - Pre-filled heart rate queue and initialized monitoring");
        }
    }

    private void PreFillHeartRateQueue()
    {
        // Clear any existing samples
        heartRateSamples.Clear();
        
        // Get baseline heart rate from game manager, or use default
        float baselineHR = 75f; // Default value if nothing else is available
        
        if (gameManager != null)
        {
            float managerBaseline = gameManager.BaselineHeartbeat;
            if (managerBaseline > 0)
            {
                baselineHR = managerBaseline;
            }
        }
        
        // Pre-fill the queue with the baseline value
        for (int i = 0; i < runningAvgCount; i++)
        {
            heartRateSamples.Enqueue((int)baselineHR);
        }
        
        // Calculate initial running average
        UpdateHeartRateAverage(false); // false = don't add a new sample, just recalculate
        
        if (showDebugLogs)
        {
            Debug.Log($"Pre-filled heart rate queue with {runningAvgCount} samples of {baselineHR}");
        }
    }

    private void ValidateComponents()
    {
        bool hasErrors = false;
        
        if (enemyTimeline == null)
        {
            Debug.LogError("EnemySpawner: Timeline reference is missing!");
            hasErrors = true;
        }
        
        if (enemyObject == null)
        {
            Debug.LogError("EnemySpawner: Enemy object reference is missing!");
            hasErrors = true;
        }
        
        if (triggerZoneObject == null)
        {
            Debug.LogError("EnemySpawner: Trigger Zone object reference is missing!");
            hasErrors = true;
        }
        else if (triggerZoneObject.GetComponent<Collider>() == null)
        {
            Debug.LogError("EnemySpawner: Trigger Zone object must have a Collider component!");
            hasErrors = true;
        }
        
        if (gameManager == null)
        {
            Debug.LogWarning("EnemySpawner: GameManager reference is missing! Baseline heart rate will use default value.");
        }
        
        if (hasErrors)
        {
            Debug.LogError("EnemySpawner: Critical components missing, disabling script!");
            enabled = false;
        }
    }

    private void SetupZoneIdentifiers()
    {
        if (triggerZoneObject != null && triggerZoneObject.GetComponent<ZoneIdentifier>() == null)
        {
            ZoneIdentifier identifier = triggerZoneObject.AddComponent<ZoneIdentifier>();
            identifier.zoneType = ZoneType.Trigger;
            
            // Make sure collider is a trigger
            Collider collider = triggerZoneObject.GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                collider.isTrigger = true;
                Debug.Log("Set Trigger Zone collider to be a trigger");
            }
        }
    }

    private IEnumerator MonitorHeartRate()
    {
        while (true)
        {
            // Monitor heart rate at all times, regardless of player position
            UpdateHeartRateAverage();
            UpdateTargetHeartRate();
            
            // Only log heart rate if this is the designated logger and interval > 0
            if (isMainHeartRateLogger && heartRateLoggingInterval > 0)
            {
                heartRateLogTimer += heartRateCheckInterval;
                if (heartRateLogTimer >= heartRateLoggingInterval && TelemetryManager.Instance != null)
                {
                    TelemetryManager.Instance.LogEvent("runningHeartRate", runningHeartRateAverage.ToString("F1"));
                    heartRateLogTimer = 0f;
                }
            }
            
            // Only update heart rate state when not in menu
            if (gameManager != null && gameManager.IsGameActive())
            {
                if (!ignoreHeartRate)
                {
                    UpdateHeartRateState();
                }
                
                // Regularly check for timeline trigger conditions if player is in trigger zone
                if (isPlayerInTriggerZone && !hasTriggeredTimeline && !isInCooldown)
                {
                    CheckForTimelineTrigger();
                }
            }
            
            yield return new WaitForSeconds(heartRateCheckInterval);
        }
    }

    private void UpdateHeartRateAverage(bool addNewSample = true)
    {
        if (UDPReceiver.Instance == null && addNewSample) return;
        
        // Add the current heartbeat to the samples if requested
        if (addNewSample)
        {
            int currentReading = UDPReceiver.Instance.Heartbeat;
            
            // Add to the queue
            heartRateSamples.Enqueue(currentReading);
            
            // Remove old samples if we have too many
            while (heartRateSamples.Count > runningAvgCount)
            {
                heartRateSamples.Dequeue();
            }
        }
        
        // Calculate the average
        float sum = 0;
        foreach (int sample in heartRateSamples)
        {
            sum += sample;
        }
        
        // Store the running average in our variable
        runningHeartRateAverage = heartRateSamples.Count > 0 ? sum / heartRateSamples.Count : 0;
    }

    private void UpdateTargetHeartRate()
    {
        // Get the baseline heart rate from the game manager
        float baselineHeartRate = gameManager != null ? gameManager.BaselineHeartbeat : 0;
        
        // If we have a valid baseline, calculate target
        if (baselineHeartRate > 0)
        {
            // Calculate target as baseline + (baseline * percentage)
            targetHeartRate = baselineHeartRate * (1f + targetPercentageOfBaseline);
        }
        else
        {
            // Fallback if no calibration was done
            targetHeartRate = 75f; // Default baseline
        }
    }

    private void UpdateHeartRateState()
    {
        // Skip heart rate state updates if in control mode - use timeline events instead
        if (ignoreHeartRate)
        {
            return;
        }
        
        // Define the acceptable range for "moderate" state
        float lowerThreshold = targetHeartRate - eventTriggerRange;
        float upperThreshold = targetHeartRate + eventTriggerRange;
        
        HeartRateState previousState = currentState;
        
        // Determine new state based on heart rate relative to target
        if (runningHeartRateAverage < lowerThreshold)
        {
            currentState = HeartRateState.Excite; // Below target = Excite
        }
        else if (runningHeartRateAverage > upperThreshold)
        {
            currentState = HeartRateState.Relax; // Above target = Relax
        }
        else
        {
            currentState = HeartRateState.Moderate; // Within target = Moderate
        }
        
        // Only update if state has changed
        if (previousState != currentState)
        {
            SetAudioState(currentState);
        }
    }

    private void SetAudioState(HeartRateState state)
    {
        switch (state)
        {
            case HeartRateState.Relax:
                if (relaxState != null && relaxState.IsValid())
                {
                    relaxState.SetValue();
                    
                    // Log telemetry event for music state change
                    if (TelemetryManager.Instance != null)
                    {
                        TelemetryManager.Instance.LogEvent("musicState", "relax");
                    }
                }
                break;
                
            case HeartRateState.Moderate:
                if (moderateState != null && moderateState.IsValid())
                {
                    moderateState.SetValue();
                    
                    // Log telemetry event for music state change
                    if (TelemetryManager.Instance != null)
                    {
                        TelemetryManager.Instance.LogEvent("musicState", "moderate");
                    }
                }
                break;
                
            case HeartRateState.Excite:
                if (exciteState != null && exciteState.IsValid())
                {
                    exciteState.SetValue();
                    
                    // Log telemetry event for music state change
                    if (TelemetryManager.Instance != null)
                    {
                        TelemetryManager.Instance.LogEvent("musicState", "excite");
                    }
                }
                break;
                
            case HeartRateState.None:
                if (noState != null && noState.IsValid())
                {
                    noState.SetValue();
                    
                    // Log telemetry event for music state change
                    if (TelemetryManager.Instance != null)
                    {
                        TelemetryManager.Instance.LogEvent("musicState", "none");
                    }
                }
                break;
        }
    }

    private void PlayTimeline()
    {
        if (hasTriggeredTimeline || isInCooldown)
        {
            if (showDebugLogs && isInCooldown)
            {
                float remainingTime = respawnCooldown - respawnTimer;
                Debug.Log($"Cannot play timeline - in cooldown ({remainingTime:F1} seconds remaining)");
            }
            return;
        }
            
        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.LogEvent("jumpscare", "jumpscareTriggered");
        }
        
        hasTriggeredTimeline = true;
        enemyIsActive = true;
        
        // For control mode, change state to excite when playing timeline
        if (ignoreHeartRate)
        {
            SetAudioState(HeartRateState.Excite);
        }
        
        // Play the timeline
        if (enemyTimeline != null)
        {
            enemyTimeline.time = 0;
            enemyTimeline.Play();
        }
        
        // Make sure the enemy is active for the timeline
        if (enemyObject != null)
        {
            // Enable the enemy
            enemyObject.SetActive(true);
            
            // Reset the enemy AI state
            EnemyAI enemyAI = enemyObject.GetComponent<EnemyAI>();
            if (enemyAI != null)
            {
                enemyAI.ResetState();
            }
        }
    }

    public void DeactivateEnemy()
    {
        if (enemyObject != null)
        {
            enemyObject.SetActive(false);
        }
        
        // Start the cooldown
        isInCooldown = true;
        respawnTimer = 0f;
        isTimerRunning = true;
        
        if (showDebugLogs)
        {
            Debug.Log("Enemy deactivated - starting cooldown timer");
        }
        
        // For control mode, change state to relax during cooldown
        if (ignoreHeartRate)
        {
            SetAudioState(HeartRateState.Relax);
        }
    }

    public void NotifyEnemyDestroyed()
    {
        enemyIsActive = false;
        hasTriggeredTimeline = false;
        
        // Start cooldown when enemy is destroyed
        isInCooldown = true;
        respawnTimer = 0f;
        isTimerRunning = true;
        
        if (showDebugLogs)
        {
            Debug.Log("Enemy destroyed - starting cooldown timer");
        }
    }

    // This would typically be called from a game manager when resetting the level
    public static void ResetAllSpawners()
    {
        // Reset static cooldown state to allow spawning on all instances
        isInCooldown = false;
        isTimerRunning = false;
        respawnTimer = 0f;
        
        Debug.Log("All spawners reset - ready for immediate spawn");
    }
    
    public void ResetSpawner()
    {
        hasTriggeredTimeline = false;
        enemyIsActive = false;
        
        // Reset cooldown state to allow spawning
        isInCooldown = false;
        isTimerRunning = false;
        
        if (showDebugLogs)
        {
            Debug.Log("Spawner reset - ready for immediate spawn");
        }
    }

    // Method called by zone identifiers when player enters
    public void OnPlayerEnteredZone(ZoneType zoneType)
    {
        if (zoneType == ZoneType.Trigger)
        {
            isPlayerInTriggerZone = true;
            
            // Log telemetry event
            if (TelemetryManager.Instance != null)
            {
                TelemetryManager.Instance.LogEvent("zoneEntered", "trigger");
            }

            // Add detailed heart rate range debug logs
            if (showDebugLogs)
            {
                float lowerThreshold = targetHeartRate - eventTriggerRange;
                float upperThreshold = targetHeartRate + eventTriggerRange;
                bool isInTargetRange = runningHeartRateAverage >= lowerThreshold && 
                                    runningHeartRateAverage <= upperThreshold;
                                    
                string rangeStatus = isInTargetRange ? "IN TARGET RANGE" : "OUTSIDE TARGET RANGE";
                
                Debug.Log($"[TRIGGER ZONE] Player entered trigger zone. Heart Rate Status: {rangeStatus}");
                Debug.Log($"[TRIGGER ZONE] Current HR: {runningHeartRateAverage:F1} | Target Range: {lowerThreshold:F1} - {upperThreshold:F1}");
                Debug.Log($"[TRIGGER ZONE] Base Target HR: {targetHeartRate:F1} | Range: ±{eventTriggerRange}");
                Debug.Log($"[TRIGGER ZONE] Control Mode (ignore heart rate): {ignoreHeartRate}");
                
                if (isInCooldown)
                {
                    Debug.Log($"[TRIGGER ZONE] Spawner in cooldown: {respawnTimer:F1}/{respawnCooldown:F1} seconds remaining");
                }
            }
            
            // Check if we should play the timeline
            CheckForTimelineTrigger();
        }
    }

    // Method called by zone identifiers when player exits
    public void OnPlayerExitedZone(ZoneType zoneType)
    {
        if (zoneType == ZoneType.Trigger)
        {
            isPlayerInTriggerZone = false;
            
            // Log telemetry event
            if (TelemetryManager.Instance != null)
            {
                TelemetryManager.Instance.LogEvent("zoneExited", "trigger");
            }
        }
    }

    private void CheckForTimelineTrigger()
    {
        // First check if in cooldown - this is the primary determining factor
        if (isInCooldown)
        {
            if (showDebugLogs)
            {
                float remainingTime = respawnCooldown - respawnTimer;
                Debug.Log($"[TRIGGER ZONE] Spawner in cooldown: {remainingTime:F1} seconds remaining");
            }
            return;
        }
        
        // Check if already triggered
        if (hasTriggeredTimeline)
        {
            if (showDebugLogs)
            {
                Debug.Log("[TRIGGER ZONE] Timeline already triggered");
            }
            return;
        }
            
        // In control mode, always play timeline when player enters trigger zone
        if (ignoreHeartRate)
        {
            PlayTimeline();
            return;
        }
        
        // Define the acceptable range for "moderate" state
        float lowerThreshold = targetHeartRate - eventTriggerRange;
        float upperThreshold = targetHeartRate + eventTriggerRange;
        
        bool isInTargetRange = runningHeartRateAverage >= lowerThreshold &&
                               runningHeartRateAverage <= upperThreshold;
                               
        if (isInTargetRange)
        {
            PlayTimeline();
        }
        else if (showDebugLogs)
        {
            Debug.Log($"[TRIGGER ZONE] Heart rate not in target range. Current: {runningHeartRateAverage:F1}, Target range: {lowerThreshold:F1}-{upperThreshold:F1}");
        }
    }

    public void SetPlayerInMenu(bool inMenu)
    {
        if (inMenu)
        {
            // Store the current state before switching to None
            previousStateBeforePause = currentState;
            
            // Set the "none" state when player is in menu
            SetAudioState(HeartRateState.None);
            currentState = HeartRateState.None; // Make sure to update current state to None
        }
        else
        {
            // When returning from menu, restore the appropriate state
            if (ignoreHeartRate)
            {
                // In control mode, always restore to Moderate when resuming
                currentState = HeartRateState.Moderate;
                SetAudioState(HeartRateState.Moderate);
            }
            else
            {
                // In experimental mode, restore previous state
                currentState = previousStateBeforePause;
                SetAudioState(previousStateBeforePause);
            }
        }
    }

    // Public method to toggle control condition at runtime
    public void SetControlCondition(bool isControlMode)
    {
        ignoreHeartRate = isControlMode;
        
        // Reset to the appropriate state when switching modes
        if (isControlMode)
        {
            currentState = HeartRateState.Moderate;
            SetAudioState(HeartRateState.Moderate);
        }
        else
        {
            // When switching back to experimental, update state based on heart rate
            UpdateHeartRateState();
        }
    }

    // Define zone types
    public enum ZoneType { Trigger }
}

// Helper component to identify zones
public class ZoneIdentifier : MonoBehaviour
{
    public EnemySpawner.ZoneType zoneType;
    private EnemySpawner parent;
    
    private void Start()
    {
        // Get reference to parent EnemySpawner
        parent = GetComponentInParent<EnemySpawner>();
        if (parent == null)
        {
            Debug.LogError($"ZoneIdentifier on {gameObject.name} couldn't find parent EnemySpawner");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && parent != null)
        {
            parent.OnPlayerEnteredZone(zoneType);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && parent != null)
        {
            parent.OnPlayerExitedZone(zoneType);
        }
    }
}
