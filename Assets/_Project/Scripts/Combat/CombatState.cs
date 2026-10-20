using System;
using System.Collections.Generic;
using System.Linq;
using DungeonDice.Core;
using DungeonDice.Dice;
using DungeonDice.Run;

namespace DungeonDice.Combat
{
    public enum CombatPhase
    {
        /// <summary>Waiting for the player to throw the party's resource dice.</summary>
        Throw,
        /// <summary>Dice settled; the player may re-throw a chosen subset (only when the rules allow rerolls).</summary>
        Reroll,
        /// <summary>Spend the mana pool on hero abilities, then end the turn.</summary>
        Act,
        /// <summary>Enemies resolve their telegraphed intents.</summary>
        EnemyTurn,
        Won,
        Lost,
    }

    /// <summary>What an ability needs the player to pick.</summary>
    public enum TargetKind
    {
        None,
        Enemy,
        Die,
    }

    /// <summary>
    /// The rules of one fight, with no Unity scene dependency. Each turn the party throws its resource
    /// dice; every settled die adds one mana of its colour to the pool; heroes spend the pool on their
    /// abilities (one cast per hero per turn by default, see <see cref="CombatRules"/>); then the enemies act on their telegraphed
    /// intents. <see cref="UI.CombatScreen"/> feeds it dice results from the box and renders it.
    /// All randomness goes through the run's seeded <see cref="RollService"/>.
    /// </summary>
    public sealed class CombatState
    {
        private readonly List<CombatHero> m_heroes = new();
        private readonly List<CombatEnemy> m_enemies = new();
        private readonly List<CombatDie> m_dice = new();
        private readonly List<string> m_log = new();
        private readonly Dictionary<DieColor, int> m_pool = new();
        private readonly RollService m_rng;

        public CombatRules Rules { get; }
        public DieDefinition ResourceDie { get; }
        public CombatPhase Phase { get; private set; } = CombatPhase.Throw;
        public int Turn { get; private set; } = 1;
        public int RerollsUsed { get; private set; }

        public IReadOnlyList<CombatHero> Heroes => m_heroes;
        public IReadOnlyList<CombatEnemy> Enemies => m_enemies;
        /// <summary>This turn's resource dice, in throw order.</summary>
        public IReadOnlyList<CombatDie> Dice => m_dice;
        public IReadOnlyList<string> Log => m_log;
        public IEnumerable<CombatHero> LivingHeroes => m_heroes.Where(h => h.IsAlive);
        public IEnumerable<CombatEnemy> LivingEnemies => m_enemies.Where(e => e.IsAlive);
        public bool IsOver => Phase == CombatPhase.Won || Phase == CombatPhase.Lost;
        public bool AnyDiePending => m_dice.Any(d => d.Pending);

        public int Red => Mana(DieColor.Red);
        public int Blue => Mana(DieColor.Blue);
        public int Yellow => Mana(DieColor.Yellow);
        public int Mana(DieColor color) => m_pool.TryGetValue(color, out var v) ? v : 0;

        /// <summary>Fired after every state change so a renderer can refresh.</summary>
        public event Action Changed;
        /// <summary>Something visible happened to a unit (damage, heal, shield, status, death); renderers animate from this.</summary>
        public event Action<CombatEvent> UnitEvent;
        /// <summary>A die's colour changed (Recolour); the box should turn the physical die to a face of that colour.</summary>
        public event Action<int, DieColor> DieRecolored;
        /// <summary>These dice should be re-thrown by the box (reroll step or a Reroll-die ability).</summary>
        public event Action<IReadOnlyList<int>> DiceRerollRequested;

        public CombatState(IEnumerable<HeroRunState> party, IEnumerable<EnemyDefinition> enemies, DieDefinition resourceDie, CombatRules rules, RollService rng)
        {
            m_rng = rng ?? throw new ArgumentNullException(nameof(rng));
            ResourceDie = resourceDie ?? throw new ArgumentNullException(nameof(resourceDie));
            Rules = rules?.Clone() ?? new CombatRules();
            foreach (var hero in party) m_heroes.Add(new CombatHero(hero));
            int i = 0;
            foreach (var enemy in enemies) m_enemies.Add(new CombatEnemy(enemy, i++));
            if (m_enemies.Count == 0) throw new ArgumentException("A fight needs at least one enemy.", nameof(enemies));

            ChooseIntents();
            Say($"Combat begins: {string.Join(", ", m_enemies.Select(e => e.Name))}.");
        }

        public CombatHero Hero(int slot) => m_heroes.FirstOrDefault(h => h.Slot == slot);
        public CombatDie Die(int index) => m_dice.FirstOrDefault(d => d.Index == index);

        // ---------------------------------------------------------------- dice in / out

        /// <summary>The dice to put in the box this turn: <see cref="CombatRules.resourceDiceCount"/> copies of the resource die.</summary>
        public List<DieDefinition> DiceToThrow(int count) => Enumerable.Repeat(ResourceDie, count).ToList();

        /// <summary>
        /// Results from the box, keyed by die index (= box owner id). In Throw every die joins the pool
        /// and the turn moves to Reroll (if allowed) or Act; in Reroll or Act the re-thrown dice rejoin
        /// the pool with their new colours.
        /// </summary>
        public void ReceiveRoll(IEnumerable<(int index, RollResult result)> results)
        {
            var list = results.ToList();
            switch (Phase)
            {
                case CombatPhase.Throw:
                    m_dice.Clear();
                    foreach (var (index, result) in list.OrderBy(r => r.index))
                    {
                        var die = new CombatDie(index, result);
                        m_dice.Add(die);
                        AddMana(die.Color, 1);
                    }
                    Phase = Rules.rerollsPerTurn > RerollsUsed ? CombatPhase.Reroll : CombatPhase.Act;
                    Say($"Turn {Turn}: rolled {Describe(m_dice)}. Pool: {PoolText()}.");
                    break;

                case CombatPhase.Reroll:
                    Rejoin(list);
                    RerollsUsed++;
                    Phase = CombatPhase.Act;
                    Say($"After reroll: {Describe(m_dice)}. Pool: {PoolText()}.");
                    break;

                case CombatPhase.Act:
                    Rejoin(list);
                    break;

                default:
                    return;
            }
            Changed?.Invoke();
        }

        private void Rejoin(List<(int index, RollResult result)> list)
        {
            foreach (var (index, result) in list)
            {
                var die = Die(index);
                if (die == null || !die.Pending) continue;
                die.Result = result;
                die.Color = result.Face.ManaColor;
                die.Pending = false;
                AddMana(die.Color, 1);
                Say($"Die {index + 1} re-thrown: {die.Color}.");
            }
        }

        // ---------------------------------------------------------------- reroll step (rules-gated)

        public bool CanReroll => Phase == CombatPhase.Reroll && RerollsUsed < Rules.rerollsPerTurn;

        /// <summary>Keep every die as rolled.</summary>
        public void KeepAll()
        {
            if (Phase != CombatPhase.Reroll) return;
            Phase = CombatPhase.Act;
            Changed?.Invoke();
        }

        /// <summary>Reroll step: pull the chosen dice's mana back out and ask the box to re-throw them.</summary>
        public bool RequestReroll(IEnumerable<int> indices)
        {
            if (!CanReroll) return false;
            var dice = indices.Distinct().Select(Die).Where(d => d != null && !d.Pending && Mana(d.Color) >= 1).ToList();
            if (dice.Count == 0) return false;
            foreach (var die in dice)
            {
                AddMana(die.Color, -1);
                die.Pending = true;
            }
            DiceRerollRequested?.Invoke(dice.Select(d => d.Index).ToList());
            Changed?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- abilities

        public bool CanAct => Phase == CombatPhase.Act && !AnyDiePending;

        public bool CanCast(int slot, int abilityIndex)
        {
            var hero = Hero(slot);
            var ability = AbilityOf(hero, abilityIndex);
            return CanAct && hero is { IsAlive: true } && HasActionLeft(hero) && ability != null && Mana(ability.costColor) >= ability.cost;
        }

        /// <summary>True while the hero is under <see cref="CombatRules.actionsPerHeroPerTurn"/> (0 = unlimited).</summary>
        public bool HasActionLeft(int slot) => Hero(slot) is { } hero && HasActionLeft(hero);

        private bool HasActionLeft(CombatHero hero) =>
            Rules.actionsPerHeroPerTurn <= 0 || hero.ActionsThisTurn < Rules.actionsPerHeroPerTurn;

        private static AbilityDefinition AbilityOf(CombatHero hero, int index) =>
            hero != null && index >= 0 && index < hero.Abilities.Count ? hero.Abilities[index] : null;

        /// <summary>Dice a Recolour or Reroll ability may target: settled, not pending, with its mana still in the pool.</summary>
        public bool CanTargetDie(int index)
        {
            var die = Die(index);
            return die != null && !die.Pending && Mana(die.Color) >= 1;
        }

        /// <summary>
        /// <see cref="CanTargetDie(int)"/> plus affordability: pulling a Yellow die out of the pool must
        /// still leave enough Yellow to pay a Yellow-costed ability.
        /// </summary>
        public bool CanTargetDie(int index, AbilityDefinition ability)
        {
            if (!CanTargetDie(index) || ability == null) return false;
            var die = Die(index);
            int needed = ability.cost + (die.Color == ability.costColor ? 1 : 0);
            return Mana(ability.costColor) >= needed;
        }

        /// <summary>
        /// Cast an ability. <paramref name="target"/> is an enemy index or a die index per the ability's
        /// <see cref="AbilityDefinition.Target"/>; <paramref name="recolorTo"/> is the new colour for Recolour.
        /// Each successful cast uses one of the hero's actions for the turn; a refused cast costs nothing.
        /// </summary>
        public bool Cast(int slot, int abilityIndex, int target = -1, DieColor recolorTo = DieColor.None)
        {
            if (!CanCast(slot, abilityIndex)) return false;
            var hero = Hero(slot);
            var ability = hero.Abilities[abilityIndex];

            bool ok = ability.kind switch
            {
                AbilityKind.Damage => Damage(hero, ability, target),
                AbilityKind.Shield => Shield(hero, hero, ability.value),
                AbilityKind.Guard => Guard(hero, ability.value),
                AbilityKind.ShieldAll => ShieldAll(hero, ability.value),
                AbilityKind.Status => Status(hero, ability, target),
                AbilityKind.Detonate => Detonate(hero, target),
                AbilityKind.Recolor => Recolor(hero, target, recolorTo),
                AbilityKind.RerollDie => RerollDie(hero, target),
                _ => false,
            };
            if (!ok) return false;

            AddMana(ability.costColor, -ability.cost);
            hero.ActionsThisTurn++;
            CheckVictory();
            Changed?.Invoke();
            return true;
        }

        private bool Damage(CombatHero hero, AbilityDefinition ability, int target)
        {
            var enemy = LivingEnemy(target);
            if (enemy == null) return Refuse($"{ability.name} needs a living enemy");
            int dealt = DamageEnemy(enemy, ability.value);
            Say($"{hero.Name}: {ability.name} hits {enemy.Name} for {dealt}.");
            return true;
        }

        private bool Shield(CombatHero caster, CombatHero target, int amount)
        {
            target.Shield += amount;
            Say($"{caster.Name} shields {(target == caster ? "themself" : target.Name)} for {amount}.");
            Emit(CombatEvent.Hero(CombatEventKind.Shield, target.Slot, amount));
            return true;
        }

        private bool Guard(CombatHero hero, int amount)
        {
            Shield(hero, hero, amount);
            hero.Statuses.Set(StatusKind.Taunt, 1);
            Say($"{hero.Name} taunts: enemy attacks go to them this turn.");
            Emit(CombatEvent.Hero(CombatEventKind.Status, hero.Slot, 1, "Taunt"));
            return true;
        }

        private bool ShieldAll(CombatHero hero, int amount)
        {
            foreach (var ally in LivingHeroes)
            {
                ally.Shield += amount;
                Emit(CombatEvent.Hero(CombatEventKind.Shield, ally.Slot, amount));
            }
            Say($"{hero.Name} shields the party for {amount}.");
            return true;
        }

        private bool Status(CombatHero hero, AbilityDefinition ability, int target)
        {
            var enemy = LivingEnemy(target);
            if (enemy == null) return Refuse($"{ability.name} needs a living enemy");
            if (ability.status is StatusKind.Burn or StatusKind.Poison) enemy.Statuses.Add(ability.status, ability.value);
            else enemy.Statuses.Set(ability.status, Math.Max(1, ability.value));
            Say($"{hero.Name}: {ability.name} on {enemy.Name} ({ability.status} {enemy.Statuses.Get(ability.status)}).");
            Emit(CombatEvent.Enemy(CombatEventKind.Status, enemy.Index, Math.Max(1, ability.value), ability.status.ToString()));
            return true;
        }

        private bool Detonate(CombatHero hero, int target)
        {
            var enemy = LivingEnemy(target);
            if (enemy == null) return Refuse("Detonate needs a living enemy");
            int stacks = enemy.Statuses.Total;
            enemy.Statuses.Clear();
            int dealt = DamageEnemy(enemy, stacks);
            Say($"{hero.Name} detonates {enemy.Name}: {stacks} stacks consumed, {dealt} damage.");
            return true;
        }

        private bool Recolor(CombatHero hero, int target, DieColor to)
        {
            var die = Die(target);
            if (die == null || !CanTargetDie(target, hero.Abilities.First(a => a.kind == AbilityKind.Recolor))) return Refuse("Recolour needs a rolled die whose mana is unspent (and the cost still covered)");
            if (to == DieColor.None || to == die.Color) return Refuse("Recolour needs a different colour");
            AddMana(die.Color, -1);
            AddMana(to, 1);
            Say($"{hero.Name} recolours die {die.Index + 1}: {die.Color} → {to}.");
            die.Color = to;
            DieRecolored?.Invoke(die.Index, to);
            return true;
        }

        private bool RerollDie(CombatHero hero, int target)
        {
            var die = Die(target);
            if (die == null || !CanTargetDie(target, hero.Abilities.First(a => a.kind == AbilityKind.RerollDie))) return Refuse("Reroll needs a rolled die whose mana is unspent (and the cost still covered)");
            AddMana(die.Color, -1);
            die.Pending = true;
            Say($"{hero.Name} re-throws die {die.Index + 1}.");
            DiceRerollRequested?.Invoke(new[] { die.Index });
            return true;
        }

        private bool Refuse(string reason)
        {
            Say($"Cannot cast: {reason}.");
            return false;
        }

        // ---------------------------------------------------------------- enemy turn

        /// <summary>End the player's turn: enemies act, statuses tick, then a new turn begins (or the fight ends).</summary>
        public void EndHeroTurn()
        {
            if (Phase != CombatPhase.Act || AnyDiePending) return;
            Phase = CombatPhase.EnemyTurn;
            Say($"— Enemy turn {Turn} —");

            foreach (var enemy in LivingEnemies.ToList())
            {
                int burn = enemy.Statuses.Get(StatusKind.Burn);
                if (burn <= 0) continue;
                int lost = enemy.LoseHealth(burn);
                enemy.Statuses.Set(StatusKind.Burn, burn - 1);
                Say($"{enemy.Name} burns for {lost}.");
                Emit(CombatEvent.Enemy(CombatEventKind.Damage, enemy.Index, lost, "Burn"));
                if (!enemy.IsAlive) Emit(CombatEvent.Enemy(CombatEventKind.Death, enemy.Index, 0));
            }

            foreach (var enemy in LivingEnemies.ToList())
            {
                enemy.Shield = 0; // Block lasts until the enemy next acts
                var intent = enemy.Intent;
                if (intent == null) continue;
                int weaken = enemy.Statuses.Has(StatusKind.Weaken) ? 1 : 0;
                switch (intent.kind)
                {
                    case IntentKind.Attack:
                    {
                        var target = PickAttackTarget();
                        if (target == null) break;
                        int incoming = Math.Max(0, intent.value - weaken);
                        int dealt = target.TakeDamage(incoming);
                        Say($"{enemy.Name} attacks {target.Name} for {incoming}{(dealt < incoming ? $" ({dealt} through shield)" : "")}.{(target.IsAlive ? "" : $" {target.Name} falls!")}");
                        Emit(CombatEvent.Hero(CombatEventKind.Damage, target.Slot, dealt, dealt < incoming ? "absorbed" : null));
                        if (!target.IsAlive) Emit(CombatEvent.Hero(CombatEventKind.Death, target.Slot, 0));
                        break;
                    }
                    case IntentKind.AttackAll:
                        foreach (var target in LivingHeroes.ToList())
                        {
                            int dealt = target.TakeDamage(Math.Max(0, intent.value - weaken));
                            Emit(CombatEvent.Hero(CombatEventKind.Damage, target.Slot, dealt));
                            if (!target.IsAlive)
                            {
                                Say($"{target.Name} falls!");
                                Emit(CombatEvent.Hero(CombatEventKind.Death, target.Slot, 0));
                            }
                        }
                        Say($"{enemy.Name} attacks everyone for {intent.value - weaken}.");
                        break;
                    case IntentKind.Block:
                        enemy.Shield += intent.value;
                        Say($"{enemy.Name} blocks for {intent.value}.");
                        Emit(CombatEvent.Enemy(CombatEventKind.Shield, enemy.Index, intent.value));
                        break;
                }
            }

            foreach (var enemy in LivingEnemies.ToList())
            {
                int poison = enemy.Statuses.Get(StatusKind.Poison);
                if (poison > 0)
                {
                    int lost = enemy.LoseHealth(poison);
                    Say($"{enemy.Name} suffers {lost} poison.");
                    Emit(CombatEvent.Enemy(CombatEventKind.Damage, enemy.Index, lost, "Poison"));
                    if (!enemy.IsAlive) Emit(CombatEvent.Enemy(CombatEventKind.Death, enemy.Index, 0));
                }
                enemy.Statuses.Remove(StatusKind.Weaken);
                enemy.Statuses.Remove(StatusKind.Hex);
            }

            foreach (var hero in m_heroes)
            {
                hero.Shield = 0;
                hero.Statuses.Remove(StatusKind.Taunt);
                hero.ActionsThisTurn = 0;
            }

            if (!LivingHeroes.Any())
            {
                Phase = CombatPhase.Lost;
                Say("The party falls.");
            }
            else if (!LivingEnemies.Any())
            {
                Phase = CombatPhase.Won;
                Say("Victory!");
            }
            else
            {
                Turn++;
                RerollsUsed = 0;
                m_dice.Clear();
                if (!Rules.manaCarriesBetweenTurns)
                {
                    m_pool.Clear();
                    Say("Unspent mana is lost.");
                }
                ChooseIntents();
                Phase = CombatPhase.Throw;
            }
            Changed?.Invoke();
        }

        private CombatHero PickAttackTarget()
        {
            var living = LivingHeroes.ToList();
            if (living.Count == 0) return null;
            var taunter = living.FirstOrDefault(h => h.Statuses.Has(StatusKind.Taunt));
            return taunter ?? m_rng.Pick(living);
        }

        private void ChooseIntents()
        {
            foreach (var enemy in LivingEnemies)
            {
                var options = enemy.Definition.intents;
                enemy.Intent = options != null && options.Count > 0 ? m_rng.Pick(options) : null;
            }
        }

        // ---------------------------------------------------------------- helpers

        private int DamageEnemy(CombatEnemy enemy, int amount)
        {
            if (enemy.Statuses.Has(StatusKind.Hex)) amount += 1;
            int dealt = enemy.TakeDamage(amount);
            Emit(CombatEvent.Enemy(CombatEventKind.Damage, enemy.Index, dealt, dealt < amount ? "absorbed" : null));
            if (!enemy.IsAlive) Emit(CombatEvent.Enemy(CombatEventKind.Death, enemy.Index, 0));
            return dealt;
        }

        private void CheckVictory()
        {
            if (!LivingEnemies.Any() && !IsOver)
            {
                Phase = CombatPhase.Won;
                Say("Victory!");
            }
        }

        private CombatEnemy LivingEnemy(int index)
        {
            var enemy = index >= 0 && index < m_enemies.Count ? m_enemies[index] : null;
            return enemy != null && enemy.IsAlive ? enemy : null;
        }

        private void AddMana(DieColor color, int amount)
        {
            if (color == DieColor.None) return;
            m_pool[color] = Math.Max(0, Mana(color) + amount);
        }

        private void Emit(CombatEvent evt) => UnitEvent?.Invoke(evt);

        private string PoolText() => $"Red {Red}, Blue {Blue}, Yellow {Yellow}";

        private static string Describe(IEnumerable<CombatDie> dice) => string.Join(" ", dice.Select(d => d.Color.ToString()[0]));

        private void Say(string line) => m_log.Add(line);

        /// <summary>Test hook: put mana in the pool without throwing dice.</summary>
        internal void GrantManaForTests(DieColor color, int amount) => AddMana(color, amount);
    }
}
