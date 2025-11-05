using UnityEngine;

public class ZombieSpawnerArea : MonoBehaviour
{
    public GameObject zombiePrefab;
    public int zombieCount = 10;

    [Header("Zombie Layer Configuration")]
    [SerializeField] private string zombieLayerName = "Zombie";

    private BoxCollider boxCollider;
    private int zombieLayer = -1;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();

        // Obtener el layer ID
        zombieLayer = LayerMask.NameToLayer(zombieLayerName);
        if (zombieLayer == -1)
        {
            Debug.LogWarning($"Layer '{zombieLayerName}' no existe. Créalo en Project Settings > Tags and Layers");
        }

        SpawnZombies();
    }

    void SpawnZombies()
    {
        for (int i = 0; i < zombieCount; i++)
        {
            Vector3 randomPos = GetRandomPointInBox();
            GameObject zombie = Instantiate(zombiePrefab, randomPos, Quaternion.identity);

            // Asignar el layer al zombie para que funcione la comunicación
            if (zombieLayer != -1)
            {
                zombie.layer = zombieLayer;
                Debug.Log($"Zombie {zombie.name} configurado en layer '{zombieLayerName}'");
            }
        }
    }

    Vector3 GetRandomPointInBox()
    {
        Vector3 center = boxCollider.center + transform.position;
        Vector3 size = boxCollider.size;

        float randomX = Random.Range(-size.x / 2, size.x / 2);
        float randomY = Random.Range(-size.y / 2, size.y / 2);
        float randomZ = Random.Range(-size.z / 2, size.z / 2);

        return center + new Vector3(randomX, randomY, randomZ);
    }
}
