using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public class ClientHandle : MonoBehaviour
{
    public static void Welcome(Packet _packet)
    {
        string _msg = _packet.ReadString();
        int _myId = _packet.ReadInt();

        Debug.Log($"Message from server: {_msg}");
        Client.instance.myId = _myId;
        ClientSend.WelcomeReceived();

        // Now that we have the client's id, connect UDP
        Client.instance.udp.Connect(((IPEndPoint)Client.instance.tcp.socket.Client.LocalEndPoint).Port);
    }

    public static void SpawnPlayer(Packet _packet)
    {
        int _id = _packet.ReadInt();
        string _username = _packet.ReadString();
        Vector3 _position = _packet.ReadVector3();
        Quaternion _rotation = _packet.ReadQuaternion();

        GameManager.instance.SpawnPlayer(_id, _username, _position, _rotation);
    }

    public static void PlayerMovement(Packet _packet)
    {
        int _id = _packet.ReadInt();
        Vector3 _position = _packet.ReadVector3();
        Quaternion _camRotation = _packet.ReadQuaternion();
        Quaternion _orientation = _packet.ReadQuaternion();
        Vector3 _velocity = _packet.ReadVector3();
        int _tick = _packet.ReadInt();

        PlayerController controller = GameManager.players[_id].GetComponent<PlayerController>();
        controller.currentServerSnapshot.position = _position;
        controller.currentServerSnapshot.camRotation = _camRotation;
        controller.currentServerSnapshot.orientation = _orientation;
        controller.currentServerSnapshot.velocity = _velocity;
        controller.currentServerSnapshot.tick = _tick;
        controller.snapshots.Enqueue(controller.currentServerSnapshot);
    }

    public static void PlayerRotation(Packet _packet)
    {
        int _id = _packet.ReadInt();           
    }
}
