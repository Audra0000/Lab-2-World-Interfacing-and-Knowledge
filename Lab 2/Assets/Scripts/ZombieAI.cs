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
    [SerializeField] private Camera visionCamera;
    [SerializeField] private LayerMask visionMask;
    [SerializeField] private float visionRange = 4f;
    [SerializeField] private float visionCheckInterval = 0.2f;
    private float timeSinceVisionCheck = 0f;
    private Transform playerTransform;

    [Header("Communication Settings")]
    [SerializeField] private float communicationRange = 15f;
    [SerializeField] private string zombieTag = "Zombie";

    [Header("Stuck Detection")]
    [SerializeField] private float stuckCheckInterval = 1f;
    [SerializeField] private float stuckDistanceThreshold = 0.5f;
    [SerializeField] private float stuckTimeThreshold = 3f;
    private Vector3 lastPosition;
    private float timeSinceLastCheck = 0f;
    private float timeStuck = 0f;

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

        if (visionCamera == null)
        {
            Debug.LogWarning("Vision Camera not assigned on " + gameObject.name + ". Vision system will not work.");
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
        lastPosition = transform.position;
    }

    void Update()
    {
        UpdateAnimation();
        CheckVisionWithFrustum();
        CheckIfStuck();

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
            if (CanSeePlayerWithFrustum())
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

    // Vision system using Frustum Culling and Raycast
    void CheckVisionWithFrustum()
    {
        if (playerTransform == null || visionCamera == null) return;

        // Throttle vision checks for performance
        timeSinceVisionCheck += Time.deltaTime;
        if (timeSinceVisionCheck < visionCheckInterval) return;
        timeSinceVisionCheck = 0f;

        // Check distance first (early rejection)
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > visionRange) return;

        // Use OverlapSphere to detect objects within range
        Collider[] colliders = Physics.OverlapSphere(transform.position, visionRange, visionMask);
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(visionCamera);

        foreach (Collider col in colliders)
        {
            // Check if it's the player
            if (col.gameObject != playerTransform.gameObject) continue;

            // Test if object is within camera frustum
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, col.bounds))
            {
                // Perform raycast to check line of sight from camera position
                // Apuntar al centro del collider en lugar de la posición del transform
                Vector3 playerCenter = col.bounds.center;
                Vector3 directionToPlayer = (playerCenter - visionCamera.transform.position).normalized;
                float distanceToCameraPosition = Vector3.Distance(visionCamera.transform.position, playerCenter);
                RaycastHit hit;

                if (Physics.Raycast(visionCamera.transform.position, directionToPlayer, out hit, distanceToCameraPosition + 0.5f, visionMask))
                {
                    // Check if we hit the player
                    if (hit.collider.gameObject == playerTransform.gameObject)
                    {
                        OnPlayerSeen(playerTransform.position);
                    }
                }
            }
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

    bool CanSeePlayerWithFrustum()
    {
        if (playerTransform == null || visionCamera == null) return false;

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > visionRange) return false;

        // Calculate frustum planes
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(visionCamera);

        // Get player collider
        Collider playerCollider = playerTransform.GetComponent<Collider>();
        if (playerCollider == null) return false;

        // Test if player is within frustum
        if (!GeometryUtility.TestPlanesAABB(frustumPlanes, playerCollider.bounds))
            return false;

        // Raycast to check line of sight from camera position
        // Apuntar al centro del collider del jugador
        Vector3 playerCenter = playerCollider.bounds.center;
        Vector3 directionToPlayer = (playerCenter - visionCamera.transform.position).normalized;
        float actualDistance = Vector3.Distance(visionCamera.transform.position, playerCenter);
        RaycastHit hit;

        if (Physics.Raycast(visionCamera.transform.position, directionToPlayer, out hit, actualDistance + 0.5f, visionMask))
        {
            return hit.collider.gameObject == playerTransform.gameObject;
        }

        return false;
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

    // Stuck detection system
    void CheckIfStuck()
    {
        timeSinceLastCheck += Time.deltaTime;

        if (timeSinceLastCheck >= stuckCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);

            // If zombie has barely moved and should be moving
            if (distanceMoved < stuckDistanceThreshold && agent.hasPath && !agent.pathPending)
            {
                timeStuck += timeSinceLastCheck;

                if (timeStuck >= stuckTimeThreshold)
                {
                    Debug.Log(gameObject.name + " got stuck! Finding new route...");
                    HandleStuckState();
                    timeStuck = 0f;
                }
            }
            else
            {
                timeStuck = 0f;
            }

            lastPosition = transform.position;
            timeSinceLastCheck = 0f;
        }
    }

    void HandleStuckState()
    {
        // Try to find a new path
        agent.ResetPath();

        switch (currentState)
        {
            case ZombieState.Searching:
                SetRandomSearchDestination();
                break;
            case ZombieState.Chasing:
                // If stuck while chasing, try a position near the target
                Vector3 offset = Random.insideUnitSphere * 3f;
                offset.y = 0;
                Vector3 newTarget = chaseTarget + offset;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(newTarget, out hit, 5f, NavMesh.AllAreas))
                {
                    agent.SetDestination(hit.position);
                }
                else
                {
                    // If can't find alternative, return to searching
                    isChasingPlayer = false;
                    ChangeState(ZombieState.Searching);
                }
                break;
        }
    }

    // Gizmos visualization
    void OnDrawGizmos()
    {
        if (visionCamera != null)
        {
            // Draw vision range sphere
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, visionRange);

            // Draw camera frustum
            Gizmos.color = Color.blue;
            Gizmos.matrix = visionCamera.transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero, visionCamera.fieldOfView, visionRange, visionCamera.nearClipPlane, visionCamera.aspect);
            Gizmos.matrix = Matrix4x4.identity;

            // Draw frustum planes for better visualization
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(visionCamera);
            DrawFrustumPlanes(planes);

            // Draw line to player if visible
            if (playerTransform != null && CanSeePlayerWithFrustum())
            {
                Gizmos.color = Color.red;
                Collider playerCollider = playerTransform.GetComponent<Collider>();
                Vector3 targetPos = playerCollider != null ? playerCollider.bounds.center : playerTransform.position;
                Gizmos.DrawLine(visionCamera.transform.position, targetPos);
            }
        }

        // Communication range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, communicationRange);
    }

    void OnDrawGizmosSelected()
    {
        // More detailed frustum visualization when selected
        if (visionCamera != null)
        {
            DrawDetailedFrustum();
        }
    }

    void DrawFrustumPlanes(Plane[] planes)
    {
        // Draw frustum edges more clearly
        if (visionCamera == null) return;

        Vector3 camPos = visionCamera.transform.position;
        Vector3 camForward = visionCamera.transform.forward;
        Vector3 camUp = visionCamera.transform.up;
        Vector3 camRight = visionCamera.transform.right;

        float halfHeight = Mathf.Tan(visionCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * visionRange;
        float halfWidth = halfHeight * visionCamera.aspect;

        Vector3 farCenter = camPos + camForward * visionRange;
        Vector3 farTopLeft = farCenter + camUp * halfHeight - camRight * halfWidth;
        Vector3 farTopRight = farCenter + camUp * halfHeight + camRight * halfWidth;
        Vector3 farBottomLeft = farCenter - camUp * halfHeight - camRight * halfWidth;
        Vector3 farBottomRight = farCenter - camUp * halfHeight + camRight * halfWidth;

        // Draw frustum lines
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawLine(camPos, farTopLeft);
        Gizmos.DrawLine(camPos, farTopRight);
        Gizmos.DrawLine(camPos, farBottomLeft);
        Gizmos.DrawLine(camPos, farBottomRight);

        // Draw far plane rectangle
        Gizmos.DrawLine(farTopLeft, farTopRight);
        Gizmos.DrawLine(farTopRight, farBottomRight);
        Gizmos.DrawLine(farBottomRight, farBottomLeft);
        Gizmos.DrawLine(farBottomLeft, farTopLeft);
    }

    void DrawDetailedFrustum()
    {
        Vector3 camPos = visionCamera.transform.position;
        Vector3 camForward = visionCamera.transform.forward;
        Vector3 camUp = visionCamera.transform.up;
        Vector3 camRight = visionCamera.transform.right;

        // Draw multiple depth slices for better depth perception
        for (float depth = visionCamera.nearClipPlane; depth <= visionRange; depth += visionRange / 5f)
        {
            float halfHeight = Mathf.Tan(visionCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth;
            float halfWidth = halfHeight * visionCamera.aspect;

            Vector3 center = camPos + camForward * depth;
            Vector3 topLeft = center + camUp * halfHeight - camRight * halfWidth;
            Vector3 topRight = center + camUp * halfHeight + camRight * halfWidth;
            Vector3 bottomLeft = center - camUp * halfHeight - camRight * halfWidth;
            Vector3 bottomRight = center - camUp * halfHeight + camRight * halfWidth;

            Gizmos.color = new Color(1, 1, 0, 0.2f);
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
    }

    void UpdateAnimation()
    {
        bool isMoving = agent.velocity.magnitude > 0.1f;
        animator.SetBool(isWalkingHash, isMoving);
    }
}