using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Player : MonoBehaviour
{
    #region Movement variables


    //Assingables
    public Transform playerCam;
    public Transform orientation;

    //Other
    public Rigidbody rb;

    //Movement
    public float moveSpeed = 4500;
    public float maxSpeed = 20;
    public bool grounded;
    public LayerMask whatIsGround;

    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    //Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;
    public float slideForce = 400;
    public float slideCounterMovement = 0.2f;
    private bool isAlreadyCrouched = false;

    //Jumping
    private bool readyToJump = true;
    private float jumpCooldown = 0.25f;
    public float jumpForce = 550f;

    //Input
    float x, y;
    bool jumping, sprinting, crouching;

    //Sliding
    private Vector3 normalVector = Vector3.up;
    private Vector3 wallNormalVector;

    #endregion


    public int id;
    public string username;
    private bool[] inputs;
    public int currentTick;
    public int currentClientTick;

    public struct InputPack
    {
        public bool[] input;
        public Quaternion camRotation;
        public Quaternion orientation;
        public int client_tick;
    }

    public InputPack newInputPack;
    public Queue<InputPack> inputBuffer;
    public InputPack currentInputPack;

    public uint inputBufferSize = 4;
    public short initBuffer = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        InitializeData();
    }

    void Start()
    {
        playerScale = transform.localScale;
    }


    private void FixedUpdate()
    {
        if (initBuffer >= inputBufferSize)
        {
            SetInput(UseInputPack());
            ResolveInput();
            Movement();
            SendMovementData();
        }
        else
        {
            initBuffer++;
        }

        currentTick++;
        if (inputBuffer.Count < inputBufferSize - 1) Debug.LogWarning($"Input buffer is low: currently holding {inputBuffer.Count}");
        if (inputBuffer.Count > inputBufferSize) Debug.LogWarning($"Input buffer is high: currently holding {inputBuffer.Count}");
    }

    private void SendMovementData()
    {
        ServerSend.PlayerMovement(this); //this void is stupid
    }

    /// <summary>Passes a input pack to execution.</summary>
    /// <param name="_inputs">The new key inputs.</param>
    /// <param name="_rotation">The new rotation.</param>
    public void SetInput(InputPack _pack)
    {
        inputs = _pack.input;
        playerCam.rotation = _pack.camRotation;
        orientation.rotation = _pack.orientation;
        currentClientTick = _pack.client_tick;
    }

    public void ResolveInput()
    {
        if (inputs[0])
        {
            y = 1;
        }
        else if (inputs[1])
        {
            y = -1;
        }
        else
        {
            y = 0;
        }
        if (inputs[2])
        {
            x = 1;
        }
        else if (inputs[3])
        {
            x = -1;
        }
        else
        {
            x = 0;
        }
        crouching = inputs[4];
        jumping = inputs[5];
    }

    /// <summary>Stores an arrived InputPack the inputBuffer.</summary>
    /// <param name="_inputs">The key inputs.</param>
    /// <param name="_orientation">The player orientation.</param>
    /// <param name="_camRotation">The player camera rotation.</param>
    /// <param name="_tick">The tick the client is currently on (server's perspective).</param>
    public void QueueIncomingInputPack(bool[] _inputs, Quaternion _camRotation, Quaternion _orientation, int _tick)
    {
        newInputPack.input = _inputs;
        newInputPack.camRotation = _camRotation;
        newInputPack.orientation = _orientation;
        newInputPack.client_tick = _tick;
        inputBuffer.Enqueue(newInputPack);
    }
    public InputPack UseInputPack()
    {
        if (inputBuffer.Count != 0)
        {
            currentInputPack = inputBuffer.Dequeue();
        }
        else
        {
            Debug.LogError("INPUTBUFFER DRAINED");
        }

        return currentInputPack;
    }

    public void InitializeData()
    {
        newInputPack.input = new bool[8];
        inputBuffer = new Queue<InputPack>();
    }

    public void InitializeClient(int _id, string _username)
    {
        id = _id;
        username = _username;
        inputs = new bool[6];
    }

    #region Movement functions

    private void Crouch()
    {
        if (!isAlreadyCrouched)
        {
            transform.localScale = crouchScale;
            transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
            if (rb.velocity.magnitude > 0.5f)
            {
                if (grounded)
                {
                    rb.AddForce(orientation.transform.forward * slideForce);
                }
            }
            isAlreadyCrouched = true;
        }
        else
        {
            return;
        }
    }

    private void Stand()
    {
        if (isAlreadyCrouched)
        {
            transform.localScale = playerScale;
            transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
            isAlreadyCrouched = false;
        }
        else
        {
            return;
        }
    }

    private void Movement()
    {
        //Extra gravity
        rb.AddForce(Vector3.down * Time.deltaTime * 10);

        //Find actual velocity relative to where player is looking
        Vector2 mag = FindVelRelativeToLook();
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        CounterMovement(x, y, mag);

        //If holding jump && ready to jump, then jump
        if (readyToJump && jumping) Jump();

        //Set max speed
        float maxSpeed = this.maxSpeed;

        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (crouching && grounded && readyToJump)
        {
            rb.AddForce(Vector3.down * Time.deltaTime * 3000);
            return;
        }

        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (x > 0 && xMag > maxSpeed) x = 0;
        if (x < 0 && xMag < -maxSpeed) x = 0;
        if (y > 0 && yMag > maxSpeed) y = 0;
        if (y < 0 && yMag < -maxSpeed) y = 0;

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;

        // Movement in air
        if (!grounded)
        {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        // Movement while sliding
        //if (grounded && crouching) { multiplierV = 0.5f; multiplier = 0.5f; }

        //Apply forces to move player
        rb.AddForce(orientation.transform.forward * y * moveSpeed * Time.deltaTime * multiplier * multiplierV);
        rb.AddForce(orientation.transform.right * x * moveSpeed * Time.deltaTime * multiplier);
        Debug.Log($"Applied motion at client tick no. {currentClientTick}");

    }

    private void Jump()
    {
        if (grounded && readyToJump)
        {
            readyToJump = false;

            //Add jump forces
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);

            //If jumping while falling, reset y velocity.
            Vector3 vel = rb.velocity;
            if (rb.velocity.y < 0.5f)
                rb.velocity = new Vector3(vel.x, 0, vel.z);
            else if (rb.velocity.y > 0)
                rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);

            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private void CounterMovement(float x, float y, Vector2 mag)
    {
        if (!grounded || jumping) return;

        //Slow down sliding
        if (crouching)
        {
            rb.AddForce(moveSpeed * Time.deltaTime * -rb.velocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0))
        {
            rb.AddForce(moveSpeed * orientation.transform.right * Time.deltaTime * -mag.x * counterMovement);
        }
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0))
        {
            rb.AddForce(moveSpeed * orientation.transform.forward * Time.deltaTime * -mag.y * counterMovement);
        }

        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (Mathf.Sqrt((Mathf.Pow(rb.velocity.x, 2) + Mathf.Pow(rb.velocity.z, 2))) > maxSpeed)
        {
            float fallspeed = rb.velocity.y;
            Vector3 n = rb.velocity.normalized * maxSpeed;
            rb.velocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    /// <summary>
    /// Find the velocity relative to where the player is looking
    /// Useful for vectors calculations regarding movement and limiting movement
    /// </summary>
    /// <returns></returns>
    public Vector2 FindVelRelativeToLook()
    {
        float lookAngle = orientation.transform.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);

        return new Vector2(xMag, yMag);
    }

    private bool IsFloor(Vector3 v)
    {
        float angle = Vector3.Angle(Vector3.up, v);
        return angle < maxSlopeAngle;
    }

    private bool cancellingGrounded;

    /// <summary>
    /// Handle ground detection
    /// </summary>
    private void OnCollisionStay(Collision other)
    {
        //Make sure we are only checking for walkable layers
        int layer = other.gameObject.layer;
        if (whatIsGround != (whatIsGround | (1 << layer))) return;

        //Iterate through every collision in a physics update
        for (int i = 0; i < other.contactCount; i++)
        {
            Vector3 normal = other.contacts[i].normal;
            //FLOOR
            if (IsFloor(normal))
            {
                grounded = true;
                cancellingGrounded = false;
                normalVector = normal;
                CancelInvoke(nameof(StopGrounded));
            }
        }

        //Invoke ground/wall cancel, since we can't check normals with CollisionExit
        float delay = 3f;
        if (!cancellingGrounded)
        {
            cancellingGrounded = true;
            Invoke(nameof(StopGrounded), Time.deltaTime * delay);
        }
    }

    private void StopGrounded()
    {
        grounded = false;
    }

    #endregion
}
