using UnityEngine;

public class ZombieSpawnerArea : MonoBehaviour
{
    public GameObject zombiePrefab;
    public int zombieCount = 10;

    [Header("Zombie Layer Configuration")]
    [SerializeField] private string zombieLayerName = "Zombie";

    [Header("Vision Camera Setup")]
    [SerializeField] private float cameraFOV = 90f;
    [SerializeField] private float cameraNearClip = 0.3f;
    [SerializeField] private float cameraFarClip = 10f;
    [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0, 1.5f, 0.2f);
    [SerializeField] private bool disableCameraRendering = true;

    [Header("Vision Layer Mask - IMPORTANTE")]
    [SerializeField] private LayerMask visionLayerMask = -1; // Everything by default
    [Tooltip("Los zombies pueden ver estos layers. DEBE incluir el layer del Player!")]

    private BoxCollider boxCollider;
    private int zombieLayer = -1;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider>();

        // Obtener el layer ID
        zombieLayer = LayerMask.NameToLayer(zombieLayerName);
        if (zombieLayer == -1)
        {
            Debug.LogWarning($"⚠️ Layer '{zombieLayerName}' no existe. Créalo en Project Settings > Tags and Layers");
        }

        // Verificar que el LayerMask incluya al jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            int playerLayer = player.layer;
            if ((visionLayerMask.value & (1 << playerLayer)) == 0)
            {
                Debug.LogError($"❌ CRITICAL: Vision LayerMask no incluye el layer del Player ({LayerMask.LayerToName(playerLayer)})! Los zombies NO podrán ver al jugador!");
            }
            else
            {
                Debug.Log($"✓ Vision LayerMask incluye el layer del Player ({LayerMask.LayerToName(playerLayer)})");
            }
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
            }

            // Configurar la cámara de visión
            SetupVisionCamera(zombie);

            // Configurar el LayerMask de visión
            ZombieAI zombieAI = zombie.GetComponent<ZombieAI>();
            if (zombieAI != null)
            {
                // Usar reflexión para asignar el visionMask
                var maskField = typeof(ZombieAI).GetField("visionMask",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                if (maskField != null)
                {
                    maskField.SetValue(zombieAI, visionLayerMask);
                    Debug.Log($"✓ Vision LayerMask asignado a {zombie.name}");
                }
            }

            Debug.Log($"✓ Zombie {zombie.name} spawneado en layer '{zombieLayerName}' en posición {randomPos}");
        }

        Debug.Log($"========== SPAWNING COMPLETO: {zombieCount} zombies creados ==========");
    }

    void SetupVisionCamera(GameObject zombie)
    {
        // Buscar si ya tiene una cámara
        Camera existingCamera = zombie.GetComponentInChildren<Camera>();

        if (existingCamera != null)
        {
            // Si ya tiene cámara, solo configurarla
            ConfigureCamera(existingCamera);
            AssignCameraToZombieAI(zombie, existingCamera);
        }
        else
        {
            // Crear nueva cámara
            GameObject cameraObject = new GameObject("VisionCamera");
            cameraObject.transform.SetParent(zombie.transform);
            cameraObject.transform.localPosition = cameraLocalPosition;
            cameraObject.transform.localRotation = Quaternion.identity;

            Camera visionCamera = cameraObject.AddComponent<Camera>();
            ConfigureCamera(visionCamera);
            AssignCameraToZombieAI(zombie, visionCamera);
        }
    }

    void AssignCameraToZombieAI(GameObject zombie, Camera camera)
    {
        ZombieAI zombieAI = zombie.GetComponent<ZombieAI>();
        if (zombieAI != null)
        {
            // Usar reflexión para asignar la cámara (funciona con campos públicos o privados)
            var cameraField = typeof(ZombieAI).GetField("visionCamera",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (cameraField != null)
            {
                cameraField.SetValue(zombieAI, camera);
                Debug.Log($"✓ Cámara de visión asignada a {zombie.name}");
            }
            else
            {
                Debug.LogError($"❌ No se pudo encontrar el campo 'visionCamera' en ZombieAI!");
            }
        }
        else
        {
            Debug.LogError($"❌ ZombieAI component not found on {zombie.name}!");
        }
    }

    void ConfigureCamera(Camera cam)
    {
        cam.fieldOfView = cameraFOV;
        cam.nearClipPlane = cameraNearClip;
        cam.farClipPlane = cameraFarClip;

        // Desactivar el renderizado para que no consuma recursos
        if (disableCameraRendering)
        {
            cam.enabled = false;
            cam.cullingMask = 0; // No renderiza nada
        }

        // Configuración adicional para reducir overhead
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.allowDynamicResolution = false;
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

    void OnDrawGizmos()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(bc.center, bc.size);

            Gizmos.color = new Color(1f, 0.5f, 0f); // naranja
            Gizmos.DrawWireCube(bc.center, bc.size);
        }
    }
}