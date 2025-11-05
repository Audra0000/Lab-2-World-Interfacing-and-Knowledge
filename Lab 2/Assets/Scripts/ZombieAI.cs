using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private int isWalkingHash;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        isWalkingHash = Animator.StringToHash("isWalking");

        if (agent == null)
        {
            Debug.LogError("NavMeshAgent not found " + gameObject.name);
        }

        if (animator == null)
        {
            Debug.LogError("Animator not found" + gameObject.name);
        }
    }

    void Update()
    {
        UpdateAnimation();
    }

    public void OnSmellDetected(Vector3 smellPosition)
    {
        agent.SetDestination(smellPosition);
        Debug.Log("Zombie" + gameObject.name + " found blood: " + smellPosition);
    }

    void UpdateAnimation()
    {
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool(isWalkingHash, isMoving);
    }
}