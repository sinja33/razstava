using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class PlaneRevealManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Transform cameraTransform;
    
    [Header("Reveal Settings")]
    [SerializeField] private float revealDistance = 10f;
    [SerializeField] private float revealAngle = 30f;
    [SerializeField] private float fadeInDuration = 1f;
    
    [Header("Coverage")]
    [SerializeField] private float targetCoverage = 0.8f;
    
    [Header("Solidify Materials")]
    [SerializeField] private Material floorSolidMaterial;
    [SerializeField] private Material wallSolidMaterial;
    [SerializeField] private float solidifyDuration = 2f;

    [Header("Passthrough Control")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float passthroughFadeDuration = 1f;
    
    private Dictionary<ARPlane, PlaneRevealState> planeStates = new Dictionary<ARPlane, PlaneRevealState>();
    private bool transitionTriggered = false;
    private bool isSolidifying = false;
    private float solidifyProgress = 0f;
    
    public float CurrentCoverage { get; private set; }
    public bool IsComplete => CurrentCoverage >= targetCoverage;
    public bool IsSolidified => solidifyProgress >= 1f;

    private bool isFadingPassthrough = false;
    private float passthroughFadeProgress = 0f;
    
    // Events
    public System.Action OnScanningComplete;
    public System.Action OnSolidifyComplete;
    
    private class PlaneRevealState
    {
        public bool isRevealed;
        public float revealProgress;
        public MeshRenderer renderer;
        public Material material;
        public Material originalMaterial;
        public bool isFloor;
    }
    
    void Start()
    {
        if (planeManager == null)
            planeManager = FindObjectOfType<ARPlaneManager>();
            
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        
        planeManager.planesChanged += OnPlanesChanged;
    }
    
    void OnDestroy()
    {
        if (planeManager != null)
            planeManager.planesChanged -= OnPlanesChanged;
    }
    
    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        foreach (var plane in args.added)
        {
            SetupPlane(plane);
        }
        
        foreach (var plane in args.removed)
        {
            if (planeStates.ContainsKey(plane))
                planeStates.Remove(plane);
        }
        
        UpdateCoverage();
    }
    
    void SetupPlane(ARPlane plane)
    {
        var renderer = plane.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        
        var material = renderer.material;
        
        // Check if this is a floor (horizontal plane facing up, below camera)
        bool isHorizontal = Mathf.Abs(Vector3.Dot(plane.normal, Vector3.up)) > 0.7f;
        bool isBelowCamera = plane.center.y < cameraTransform.position.y - 0.5f;
        bool isFloor = isHorizontal && isBelowCamera;
        
        Debug.Log($"Plane detected: horizontal={isHorizontal}, belowCamera={isBelowCamera}, isFloor={isFloor}, classification={plane.classification}");
        
        // Start invisible
        SetMaterialAlpha(material, 0f);
        
        planeStates[plane] = new PlaneRevealState
        {
            isRevealed = false,
            revealProgress = 0f,
            renderer = renderer,
            material = material,
            originalMaterial = material,
            isFloor = isFloor
        };
    }
    
    void Update()
    {
        if (!isSolidifying)
        {
            CheckGaze();
            UpdateFading();
            
            if (IsComplete && !transitionTriggered)
            {
                transitionTriggered = true;
                OnScanningComplete?.Invoke();
                Debug.Log("Scanning complete! Starting solidify...");
                StartSolidify();
            }
        }
        else
        {
            UpdateSolidify();
        }

        UpdatePassthroughFade();
    }
    
    void CheckGaze()
    {
        foreach (var kvp in planeStates)
        {
            var plane = kvp.Key;
            var state = kvp.Value;
            
            if (state.isRevealed) continue;
            if (plane == null) continue;
            
            Vector3 toPlane = plane.center - cameraTransform.position;
            float distance = toPlane.magnitude;
            float angle = Vector3.Angle(cameraTransform.forward, toPlane);
            
            if (distance <= revealDistance && angle <= revealAngle)
            {
                state.isRevealed = true;
                UpdateCoverage();
            }
        }
    }
    
    void UpdateFading()
    {
        foreach (var state in planeStates.Values)
        {
            if (!state.isRevealed) continue;
            if (state.revealProgress >= 1f) continue;
            
            state.revealProgress += Time.deltaTime / fadeInDuration;
            state.revealProgress = Mathf.Clamp01(state.revealProgress);
            
            SetMaterialAlpha(state.material, state.revealProgress * 0.5f);
        }
    }
    
    void SetMaterialAlpha(Material mat, float alpha)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            Color color = mat.GetColor("_BaseColor");
            color.a = alpha;
            mat.SetColor("_BaseColor", color);
        }
    }
    
    void UpdateCoverage()
    {
        if (planeStates.Count == 0)
        {
            CurrentCoverage = 0f;
            return;
        }
        
        int revealed = 0;
        foreach (var state in planeStates.Values)
        {
            if (state.isRevealed) revealed++;
        }
        
        CurrentCoverage = (float)revealed / planeStates.Count;
    }
    
    // ===== SOLIDIFY =====
    
    public void StartSolidify()
    {
        isSolidifying = true;
        solidifyProgress = 0f;
        
        // Swap materials to solid versions
        foreach (var kvp in planeStates)
        {
            var state = kvp.Value;
            if (state.renderer == null) continue;
            
            Material targetMaterial = state.isFloor ? floorSolidMaterial : wallSolidMaterial;
            
            if (targetMaterial != null)
            {
                // Create instance so we can fade it
                Material matInstance = new Material(targetMaterial);
                SetMaterialAlpha(matInstance, 0f);
                state.renderer.material = matInstance;
                state.material = matInstance;
            }
        }
    }
    
    void UpdateSolidify()
    {
        solidifyProgress += Time.deltaTime / solidifyDuration;
        solidifyProgress = Mathf.Clamp01(solidifyProgress);
        
        foreach (var state in planeStates.Values)
        {
            if (state.renderer == null) continue;
            
            // Fade from transparent to fully opaque
            SetMaterialAlpha(state.material, solidifyProgress);
            
            // Also need to change surface type to opaque at the end
            if (solidifyProgress >= 1f && state.material.HasProperty("_Surface"))
            {
                state.material.SetFloat("_Surface", 0); // 0 = Opaque
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
    
    // Manual trigger for testing
    [ContextMenu("Force Complete Scanning")]
    public void ForceCompleteScan()
    {
        foreach (var state in planeStates.Values)
        {
            state.isRevealed = true;
            state.revealProgress = 1f;
        }
        CurrentCoverage = 1f;
        transitionTriggered = true;
        OnScanningComplete?.Invoke();
        StartSolidify();
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
    }

    void UpdatePassthroughFade()
    {
        if (!isFadingPassthrough) return;
        
        passthroughFadeProgress += Time.deltaTime / passthroughFadeDuration;
        passthroughFadeProgress = Mathf.Clamp01(passthroughFadeProgress);
        
        if (mainCamera != null)
        {
            // Fade from transparent (passthrough) to opaque black (VR)
            mainCamera.backgroundColor = new Color(0, 0, 0, passthroughFadeProgress);
        }
        
        if (passthroughFadeProgress >= 1f)
        {
            isFadingPassthrough = false;
            Debug.Log("Passthrough disabled - now in full VR");
        }
    }
}