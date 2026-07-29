using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class CharactorMoventTest : MonoBehaviour
{
    public Camera camera;
    public Transform target;

    public float distance = 10f;
    public float height = 5f;
    public float moveSpeed = 5f;
    public float rotateSpeed = 5f;
    public float cameraSmoothSpeed = 10f; 

    public LayerMask collisionLayers = 1; 

    private CharacterController cc;

    public GameObject rigidGameObject;
    public GameObject emitPoint;

    private Animator animator;
    private AnimatorStateInfo currentBaseState;

    private bool isThrowing = false;
    public float throwAnimationTime = 0.5f; 
    private List<GameObject> rbs = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (target == null)
        {
            target = transform;
        }
        StartCoroutine(RemoveOldestObjectCoroutine());
    }

    // Update is called once per frame
    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        currentBaseState = animator.GetCurrentAnimatorStateInfo(0);


        if (!isThrowing)
        {
            Vector3 velocity = transform.forward * v * moveSpeed;
            cc.SimpleMove(velocity);

            transform.Rotate(Vector3.up * h * rotateSpeed * Time.deltaTime * 20f);

            animator.SetFloat("Blend", velocity.normalized.magnitude);
          
        }


        if (Input.GetKeyDown(KeyCode.Mouse0) && !isThrowing)
        {
            StartCoroutine(ThrowCoroutine());
        }
    }

    private IEnumerator RemoveOldestObjectCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);

            if (rbs.Count > 0)
            {
                GameObject oldestObject = rbs[0];
                rbs.RemoveAt(0);
                
                if (oldestObject != null)
                {
                    Destroy(oldestObject);
                }
            }
        }
    }

    private IEnumerator ThrowCoroutine()
    {
        isThrowing = true;
        

        animator.CrossFade("Throw", 0.1f);
        

        yield return new WaitForSeconds(throwAnimationTime);
        

        GameObject bullet = Instantiate(rigidGameObject, emitPoint.transform.position, emitPoint.transform.rotation);
        rbs.Add(bullet);


        if (bullet.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.velocity = (emitPoint.transform.forward+emitPoint.transform.up).normalized * 5f;
            rb.angularVelocity = Random.insideUnitSphere * 10f;
        }
        
        yield return new WaitForSeconds(0.5f);
        
        isThrowing = false;
    }

    void LateUpdate()
    {
        if (camera != null && target != null)
        {

            Vector3 lookAtTarget = target.position + Vector3.up * (height * 0.5f); 

            Vector3 targetPosition = target.position - target.forward * distance + Vector3.up * height;
            
            Vector3 finalPosition = targetPosition;

            RaycastHit hit;
            if (Physics.Linecast(lookAtTarget, targetPosition, out hit, collisionLayers))
            {

                finalPosition = hit.point + (lookAtTarget - targetPosition).normalized * 0.2f;
            }

            camera.transform.position = Vector3.Lerp(camera.transform.position, finalPosition, Time.deltaTime * cameraSmoothSpeed);
            
            camera.transform.LookAt(target);
        }
    }
}
