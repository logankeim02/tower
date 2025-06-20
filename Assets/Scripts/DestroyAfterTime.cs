using UnityEngine;

public class DestroyAfterTime : MonoBehaviour
{
    [Tooltip("Time in seconds before this object is destroyed.")]
    [SerializeField] private float lifetime = 5f; // Customizable lifetime

    void Start()
    {
        Destroy(gameObject, lifetime); // Destroy this GameObject after 'lifetime' seconds
    }
}