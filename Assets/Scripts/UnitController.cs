using GameUtility;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider))]
public class UnitController : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    private BaseCharacters m_baseCharacter;

    private BaseSkill[] m_lstSkills;
    private int m_health;

    // Getters
    public BaseCharacters Character => m_baseCharacter;
    public int Health => m_health;

    public BaseSkill[] LstSkills { get => m_lstSkills; private set => m_lstSkills = value; }

    private void Awake()
    {
        if (m_baseCharacter == null)
        {
            Debug.LogError($"Unit {name} has no BaseCharacters asset assigned.");
            m_health = 0;
            return;
        }

        m_health = (int)m_baseCharacter.m_nHealth;
        m_lstSkills = m_baseCharacter.m_lstSkills;
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, DamageType.Normal, null);
    }

    public void TakeDamage(int damage, DamageType damageType, GameObject owner)
    {
        if (damage <= 0)
        {
            return;
        }

        var damageEvent = new DamageEventMessage(gameObject, owner, damage, damageType);
        EventManager.TriggerEvent(EventNames.BeforeDamageApplied, damageEvent);

        damage = Mathf.Max(0, damageEvent.GetDamageValue());
        if (damage > 0)
        {
            m_health = Mathf.Max(0, m_health - damage);
        }

        EventManager.TriggerEvent(EventNames.AfterDamageApplied, damageEvent);
    }

    virtual public void OnPointerClick(PointerEventData eventData)
    {
        throw new System.NotImplementedException();
    }
}

