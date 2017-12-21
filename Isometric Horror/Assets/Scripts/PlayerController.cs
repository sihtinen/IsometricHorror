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
    private CharacterController characterController;

    private ParticleSystem particle_muzzleFlash;
    private AudioSource audio_HandgunFire;

    [Header("Camera variables")]
    public float cameraFollowSpeed = 0.1f;

    public Transform WatchTarget;
    public LayerMask OccluderMask;
    public Material HiderMaterial;

    private Dictionary<Transform, Material> _LastTransforms;

    void Start()
    {
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
        characterController = GetComponent<CharacterController>();

        particle_muzzleFlash = GetComponentInChildren<ParticleSystem>();
        particle_muzzleFlash.Stop();

        audio_HandgunFire = particle_muzzleFlash.gameObject.GetComponent<AudioSource>();

        _LastTransforms = new Dictionary<Transform, Material>();
    }

	// Update is called once per frame
	void Update ()
    {
        Movement();
        CameraControl();
        CheckForOcclusion();
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
            Vector3 moveDirection = new Vector3(inputDir.x, 0, inputDir.y);
            moveDirection = Quaternion.Euler(0, 45, 0) * moveDirection;
            Vector3 vel = moveDirection * currentSpeed;
            characterController.Move(vel * Time.deltaTime);

            if (running)
            {
                currentSpeed = Mathf.SmoothDamp(currentSpeed, runSpeed, ref speedSmoothVelocity, speedSmoothTime);
            }
            else
            {
                currentSpeed = Mathf.SmoothDamp(currentSpeed, walkSpeed, ref speedSmoothVelocity, speedSmoothTime);
            }
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

            //transform.Translate(moveDirection * currentSpeed * directionModifier * Time.deltaTime, Space.World);
            Vector3 vel = moveDirection * currentSpeed * directionModifier;
            characterController.Move(vel * Time.deltaTime);

            animator.SetFloat("rightDir", localMove.x);
            animator.SetFloat("forwardDir", localMove.z);

        }

        float realVel = new Vector2(characterController.velocity.x, characterController.velocity.z).magnitude;
        float animationSpeedPercent = ((running) ? realVel / runSpeed : realVel / walkSpeed * 0.5f);
        animator.SetFloat("speedPercent", animationSpeedPercent, speedSmoothTime, Time.deltaTime);
    }

    void CameraControl()
    {
        Vector3 cameraTargetPos = transform.position - mainCamera.transform.forward * 25.0f;
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

    void CheckForOcclusion()
    {
        //Cast a ray from this object's transform the the watch target's transform.
        RaycastHit[] hits = Physics.RaycastAll
        (
            mainCamera.transform.position,
            (transform.position - mainCamera.transform.position).normalized,
            Vector3.Distance(mainCamera.transform.position, transform.position),
            OccluderMask
        );

        Debug.DrawLine(mainCamera.transform.position, transform.position);

        List<Transform> makeTransparent = new List<Transform>();

        //Loop through all overlapping objects and disable their mesh renderer
        if (hits.Length > 0)
        {
            foreach (RaycastHit hit in hits)
            {
                bool continueLoop = true;

                if (_LastTransforms.ContainsKey(hit.transform))
                {
                    makeTransparent.Add(hit.transform);
                    continueLoop = false;
                }

                if (continueLoop)
                {
                    if (hit.collider.gameObject.transform != WatchTarget && hit.collider.transform.root != WatchTarget)
                    {
                        _LastTransforms.Add(hit.collider.gameObject.transform, hit.collider.gameObject.GetComponent<Renderer>().material);
                        makeTransparent.Add(hit.transform);
                        hit.collider.gameObject.GetComponent<Renderer>().material = HiderMaterial;
                        hit.collider.gameObject.GetComponent<Renderer>().material.SetFloat("_Transparency", 0.49f);
                    }
                }
            }
        }

        List<Transform> temp = new List<Transform>(_LastTransforms.Keys);

        if (temp.Count > 0)
        {
            foreach (Transform t in temp)
            {
                float value = t.gameObject.GetComponent<Renderer>().material.GetFloat("_Transparency");

                bool decrease = false;

                if (makeTransparent.Count > 0)
                {
                    if (makeTransparent.Contains(t))
                    {
                        if (value > 0.1f)
                        {
                            value -= Time.deltaTime * 4f;

                            if (value > 0.1f)
                            {
                                value = 0.1f;
                            }

                            t.gameObject.GetComponent<Renderer>().material.SetFloat("_Transparency", value);
                        }
                        else
                        {
                            t.gameObject.GetComponent<Renderer>().material.SetFloat("_Transparency", 0.1f);
                        }
                    }
                    else
                    {
                        decrease = true;
                    }
                }
                else
                {
                    decrease = true;
                }

                if (decrease)
                {
                    value += Time.deltaTime * 2f;
                    t.gameObject.GetComponent<Renderer>().material.SetFloat("_Transparency", value);

                    if (value >= 0.5f)
                    {
                        t.GetComponent<Renderer>().material = _LastTransforms[t];
                        _LastTransforms.Remove(t);

                        Debug.Log("REMOVED");
                    }
                }
            }
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
