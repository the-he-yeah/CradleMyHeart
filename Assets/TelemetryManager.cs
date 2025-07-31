using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }
    
    [Header("Telemetry Settings")]
    [SerializeField] private string fileNamePrefix = "telemetry_";
    [SerializeField] private string fileExtension = ".csv";
    [SerializeField] private bool appendTimestampToFileName = true;
    [SerializeField] private bool logEvents = true;
    [SerializeField] private float heartRateLoggingInterval = 2.0f;

    private float heartRateLogTimer = 0f;
    private string sessionID;
    private string filePath;
    private List<TelemetryEvent> eventQueue = new List<TelemetryEvent>();
    private bool isInitialized = false;
    
    // Structure to hold telemetry events
    private struct TelemetryEvent
    {
        public string timestamp;
        public float heartrate;
        public string eventName;
        public string eventValue;
        
        public override string ToString()
        {
            return $"{timestamp},{heartrate},{eventName},{eventValue}";
        }
    }
    
    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeTelemetry();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeTelemetry()
    {
        // Create a unique session ID
        sessionID = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        // Set up file path
        string fileName = fileNamePrefix + (appendTimestampToFileName ? sessionID : "") + fileExtension;
        filePath = Path.Combine(Application.persistentDataPath, fileName);
        
        // Create and initialize the CSV file with headers
        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            writer.WriteLine("timestamp,heartrate,eventName,eventValue");
        }
        
        isInitialized = true;
        Debug.Log($"Telemetry file initialized at: {filePath}");
        
        // Log session start event
        LogEvent("session", "start");
    }
    
    private void OnApplicationQuit()
    {
        // Log session end event
        LogEvent("session", "end");
        
        // Save any remaining events
        SaveEvents();
    }
    
    private void Update()
    {
        // Periodically save events to avoid losing data if game crashes
        if (eventQueue.Count > 10)
        {
            SaveEvents();
        }

        // Code for periodic heartrate logging
        //heartRateLogTimer += Time.deltaTime;
        //if (heartRateLogTimer >= heartRateLoggingInterval)
        //{
        //    LogEvent("heartrate", UDPReceiver.Instance.Heartbeat.ToString());
        //    heartRateLogTimer = 0f;
        //}
    }
    
    public void LogEvent(string eventName, string eventValue)
    {
        if (!isInitialized)
        {
            Debug.LogError("TelemetryManager not initialized!");
            return;
        }
        
        // Create timestamp as epoch time (seconds since Unix epoch)
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        
        // Get current heartrate from UDPReceiver
        float heartrate = 0;
        if (UDPReceiver.Instance != null)
        {
            heartrate = UDPReceiver.Instance.Heartbeat;
        }
        
        // Create and queue the event
        TelemetryEvent newEvent = new TelemetryEvent
        {
            timestamp = timestamp,
            heartrate = heartrate,
            eventName = eventName,
            eventValue = eventValue
        };
        
        eventQueue.Add(newEvent);
        
        if (logEvents)
        {
            Debug.Log($"Telemetry event: {newEvent}");
        }
    }
    
    private void SaveEvents()
    {
        if (eventQueue.Count == 0) return;
        
        try
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                foreach (var evt in eventQueue)
                {
                    writer.WriteLine(evt.ToString());
                }
            }
            
            Debug.Log($"Saved {eventQueue.Count} telemetry events to {filePath}");
            eventQueue.Clear();
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save telemetry events: {e.Message}");
        }
    }
}
