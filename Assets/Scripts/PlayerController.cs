using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float speed;
    public Vector3 playerVelocity;
    public float sensitivity;
    public Camera playerCamera;
    private float cameraPitch;
    public float playerRadious;
    public float jumpForce = 10;
    private Plane[] planes;
    private Vector3 inputDir;
    private PauseManager pauseManager;
    private bool jumpScheduled;
    private bool canJump;
    private bool wallJump;
    private Vector3 wallNormal;
    public float wallJumpForce = 5.0f;
    public float wallBuffer = 0.5f;
    public float playerGravity = 4.0f;

    // Start is called before the first frame update
    void Start()
    {
        cameraPitch = 0;
        planes = FindObjectsOfType<Plane>();
        pauseManager = FindObjectOfType<PauseManager>();
        jumpScheduled = false;
        canJump = false;
        wallJump = false;

    }

    private void FixedUpdate()
    {
        Vector3 newPosition = transform.position + playerVelocity;
        Vector3 newVelocity = playerVelocity;
        Vector3 refVelocity = Vector3.zero;
        newVelocity = Vector3.SmoothDamp(newVelocity, new Vector3(inputDir.x * speed * Time.fixedDeltaTime, playerVelocity.y, inputDir.z * speed * Time.fixedDeltaTime), ref refVelocity, 0.1f);
        newVelocity -= Vector3.up * playerGravity * Time.fixedDeltaTime;
        canJump = false;
        wallJump = false;

        foreach (var plane in planes)
        {
            float distance = Vector3.Dot(newPosition - plane.position, plane.normal);
            Vector3 projectedPos = newPosition - distance * plane.normal;
            Vector3 localPoint = projectedPos - plane.position;

            if (plane.ground && distance < playerRadious + wallBuffer && Mathf.Abs(Vector3.Dot(localPoint, plane.right)) <= plane.size.x * 0.5f && Mathf.Abs(Vector3.Dot(localPoint, plane.forward)) <= plane.size.y * 0.5f)
            {
                canJump = true;

                if (distance < playerRadious)
                {
                    newVelocity.y = 0;
                    float penetrationDepth = playerRadious - distance;
                    Vector3 correction = plane.normal * penetrationDepth;
                    newPosition += correction;
                }
            }

            if (!plane.ground && Mathf.Abs(distance) < playerRadious + wallBuffer && Mathf.Abs(Vector3.Dot(localPoint, plane.right)) <= plane.size.x * 0.5f && Mathf.Abs(Vector3.Dot(localPoint, plane.forward)) <= plane.size.y * 0.5f)
            {
                wallJump = true;
                wallNormal = plane.normal;
                newVelocity.y *= 0.9f;

                if (Mathf.Abs(distance) < playerRadious)
                {
                    float penetrationDepth = playerRadious - Mathf.Abs(distance);
                    Vector3 correction = plane.normal * penetrationDepth * Mathf.Sign(distance);
                    newPosition += correction;
                }
            }
        }

        

        if (jumpScheduled)
        {
            
            if (canJump)
            {
                newVelocity += Vector3.up * jumpForce * Time.fixedDeltaTime;
            } else if (wallJump)
            {
                newVelocity += wallNormal * wallJumpForce * Time.fixedDeltaTime;
                Debug.Log(Vector3.Dot(playerVelocity, wallNormal));
                newVelocity += Vector3.up * (jumpForce - Vector3.Dot(playerVelocity, wallNormal) * 20.0f) * Time.fixedDeltaTime  ;
            }

        }

        transform.position = newPosition;
        playerVelocity = newVelocity;
        jumpScheduled = false;
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, playerRadious);
        Vector3 direction = playerCamera.transform.TransformDirection(Vector3.forward) * 5;
        Gizmos.DrawRay(transform.position, direction);
    }

    void Update()
    {
        if (!pauseManager.isPaused)
        {
            float inputX = Input.GetAxis("Horizontal");
            float inputZ = Input.GetAxis("Vertical");
            inputDir = new Vector3(inputX, 0f, inputZ).normalized;
            inputDir = transform.TransformDirection(inputDir);

            float mouseX = Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

            if (Input.GetButtonDown("Jump"))
            {
                jumpScheduled = true;
            }

            transform.Rotate(Vector3.up, mouseX);
            cameraPitch -= mouseY;
            cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);
            playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }

    }
}
