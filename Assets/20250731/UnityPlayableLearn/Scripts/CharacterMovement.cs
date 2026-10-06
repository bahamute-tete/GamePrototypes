
using System;
using UnityEngine;

[RequireComponent(typeof(CharacterAnimationDemo))]
public class CharacterMovement : MonoBehaviour
{
    [Min(0.01f)]
    public float moveSpeed = 2f;
    [Min(0.01f)]
    public float runSpeed = 4f;

    [Min(0.01f)]
    public float acceleration = 6f;

    [Min(0.01f)]
    public float deceleration = 10f;

    public float turnSpeed = 540f;

    [SerializeField]
    private Vector3 currentVelocity;

    private CharacterAnimationDemo animationDemo;

    private void Awake()
    {
        animationDemo = GetComponent<CharacterAnimationDemo>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        Vector3 input = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical"));

        input = Vector3.ClampMagnitude(input, 1f);

        // 输入决定希望达到的速度。
        bool wantsToRun = Input.GetKey(KeyCode.LeftShift);

        float walkSpeed = Mathf.Max(0.01f, moveSpeed);
        float sprintSpeed = Mathf.Max(walkSpeed + 0.01f, runSpeed);

        float targetSpeed = wantsToRun ? sprintSpeed : walkSpeed;

        Vector3 targetVelocity = input * targetSpeed;

        // 有输入时调整速度；松开后逐渐减速。
        float rate = input.sqrMagnitude > 0.001f
            ? acceleration
            : deceleration;

        currentVelocity = Vector3.MoveTowards(
            currentVelocity,
            targetVelocity,
            rate * dt);

        // 用当前速度移动。
        transform.position += currentVelocity * dt;

        // 朝实际移动方向转身。
        if (currentVelocity.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                currentVelocity.normalized,
                Vector3.up);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * dt);
        }

      

        float speed = currentVelocity.magnitude;
        float blend;

        if (speed <= walkSpeed)
        {
            // 静止 → 走路速度，对应 Idle → Walk。
            blend = Mathf.InverseLerp(0f, walkSpeed, speed) * 0.5f;
        }
        else
        {
            // 走路速度 → 跑步速度，对应 Walk → Run。
            blend = 0.5f
                + Mathf.InverseLerp(walkSpeed, sprintSpeed, speed) * 0.5f;
        }

        animationDemo.SetMoveBlend(blend);
    }

    private void OnDisable()
    {
        currentVelocity = Vector3.zero;

        if (animationDemo != null)
        {
            animationDemo.SetMoveBlend(0f);
        }
    }
}