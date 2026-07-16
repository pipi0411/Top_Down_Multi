using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;
    [SerializeField] private RoomTemplate roomTemplates;
    public RoomTemplate RoomTemplates => roomTemplates;
    
    private void Awake()
    {
        Instance = this;
    }
}
