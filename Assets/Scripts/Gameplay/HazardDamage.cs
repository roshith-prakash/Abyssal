using UnityEngine;

namespace Abyssal
{
    /// <summary>
    /// Attached to hazardous objects or creatures to configure custom collision damage.
    /// </summary>
    public class HazardDamage : MonoBehaviour
    {
        [Tooltip("Damage dealt to the diver upon collision.")]
        [SerializeField] private float damageAmount = 35f;

        public float DamageAmount => damageAmount;
    }
}
