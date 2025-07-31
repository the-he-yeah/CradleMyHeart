using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;
using System.Collections;


public class TakeCover : MonoBehaviour
{
    [Header("Cover Settings")]
    [SerializeField] private float detectionRange = 2f;
    [SerializeField] private LayerMask coverLayers;
    [SerializeField] private float coverOffset = 0.5f;
    [SerializeField] private float moveSpeed = 10f;
    private CharacterController _characterController;

    [Header("References")]
    private FirstPersonController _fpsController;
    private Transform _playerTransform;
    private bool _isInCover;
    private StarterAssetsInputs _input;

    private void Start()
    {
        _fpsController = GetComponent<FirstPersonController>();
        _playerTransform = transform;
        _input = GetComponent<StarterAssetsInputs>();
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // Check for hide button press
        if (_input.hide)
        {
            Debug.Log("Hide button pressed");
            TryTakeCover();
            _input.hide = false;
        }

        // Check if player manually uncrouched while in cover
        if (_isInCover && !_fpsController.IsCrouching)
        {
            Debug.Log("Exiting cover due to manual uncrouch");
            ExitCover();
        }
    }

    private void TryTakeCover()
    {
        Debug.Log("Trying to take cover");

        // If already in cover, exit cover
        if (_isInCover)
        {
            Debug.Log("Exiting cover");
            ExitCover();
            return;
        }

        // Cast a ray to detect nearby cover objects
        Ray ray = new Ray(_playerTransform.position, _playerTransform.forward);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * detectionRange, Color.red, 2f);

        if (Physics.Raycast(ray, out hit, detectionRange, coverLayers))
        {
            Debug.Log($"Hit object: {hit.collider.gameObject.name} on layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            EnterCover(hit);
        }
        else
        {
            Debug.Log("No cover object detected");
        }
    }

    private void EnterCover(RaycastHit hit)
    {
        Debug.Log("Entering cover");

        // First set the cover state
        _isInCover = true;

        // Then set crouch state before movement
        _fpsController.SetCrouch(true);

        // Calculate target position
        Vector3 targetPosition = hit.point + hit.normal * coverOffset;
        targetPosition.y = _playerTransform.position.y;

        // Rotate the player 180 degrees around the Y axis
        Vector3 currentRotation = _playerTransform.eulerAngles;
        _playerTransform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y + 180f, currentRotation.z);

        // Start smooth movement coroutine last
        StartCoroutine(MoveToPosition(targetPosition));
    }

    private void ExitCover()
    {
        Debug.Log("Exiting cover");

        // Stop any ongoing movement coroutines
        StopAllCoroutines();

        // Make sure to reset cover state first
        _isInCover = false;

        // Rotate player back 180 degrees to original direction
        Vector3 currentRotation = _playerTransform.eulerAngles;
        _playerTransform.rotation = Quaternion.Euler(currentRotation.x, currentRotation.y + 180f, currentRotation.z);

        // Then uncrouch the player
        _fpsController.SetCrouch(false);

        // Reset movement state
        _fpsController.ResetMovement();

        // Reset the CharacterController
        _characterController.enabled = false;
        _characterController.enabled = true;
    }   

    private IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        float distanceThreshold = 0.01f;

        while (Vector3.Distance(_playerTransform.position, targetPosition) > distanceThreshold)
        {
            Vector3 moveDirection = (targetPosition - _playerTransform.position).normalized;
            Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
            
            // Ensure we don't overshoot
            if (movement.magnitude > Vector3.Distance(_playerTransform.position, targetPosition))
            {
                movement = targetPosition - _playerTransform.position;
            }

            _characterController.Move(movement);
            yield return null;
        }
    }

    // Visualize the detection range in the editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}