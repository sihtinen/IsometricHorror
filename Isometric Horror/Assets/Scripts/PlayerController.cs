using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement variables")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;

    public float speedSmoothTime = 0.5f;
    private float speedSmoothVelocity;
    private float currentSpeed = 0.0f;

    public float turnSmoothTime = 0.1f;
    private float turnSmoothVelocity;

    private Animator animator;
    private Camera mainCamera;
    private ParticleSystem particle_muzzleFlash;
    private AudioSource audio_HandgunFire;

    [Header("Camera variables")]
    public float cameraFollowSpeed = 0.1f;


    void Start()
    {
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;

        particle_muzzleFlash = GetComponentInChildren<ParticleSystem>();
        particle_muzzleFlash.Stop();

        audio_HandgunFire = particle_muzzleFlash.gameObject.GetComponent<AudioSource>();
    }

	// Update is called once per frame
	void Update ()
    {
        Movement();
        CameraControl();
    }

    void Movement()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 inputDir = input.normalized;

        if (inputDir != Vector2.zero)
        {
            float targetRotation = Mathf.Atan2(inputDir.x, inputDir.y) * Mathf.Rad2Deg + 45.0f;
            transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref turnSmoothVelocity, turnSmoothTime);
        }

        bool running = Input.GetKey(KeyCode.LeftShift);
        float targetSpeed;

        if (!Input.GetMouseButton(1))
        {
            transform.Translate(transform.forward * currentSpeed * Time.deltaTime, Space.World);
            targetSpeed = ((running) ? runSpeed : walkSpeed) * inputDir.magnitude;
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);
        }
        else
        {
            targetSpeed = walkSpeed * inputDir.magnitude * 0.8f;
            currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

            Vector3 moveDirection = new Vector3(inputDir.x, 0, inputDir.y);
            moveDirection = Quaternion.Euler(0, 45, 0) * moveDirection;

            float directionModifier = 1;

            Vector3 localMove = transform.InverseTransformDirection(moveDirection);

            float angle = Vector3.Angle(transform.forward, moveDirection);
            directionModifier = Remap(angle, 0, 140, 1, 0.43f);

            transform.Translate(moveDirection * currentSpeed * directionModifier * Time.deltaTime, Space.World);
            animator.SetFloat("rightDir", localMove.x);
            animator.SetFloat("forwardDir", localMove.z);

        }

        float animationSpeedPercent = ((running) ? 1 : 0.5f) * inputDir.magnitude;
        animator.SetFloat("speedPercent", animationSpeedPercent, speedSmoothTime, Time.deltaTime);
    }

    void CameraControl()
    {
        Vector3 cameraTargetPos = transform.position - mainCamera.transform.forward * 15.0f;
        Vector3 cameraTargetDir = cameraTargetPos - mainCamera.transform.position;
        mainCamera.transform.position += cameraTargetDir.normalized * cameraTargetDir.magnitude * cameraFollowSpeed * Time.deltaTime;

        if (Input.GetMouseButton(1))
        {
            animator.SetBool("aiming", true);

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100))
            {
                Vector3 lookDir = hit.point - transform.position;
                transform.LookAt(transform.position + lookDir, Vector3.up);
            }

            if (Input.GetMouseButtonDown(0))
            {
                Shoot();
            }
        }
        else
        {
            animator.SetBool("aiming", false);
        }
    }

    void Shoot()
    {
        particle_muzzleFlash.Play();
        audio_HandgunFire.Play();
    }

    float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }
}
