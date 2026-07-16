using UnityEngine;

public class FishController : MonoBehaviour
{
    [Header("Target Points")]
    public Transform homePoint;
    public Transform bitePoint;

    [Header("Movement")]
    public float swimSpeed = 0.2f;
    public float rotateSpeed = 3f;

    [Header("Behaviour")]
    public float waitAtHome = 2f;
    public float waitAtBite = 1f;

    private bool goingToBite = true;
    private float waitTimer = 0f;

    void Start()
    {
        if (homePoint != null)
        {
            transform.position = homePoint.position;
            transform.rotation = homePoint.rotation;
        }
    }

    void Update()
    {
        Transform target = goingToBite ? bitePoint : homePoint;

        if (target == null)
            return;

        MoveTowardsTarget(target);
    }

    void MoveTowardsTarget(Transform target)
    {
        Vector3 direction = target.position - transform.position;

        // 转向
        if (direction.magnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.deltaTime);
        }

        // 移动
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            swimSpeed * Time.deltaTime);

        // 是否抵达
        if (Vector3.Distance(transform.position, target.position) < 0.01f)
        {
            waitTimer += Time.deltaTime;

            if (goingToBite)
            {
                if (waitTimer >= waitAtBite)
                {
                    goingToBite = false;
                    waitTimer = 0f;
                }
            }
            else
            {
                if (waitTimer >= waitAtHome)
                {
                    goingToBite = true;
                    waitTimer = 0f;
                }
            }
        }
        else
        {
            waitTimer = 0f;
        }
    }
}
