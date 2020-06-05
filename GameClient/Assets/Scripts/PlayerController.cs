using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


public class PlayerController : MonoBehaviour
{
    [HideInInspector]
    public InputPack inputs;
    [HideInInspector]
    public ServerSnapshot currentServerSnapshot;
    public ClientState currentClientState;

    [SerializeField]
    private int currentTick = 0;


    private Queue<ClientState> clientStateBuffer = new Queue<ClientState>();
    private Queue<InputPack> inputHistory = new Queue<InputPack>();
    public Queue<ServerSnapshot> snapshots = new Queue<ServerSnapshot>();

    [HideInInspector]
    public Vector3 posError;
    [SerializeField]
    float posErrorMagnitude;
    public float posErrorTolerance = 3f;

    Vector3 fixedPosError;
    Vector3 lastSnapshotPos;

    private int lastRecievedTick = 0;

    ClientInputPredictor predictor;
    InputMover inputMover;

    //DEBUG
    public GameObject serverPlayer;
    public GameObject serverPos;
    public GameObject playerPos;
    public GameObject playerPosTrack;
    Vector3 beforeInputMoving;
    Vector3 afterInputMoving;

    private void Awake()
    {
        InitializeData();
    }

    private void Update()
    {
        Instantiate(playerPosTrack, transform.position, Quaternion.identity);
    }

    private void FixedUpdate()
    {
        Debug.Log($"CURRENT TICK: {currentTick}");
        UpdateInputPack();
        predictor.inputs = DecodeByte(inputs.input);
        SendInputToServer(inputs);
        predictor.Movement();
        StoreClientState();
        handlePosErrorIfSnapshotsAvailable();
        #region DEBUG
        serverPlayer.transform.position = lastSnapshotPos;
        posErrorMagnitude = posError.magnitude;
        #endregion 
        Debug.Log($"================== End of tick {currentTick} ====================");
        currentTick++;
    }

    /// <summary>Sends player input to the server.</summary>
    private void SendInputToServer(InputPack _pack)
    {
        ClientSend.PlayerMovement(_pack);
    }

    public byte ByteInput()
    {
        byte input = 0;
        if (Input.GetKey(KeyCode.W)) input += 1;
        if (Input.GetKey(KeyCode.S)) input += 2;
        if (Input.GetKey(KeyCode.D)) input += 4;
        if (Input.GetKey(KeyCode.A)) input += 8;
        if (Input.GetKey(KeyCode.LeftControl)) input += 16;
        if (Input.GetKey(KeyCode.Space)) input += 32;
        if (Input.GetKey(KeyCode.Mouse0)) input += 64;
        if (Input.GetKey(KeyCode.G)) input += 128;
        return input;
    }

    public struct InputPack
    {
        public byte input;
        public Quaternion camRotation;
        public Quaternion orientation;
        public int tick;
    }

    public struct ClientState
    {
        public Vector3 position;
        public int tick;
    }

    public struct ServerSnapshot
    {
        public Vector3 position;
        public Quaternion camRotation;
        public Quaternion orientation;
        public Vector3 velocity;
        public int tick;
    }

    private void UpdateInputPack()
    {
        inputs.input = ByteInput();
        inputs.camRotation = LocalCameraController.instance.playerCam.rotation;
        inputs.orientation = LocalCameraController.instance.orientation.rotation;
        inputs.tick = currentTick;
        inputHistory.Enqueue(inputs);
    }

    private void DequeueDeprecatedInputPacks()
    {
        if (inputHistory.Count != 0)
        {
            while (inputHistory.Peek().tick < lastRecievedTick)
            {
                inputHistory.Dequeue();
            }
        }
        else
        {
            Debug.LogError("IDK WTF HAPPENED");
        }
    }

    private void StoreClientState()
    {
        currentClientState.position = transform.position;
        currentClientState.tick = currentTick;
        clientStateBuffer.Enqueue(currentClientState);
    }

    private void StoreClientState(int _tick)
    {
        currentClientState.position = transform.position;
        currentClientState.tick = _tick;
        clientStateBuffer.Enqueue(currentClientState);
    }

    private void InitializeData()
    {
        posError = Vector3.zero;
        predictor = GetComponent<ClientInputPredictor>();
        inputMover = GetComponent<InputMover>();
        #region DEBUG
        serverPlayer = GameObject.FindGameObjectWithTag("ServerPlayerDebug");
        lastSnapshotPos = Vector3.zero;
        #endregion
    }

    private void handlePosErrorIfSnapshotsAvailable()
    {
        ServerSnapshot snapshot;
        ClientState state;

        if (snapshots.Count > 0 && clientStateBuffer.Count > 0)
        {
            snapshot = snapshots.Dequeue();
            state = clientStateBuffer.Peek();


            while (snapshots.Count > 1)
            {
                snapshot = snapshots.Dequeue();
                Debug.LogWarning($"Dequeued deprecated server snapshots");
            }

            while (state.tick < snapshot.tick)
            {
                if (clientStateBuffer.Count != 0)
                {
                    state = clientStateBuffer.Dequeue();
                    Debug.Log($"Dequeued deprecated clientStates");
                }
                else
                {
                    Debug.LogError($"RAN OUT OF CLIENT PACKS?");
                }
            }
            if (state.tick != snapshot.tick)
            {
                Debug.LogError("Critcal error: comparing wrong ticks");
            }
            if (lastRecievedTick + 1 != snapshot.tick)
            {
                Debug.LogError($"Missed {currentTick - lastRecievedTick} tick(s)");
            }
            Debug.Log($"COMPARING:  client: {state.tick}; server: {snapshot.tick}");
            Debug.Log($"AVAILABLE SNAPSHOTS: client: {clientStateBuffer.Count}; server: {snapshots.Count}");

            lastSnapshotPos = snapshot.position;
            lastRecievedTick = snapshot.tick;

            posError = calculatePosError(state.position, snapshot.position);

            if (posError.sqrMagnitude > posErrorTolerance * posErrorTolerance)
            {
                Debug.LogError("Input prediction error");

                DequeueDeprecatedInputPacks();

                UnityEngine.Debug.DrawLine(state.position, snapshot.position, Color.green, 144f);
                Instantiate(playerPos, state.position, Quaternion.identity);
                Instantiate(serverPos, snapshot.position, Quaternion.identity);

                Physics.autoSimulation = false;
                transform.position = snapshot.position;
                predictor.orientation.rotation = snapshot.orientation;
                predictor.playerCam.rotation = snapshot.camRotation;
                predictor.rb.velocity = snapshot.velocity;
                clientStateBuffer.Clear();
                //StoreClientState(snapshot.tick);
                Queue<InputPack> inputHistory2exec = new Queue<InputPack>(inputHistory);
                InputPack currentInputPack;
                beforeInputMoving = snapshot.position;
                while (inputHistory2exec.Count != 0)
                {
                    Vector3 prevPos = transform.position;
                    currentInputPack = inputHistory2exec.Dequeue();
                    Debug.LogWarning($"Executing physStep no. {currentInputPack.tick}");
                    inputMover.Movement(currentInputPack);
                    Physics.Simulate(Time.fixedDeltaTime);
                    StoreClientState(currentInputPack.tick);
                    //UnityEngine.Debug.DrawLine(prevPos, transform.position, Color.yellow, 144f);
                }
                UnityEngine.Debug.DrawLine(beforeInputMoving, transform.position, Color.red, 144f);
                afterInputMoving = transform.position;
                Physics.autoSimulation = true;
            }
        }
        else
        {
            if(snapshots.Count == 0)
            {
                Debug.LogWarning($"No usable snapshots: tick {currentTick}");
            }
            if(clientStateBuffer.Count == 0)
            {
                Debug.LogError($"Ran out of clientstates (cringe) tick: {currentTick}");
            }
        }
    }

    private Vector3 calculatePosError(Vector3 _clientPos, Vector3 _serverPos)
    {
        return _serverPos - _clientPos;
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
