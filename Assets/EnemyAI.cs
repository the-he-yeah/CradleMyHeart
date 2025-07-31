using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using StarterAssets;
using AK.Wwise;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    private enum EnemyState
    {
        Idle = 0,
        Walk = 1,
        Chase = 2,
        Attack = 3
    }
    

    [Header("Components")]
    [SerializeField] private Transform eyePosition; // Reference to the Eye GameObject


    [Header("Detection Settings")]
    [SerializeField] private float immediateDetectionRange = 2f;
    [SerializeField] private float peripheralDetectionRange = 8f;
    [SerializeField] private float mainDetectionRange = 15f;
    [SerializeField] private float mainDetectionAngle = 60f;
    [SerializeField] private float peripheralDetectionAngle = 90f;
    [SerializeField] private LayerMask detectionLayers;
    [SerializeField] private LayerMask obstacleLayer; // Add this for obstacle detection
    [SerializeField] private bool showDebugLines = true; // Add debug toggle
    [SerializeField] private float detectionTimeout = 5f; // Time in seconds to track player before rechecking detection



    [Header("Movement Settings")]
    [SerializeField] private float patrolSpeed = 3.5f;
    [SerializeField] private float chaseSpeed = 7f;
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float minPatrolRadius = 10f;
    [SerializeField] private float maxPatrolRadius = 30f;
    [SerializeField] private float attackRange = 2f;


    [Header("Detection Settings")]
    [SerializeField] private float destroyAfterLostTimeout = 10f;  // Time before destruction after losing player


    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemySpawner mySpawner; // Reference to the spawner that created this enemy

    [Header("Audio")]
    [SerializeField] private AK.Wwise.Event detectSound; // Reference to the Wwise event
    [SerializeField] private AK.Wwise.Event footstepSound; // Reference to the footstep Wwise event

    private EnemyState currentState;
    private Vector3 currentPatrolDestination;
    private float stateTimer;
    private bool isWaiting;
    private int failedPatrolAttempts;
    private const int MAX_PATROL_ATTEMPTS = 3;
    private bool gameOverTriggered = false;
    private float detectionTimer = 0f;
    private NavMeshAgent agent;
    private Animator animator;
    private Transform player;
    private float lostPlayerTimer = 0f;
    private bool hasDetectedPlayer = false;



#region Unity Lifecycle Methods

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player").transform;

        if (eyePosition == null)
        {
            Debug.LogError("Eye position not set! Please assign the Eye GameObject in the inspector.");
            enabled = false;
            return;
        }

        if (mySpawner == null)
        {
            mySpawner = GetComponentInParent<EnemySpawner>();
        }

        // Initialize with proper movement settings
        agent.speed = patrolSpeed;
        agent.isStopped = false;

        // Start in walk state and set initial patrol destination
        currentState = EnemyState.Walk;
        UpdateAnimatorState();
        SetNewPatrolDestination();
    }

    private void Update()
    {
        
        if (showDebugLines && player != null && eyePosition != null)
        {
            float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);
            
            Debug.Log($"Distance to player: {distanceToPlayer}, Angle: {angle}, " +
                    $"Line of sight: {HasLineOfSightToPlayer()}, " +
                    $"Player layer: {LayerMask.LayerToName(player.gameObject.layer)}");
        }

        if (hasDetectedPlayer && currentState != EnemyState.Chase && currentState != EnemyState.Attack)
        {
            lostPlayerTimer += Time.deltaTime;
            
            if (lostPlayerTimer >= destroyAfterLostTimeout)
            {
                if (showDebugLines)
                {
                    Debug.Log($"Player lost for {lostPlayerTimer:F1} seconds - deactivating enemy");
                }
                
                // Notify spawner to deactivate instead of destroying
                if (mySpawner != null)
                {
                    mySpawner.DeactivateEnemy();
                }
                else
                {
                    // Fallback if spawner not found
                    gameObject.SetActive(false);
                }
            }
        }
        else
        {
            lostPlayerTimer = 0f;
        }


        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Walk:
                UpdatePatrolState();
                CheckForPlayerDetection();
                break;
            case EnemyState.Chase:
                UpdateChaseState();
                break;
            case EnemyState.Attack:
                UpdateAttackState();
                break;
        }

        UpdateAnimatorState();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Player collision detected!");
            TriggerGameOver();
        }
    }

#endregion


#region State Management

    private void TransitionToState(EnemyState newState)
    {
        if (currentState == newState) return;

        Debug.Log($"Transitioning from {currentState} to {newState}");

        detectionTimer = 0f;

        // Track when player is first detected
         if (newState == EnemyState.Chase)
        {
            hasDetectedPlayer = true;
            lostPlayerTimer = 0f;  // Reset the lost timer when transitioning to chase
            Debug.Log("Player detected - resetting lost timer");

            if (detectSound != null)
            {
                detectSound.Post(gameObject);
                Debug.Log("Chase music event triggered");
            }
        }

        switch (newState)
        {
            case EnemyState.Idle:
                agent.isStopped = true;
                agent.speed = 0;
                break;

            case EnemyState.Walk:
                agent.isStopped = false;
                agent.speed = patrolSpeed;
                break;

            case EnemyState.Chase:
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                break;

            case EnemyState.Attack:
                agent.isStopped = true;
                agent.speed = 0;
                break;
        }

        currentState = newState;
        UpdateAnimatorState();
    }

    private void UpdateAnimatorState()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Walk:
                // Set to walk animation if moving, idle if stopped
                float speed = agent.velocity.magnitude;
                animator.SetInteger("State", speed > 0.1f ? 1 : 0);
                break;
            case EnemyState.Chase:
                animator.SetInteger("State", 2); // Chase
                break;
            case EnemyState.Attack:
                animator.SetInteger("State", 3); // Attack
                break;
        }
    }

    public void ResetState()
    {
        // Reset all state variables
        currentState = EnemyState.Walk;
        detectionTimer = 0f;
        lostPlayerTimer = 0f;
        hasDetectedPlayer = false;
        gameOverTriggered = false;
        isWaiting = false;
        
        // Reset the agent
        if (agent != null)
        {
            agent.ResetPath();
            agent.isStopped = false;
            agent.speed = patrolSpeed;
        }
        
        // Reset animator
        if (animator != null)
        {
            animator.SetInteger("State", 0);
        }
        
        // Set initial patrol destination
        SetNewPatrolDestination();
        
        if (showDebugLines)
        {
            Debug.Log("Enemy AI state reset");
        }
    }

#endregion



#region State Update Methods

    private void UpdatePatrolState()
    {
        if (isWaiting)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0)
            {
                isWaiting = false;
                TransitionToState(EnemyState.Walk);
                SetNewPatrolDestination();
                Debug.Log("Patrol wait time over - starting patrol");

            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            isWaiting = true;
            stateTimer = patrolWaitTime;
            TransitionToState(EnemyState.Idle);
            Debug.Log("Reached patrol point - waiting");
        }
    }

    private void UpdateChaseState()
    {
        detectionTimer += Time.deltaTime;

        if (detectionTimer >= detectionTimeout)
        {
            if (IsPlayerDetected())
            {
                // Reset timer and continue chase
                detectionTimer = 0f;
                lostPlayerTimer = 0f;  // Reset lost player timer when player is detected
                if (showDebugLines)
                {
                    Debug.Log("Player still detected - continuing chase");
                }
            }
            else
            {
                // Lost player - transition to patrol and start counting lost time
                if (showDebugLines)
                {
                    Debug.Log("Lost player - transitioning to patrol");
                }
                isWaiting = true;
                stateTimer = patrolWaitTime;
                TransitionToState(EnemyState.Idle);
                
                // Start counting time since player was lost
                if (hasDetectedPlayer)
                {
                    lostPlayerTimer += Time.deltaTime;
                    if (lostPlayerTimer >= destroyAfterLostTimeout)
                    {
                        Debug.Log("Player lost for too long - destroying enemy");
                        Destroy(gameObject);
                    }
                }
                return;
            }
        }

        // Update destination to current player position
        agent.SetDestination(player.position);

        // Check for attack range
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
        {
            if (player.TryGetComponent<FirstPersonController>(out var playerController))
            {
                playerController.MoveSpeed = 0f;
                playerController.SprintSpeed = 0f;
                Debug.Log("Player movement disabled");
            }
            TransitionToState(EnemyState.Attack);
        }
    }


    private void UpdateAttackState()
    {
        if (gameOverTriggered) return;
        // Turn to face player
        transform.LookAt(player.position);
    }    

#endregion


#region Player Detection
    private void CheckForPlayerDetection()
    {
        if (currentState == EnemyState.Attack) return;

        float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);


        // Immediate detection check
        if (distanceToPlayer <= immediateDetectionRange)
        {
            Debug.Log("Immediate detection - Transitioning to Chase");
            TransitionToState(EnemyState.Chase);
            
        }

        // Main detection cone check
        if (distanceToPlayer <= mainDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);

            if (showDebugLines)
            {
                Debug.Log($"Angle to player: {angle}");
            }

            if (angle <= mainDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                Debug.Log("Main cone detection - Transitioning to Chase");
                TransitionToState(EnemyState.Chase);
                return;
            }
        }

        // Peripheral vision check
        if (distanceToPlayer <= peripheralDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);

            if (angle <= peripheralDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                Debug.Log("Peripheral detection - Transitioning to Chase");
                TransitionToState(EnemyState.Chase);
            }
        }
    }

    private bool IsPlayerDetected()
    {
        float distanceToPlayer = Vector3.Distance(eyePosition.position, player.position);

        // Immediate detection check - no line of sight needed
        if (distanceToPlayer <= immediateDetectionRange)
        {
            return true;
        }

        // Main detection cone check
        if (distanceToPlayer <= mainDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);

            if (angle <= mainDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                return true;
            }
        }

        // Peripheral vision check
        if (distanceToPlayer <= peripheralDetectionRange)
        {
            Vector3 directionToPlayer = (player.position - eyePosition.position).normalized;
            float angle = Vector3.Angle(eyePosition.forward, directionToPlayer);

            if (angle <= peripheralDetectionAngle * 0.5f && HasLineOfSightToPlayer())
            {
                return true;
            }
        }

        return false;
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null || eyePosition == null) return false;

        Vector3 directionToPlayer = player.position - eyePosition.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        // Check if player is below eye level
        float heightDifference = eyePosition.position.y - player.position.y;

        // Only allow detection if player is below eye level
        if (heightDifference < 0) // Player is above eye level
        {
            if (showDebugLines)
            {
                Debug.DrawLine(eyePosition.position, player.position, Color.blue, 0.1f);
                Debug.Log("Player above eye level - cannot detect");
            }
            return false;
        }

        if (showDebugLines)
        {
            Debug.DrawLine(eyePosition.position, player.position, Color.yellow, 0.1f);
            Debug.Log($"Checking line of sight. Distance: {distanceToPlayer}, Height diff: {heightDifference}");
        }

        // Check for obstacles
        if (Physics.Raycast(eyePosition.position, directionToPlayer.normalized, out RaycastHit obstacleHit, distanceToPlayer, obstacleLayer))
        {
            if (showDebugLines)
            {
                Debug.DrawLine(eyePosition.position, obstacleHit.point, Color.red, 0.1f);
                Debug.Log($"View blocked by: {obstacleHit.transform.name}");
            }
            return false;
        }

        // Check for player
        if (Physics.Raycast(eyePosition.position, directionToPlayer.normalized, out RaycastHit playerHit, distanceToPlayer, detectionLayers))
        {
            bool isPlayer = playerHit.transform.CompareTag("Player");
            if (showDebugLines)
            {
                Color rayColor = isPlayer ? Color.green : Color.red;
                Debug.DrawLine(eyePosition.position, playerHit.point, rayColor, 0.1f);
                Debug.Log($"Hit {playerHit.transform.name}, isPlayer: {isPlayer}");
            }
            return isPlayer;
        }

        return false;
    }

#endregion


#region Patrol Logic
    private void SetNewPatrolDestination()
    {
        if (failedPatrolAttempts >= MAX_PATROL_ATTEMPTS)
        {
            // If we've failed too many times, try a closer point
            failedPatrolAttempts = 0;
            FindPatrolPoint(minPatrolRadius * 0.5f);
            return;
        }

        if (!FindPatrolPoint(Random.Range(minPatrolRadius, maxPatrolRadius)))
        {
            failedPatrolAttempts++;
            SetNewPatrolDestination();
        }
    }

    private bool FindPatrolPoint(float radius)
    {
        Vector3 randomDirection = Random.insideUnitSphere * radius;
        randomDirection += transform.position;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(randomDirection, out hit, radius, NavMesh.AllAreas))
        {
            currentPatrolDestination = hit.position;
            agent.SetDestination(currentPatrolDestination);
            return true;
        }

        return false;
    }

#endregion


#region Game State Management
    private void TriggerGameOver()
    {
        if (gameOverTriggered) return;

        if (gameManager != null)
        {
            gameOverTriggered = true;
            agent.isStopped = true;
            Debug.Log("Triggering game over!");
            gameManager.ShowGameOver();
        }
        else
        {
            Debug.LogError("GameManager reference not set on " + gameObject.name);
        }
    }

    public void OnAttackAnimationComplete()
    {
        TriggerGameOver();
        Debug.Log($"GameOver Triggered");
    }

    private void OnDestroy()
    {
        // Notify spawner that enemy is destroyed
        if (mySpawner != null)
        {
            mySpawner.NotifyEnemyDestroyed();
        }
    }

#endregion


#region Debug Visualization
    private void OnDrawGizmosSelected()
    {
        if (!eyePosition) return;

        // Draw immediate detection range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(eyePosition.position, immediateDetectionRange);

        // Draw main detection cone
        Gizmos.color = Color.yellow;
        DrawDetectionCone(mainDetectionAngle, mainDetectionRange);

        // Draw peripheral detection cone
        Gizmos.color = Color.blue;
        DrawDetectionCone(peripheralDetectionAngle, peripheralDetectionRange);

        // Draw horizontal line to show eye level
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(
            eyePosition.position - eyePosition.right * mainDetectionRange,
            eyePosition.position + eyePosition.right * mainDetectionRange
        );
         // Draw attack range
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Semi-transparent red
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    private void DrawDetectionCone(float angle, float range)
    {
        if (!eyePosition) return;

        float halfAngle = angle * 0.5f;
        Vector3 leftDirection = Quaternion.Euler(0, -halfAngle, 0) * eyePosition.forward;
        Vector3 rightDirection = Quaternion.Euler(0, halfAngle, 0) * eyePosition.forward;

        Gizmos.DrawLine(eyePosition.position, eyePosition.position + leftDirection * range);
        Gizmos.DrawLine(eyePosition.position, eyePosition.position + rightDirection * range);

        Vector3 previousPoint = eyePosition.position + leftDirection * range;
        int segments = 20;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (angle * i / segments);
            Vector3 direction = Quaternion.Euler(0, currentAngle, 0) * eyePosition.forward;
            Vector3 currentPoint = eyePosition.position + direction * range;

            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }
    }
#endregion


#region Timeline Signal Handlers

    public void SignalTurnTowardsPlayer()
    {
        // Try multiple times with a slight delay to ensure player reference is valid
        StartCoroutine(TurnTowardsPlayerWithRetry());
    }

    private IEnumerator TurnTowardsPlayerWithRetry()
    {
        bool turnSuccessful = false;
        
        for (int attempts = 0; attempts < 5; attempts++)
        {
            if (player == null)
            {
                Debug.LogWarning($"Player reference null, retrying ({attempts+1}/5)...");
                yield return new WaitForSeconds(0.2f);
                player = GameObject.FindGameObjectWithTag("Player")?.transform;
                continue;
            }

            Vector3 targetDirection = player.position - transform.position;
            targetDirection.y = 0;
            
            if (targetDirection.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(targetDirection);
                Debug.Log("Successfully turned toward player after " + (attempts+1) + " attempts");
                turnSuccessful = true;
                break;
            }
            
            yield return new WaitForSeconds(0.2f);
        }
        
        // Even if turn failed, make sure we have a valid player reference for detection
        if (!turnSuccessful)
        {
            Debug.LogWarning("Failed to turn towards player. Ensuring player reference is valid.");
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            
            // Force a check for player detection to ensure AI continues working
            if (player != null)
            {
                Debug.Log("Turn failed but player reference found. Checking for detection.");
                CheckForPlayerDetection();
            }
        }
    }

#endregion

#region Animation Events

    // This function will be called by the animation event with name "e_footstep_walk"
    public void E_footstep_walk()
    {
        if (footstepSound != null)
        {
            footstepSound.Post(gameObject);
            if (showDebugLines)
            {
                Debug.Log("Playing footstep sound");
            }
        }
        else
        {
            Debug.LogWarning("Footstep sound Wwise event not assigned");
        }
    }

#endregion

}
