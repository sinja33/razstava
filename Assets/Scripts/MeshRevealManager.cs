using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;

public class MeshRevealManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARMeshManager meshManager;
    [SerializeField] private Transform cameraTransform;
    
    [Header("Reveal Settings")]
    [SerializeField] private float revealSpeed = 5f;
    [SerializeField] private float maxRevealRadius = 50f;
    
    [Header("Materials")]
    [SerializeField] private Material revealMaterial;
    
    private float currentRevealRadius = 0f;
    private List<MeshRenderer> meshRenderers = new List<MeshRenderer>();
    private bool isRevealing = true;
    
    [Header("Coverage")]
    [SerializeField] private float targetCoverage = 0.8f;
    private float roomSize = 10f; // Approximate room size
    
    public float CurrentCoverage => Mathf.Clamp01(currentRevealRadius / roomSize);
    public bool IsComplete => CurrentCoverage >= targetCoverage;
    
    public System.Action OnScanningComplete;
    
    private bool hasTriggeredComplete = false;

    void Start()
    {
        if (meshManager == null)
            meshManager = FindObjectOfType<ARMeshManager>();
            
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;
        
        meshManager.meshesChanged += OnMeshesChanged;
        
        // Set initial reveal radius to 0
        UpdateMaterialRadius();
    }
    
    void OnDestroy()
    {
        if (meshManager != null)
            meshManager.meshesChanged -= OnMeshesChanged;
    }
    
    void OnMeshesChanged(ARMeshesChangedEventArgs args)
    {
        // Add new meshes
        foreach (var mesh in args.added)
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Create material instance
                renderer.material = new Material(revealMaterial);
                SetMaterialProperties(renderer.material);
                meshRenderers.Add(renderer);
            }
        }
        
        // Update removed
        foreach (var mesh in args.removed)
        {
            var renderer = mesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                meshRenderers.Remove(renderer);
            }
        }
    }
    
    void Update()
    {
        if (isRevealing)
        {
            // Expand reveal radius over time as player looks around
            currentRevealRadius += revealSpeed * Time.deltaTime;
            currentRevealRadius = Mathf.Min(currentRevealRadius, maxRevealRadius);
            
            UpdateMaterialRadius();
            
            // Check for completion
            if (IsComplete && !hasTriggeredComplete)
            {
                hasTriggeredComplete = true;
                OnScanningComplete?.Invoke();
                Debug.Log("Mesh scanning complete!");
            }
        }
    }
    
    void UpdateMaterialRadius()
    {
        Vector3 revealCenter = cameraTransform.position;
        
        foreach (var renderer in meshRenderers)
        {
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetVector("_RevealCenter", revealCenter);
                renderer.material.SetFloat("_RevealRadius", currentRevealRadius);
            }
        }
    }
    
    void SetMaterialProperties(Material mat)
    {
        mat.SetVector("_RevealCenter", cameraTransform.position);
        mat.SetFloat("_RevealRadius", currentRevealRadius);
    }
    
    public void StopRevealing()
    {
        isRevealing = false;
    }
}