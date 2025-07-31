using UnityEngine;
using UnityEngine.InputSystem;
using AK.Wwise;

public class AN_DoorScript : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the player GameObject")]
    public GameObject player;
    [Tooltip("Reference to the hero interactive component")]
    public AN_HeroInteractive heroInteractive;

    [Header("Interaction Settings")]
    [Tooltip("Maximum interaction distance")]
    public float interactionDistance = 3f;

    [Header("Door Settings")]
    [Tooltip("If it is false door can't be used")]
    public bool Locked = false;
    [Tooltip("It is true for remote control only")]
    public bool Remote = false;
    [Space]
    [Tooltip("Door can be opened")]
    public bool CanOpen = true;
    [Tooltip("Door can be closed")]
    public bool CanClose = true;
    [Space]
    [Tooltip("Door locked by red key (use key script to declarate any object as key)")]
    public bool RedLocked = false;
    public bool BlueLocked = false;
    [Space]
    public bool isOpened = false;
    [Range(0f, 4f)]
    [Tooltip("Speed for door opening, degrees per sec")]
    public float OpenSpeed = 3f;

    [Header("Sound Settings")]
    [Tooltip("Sound played when door is activated")]
    public AK.Wwise.Event doorActivationSound;

    // Input System
    private InputSystem_Actions inputActions;
    private bool isInteracting = false;

    // Components
    private Rigidbody rbDoor;
    private HingeJoint hinge;
    private JointLimits hingeLim;
    private float currentLim;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Player.Interact.started += ctx => isInteracting = true;
        inputActions.Player.Interact.canceled += ctx => isInteracting = false;

        // Get required components
        rbDoor = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();
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
        if (player == null)
        {
            Debug.LogWarning("Player reference not set in " + gameObject.name + ". Attempting to find Camera.main");
            try
            {
                player = Camera.main.gameObject;
            }
            catch
            {
                Debug.LogError("No player reference and no Camera.main found!");
            }
        }

        if (heroInteractive == null)
        {
            Debug.LogWarning("HeroInteractive reference not set in " + gameObject.name);
        }
    }

    void Update()
    {
        if (!Remote && isInteracting && NearView())
        {
            Action();
        }
    }

    public void Action()
    {
        if (!Locked)
        {
            // key lock checking
            if (heroInteractive != null && RedLocked && heroInteractive.RedKey)
            {
                RedLocked = false;
                heroInteractive.RedKey = false;
            }
            else if (heroInteractive != null && BlueLocked && heroInteractive.BlueKey)
            {
                BlueLocked = false;
                heroInteractive.BlueKey = false;
            }
            
            // opening/closing
            if (isOpened && CanClose && !RedLocked && !BlueLocked)
            {
                isOpened = false;
                doorActivationSound?.Post(gameObject);
            }
            else if (!isOpened && CanOpen && !RedLocked && !BlueLocked)
            {
                isOpened = true;
                rbDoor.AddRelativeTorque(new Vector3(0, 0, 20f));
                doorActivationSound?.Post(gameObject); 
            }
        }
    }

    bool NearView()
    {
        if (player == null) return false;

        float distance = Vector3.Distance(transform.position, player.transform.position);
        Vector3 direction = transform.position - player.transform.position;
        float angleView = Vector3.Angle(player.transform.forward, direction);
        
        return distance < interactionDistance;
    }

    private void FixedUpdate()
    {
        if (isOpened)
        {
            currentLim = 85f;
        }
        else
        {
            if (currentLim > 1f)
                currentLim -= .5f * OpenSpeed;
        }

        hingeLim.max = currentLim;
        hingeLim.min = -currentLim;
        hinge.limits = hingeLim;
    }

    private void OnDestroy()
    {
        inputActions?.Dispose();
    }
    public void AddDoorTorque()
    {
        if (!Locked && !isOpened && CanOpen && !RedLocked && !BlueLocked)
        {
            isOpened = true;
            rbDoor.AddRelativeTorque(new Vector3(0, 0, 20f));
            doorActivationSound?.Post(gameObject);
        }
    }
}