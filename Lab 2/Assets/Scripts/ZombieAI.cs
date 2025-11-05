using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private Animator animator;
    private int isWalkingHash;

    private enum ZombieState
    {
        Searching,
        Chasing
    }

    private ZombieState currentState = ZombieState.Searching;

    [Header("Search Settings")]
    [SerializeField] private float searchRadius = 20f;
    [SerializeField] private float minSearchWaitTime = 2f;
    [SerializeField] private float maxSearchWaitTime = 5f;
    private float searchTimer = 0f;

    [Header("Chase Settings")]
    [SerializeField] private float chaseTimeout = 0.5f;
    private float chaseTimer = 0f;
    private Vector3 chaseTarget;
    private bool isChasingPlayer = false;

    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 8f;
    [SerializeField] private float visionAngle = 100f;
    private Transform playerTransform;

    [Header("Communication Settings")]
    [SerializeField] private float communicationRange = 15f;
    [SerializeField] private string zombieTag = "Zombie";

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
            Debug.LogError("Animator not found " + gameObject.name);
        }

        // Find player in scene
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        else
        {
            Debug.LogWarning("Player object with tag 'Player' not found");
        }

        ChangeState(ZombieState.Searching);
    }

    void Update()
    {
        UpdateAnimation();
        CheckVision();

        switch (currentState)
        {
            case ZombieState.Searching:
                UpdateSearching();
                break;
            case ZombieState.Chasing:
                UpdateChasing();
                break;
        }
    }

    void ChangeState(ZombieState newState)
    {
        // Exit previous state
        switch (currentState)
        {
            case ZombieState.Searching:
                searchTimer = 0f;
                break;
            case ZombieState.Chasing:
                chaseTimer = 0f;
                break;
        }

        currentState = newState;

        // Enter new state
        switch (newState)
        {
            case ZombieState.Searching:
                SetRandomSearchDestination();
                break;
            case ZombieState.Chasing:
                agent.SetDestination(chaseTarget);
                break;
        }
    }

    // Searching behavior
    void UpdateSearching()
    {
        // If reached destination
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            searchTimer += Time.deltaTime;

            if (searchTimer >= Random.Range(minSearchWaitTime, maxSearchWaitTime))
            {
                SetRandomSearchDestination();
                searchTimer = 0f;
            }
        }
    }

    void SetRandomSearchDestination()
    {
        Vector3 randomDirection = Random.insideUnitSphere * searchRadius;
        randomDirection += transform.position;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, searchRadius, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    // Chasing behavior
    void UpdateChasing()
    {
        // If chasing player directly
        if (isChasingPlayer && playerTransform != null)
        {
            // Update position only if currently seeing player
            if (CanSeePlayer())
            {
                chaseTarget = playerTransform.position;
                agent.SetDestination(chaseTarget);

                BroadcastPlayerPosition(playerTransform.position);
            }
            else
            {
                // When reaching last known position, return to searching
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
                {
                    isChasingPlayer = false;
                    ChangeState(ZombieState.Searching);
                }
            }
        }
        else
        {
            // Chasing a fixed position (smell or alert from another zombie)
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                ChangeState(ZombieState.Searching);
            }
        }
    }

    // Smell detection
    public void OnSmellDetected(Vector3 smellPosition)
    {
        if (isChasingPlayer) return;

        chaseTarget = smellPosition;
        isChasingPlayer = false;
        ChangeState(ZombieState.Chasing);
    }

    // Vision system
    void CheckVision()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer > visionRange) return;

        // Calculate angle between zombie forward and player
        Vector3 directionFlat = new Vector3(directionToPlayer.x, 0, directionToPlayer.z);
        Vector3 forwardFlat = new Vector3(transform.forward.x, 0, transform.forward.z);
        float angleToPlayer = Vector3.Angle(forwardFlat, directionFlat);

        // Check if within vision cone
        if (angleToPlayer <= visionAngle / 2f)
        {
            OnPlayerSeen(playerTransform.position);
        }
    }

    void OnPlayerSeen(Vector3 playerPosition)
    {
        if (currentState != ZombieState.Chasing || !isChasingPlayer)
        {
            Debug.Log(gameObject.name + " Player spotted!");
            chaseTarget = playerPosition;
            isChasingPlayer = true;
            ChangeState(ZombieState.Chasing);

            BroadcastPlayerPosition(playerPosition);
        }
    }

    bool CanSeePlayer()
    {
        if (playerTransform == null) return false;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        float distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer > visionRange) return false;

        Vector3 directionFlat = new Vector3(directionToPlayer.x, 0, directionToPlayer.z);
        Vector3 forwardFlat = new Vector3(transform.forward.x, 0, transform.forward.z);
        float angleToPlayer = Vector3.Angle(forwardFlat, directionFlat);

        return angleToPlayer <= visionAngle / 2f;
    }

    // Communication system between zombies
    void BroadcastPlayerPosition(Vector3 playerPosition)
    {
        GameObject[] allZombies = GameObject.FindGameObjectsWithTag(zombieTag);

        int notifiedCount = 0;

        foreach (GameObject zombie in allZombies)
        {
            if (zombie == gameObject) continue;

            float distance = Vector3.Distance(transform.position, zombie.transform.position);
            if (distance <= communicationRange)
            {
                zombie.BroadcastMessage("PlayerLocalizado", playerPosition, SendMessageOptions.DontRequireReceiver);
                notifiedCount++;
            }
        }

        if (notifiedCount > 0)
        {
            Debug.Log(gameObject.name + " Notified " + notifiedCount + " nearby zombies");
        }
    }

    public void PlayerLocalizado(Vector3 ultimaPosicionVisto)
    {
        if (isChasingPlayer) return;

        Debug.Log(gameObject.name + " Alert received - investigating location");

        chaseTarget = ultimaPosicionVisto;
        isChasingPlayer = false;
        ChangeState(ZombieState.Chasing);
    }

    // Gizmos visualization
    void OnDrawGizmos()
    {
        // Vision cone
        Vector3 leftBoundary = Quaternion.Euler(0, -visionAngle / 2f, 0) * transform.forward * visionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, visionAngle / 2f, 0) * transform.forward * visionRange;

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        // Communication range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, communicationRange);
    }

    void UpdateAnimation()
    {
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool(isWalkingHash, isMoving);
    }
}