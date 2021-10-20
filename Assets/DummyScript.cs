using UnityEngine;

public class DummyScript : MonoBehaviour
{
    void Start()
    {
        GetComponent<KMSelectable>().Children[0].OnInteract += () => { GetComponent<KMBombModule>().HandlePass(); return false; };
    }
}
