using System;
using BML.ScriptableObjectCore.Scripts.Variables;
using BML.Scripts;
using BML.Scripts.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField, FoldoutGroup("References")] protected Transform _mainCamera;
        [SerializeField, FoldoutGroup("References")] protected BoolReference _isPlayerInputDisabled;

        [SerializeField, FoldoutGroup("Interact")] protected float _interactDistance = 5f;
        [SerializeField, FoldoutGroup("Interact")] protected float _interactCastRadius = .25f;
        [SerializeField, FoldoutGroup("Interact")] protected LayerMask _interactMask;
        [SerializeField, FoldoutGroup("Interact")] protected TMP_Text _hoverText;
        [SerializeField, FoldoutGroup("Interact")] protected float _hoverUpdateDelay = .1f;

        [SerializeField, FoldoutGroup("Caffeine")] protected BoolReference _isCaffeineUnlocked; 
        [SerializeField, FoldoutGroup("Caffeine")] protected BoolReference _isCaffeinated;
        [SerializeField, FoldoutGroup("Caffeine")] protected TimerReference _caffeineTimer;
        [SerializeField, FoldoutGroup("Caffeine")] protected TimerReference _caffeineCooldownTimer;
        [SerializeField, FoldoutGroup("Caffeine")] protected BoolReference _outputShowCaffeineAvailable;

        private float lastHoverUpdateTime = Mathf.NegativeInfinity;
        
        #region Unity Lifecycle

        protected virtual void OnEnable()
        {
            UpdateCaffeineIndicator();
            _caffeineTimer.SubscribeFinished(DisableCaffeine);
            _caffeineCooldownTimer.SubscribeFinished(UpdateCaffeineIndicator);
        }

        protected virtual void OnDisable()
        {
            _caffeineTimer.UnsubscribeFinished(DisableCaffeine);
            _caffeineCooldownTimer.UnsubscribeFinished(UpdateCaffeineIndicator);
        }

        protected void Update()
        {
            if (lastHoverUpdateTime + _hoverUpdateDelay < Time.time)
            {
                CheckHover();
            }
        }

        protected virtual void FixedUpdate()
        {
            if (_caffeineTimer != null)
            {
                _caffeineTimer.UpdateTime();
                _caffeineCooldownTimer.UpdateTime();
            }
        }

        #endregion

        #region Input Callback

        protected virtual void OnPrimary(InputValue value)
        {
            if (_isPlayerInputDisabled.Value) return;
            if (!value.isPressed) return;
            
            TryInteract();
        }

        protected virtual void OnUseCaffeine(InputValue value)
        {
            if (_isPlayerInputDisabled.Value) return;
            if (!value.isPressed) return;
            
            TryUseCaffeine();
        }

        #endregion

        #region Interact

        protected virtual void TryInteract()
        {
            RaycastHit hit;
            
            if (Physics.SphereCast(_mainCamera.position, _interactCastRadius, _mainCamera.forward, out hit,
                _interactDistance, _interactMask, QueryTriggerInteraction.Ignore))
            {
                InteractionReceiver interactionReceiver = hit.collider.GetComponent<InteractionReceiver>();
                if (interactionReceiver == null)
                    return;

                interactionReceiver.ReceiveInteraction();
            }
        }
        
        private void CheckHover()
        {
            if (Physics.Raycast(_mainCamera.position, _mainCamera.forward, out var hit, _interactDistance, _interactMask, QueryTriggerInteraction.Ignore))
            {
                InteractionReceiver interactionReceiver = hit.collider.GetComponent<InteractionReceiver>();
                if (interactionReceiver == null) return;

                _hoverText.text = interactionReceiver.HoverText;
            }
            else
            {
                _hoverText.text = "";
            }

            lastHoverUpdateTime = Time.time;
        }

        #endregion

        #region Caffeine

        protected virtual void TryUseCaffeine()
        {
            if (_isCaffeineUnlocked.Value
                && !_isCaffeinated.Value 
                && (!_caffeineTimer.IsStarted || _caffeineTimer.IsFinished)
                && (!_caffeineCooldownTimer.IsStarted || _caffeineCooldownTimer.IsFinished))
            {
                _isCaffeinated.Value = true;
                
                _caffeineTimer.RestartTimer();
            }

            UpdateCaffeineIndicator();
        }

        protected virtual void DisableCaffeine()
        {
            _isCaffeinated.Value = false;

            _caffeineTimer.ResetTimer();
            _caffeineCooldownTimer.RestartTimer();
            
            UpdateCaffeineIndicator();
        }

        protected virtual void UpdateCaffeineIndicator()
        {
            _outputShowCaffeineAvailable.Value =
                _isCaffeinated.Value || (
                    (!_caffeineCooldownTimer.IsStarted || _caffeineCooldownTimer.IsFinished) 
                    && (!_caffeineTimer.IsStarted || _caffeineTimer.IsFinished));
        }

        #endregion

    }
}