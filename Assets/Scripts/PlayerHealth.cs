using System;
using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float respawnDelay = 1.5f;
    [SerializeField] float respawnInvulnerability = 1.5f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public float Normalized => maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDied;
    public event Action OnRespawned;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private PlayerMovement movement;
    private CharacterController controller;
    private float invulnerableUntil;

    void Awake()
    {
        CurrentHealth = maxHealth;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        gameObject.AddComponent<PlayerHealthHud>().Bind(this);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        if (Time.time < invulnerableUntil)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        OnDied?.Invoke();

        if (movement != null)
            movement.enabled = false;
        if (controller != null)
            controller.enabled = false;

        StartCoroutine(RespawnRoutine());
    }

    IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        if (controller != null)
            controller.enabled = true;
        if (movement != null)
        {
            movement.ResetMovement();
            movement.enabled = true;
        }

        CurrentHealth = maxHealth;
        invulnerableUntil = Time.time + respawnInvulnerability;
        IsDead = false;

        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        OnRespawned?.Invoke();
    }
}
