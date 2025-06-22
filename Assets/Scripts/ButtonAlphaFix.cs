using UnityEngine;
using UnityEngine.UI;

// This script ensures that an Image component can have its alpha-hit-test threshold set from the inspector.
[RequireComponent(typeof(Image))]
public class ButtonAlphaFix : MonoBehaviour
{
    [Tooltip("The alpha threshold below which clicks are ignored. A value of 0.1 is a good start.")]
    [Range(0.01f, 1f)]
    public float alphaThreshold = 0.1f;

    void Start()
    {
        // Get the Image component on this same GameObject
        Image image = GetComponent<Image>();

        // Set its alpha hit test threshold to the value you set in the Inspector
        image.alphaHitTestMinimumThreshold = alphaThreshold;
    }
}