// using UnityEngine;
// using UnityEngine.XR.ARFoundation;

// #if UNITY_ANDROID
// using UnityEngine.Android;
// #endif

// public class ScenePermissionRequester : MonoBehaviour
// {
//     const string SCENE_PERMISSION = "com.oculus.permission.USE_SCENE";
    
//     [SerializeField] private ARPlaneManager planeManager;

//     void Start()
//     {
//         // Disable plane manager until we have permission
//         if (planeManager != null)
//             planeManager.enabled = false;
            
//         RequestPermission();
//     }

//     void RequestPermission()
//     {
// #if UNITY_ANDROID
//         if (Permission.HasUserAuthorizedPermission(SCENE_PERMISSION))
//         {
//             Debug.Log("Scene permission already granted");
//             EnableSpatialFeatures();
//         }
//         else
//         {
//             Debug.Log("Requesting scene permission...");
//             var callbacks = new PermissionCallbacks();
//             callbacks.PermissionGranted += OnPermissionGranted;
//             callbacks.PermissionDenied += OnPermissionDenied;
//             Permission.RequestUserPermission(SCENE_PERMISSION, callbacks);
//         }
// #endif
//     }

//     void OnPermissionGranted(string permission)
//     {
//         Debug.Log("Permission granted: " + permission);
//         EnableSpatialFeatures();
//     }

//     void OnPermissionDenied(string permission)
//     {
//         Debug.Log("Permission denied: " + permission);
//     }

//     void EnableSpatialFeatures()
//     {
//         if (planeManager != null)
//             planeManager.enabled = true;
//     }
// }

using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class ScenePermissionRequester : MonoBehaviour
{
    const string SCENE_PERMISSION = "com.oculus.permission.USE_SCENE";

    void Start()
    {
        Debug.Log("ScenePermissionRequester: Start called");
        RequestPermission();
    }

    void RequestPermission()
    {
#if UNITY_ANDROID
        if (Permission.HasUserAuthorizedPermission(SCENE_PERMISSION))
        {
            Debug.Log("Scene permission already granted");
        }
        else
        {
            Debug.Log("Requesting scene permission...");
            Permission.RequestUserPermission(SCENE_PERMISSION);
        }
#endif
    }
}