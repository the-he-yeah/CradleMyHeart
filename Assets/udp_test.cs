using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class Udp_test : MonoBehaviour
{
    public int listenPort;

    private string receivedMessage;
    public int[] sensorValues;

    private UdpClient udpClient;

    private void Start()
    {
        sensorValues = new int[1];  // Initialize the sensor array

        udpClient = new UdpClient(listenPort);
        udpClient.BeginReceive(ReceiveCallback, null);
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, listenPort);
        byte[] receivedBytes = udpClient.EndReceive(ar, ref remoteEndPoint);
        receivedMessage = Encoding.ASCII.GetString(receivedBytes);

        // Remove square brackets and whitespace
        receivedMessage = receivedMessage.Replace("[", "").Replace("]", "").Trim();

        string[] values = receivedMessage.Split(',');

        int valuesToParse = Mathf.Min(sensorValues.Length, values.Length);

        for (int i = 0; i < valuesToParse; i++)
        {
            int value;
            if (int.TryParse(values[i], out value))
            {
                sensorValues[i] = value;
            }
            else
            {
                Debug.LogError($"Failed to parse sensor value at index {i}: {values[i]}");
            }
        }

        udpClient.BeginReceive(ReceiveCallback, null);
    }

    private void OnApplicationQuit()
    {
        udpClient.Close();
    }
}
