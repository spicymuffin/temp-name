using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ServerHandle
{
    public static void WelcomeReceived(int _fromClient, Packet _packet)
    {
        int _clientIdCheck = _packet.ReadInt();
        string _username = _packet.ReadString();

        Debug.Log($"{Server.clients[_fromClient].tcp.socket.Client.RemoteEndPoint} connected successfully and is now player {_fromClient}.");
        if (_fromClient != _clientIdCheck)
        {
            Debug.Log($"Player \"{_username}\" (ID: {_fromClient}) has assumed the wrong client ID ({_clientIdCheck})!");
        }
        Server.clients[_fromClient].SendIntoGame(_username);
    }

    public static void PlayerMovement(int _fromClient, Packet _packet)
    {
        bool[] _inputs = new bool[8];
        _inputs = DecodeByte(_packet.ReadByte());
        Quaternion _camRotation = _packet.ReadQuaternion();
        Quaternion _orientation = _packet.ReadQuaternion();
        int _tick = _packet.ReadInt();

        //pass data to player instance to queue later

        Server.clients[_fromClient].player.QueueIncomingInputPack(_inputs, _camRotation, _orientation, _tick);
    }

    static public bool[] DecodeByte(byte _2decode)
    {
        bool[] _inputs = new bool[8];

        //_inputs[7] => W
        //_inputs[6] => S
        //_inputs[5] => D
        //_inputs[4] => A
        //_inputs[3] => LEFT_CTRL
        //_inputs[2] => SPACE
        //_inputs[1] => LEFT_MOUSE
        //_inputs[0] => G

        //========================
        if (_2decode - 128 >= 0)
        {
            _inputs[7] = true;
            _2decode -= 128;
        }
        else
        {
            _inputs[7] = false;
        }
        //========================
        if (_2decode - 64 >= 0)
        {
            _inputs[6] = true;
            _2decode -= 64;
        }
        else
        {
            _inputs[6] = false;
        }
        //========================
        if (_2decode - 32 >= 0)
        {
            _inputs[5] = true;
            _2decode -= 32;
        }
        else
        {
            _inputs[5] = false;
        }
        //========================
        if (_2decode - 16 >= 0)
        {
            _inputs[4] = true;
            _2decode -= 16;
        }
        else
        {
            _inputs[4] = false;
        }
        //========================
        if (_2decode - 8 >= 0)
        {
            _inputs[3] = true;
            _2decode -= 8;
        }
        else
        {
            _inputs[3] = false;
        }
        //========================
        if (_2decode - 4 >= 0)
        {
            _inputs[2] = true;
            _2decode -= 4;
        }
        else
        {
            _inputs[2] = false;
        }
        //========================
        if (_2decode - 2 >= 0)
        {
            _inputs[1] = true;
            _2decode -= 2;
        }
        else
        {
            _inputs[1] = false;
        }
        //========================
        if (_2decode - 1 >= 0)
        {
            _inputs[0] = true;
        }
        else
        {
            _inputs[0] = false;
        }
        //========================

        return _inputs;
    }
}
