using UnityEngine;

public class GenericSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T _Instance;

    public static T Instance
    {
        get
        {
            if (_Instance == null)
            {
                _Instance = GameObject.FindObjectOfType<T>();
                if (_Instance == null)
                {
                    GameObject go = new GameObject();
                    _Instance = go.AddComponent<T>();
                }
            }
            return _Instance;
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        _Instance = null;
    }
}
