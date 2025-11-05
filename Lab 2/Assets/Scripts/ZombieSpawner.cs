using UnityEngine;

public class ZombieSpawnerArea : MonoBehaviour
{
    public GameObject zombiePrefab;
    public int zombieCount = 10;   
    private BoxCollider boxCollider;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
        SpawnZombies();
    }

    void SpawnZombies()
    {
        for (int i = 0; i < zombieCount; i++)
        {
            Vector3 randomPos = GetRandomPointInBox();

            Instantiate(zombiePrefab, randomPos, Quaternion.identity);
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
