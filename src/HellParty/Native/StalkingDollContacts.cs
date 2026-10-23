using System.Collections.Generic;
using HellParty.Rules;
using UnityEngine;

namespace HellParty.Native;

// Feed back actual native collider contacts, rather than stopping on a
// renderer-sized sphere that may already overlap a wall or another doll.
internal sealed class StalkingDollContacts
{
    private readonly List<Contact> _contacts = new();

    private readonly struct Contact
    {
        internal readonly Collider Collider;
        internal readonly Vector3 Normal;
        internal readonly float Time;
        internal Contact(Collider collider, Vector3 normal)
        { Collider = collider; Normal = normal; Time = UnityEngine.Time.fixedTime; }
    }

    internal void Forget(Collider collider) => _contacts.RemoveAll(contact => contact.Collider == collider);

    internal void Record(Collision collision)
    {
        Forget(collision.collider);
        for (int i = 0; i < collision.contactCount && i < 4; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.y) >= .7f) continue; // Supporting floor/slope is not a lateral obstacle.
            _contacts.Add(new Contact(collision.collider, new Vector3(normal.x, 0, normal.z).normalized));
        }
    }

    internal Vector3 Steer(Vector3 desired)
    {
        float expires = Time.fixedTime - Time.fixedDeltaTime * 2.5f;
        _contacts.RemoveAll(contact => contact.Collider == null || !contact.Collider.enabled || contact.Time < expires);
        Vector3 steered = desired;
        foreach (Contact contact in _contacts)
        {
            StalkingDollContactRules.Slide(steered.x, steered.z, contact.Normal.x, contact.Normal.z,
                out double x, out double z);
            steered = new Vector3((float)x, 0, (float)z);
        }
        // Multiple surfaces can form a corner. Never command motion into an
        // earlier surface after handling a later contact. Native physics still
        // resolves the complete collider shape and all dynamic body contacts.
        for (int pass = 0; pass < 2; pass++)
            foreach (Contact contact in _contacts)
            {
                float into = Vector3.Dot(steered, contact.Normal);
                if (into < 0) steered -= contact.Normal * into;
            }
        return Vector3.ClampMagnitude(steered, desired.magnitude);
    }
}
