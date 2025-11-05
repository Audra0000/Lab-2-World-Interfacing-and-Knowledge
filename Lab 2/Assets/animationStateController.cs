using UnityEngine;
using UnityEngine.AI;

public class ZombieAnimationController : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;
    private int isWalkingHash;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        isWalkingHash = Animator.StringToHash("isWalking");
    }

    void Update()
    {
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool(isWalkingHash, isMoving);
    }
}
