using UnityEngine;

public class AnamorphicFocusTrigger : MonoBehaviour
{
    public AnamorphicFocusDirector director;
    public AnamorphicDrawingInstance drawing;
    public bool returnOnExit = true;

    private void Reset()
    {
        drawing = GetComponentInParent<AnamorphicDrawingInstance>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            director.FocusOnDrawing(drawing);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!returnOnExit) return;
        if (other.CompareTag("Player"))
            director.ReturnToPlayer();
    }
}
