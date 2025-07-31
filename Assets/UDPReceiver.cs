using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using System.Collections.Concurrent;

public class UDPReceiver : MonoBehaviour
{
    public static UDPReceiver Instance { get; private set; }

    [Header("UDP Settings")]
    [SerializeField] private int listenPort = 4848;
    [SerializeField] private float noDataThreshold = 5f;

    private UdpClient udpClient;
    private int heartbeat;
    private bool isReceiving = true;
    private object heartbeatLock = new object();
    private float lastReceiveTime;
    private bool hasLoggedNoData;
    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();
    private bool needsTimeUpdate = false;


    public int Heartbeat
    {
        get
        {
            lock (heartbeatLock)
            {
                return heartbeat;
            }
        }
        private set
        {
            lock (heartbeatLock)
            {
                heartbeat = value;
            }
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("HeartbeatReceiver instance created");
        }
        else
        {
            Debug.LogWarning("Multiple HeartbeatReceiver instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializeUDPClient();
    }

    private void InitializeUDPClient()
    {
        lastReceiveTime = Time.time;
        hasLoggedNoData = false;

        try
        {
            // Create an endpoint that listens on all available network interfaces
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
            udpClient = new UdpClient(localEndPoint);
            udpClient.BeginReceive(ReceiveCallback, null);
            Debug.Log($"UDP client started listening on all interfaces, port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize UDP client: {e.Message}");
            isReceiving = false;
        }
    }

    private void Update()
    {
        CheckDataReception();
        
        // Process any queued messages on the main thread
        while (messageQueue.TryDequeue(out string message))
        {
            ProcessMessage(message);
        }

        // Update the last receive time on the main thread if needed
        if (needsTimeUpdate)
        {
            lastReceiveTime = Time.time;
            needsTimeUpdate = false;
            hasLoggedNoData = false;
        }
    }

    private void ProcessMessage(string receivedMessage)
    {
        if (int.TryParse(receivedMessage, out int value))
        {
            Heartbeat = value;
            Debug.Log($"Received heartbeat: {value} BPM");
        }
        else
        {
            Debug.LogWarning($"Failed to parse heartbeat value: {receivedMessage}");
        }
    }

    private void CheckDataReception()
    {
        if (!isReceiving) return;

        float timeSinceLastReceive = Time.time - lastReceiveTime;
        if (timeSinceLastReceive >= noDataThreshold && !hasLoggedNoData)
        {
            Debug.LogWarning($"No heartbeat data received for {timeSinceLastReceive:F1} seconds");
            hasLoggedNoData = true;
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        if (!isReceiving) return;

        try
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
            byte[] receivedBytes = udpClient.EndReceive(ar, ref remoteEndPoint);
            string receivedMessage = Encoding.UTF8.GetString(receivedBytes);
            
            // Queue the message for processing on the main thread
            messageQueue.Enqueue(receivedMessage);
            needsTimeUpdate = true;

            if (isReceiving)
                udpClient.BeginReceive(ReceiveCallback, null);
        }
        catch (ObjectDisposedException)
        {
            Debug.Log("UDP client was closed");
        }
        catch (Exception e)
        {
            if (isReceiving)
            {
                Debug.LogError($"Error receiving UDP data: {e.Message}");
                RestartUDPClient();
            }
        }
    }

    private void RestartUDPClient()
    {
        try
        {
            CleanUp();
            InitializeUDPClient();
            Debug.Log("UDP client restarted successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to restart UDP client: {e.Message}");
        }
    }

    private void CleanUp()
    {
        isReceiving = false;
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
                Debug.Log("UDP client closed successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing UDP client: {e.Message}");
            }
            udpClient = null;
        }
    }

    private void OnApplicationQuit()
    {
        CleanUp();
    }

    private void OnDestroy()
    {
        CleanUp();
    }
}
