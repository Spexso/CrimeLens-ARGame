using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;

public class FirebaseImportTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("ALL Firebase namespaces recognized!");
        Debug.Log("Firebase.App available");
        Debug.Log("Firebase.Auth available");
        Debug.Log("Firebase.Database available");
        Debug.Log("Firebase.Extensions available");
    }
}