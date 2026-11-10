using System.Collections.Generic;
using CasualtiesSize.Core;
using UnityEngine;

namespace CasualtiesSize
{
    public sealed class StompRunner : MonoBehaviour
    {
        private const float FootContactTolerance = 0.6f;
        private readonly Dictionary<long, float> lastStompTime = new Dictionary<long, float>();

        private void FixedUpdate()
        {
            if (!Plugin.IsAuthority()) return;
            List<BodyHandle> handles = new List<BodyHandle>(Plugin.CurrentDirectory().Bodies());
            if (handles.Count < 2) return;
            foreach (BodyHandle actor in handles)
            {
                foreach (BodyHandle other in handles)
                {
                    if (actor.Key == other.Key || actor.Body == null || other.Body == null) continue;
                    TryStomp(actor, other);
                }
            }
        }

        private void TryStomp(BodyHandle actor, BodyHandle other)
        {
            SizeRules rules = SizeBook.Rules;
            float actorSize = SizedBody.SizeOf(actor.Body);
            float otherSize = SizedBody.SizeOf(other.Body);
            if (!SizeMath.CanStomp(rules, actorSize, otherSize)) return;
            if (CaptureBook.IsHeld(actor.Key) || CaptureBook.IsHeld(other.Key)) return;
            if (!actor.Body.alive) return;
            Bounds actorBounds = BoundsOf(actor.Body);
            Bounds otherBounds = BoundsOf(other.Body);
            if (actorBounds.min.x > otherBounds.max.x || actorBounds.max.x < otherBounds.min.x) return;
            float footHeightAboveTop = actorBounds.min.y - otherBounds.max.y;
            if (Mathf.Abs(footHeightAboveTop) > FootContactTolerance * actorSize) return;
            float downwardSpeed = other.Body.rb.velocity.y - actor.Body.rb.velocity.y;
            float force = SizeMath.StompForce(rules, actorSize, otherSize, downwardSpeed);
            if (force <= 0f) return;
            long pair = ((long)actor.Key << 32) | (uint)other.Key;
            float last;
            if (lastStompTime.TryGetValue(pair, out last) && Time.time - last < rules.StompCooldown) return;
            lastStompTime[pair] = Time.time;
            Apply(other.Body, force);
            Plugin.Debug("stomp " + actor.Key + " on " + other.Key + " force " + force);
        }

        private static void Apply(Body victim, float force)
        {
            ImpactFragilityPatch.Bypass = true;
            try
            {
                foreach (Limb limb in victim.limbs)
                    if (limb.isVital || limb.isHead) limb.ImpactDamage(force, Vector2.down);
            }
            finally
            {
                ImpactFragilityPatch.Bypass = false;
            }
            victim.Ragdoll();
            if (CaptureBook.HealthChanged != null) CaptureBook.HealthChanged(victim);
        }

        private static Bounds BoundsOf(Body body)
        {
            Bounds bounds = new Bounds(body.transform.position, Vector3.zero);
            foreach (Limb limb in body.limbs)
            {
                Collider2D collider = limb.GetComponent<Collider2D>();
                if (collider != null && !limb.dismembered) bounds.Encapsulate(collider.bounds);
            }
            return bounds;
        }
    }
}
