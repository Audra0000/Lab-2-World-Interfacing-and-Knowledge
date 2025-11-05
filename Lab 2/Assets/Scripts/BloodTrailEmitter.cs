using UnityEngine;

public class BloodTrailEmitter : MonoBehaviour
{
    [SerializeField] private GameObject bloodMarkerPrefab;
    [SerializeField] private float emissionInterval = 1.5f;

    private float timer = 0f;
    private Vector3 lastPosition;
    private float minDistanceToEmit = 0.5f;

    void Start()
    {
        lastPosition = transform.position;

        if (bloodMarkerPrefab == null)
        {
            Debug.LogError("Prefab not assigned");
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        if (timer >= emissionInterval && distanceMoved > minDistanceToEmit)
        {
            EmitBloodMarker();
            timer = 0f;
            lastPosition = transform.position;
        }
    }

    void EmitBloodMarker()
    {
        GameObject marker = Instantiate(bloodMarkerPrefab, transform.position, Quaternion.identity);
        Debug.Log("Blood created: " + transform.position + " | Object: " + marker.name);
    }
}