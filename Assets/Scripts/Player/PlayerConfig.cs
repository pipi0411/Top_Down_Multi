using UnityEngine;

[CreateAssetMenu]
public class PlayerConfig : ScriptableObject
{
    [Header("Data")]
    public int Level;
    public string Name;
    public Sprite Icon;
    [Header("Values")]
    public float CurrentHealth;
    public float MaxHealth;
    public float Armor;
    public float MaxArmor;
    public float Energy;
    public float MaxEnergy;
    public float CriticalChance;
    public float CriticalDamage;
}
