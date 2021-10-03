#region

using UnityEngine;

#endregion

namespace Appalachia.Simulation.Physical.Relays
{
    public class CollisionRelay_EnterStay : CollisionRelay
    {
        public event OnRelayedCollision OnRelayedCollisionEnter;
        public event OnRelayedCollision OnRelayedCollisionStay;

        private void OnCollisionEnter(Collision other)
        {
            OnRelayedCollisionEnter?.Invoke(this, relayingColliders, other);
        }

        private void OnCollisionStay(Collision other)
        {
            OnRelayedCollisionStay?.Invoke(this, relayingColliders, other);
        }
    }
}
