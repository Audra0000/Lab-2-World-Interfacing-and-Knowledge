using UnityEngine;

public class ZombieSmellSensor : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Smell Source"))
        {
            ZombieAI zombieAI = GetComponentInParent<ZombieAI>();

            if (zombieAI != null)
            {
                zombieAI.OnSmellDetected(other.transform.position);
            }
        }
    }
}
