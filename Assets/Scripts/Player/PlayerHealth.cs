using UnityEngine;

// The player's health. Added to the player by FirstPersonPlayer, and shown by HealthBar.
// Radiation hurts in two ways, each with its own settings:
//   Puddles   standing in a radioactive puddle hurts while you're in it. Being near one only
//             builds up dose, it doesn't hurt directly
//   Sickness  once your total dose is high enough you keep losing health, even after leaving,
//             slowly at first and faster as the dose builds up
// Set either damage to 0 to turn that one off. At 0 health it brings up GameManager's game over.
[RequireComponent(typeof(Dosimeter))]
public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;

    [Header("Standing in a puddle")]
    [Tooltip("Health lost per second while standing in a radioactive puddle")]
    public float puddleDamage = 4f;

    [Header("Sickness (total dose so far)")]
    [Tooltip("Total dose where radiation starts to hurt, in mrem")]
    public float sicknessStartDose = 500f;
    [Tooltip("Health lost per second right at the start dose")]
    public float sicknessDamageAtStart = 0.5f;
    [Tooltip("Total dose where the damage stops getting worse, in mrem")]
    public float sicknessFullDose = 5000f;
    [Tooltip("Health lost per second at the full dose and above")]
    public float sicknessDamageAtFull = 6f;

    public float Health { get; private set; }
    public float Fraction => maxHealth > 0f ? Health / maxHealth : 0f;
    public bool IsDead { get; private set; }
    // True while the player is standing in a radioactive puddle
    public bool InPuddle { get; private set; }

    public event System.Action Died;

    private Dosimeter dosimeter;

    void Awake()
    {
        dosimeter = GetComponent<Dosimeter>();
        Health = maxHealth;
    }

    void Update()
    {
        if (IsDead)
            return;

        InPuddle = StandingInPuddle();

        // deltaTime is zero while paused, so nothing hurts behind the pause menu
        float damage = ((InPuddle ? puddleDamage : 0f) + SicknessDamage()) * Time.deltaTime;
        if (damage > 0f)
            TakeDamage(damage);
    }

    bool StandingInPuddle()
    {
        foreach (RadiationPuddle puddle in RadiationPuddle.Active)
            if (puddle.IsStandingIn(transform.position))
                return true;
        return false;
    }

    // Rises evenly from the start damage at the start dose to the full damage at the full dose
    float SicknessDamage()
    {
        if (dosimeter.TotalDose < sicknessStartDose)
            return 0f;
        float t = Mathf.InverseLerp(sicknessStartDose, sicknessFullDose, dosimeter.TotalDose);
        return Mathf.Lerp(sicknessDamageAtStart, sicknessDamageAtFull, t);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        Health = Mathf.Max(0f, Health - amount);
        if (Health <= 0f)
        {
            IsDead = true;
            Died?.Invoke();
            if (GameManager.Instance != null)
                GameManager.Instance.TriggerGameOver();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;
        Health = Mathf.Min(maxHealth, Health + amount);
    }
}
