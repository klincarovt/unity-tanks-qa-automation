using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

public class TestBridge : MonoBehaviour
{
    private TcpListener _listener;
    private Thread _thread;
    private bool _running = false;
    private const int PORT = 13000;

    private readonly ConcurrentQueue<PendingCommand> _queue = new ConcurrentQueue<PendingCommand>();

    private class PendingCommand
    {
        public string Command;
        public NetworkStream Stream;
        public TcpClient Client;
    }

    void Start()
    {
        _running = true;
        _thread = new Thread(ListenLoop) { IsBackground = true };
        _thread.Start();
        Debug.Log("[TestBridge] Listening on port " + PORT);
    }

    void Update()
    {
        while (_queue.TryDequeue(out var pending))
        {
            string response = HandleCommand(pending.Command);
            byte[] bytes = Encoding.UTF8.GetBytes(response + "\n");
            try   { pending.Stream.Write(bytes, 0, bytes.Length); }
            catch (Exception e) { Debug.LogError("[TestBridge] Send failed: " + e.Message); }
            finally { pending.Client.Close(); }
        }
    }

    void OnDestroy()
    {
        _running = false;
        _listener?.Stop();
    }

    private void ListenLoop()
    {
        _listener = new TcpListener(IPAddress.Loopback, PORT);
        _listener.Start();

        while (_running)
        {
            try
            {
                var client = _listener.AcceptTcpClient();
                var stream = client.GetStream();
                byte[] buffer = new byte[1024];
                int n = stream.Read(buffer, 0, buffer.Length);
                string command = Encoding.UTF8.GetString(buffer, 0, n).Trim();
                Debug.Log("[TestBridge] Received: " + command);
                _queue.Enqueue(new PendingCommand { Command = command, Stream = stream, Client = client });
            }
            catch (Exception e)
            {
                if (_running) Debug.LogError("[TestBridge] Error: " + e.Message);
            }
        }
    }

    private string HandleCommand(string command)
    {
        if (command == "PING")
            return "PONG";

        if (command == "FIND_TANK")
        {
            var tank = FindFirstTank();
            return tank != null ? "FOUND:" + tank.name : "NOT_FOUND";
        }

        if (command == "FIND_ALL_TANKS")
        {
            var tanks = FindAllTanks();
            if (tanks.Count == 0) return "NOT_FOUND";
            return "TANKS:" + string.Join(",", tanks.ConvertAll(t => t.name));
        }

        if (command.StartsWith("MOVE_TANK:"))
        {
            var rest   = command.Substring("MOVE_TANK:".Length);
            int colon  = rest.IndexOf(':');
            var name   = rest.Substring(0, colon);
            var coords = rest.Substring(colon + 1).Split(',');

            var tank = FindTankByName(name);
            if (tank == null) return "NOT_FOUND";

            float x = float.Parse(coords[0], CultureInfo.InvariantCulture);
            float y = float.Parse(coords[1], CultureInfo.InvariantCulture);
            float z = float.Parse(coords[2], CultureInfo.InvariantCulture);
            tank.transform.position = new Vector3(x, y, z);
            return "MOVED:" + name;
        }

        if (command.StartsWith("AIM_AT:"))
        {
            var rest    = command.Substring("AIM_AT:".Length);
            int colon   = rest.IndexOf(':');
            var shooter = FindTankByName(rest.Substring(0, colon));
            var target  = FindTankByName(rest.Substring(colon + 1));
            if (shooter == null || target == null) return "NOT_FOUND";

            shooter.transform.LookAt(target.transform);
            return "AIMED";
        }

        if (command.StartsWith("SHOOT:"))
        {
            var tank = FindTankByName(command.Substring("SHOOT:".Length));
            if (tank == null) return "NOT_FOUND";

            var shooting = GetComponentByName(tank, "TankShooting");
            if (shooting == null) return "NOT_FOUND";

            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            shooting.GetType().GetField("m_Fired", flags)?.SetValue(shooting, false);

            var maxForceField     = shooting.GetType().GetField("m_MaxLaunchForce", flags);
            var currentForceField = shooting.GetType().GetField("m_CurrentLaunchForce", flags);
            float maxForce = maxForceField != null ? (float)maxForceField.GetValue(shooting) : 30f;
            currentForceField?.SetValue(shooting, maxForce * 0.6f);

            shooting.GetType().GetMethod("Fire", flags)?.Invoke(shooting, null);
            return "SHOT:" + tank.name;
        }

        if (command.StartsWith("GET_HEALTH:"))
        {
            var tank = FindTankByName(command.Substring("GET_HEALTH:".Length));
            if (tank == null) return "NOT_FOUND";

            var health = GetComponentByName(tank, "TankHealth");
            if (health == null) return "NOT_FOUND";

            var field = health.GetType().GetField("m_CurrentHealth",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return "NOT_FOUND";

            float value = (float)field.GetValue(health);
            return "HEALTH:" + value.ToString(CultureInfo.InvariantCulture);
        }

        return "UNKNOWN_COMMAND";
    }

    // ------------------------------------------------------------------
    // Helpers — find tanks by TankHealth component, not by tag
    // ------------------------------------------------------------------

    private GameObject FindFirstTank()
    {
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            if (mb.GetType().Name == "TankHealth")
                return mb.gameObject;
        return null;
    }

    private List<GameObject> FindAllTanks()
    {
        var result = new List<GameObject>();
        var seen   = new HashSet<int>();
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            if (mb.GetType().Name == "TankHealth" && seen.Add(mb.gameObject.GetInstanceID()))
                result.Add(mb.gameObject);
        return result;
    }

    private GameObject FindTankByName(string name)
    {
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            if (mb.GetType().Name == "TankHealth" && mb.gameObject.name == name)
                return mb.gameObject;
        return null;
    }

    private MonoBehaviour GetComponentByName(GameObject go, string typeName)
    {
        foreach (var comp in go.GetComponents<MonoBehaviour>())
            if (comp.GetType().Name == typeName) return comp;
        return null;
    }
}
