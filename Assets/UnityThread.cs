using UnityEngine;
using System.Collections.Generic;

public class UnityThread : MonoBehaviour
{
    public static UnityThread instance;
    Queue<System.Action> actions = new Queue<System.Action>();

    void Awake()
    {
        instance = this;
    }

    public void AddAction(System.Action action)
    {
        lock (actions)
        {
            actions.Enqueue(action);
        }
    }

    void Update()
    {
        lock (actions)
        {
            while (actions.Count > 0)
            {
                actions.Dequeue()();
            }
        }
    }
}