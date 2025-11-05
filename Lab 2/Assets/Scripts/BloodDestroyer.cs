using UnityEngine;

public class Blood : MonoBehaviour
{
    void Start()
    {
        // Autodestruirse despu¨¦s de 4 segundos
        Destroy(gameObject, 4f);
    }
}
