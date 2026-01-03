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
    
    private Dictionary<ARPlane, PlaneRevealState> planeStates = new Dictionary<ARPlane, PlaneRevealState>();
    private bool transitionTriggered = false;
    
    public float CurrentCoverage { get; private set; }
    public bool IsComplete => CurrentCoverage >= targetCoverage;
    
    // Event for when scanning is complete
    public System.Action OnScanningComplete;
    
    private class PlaneRevealState
    {
        public bool isRevealed;
        public float revealProgress;
        public MeshRenderer renderer;
        public Material material;
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
        // Handle new planes
        foreach (var plane in args.added)
        {
            SetupPlane(plane);
        }
        
        // Handle removed planes
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
        
        // Create instance of material so we can fade individually
        var material = renderer.material;
        
        // Start invisible
        SetMaterialAlpha(material, 0f);
        
        planeStates[plane] = new PlaneRevealState
        {
            isRevealed = false,
            revealProgress = 0f,
            renderer = renderer,
            material = material
        };
    }
    
    void Update()
    {
        CheckGaze();
        UpdateFading();
        
        // Check for completion
        if (IsComplete && !transitionTriggered)
        {
            transitionTriggered = true;
            OnScanningComplete?.Invoke();
            Debug.Log("Scanning complete! Coverage: " + (CurrentCoverage * 100f) + "%");
        }
    }
    
    void CheckGaze()
    {
        foreach (var kvp in planeStates)
        {
            var plane = kvp.Key;
            var state = kvp.Value;
            
            if (state.isRevealed) continue;
            if (plane == null) continue;
            
            // Check if looking at this plane
            Vector3 toPlane = plane.center - cameraTransform.position;
            float distance = toPlane.magnitude;
            float angle = Vector3.Angle(cameraTransform.forward, toPlane);
            
            if (distance <= revealDistance && angle <= revealAngle)
            {
                // Start revealing
                state.isRevealed = true;
                Debug.Log("Revealing plane: " + plane.trackableId);
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
            
            SetMaterialAlpha(state.material, state.revealProgress * 0.5f); // 0.5 = target alpha
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
        Debug.Log($"Coverage: {revealed}/{planeStates.Count} = {CurrentCoverage * 100f}%");
    }
}