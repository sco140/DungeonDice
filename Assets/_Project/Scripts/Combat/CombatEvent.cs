namespace DungeonDice.Combat
{
    public enum CombatEventKind
    {
        Damage,
        Heal,
        Shield,
        Status,
        Death,
    }

    /// <summary>
    /// A visible change to one unit, raised by <see cref="CombatState.UnitEvent"/> so the screen can
    /// flash cards and float numbers. Heroes are addressed by party slot, enemies by index.
    /// </summary>
    public readonly struct CombatEvent
    {
        public CombatEventKind Kind { get; }
        public bool TargetIsEnemy { get; }
        /// <summary>Hero slot or enemy index.</summary>
        public int Target { get; }
        public int Amount { get; }
        /// <summary>Optional detail: a status name, "Burn"/"Poison" for tick damage, "absorbed" when a shield took part.</summary>
        public string Detail { get; }

        private CombatEvent(CombatEventKind kind, bool targetIsEnemy, int target, int amount, string detail)
        {
            Kind = kind;
            TargetIsEnemy = targetIsEnemy;
            Target = target;
            Amount = amount;
            Detail = detail;
        }

        public static CombatEvent Hero(CombatEventKind kind, int slot, int amount, string detail = null) => new(kind, false, slot, amount, detail);
        public static CombatEvent Enemy(CombatEventKind kind, int index, int amount, string detail = null) => new(kind, true, index, amount, detail);

        public override string ToString() => $"{(TargetIsEnemy ? "enemy" : "hero")} {Target}: {Kind} {Amount}{(Detail != null ? $" ({Detail})" : "")}";
    }
}
