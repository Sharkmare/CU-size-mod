using System;
using System.Collections.Generic;
using CasualtiesSize.Core;
using UnityEngine;

namespace CasualtiesSize
{
    public sealed class CaptureEntry
    {
        public int HolderKey;
        public int HeldKey;
        public CaptureTracker Tracker;
        public float SavedGravity;
        public bool SavedColliderEnabled;
        public bool Applied;
        public readonly List<KeyValuePair<SpriteRenderer, Color>> SavedColors = new List<KeyValuePair<SpriteRenderer, Color>>();
    }

    public static class CaptureBook
    {
        private const float SwallowedAlpha = 0.35f;
        private const float ReleaseDropDistance = 2.5f;

        public static readonly Dictionary<int, CaptureEntry> ByHeld = new Dictionary<int, CaptureEntry>();

        public static Action<Body> HealthChanged;
        public static Action<List<int>> Broadcast;

        public static bool IsHeld(int key)
        {
            return ByHeld.ContainsKey(key);
        }

        public static bool IsHolder(int key)
        {
            foreach (CaptureEntry entry in ByHeld.Values) if (entry.HolderKey == key) return true;
            return false;
        }

        public static string Start(int holderKey, int heldKey, CaptureMode mode)
        {
            if (holderKey == heldKey) return "a body cannot hold itself";
            if (IsHeld(heldKey)) return "that body is already held";
            if (IsHeld(holderKey)) return "a held body cannot hold another";
            ByHeld[heldKey] = new CaptureEntry { HolderKey = holderKey, HeldKey = heldKey, Tracker = new CaptureTracker(mode) };
            return null;
        }

        public static void ReplaceFromWire(List<KeyValuePair<int, KeyValuePair<int, byte>>> table)
        {
            HashSet<int> keep = new HashSet<int>();
            foreach (KeyValuePair<int, KeyValuePair<int, byte>> row in table)
            {
                keep.Add(row.Key);
                CaptureEntry existing;
                if (ByHeld.TryGetValue(row.Key, out existing) && existing.HolderKey == row.Value.Key && (byte)existing.Tracker.Mode == row.Value.Value) continue;
                if (existing != null) Restore(existing, FindBodies());
                ByHeld[row.Key] = new CaptureEntry { HolderKey = row.Value.Key, HeldKey = row.Key, Tracker = new CaptureTracker((CaptureMode)row.Value.Value) };
            }
            Dictionary<int, Body> bodies = FindBodies();
            foreach (int key in new List<int>(ByHeld.Keys))
            {
                if (keep.Contains(key)) continue;
                Restore(ByHeld[key], bodies);
                ByHeld.Remove(key);
            }
        }

        public static List<KeyValuePair<int, KeyValuePair<int, byte>>> ToWire()
        {
            List<KeyValuePair<int, KeyValuePair<int, byte>>> table = new List<KeyValuePair<int, KeyValuePair<int, byte>>>();
            foreach (CaptureEntry entry in ByHeld.Values)
                table.Add(new KeyValuePair<int, KeyValuePair<int, byte>>(entry.HeldKey, new KeyValuePair<int, byte>(entry.HolderKey, (byte)entry.Tracker.Mode)));
            return table;
        }

        public static void ClearAll()
        {
            Dictionary<int, Body> bodies = FindBodies();
            foreach (CaptureEntry entry in ByHeld.Values) Restore(entry, bodies);
            ByHeld.Clear();
        }

        public static void End(int heldKey)
        {
            CaptureEntry entry;
            if (!ByHeld.TryGetValue(heldKey, out entry)) return;
            Restore(entry, FindBodies());
            ByHeld.Remove(heldKey);
            List<int> changed = new List<int> { heldKey };
            if (Broadcast != null) Broadcast(changed);
        }

        public static Dictionary<int, Body> FindBodies()
        {
            Dictionary<int, Body> bodies = new Dictionary<int, Body>();
            foreach (BodyHandle handle in Plugin.CurrentDirectory().Bodies()) bodies[handle.Key] = handle.Body;
            return bodies;
        }

        public static void Pin(CaptureEntry entry, Dictionary<int, Body> bodies)
        {
            Body holder;
            Body held;
            if (!bodies.TryGetValue(entry.HolderKey, out holder) || !bodies.TryGetValue(entry.HeldKey, out held)) return;
            if (!entry.Applied) Prepare(entry, held);
            Vector2 anchor = AnchorOf(holder, entry.Tracker.Mode);
            Rigidbody2D torso = held.limbs[2].rb;
            if (held.standing)
            {
                held.rb.position = anchor;
                held.rb.velocity = Vector2.zero;
            }
            else
            {
                Vector2 shift = anchor - (Vector2)held.limbs[2].transform.position;
                foreach (Limb limb in held.limbs)
                {
                    if (limb.rb == null) continue;
                    limb.rb.position += shift;
                    limb.rb.velocity = Vector2.zero;
                }
            }
        }

        private static Vector2 AnchorOf(Body holder, CaptureMode mode)
        {
            if (mode == CaptureMode.Swallowed) return holder.limbs[2].transform.position;
            Transform hand = holder.slots[holder.handSlot].transform;
            return hand.position;
        }

        private static void Prepare(CaptureEntry entry, Body held)
        {
            entry.SavedGravity = held.rb.gravityScale;
            entry.SavedColliderEnabled = held.col.enabled;
            held.rb.gravityScale = 0f;
            held.col.enabled = false;
            if (entry.Tracker.Mode == CaptureMode.Swallowed)
            {
                foreach (SpriteRenderer renderer in held.GetComponentsInChildren<SpriteRenderer>())
                {
                    entry.SavedColors.Add(new KeyValuePair<SpriteRenderer, Color>(renderer, renderer.color));
                    Color faded = renderer.color;
                    faded.a *= SwallowedAlpha;
                    renderer.color = faded;
                }
            }
            entry.Applied = true;
        }

        private static void Restore(CaptureEntry entry, Dictionary<int, Body> bodies)
        {
            Body held;
            Body holder;
            bodies.TryGetValue(entry.HeldKey, out held);
            bodies.TryGetValue(entry.HolderKey, out holder);
            foreach (KeyValuePair<SpriteRenderer, Color> saved in entry.SavedColors)
                if (saved.Key != null) saved.Key.color = saved.Value;
            entry.SavedColors.Clear();
            if (held == null || !entry.Applied) return;
            held.rb.gravityScale = entry.SavedGravity;
            held.col.enabled = entry.SavedColliderEnabled;
            if (holder != null)
            {
                float side = holder.isRight ? 1f : -1f;
                Vector2 landing = (Vector2)holder.transform.position + new Vector2(side * ReleaseDropDistance, 0.5f);
                Vector2 shift = landing - (Vector2)held.limbs[2].transform.position;
                held.rb.position += shift;
                foreach (Limb limb in held.limbs) if (limb.rb != null && !held.standing) limb.rb.position += shift;
            }
            entry.Applied = false;
        }
    }

    public sealed class CaptureRunner : MonoBehaviour
    {
        private void FixedUpdate()
        {
            if (CaptureBook.ByHeld.Count == 0) return;
            Dictionary<int, Body> bodies = CaptureBook.FindBodies();
            bool authority = Plugin.IsAuthority();
            foreach (CaptureEntry entry in new List<CaptureEntry>(CaptureBook.ByHeld.Values))
            {
                CaptureBook.Pin(entry, bodies);
                if (authority) HostTick(entry, bodies, Time.fixedDeltaTime);
            }
        }

        private void Update()
        {
            if (CaptureBook.ByHeld.Count == 0) return;
            int localKey = Plugin.LocalKey();
            CaptureEntry entry;
            if (localKey < 0 || !CaptureBook.ByHeld.TryGetValue(localKey, out entry)) return;
            if (!Input.GetKeyDown(KeyCode.Space)) return;
            if (Plugin.IsAuthority()) entry.Tracker.Struggle();
            else Plugin.SendStruggle();
        }

        private static void HostTick(CaptureEntry entry, Dictionary<int, Body> bodies, float deltaSeconds)
        {
            Body holder;
            Body held;
            if (!bodies.TryGetValue(entry.HolderKey, out holder) || !bodies.TryGetValue(entry.HeldKey, out held) || !held.alive || !holder.alive)
            {
                CaptureBook.End(entry.HeldKey);
                return;
            }
            if (!holder.conscious && entry.Tracker.Mode == CaptureMode.Swallowed)
            {
                CaptureBook.End(entry.HeldKey);
                return;
            }
            if (entry.Tracker.Mode == CaptureMode.Swallowed) Digest(entry, holder, held, deltaSeconds);
            if (entry.Tracker.Tick(SizeBook.Rules, deltaSeconds) != CaptureEnd.None) CaptureBook.End(entry.HeldKey);
        }

        private static void Digest(CaptureEntry entry, Body holder, Body held, float deltaSeconds)
        {
            float damage = SizeMath.DigestionDamage(SizeBook.Rules, SizedBody.SizeOf(holder), SizedBody.SizeOf(held), deltaSeconds);
            foreach (Limb limb in held.limbs)
            {
                if (!limb.isVital) continue;
                limb.skinHealth = Mathf.Max(0f, limb.skinHealth - damage * 0.5f);
                limb.muscleHealth = Mathf.Max(0f, limb.muscleHealth - damage * 0.5f);
                limb.pain += damage * 0.2f;
            }
            holder.hunger = Mathf.Min(125f, holder.hunger + damage * SizeBook.Rules.EatHungerGain);
            if (CaptureBook.HealthChanged != null) CaptureBook.HealthChanged(held);
        }
    }
}
