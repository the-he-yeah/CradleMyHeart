using UnityEngine;
using UnityEngine.InputSystem;

public class AN_PlugScript : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the player GameObject")]
    public GameObject player;
    public AN_DoorScript DoorObject;
    
    [Header("Settings")]
    public bool OneTime = false;
    public float GrabDistance = 3f;
    public float ConnectDistance = .3f;
    
    private bool youCan = true;
    private bool follow = false;
    private bool isConnected = false;
    private Vector3 startPos;
    private Transform socket;

    // Input System
    private InputSystem_Actions inputActions;
    private bool isInteracting = false;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Player.Interact.started += ctx => isInteracting = true;
        inputActions.Player.Interact.canceled += ctx => isInteracting = false;

        if (player == null)
        {
            try
            {
                player = Camera.main.gameObject;
            }
            catch
            {
                Debug.LogError("No player reference and no Camera.main found!");
            }
        }
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    void Start()
    {
        startPos = transform.position;
        
        if (DoorObject != null)
        {
            socket = DoorObject.transform;
        }
        else
        {
            Debug.LogError("Door Object not assigned to plug: " + gameObject.name);
        }
    }

    void Update()
    {
        if (!isConnected && youCan && isInteracting && NearView()) // take/drop
        {
            follow = !follow;
        }

        if (follow) // following
        {
            transform.position = player.transform.position + player.transform.forward * 2f;
            transform.rotation = player.transform.rotation;

            float distanceToSocket = Vector3.Distance(transform.position, socket.position);
            if (distanceToSocket < ConnectDistance) // connecting
            {
                isConnected = true;
                follow = false;
                DoorObject.AddDoorTorque();
            }
        }
        if (OneTime) youCan = false;
    }

    bool NearView()
    {
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.transform.position);
        Vector3 direction = transform.position - player.transform.position;
        float angleView = Vector3.Angle(player.transform.forward, direction);
        return distance < GrabDistance && angleView < 35f;
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
}