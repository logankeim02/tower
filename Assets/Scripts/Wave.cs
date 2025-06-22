using UnityEngine;

[CreateAssetMenu(fileName = "New Wave", menuName = "Tower Defense/Wave")]
public class Wave : ScriptableObject
{
    [Tooltip("A list of enemy groups that will be spawned in this wave.")]
    public EnemyGroup[] enemyGroups;

    /// <summary>
    /// Gets the total number of enemies that will be spawned in this wave.
    /// </summary>
    public int TotalEnemies
    {
        get
        {
            int total = 0;
            foreach (var group in enemyGroups)
            {
                total += group.count;
            }
            return total;
        }
    }
}