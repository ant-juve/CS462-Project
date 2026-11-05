using UnityEngine;

// Put a component with this interface next to a PickupItem to make it usable from the hotbar:
// select its slot and click. The hotbar plays the arms' use animation, calls Use at the moment
// of contact, and then removes the item if ConsumeOnUse is true. See RadiationShot for an example.
public interface IUsable
{
    // Shown under the item's name while it's selected, e.g. "Inject"
    string UseVerb { get; }
    // Whether the item disappears from the hotbar after one use
    bool ConsumeOnUse { get; }
    // False to refuse the use right now (the click does nothing)
    bool CanUse(GameObject player);
    void Use(GameObject player);
}
