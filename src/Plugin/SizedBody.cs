using CasualtiesSize.Core;
using HarmonyLib;
using UnityEngine;

namespace CasualtiesSize
{
    public sealed class SizedBody : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<Body, float> BodyBaseMass = AccessTools.FieldRefAccess<Body, float>("baseMass");
        private static readonly AccessTools.FieldRef<Body, Vector2> BodyOriginalColliderSize = AccessTools.FieldRefAccess<Body, Vector2>("origColSize");

        public Body Body { get; private set; }
        public float Current { get; private set; } = 1f;
        public float Target { get; private set; } = 1f;

        private float prefabHeightMultiplier;
        private Vector2 prefabColliderOffset;
        private Vector2 prefabOriginalColliderSize;
        private float prefabBodyMass;
        private float[] prefabLimbMass;
        private Vector3[] gameLimbScale;
        private Vector3[] writtenLimbScale;
        private float[] prefabLimbGravity;
        private float bodyGravityRaw = 1f;
        private float bodyGravityWritten = float.NaN;
        private float appliedForMass = 1f;
        private bool captured;

        public static SizedBody For(Body body)
        {
            if (body == null) return null;
            SizedBody sized = body.GetComponent<SizedBody>();
            if (sized == null) sized = body.gameObject.AddComponent<SizedBody>();
            return sized;
        }

        public static float SizeOf(Body body)
        {
            if (body == null) return SizeMath.WorldSize;
            SizedBody sized = body.GetComponent<SizedBody>();
            return sized == null ? SizeMath.WorldSize : sized.Current;
        }

        public void SetTarget(float size)
        {
            Target = size;
        }

        private void Awake()
        {
            Body = GetComponent<Body>();
        }

        private void Capture()
        {
            if (captured || Body == null || Body.limbs == null || Body.limbs.Length == 0 || Body.col == null) return;
            prefabHeightMultiplier = Body.heightMultiplier;
            prefabColliderOffset = Body.col.offset;
            prefabOriginalColliderSize = BodyOriginalColliderSize(Body);
            prefabBodyMass = BodyBaseMass(Body);
            prefabLimbMass = new float[Body.limbs.Length];
            prefabLimbGravity = new float[Body.limbs.Length];
            gameLimbScale = new Vector3[Body.limbs.Length];
            writtenLimbScale = new Vector3[Body.limbs.Length];
            for (int i = 0; i < Body.limbs.Length; i++)
            {
                prefabLimbMass[i] = Body.limbs[i].baseMass;
                prefabLimbGravity[i] = Body.limbs[i].rb != null ? Body.limbs[i].rb.gravityScale : 1f;
                gameLimbScale[i] = Body.limbs[i].transform.localScale;
                writtenLimbScale[i] = gameLimbScale[i];
            }
            captured = true;
        }

        private void LateUpdate()
        {
            Capture();
            if (!captured) return;
            float previous = Current;
            Current = SizeMath.Ease(Current, Target, Time.unscaledDeltaTime, SizeBook.Rules.SizeEaseSeconds);
            bool changed = Mathf.Abs(Current - previous) > 1e-6f;
            if (Mathf.Approximately(Current, 1f) && Mathf.Approximately(Target, 1f) && !changed && Mathf.Approximately(appliedForMass, 1f) && !IsScaledNow())
            {
                return;
            }
            ApplyPose();
            ApplyGravity();
            if (changed || !Mathf.Approximately(appliedForMass, Current)) ApplyMass(changed);
            ApplyHeldItems();
        }

        private bool IsScaledNow()
        {
            return !Mathf.Approximately(Body.heightMultiplier, prefabHeightMultiplier);
        }

        private void ApplyPose()
        {
            Body.heightMultiplier = prefabHeightMultiplier * Current;
            BodyOriginalColliderSize(Body) = prefabOriginalColliderSize * Current;
            Body.col.offset = prefabColliderOffset * Current;
            for (int i = 0; i < Body.limbs.Length; i++)
            {
                Transform limb = Body.limbs[i].transform;
                Vector3 seen = limb.localScale;
                if (seen != writtenLimbScale[i]) gameLimbScale[i] = seen;
                Vector3 raw = gameLimbScale[i];
                Vector3 scaled = new Vector3(raw.x * Current, raw.y * Current, raw.z);
                limb.localScale = scaled;
                writtenLimbScale[i] = scaled;
            }
        }

        private void ApplyGravity()
        {
            float multiplier = SizeMath.GravityMult(SizeBook.Rules, Current);
            float seen = Body.rb.gravityScale;
            if (float.IsNaN(bodyGravityWritten) || seen != bodyGravityWritten) bodyGravityRaw = seen;
            bodyGravityWritten = bodyGravityRaw * multiplier;
            Body.rb.gravityScale = bodyGravityWritten;
            for (int i = 0; i < Body.limbs.Length; i++)
                if (Body.limbs[i].rb != null) Body.limbs[i].rb.gravityScale = prefabLimbGravity[i] * multiplier;
        }

        private void ApplyMass(bool rebuildJoints)
        {
            float massScale = SizeMath.MassMult(Current);
            for (int i = 0; i < Body.limbs.Length; i++)
            {
                Limb limb = Body.limbs[i];
                limb.baseMass = prefabLimbMass[i] * massScale;
                if (limb.rb != null) limb.rb.mass = limb.baseMass;
            }
            BodyBaseMass(Body) = prefabBodyMass * massScale;
            if (Body.standing && Body.rb != null) Body.rb.mass = BodyBaseMass(Body);
            appliedForMass = Current;
            if (rebuildJoints && !Body.standing) RebuildJoints();
        }

        private void RebuildJoints()
        {
            foreach (Limb limb in Body.limbs)
            {
                HingeJoint2D joint = limb.GetComponent<HingeJoint2D>();
                if (joint == null || !joint.enabled) continue;
                joint.enabled = false;
                joint.enabled = true;
            }
        }

        private void ApplyHeldItems()
        {
            if (Body.slots == null) return;
            for (int i = 0; i < Body.slots.Length; i++)
            {
                if (!Body.HoldingItem(i)) continue;
                Item item = Body.GetItem(i);
                if (item == null) continue;
                Vector3 limbScale = Body.slots[i].limb.transform.localScale;
                item.transform.localScale = new Vector3(1f / limbScale.x, 1f / limbScale.y, 1f);
            }
        }

        private void OnDestroy()
        {
            if (!captured || Body == null) return;
            Body.heightMultiplier = prefabHeightMultiplier;
            BodyOriginalColliderSize(Body) = prefabOriginalColliderSize;
            Body.col.offset = prefabColliderOffset;
            Body.rb.gravityScale = bodyGravityRaw;
            for (int i = 0; i < Body.limbs.Length; i++)
                if (Body.limbs[i].rb != null) Body.limbs[i].rb.gravityScale = prefabLimbGravity[i];
        }
    }
}
