using UnityEngine;
using StarterAssets;
using UnityEngine.InputSystem;

public class ObjectInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private LayerMask interactableLayer;
    [SerializeField] private Transform holdPosition; // Position where object will be held
    [SerializeField] private float throwForce = 10f;

    private StarterAssetsInputs _input;
    private GameObject _heldObject;
    private Rigidbody _heldObjectRb;
    private Transform _cameraTransform;

    private void Start()
    {
        _input = GetComponent<StarterAssetsInputs>();
        _cameraTransform = Camera.main.transform;

        if (holdPosition == null)
        {
            // Create hold position if not set
            GameObject holdPositionObj = new GameObject("HoldPosition");
            holdPosition = holdPositionObj.transform;
            holdPosition.SetParent(_cameraTransform);
            
            // Position it in front of the camera
            holdPosition.localPosition = new Vector3(0, -0.5f, 2f);
            holdPosition.localRotation = Quaternion.identity;
            
            Debug.Log($"Created hold position at {holdPosition.position}");
        }
        else
        {
            Debug.Log($"Using existing hold position at {holdPosition.position}");
        }
    }

    private void Update()
    {
        if (_input.interact)
        {
            if (_heldObject == null)
            {
                TryPickupObject();
            }
            else
            {
                DropObject();
            }
            _input.interact = false;
        }

        if (_heldObject != null)
        {
            HoldObject();
        }
    }

    private void TryPickupObject()
    {
        Ray ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionRange, interactableLayer))
        {
            _heldObject = hit.collider.gameObject;
            _heldObjectRb = _heldObject.GetComponent<Rigidbody>();

            if (_heldObjectRb != null)
            {
                // Store original values
                _heldObjectRb.interpolation = RigidbodyInterpolation.Interpolate;
                _heldObjectRb.useGravity = false;
                _heldObjectRb.isKinematic = true;

                // Unparent first to avoid scale issues
                _heldObject.transform.SetParent(null);
                
                // Set initial position to hold position
                _heldObject.transform.position = holdPosition.position;
                
                // Then parent to hold position
                _heldObject.transform.SetParent(holdPosition);
                
                // Reset local rotation
                _heldObject.transform.localRotation = Quaternion.identity;
                
                Debug.Log($"Picked up: {_heldObject.name} at position {_heldObject.transform.position}");
            }
            else
            {
                _heldObject = null;
                Debug.LogWarning("Object must have a Rigidbody component to be picked up");
            }
        }
    }

    private void HoldObject()
    {
        if (_heldObjectRb == null) return;

        // Ensure the object stays exactly at the hold position
        _heldObject.transform.position = holdPosition.position;
        _heldObject.transform.rotation = holdPosition.rotation;
    }

    private void DropObject(bool thrown = false)
    {
        if (_heldObject == null) return;

        // Unparent the object
        _heldObject.transform.SetParent(null);

        // Reset rigidbody settings
        _heldObjectRb.useGravity = true;
        _heldObjectRb.isKinematic = false;

        if (thrown)
        {
            // Add force to throw the object
            _heldObjectRb.AddForce(_cameraTransform.forward * throwForce, ForceMode.Impulse);
        }

        _heldObject = null;
        _heldObjectRb = null;
        Debug.Log("Object dropped");
    }

    // Visualize the interaction range in the editor
    private void OnDrawGizmosSelected()
    {
        if (_cameraTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_cameraTransform.position, interactionRange);
            Gizmos.DrawRay(_cameraTransform.position, _cameraTransform.forward * interactionRange);
        }
    }
}