using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class MeshRevealManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARMeshManager meshManager;
    [SerializeField] private Transform cameraTransform;
    
    [Header("Reveal Settings")]
    [SerializeField] private float revealRadius = 2f;
    [SerializeField] private float edgeSoftness = 0.5f;
    
    [Header("Materials")]
    [SerializeField] private Material revealMaterial;
    
    [Header("Coverage")]
    [SerializeField] private float targetCoverage = 0.8f;
    [SerializeField] private float roomSize = 5f;

    [Header("Solidify Materials")]
    [SerializeField] private Material floorSolidMaterial;
    [SerializeField] private Material wallSolidMaterial;
    [SerializeField] private float solidifyDuration = 2f;

    [Header("Passthrough Control")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float passthroughFadeDuration = 1f;
    
    private List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
    private List<Vector3> revealedPoints = new List<Vector3>();
    private Vector3 lastRecordedPosition;
    private float minDistanceBetweenPoints = 0.5f;
    
    private bool hasTriggeredComplete = false;
    
    public float CurrentCoverage { get; private set; }
    public bool IsComplete => CurrentCoverage >= targetCoverage;

    private bool isSolidifying = false;
    private float solidifyProgress = 0f;
    private bool isFadingPassthrough = false;
    private float passthroughFadeProgress = 0f;

    public System.Action OnSolidifyComplete;
    
    public System.Action OnScanningComplete;

    void Start()
    {
        if (meshManager == null)
            meshManager = FindObjectOfType<ARMeshManager>();
            
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        
        meshManager.meshesChanged += OnMeshesChanged;
        
        // Record starting position
        lastRecordedPosition = cameraTransform.position;
        revealedPoints.Add(lastRecordedPosition);
    }
    
    void OnDestroy()
    {
        if (meshManager != null)
            meshManager.meshesChanged -= OnMeshesChanged;
    }
    
    void OnMeshesChanged(ARMeshesChangedEventArgs args)
    {
        foreach (var mesh in args.added)
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = new Material(revealMaterial);
                meshRenderers.Add(renderer);
            }
        }
        
        foreach (var mesh in args.removed)
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                meshRenderers.Remove(renderer);
            }
        }
        
        // Update all materials with current revealed points
        UpdateAllMaterials();
    }
    
    void Update()
    {
        if (!isSolidifying)
        {
            // Track coverage based on walking
            float distanceFromLast = Vector3.Distance(cameraTransform.position, lastRecordedPosition);
            
            if (distanceFromLast >= minDistanceBetweenPoints)
            {
                revealedPoints.Add(cameraTransform.position);
                lastRecordedPosition = cameraTransform.position;
                UpdateCoverage();
            }
            
            // Check completion
            if (IsComplete && !hasTriggeredComplete)
            {
                hasTriggeredComplete = true;
                OnScanningComplete?.Invoke();
                Debug.Log("Mesh scanning complete! Starting solidify...");
                StartSolidify();
            }
        }
        else
        {
            UpdateSolidify();
        }
        
        UpdatePassthroughFade();
        
       
    }

    void UpdateCurrentPosition()
    {
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetVector("_CurrentPos", cameraTransform.position);
            }
        }
    }
    
    void UpdateAllMaterials()
    {
        // Convert points to array for shader
        Vector4[] pointsArray = new Vector4[64]; // Max 64 points
        int count = Mathf.Min(revealedPoints.Count, 64);
        
        for (int i = 0; i < count; i++)
        {
            pointsArray[i] = new Vector4(
                revealedPoints[i].x,
                revealedPoints[i].y,
                revealedPoints[i].z,
                1f
            );
        }
        
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetFloat("_RevealRadius", revealRadius);
                renderer.material.SetFloat("_EdgeSoftness", edgeSoftness);
                renderer.material.SetInt("_PointCount", count);
                renderer.material.SetVectorArray("_RevealPoints", pointsArray);
            }
        }
    }
    
    void UpdateCoverage()
    {
        // Estimate coverage based on revealed points spread
        if (revealedPoints.Count < 2)
        {
            CurrentCoverage = 0f;
            return;
        }
        
        // Calculate bounding area of revealed points
        Vector3 min = revealedPoints[0];
        Vector3 max = revealedPoints[0];
        
        foreach (var point in revealedPoints)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
        
        float revealedArea = (max.x - min.x) * (max.z - min.z);
        float roomArea = roomSize * roomSize;
        
        CurrentCoverage = Mathf.Clamp01(revealedArea / roomArea);
    }
    
    public void StopRevealing()
    {
        enabled = false;
    }


    // ===== SOLIDIFY =====
    public void StartSolidify()
    {
        isSolidifying = true;
        solidifyProgress = 0f;
        
        // Swap materials to solid versions
        foreach (var renderer in meshRenderers)
        {
            if (renderer == null) continue;
            
            // Determine if floor or wall based on mesh position/normal
            bool isFloor = IsFloorMesh(renderer);
            Material targetMaterial = isFloor ? floorSolidMaterial : wallSolidMaterial;
            
            if (targetMaterial != null)
            {
                Material matInstance = new Material(targetMaterial);
                SetMaterialTransparent(matInstance, 0f);
                renderer.material = matInstance;
            }
        }
    }

    bool IsFloorMesh(MeshRenderer renderer)
    {
        // Check if mesh is below camera (floor level)
        float meshY = renderer.bounds.center.y;
        float cameraY = cameraTransform.position.y;
        
        return meshY < cameraY - 0.5f;
    }

    void SetMaterialTransparent(Material mat, float alpha)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            Color color = mat.GetColor("_BaseColor");
            color.a = alpha;
            mat.SetColor("_BaseColor", color);
        }
        
        // Set surface type to transparent
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1); // 1 = Transparent
        }
    }

    void UpdateSolidify()
    {
        solidifyProgress += Time.deltaTime / solidifyDuration;
        solidifyProgress = Mathf.Clamp01(solidifyProgress);
        
        foreach (var renderer in meshRenderers)
        {
            if (renderer == null) continue;
            
            // Fade from transparent to opaque
            SetMaterialTransparent(renderer.material, solidifyProgress);
            
            // Make fully opaque at the end
            if (solidifyProgress >= 1f && renderer.material.HasProperty("_Surface"))
            {
                renderer.material.SetFloat("_Surface", 0); // 0 = Opaque
            }
        }
        
        if (solidifyProgress >= 1f)
        {
            isSolidifying = false;
            DisablePassthrough();
            OnSolidifyComplete?.Invoke();
            Debug.Log("Solidify complete! Now in VR room.");
        }
    }

    // ===== PASSTHROUGH CONTROL =====

    public void DisablePassthrough()
    {
        isFadingPassthrough = true;
        passthroughFadeProgress = 0f;
    }

    public void EnablePassthrough()
    {
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0, 0, 0, 0);
        }
        isFadingPassthrough = false;
    }

    void UpdatePassthroughFade()
    {
        if (!isFadingPassthrough) return;
        
        passthroughFadeProgress += Time.deltaTime / passthroughFadeDuration;
        passthroughFadeProgress = Mathf.Clamp01(passthroughFadeProgress);
        
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = new Color(0, 0, 0, passthroughFadeProgress);
        }
        
        if (passthroughFadeProgress >= 1f)
        {
            isFadingPassthrough = false;
            Debug.Log("Passthrough disabled - now in full VR");
        }
    }

    // Manual trigger for testing
    [ContextMenu("Force Complete Scanning")]
    public void ForceCompleteScan()
    {
        CurrentCoverage = 1f;
        hasTriggeredComplete = true;
        OnScanningComplete?.Invoke();
        StartSolidify();
    }
}