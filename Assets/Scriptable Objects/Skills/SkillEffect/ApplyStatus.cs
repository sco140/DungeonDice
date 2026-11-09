using System.Collections.Generic;
using UnityEngine;
using GameUtility;

[CreateAssetMenu(fileName = "ApplyStatus", menuName = "Scriptable Objects/Effect/ApplyStatus")]
public class ApplyStatus : BaseSkillEffect
{
    struct StatusInfo
    {
        public StatusEffect type;
        public int value;
    }
    List<StatusInfo> FindAllStatusEffects(Dictionary<string, int> skillData)
    {
        List<StatusInfo> result = new();
        foreach (var item in skillData)
        {
            switch (item.Key.ToLowerInvariant())
            {
                case "armor":
                    break;
                case "burn":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Burn,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "bleeding":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Bleeding,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "carrionarmor":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.CarrionArmor,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "cooldown":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Cooldown,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "usage_cooldown":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Usage_Cooldown,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "fear":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Fear,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "freeze":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Freeze,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "poison":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Poison,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
                case "stun":
                    {
                        var newStatus = new StatusInfo
                        {
                            type = StatusEffect.Stun,
                            value = item.Value
                        };

                        result.Add(newStatus);
                    }
                    break;
            }
        }

        return result;
    }
    public override void Execute(
        UnitController caster,
        UnitController target,
        Dictionary<string, int> skillData)
    {
        // var statusList = FindAllStatusEffects(skillData);
        // for each var status in statusList
        // target.ApplyStatus(status.type, status.value);
    }
}
