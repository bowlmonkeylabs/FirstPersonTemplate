using UnityEngine;
using UnityEngine.Events;

namespace BML.Scripts
{
    public class InteractionReceiver : MonoBehaviour
    {
        public string HoverText = "";
        public UnityEvent OnInteract;
        
        public void ReceiveInteraction()
        {
            OnInteract.Invoke();
        }
    }
}