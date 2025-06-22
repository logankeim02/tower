using UnityEngine;

[System.Serializable]
public class EnemyGroup
{
    [Tooltip("The type of enemy to spawn in this group.")]
    public GameObject enemyPrefab;

    [Tooltip("The number of enemies of this type to spawn.")]
    [Min(1)]
    public int count;
}