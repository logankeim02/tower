using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [Tooltip("The speed at which the bullet travels.")]
    [SerializeField] private float bulletSpeed = 10f; // Customizable bullet speed
    [Tooltip("The amount of damage this bullet deals to an enemy.")]
    [SerializeField] private int damageAmount = 1; // Customizable bullet damage
    [Tooltip("How long the bullet will exist before being destroyed if it doesn't hit anything.")]
    [SerializeField] private float lifetime = 3f; // Customizable bullet lifetime

    private Vector3 targetDirection; // Direction the bullet will travel

    /// <summary>
    /// Initializes the bullet with a direction.
    /// This should be called immediately after instantiating the bullet.
    /// </summary>
    /// <param name="direction">The normalized direction vector for the bullet.</param>
    public void SetDirection(Vector3 direction)
    {
        targetDirection = direction.normalized;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime); // Destroy the bullet after its lifetime
    }

    private void Update()
    {
        // Move the bullet in its target direction
        transform.position += targetDirection * bulletSpeed * Time.deltaTime;
    }

    /// <summary>
    /// Called when the bullet's trigger collider enters another collider.
    /// </summary>
    /// <param name="other">The other collider that was hit.</param>
    private void OnTriggerEnter(Collider other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(damageAmount); // Deal damage to the enemy
            Destroy(gameObject); // Destroy the bullet after hitting an enemy
        }
    }
}