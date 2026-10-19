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
        /// <summary>Waiting for the player to throw the living heroes' dice.</summary>
        Throw,
        /// <summary>Dice settled; the player may re-throw a chosen subset once.</summary>
        Reroll,
        /// <summary>Use or bank each die, cast signatures, then end the turn.</summary>
        Act,
        /// <summary>Enemies resolve their telegraphed intents.</summary>
        EnemyTurn,
        Won,
        Lost,
    }

    /// <summary>What a die face or signature needs the player to pick.</summary>
    public enum TargetKind
    {
        None,
        Enemy,
        Hero,
        Die,
    }

    /// <summary>
    /// The rules of one fight, with no Unity scene dependency: turn phases, use-or-bank, the colour
    /// mana pool, signature skills, statuses, and the enemy turn. <see cref="UI.CombatScreen"/> feeds
    /// it dice results from the box and renders it. All randomness goes through the run's seeded
    /// <see cref="RollService"/> so a fight replays identically apart from the physical dice.
    /// </summary>
    public sealed class CombatState
    {
        public const int SignatureCost = 3;
        public const int BankValue = 1;

        private readonly List<CombatHero> m_heroes = new();
        private readonly List<CombatEnemy> m_enemies = new();
        private readonly List<CombatDie> m_dice = new();
        private readonly List<string> m_log = new();
        private readonly RollService m_rng;

        public CombatPhase Phase { get; private set; } = CombatPhase.Throw;
        public int Turn { get; private set; } = 1;
        public bool RerollUsed { get; private set; }
        public int Red { get; private set; }
        public int Blue { get; private set; }
        public int Yellow { get; private set; }
        /// <summary>Slot of a die waiting for a Reroll-ally re-throw, or -1.</summary>
        public int PendingAllyReroll { get; private set; } = -1;

        public IReadOnlyList<CombatHero> Heroes => m_heroes;
        public IReadOnlyList<CombatEnemy> Enemies => m_enemies;
        public IReadOnlyList<CombatDie> Dice => m_dice;
        public IReadOnlyList<string> Log => m_log;
        public IEnumerable<CombatHero> LivingHeroes => m_heroes.Where(h => h.IsAlive);
        public IEnumerable<CombatEnemy> LivingEnemies => m_enemies.Where(e => e.IsAlive);
        public bool IsOver => Phase == CombatPhase.Won || Phase == CombatPhase.Lost;

        /// <summary>Fired after every state change so a renderer can refresh.</summary>
        public event Action Changed;
        /// <summary>A modifier was applied to a hero die's result; the box should turn the physical die.</summary>
        public event Action<int, IDieModifier> DieModified;
        /// <summary>Reroll-ally: the box should re-throw this slot's die.</summary>
        public event Action<int> AllyRerollRequested;

        public CombatState(IEnumerable<HeroRunState> party, IEnumerable<EnemyDefinition> enemies, RollService rng)
        {
            m_rng = rng ?? throw new ArgumentNullException(nameof(rng));
            foreach (var hero in party) m_heroes.Add(new CombatHero(hero));
            int i = 0;
            foreach (var enemy in enemies) m_enemies.Add(new CombatEnemy(enemy, i++));
            if (m_enemies.Count == 0) throw new ArgumentException("A fight needs at least one enemy.", nameof(enemies));

            ChooseIntents();
            Say($"Combat begins: {string.Join(", ", m_enemies.Select(e => e.Name))}.");
        }

        public CombatHero Hero(int slot) => m_heroes.FirstOrDefault(h => h.Slot == slot);
        public CombatDie Die(int slot) => m_dice.FirstOrDefault(d => d.Slot == slot);
        public int Mana(DieColor color) => color switch { DieColor.Red => Red, DieColor.Blue => Blue, DieColor.Yellow => Yellow, _ => 0 };

        // ---------------------------------------------------------------- dice in / out

        /// <summary>The dice to put in the box this turn (living heroes only) and the slot each one belongs to.</summary>
        public List<DieDefinition> DiceToThrow(out List<int> slots)
        {
            slots = LivingHeroes.Select(h => h.Slot).ToList();
            return LivingHeroes.Select(h => h.Run.Die).ToList();
        }

        /// <summary>
        /// Results from the box, already mapped to party slots. In Throw this opens the reroll
        /// decision; in Reroll it ends it; in Act it completes a Reroll-ally re-throw.
        /// </summary>
        public void ReceiveRoll(IEnumerable<(int slot, RollResult result)> results)
        {
            var list = results.ToList();
            switch (Phase)
            {
                case CombatPhase.Throw:
                    m_dice.Clear();
                    foreach (var (slot, result) in list.OrderBy(r => r.slot))
                        if (Hero(slot) is { IsAlive: true }) m_dice.Add(new CombatDie(slot, result));
                    Phase = RerollUsed ? CombatPhase.Act : CombatPhase.Reroll;
                    Say($"Turn {Turn}: {string.Join(" | ", m_dice.Select(d => $"{Hero(d.Slot).Name}: {d.Face}"))}");
                    break;

                case CombatPhase.Reroll:
                    UpdateDice(list);
                    RerollUsed = true;
                    Phase = CombatPhase.Act;
                    Say($"After reroll: {string.Join(" | ", m_dice.Select(d => $"{Hero(d.Slot).Name}: {d.Face}"))}");
                    break;

                case CombatPhase.Act:
                    UpdateDice(list);
                    if (PendingAllyReroll >= 0 && list.Any(r => r.slot == PendingAllyReroll))
                    {
                        Say($"{Hero(PendingAllyReroll).Name}'s die re-thrown: {Die(PendingAllyReroll).Face}.");
                        PendingAllyReroll = -1;
                    }
                    break;

                default:
                    return;
            }
            Changed?.Invoke();
        }

        private void UpdateDice(List<(int slot, RollResult result)> list)
        {
            foreach (var (slot, result) in list)
            {
                var die = Die(slot);
                if (die == null || die.Spent) continue;
                die.Result = result;
                die.MirroredFace = null;
            }
        }

        /// <summary>Keep every die as rolled.</summary>
        public void KeepAll()
        {
            if (Phase != CombatPhase.Reroll) return;
            Phase = CombatPhase.Act;
            Changed?.Invoke();
        }

        /// <summary>The player chose dice to re-throw; the box does the throwing and calls ReceiveRoll.</summary>
        public bool CanReroll => Phase == CombatPhase.Reroll && !RerollUsed;

        // ---------------------------------------------------------------- act: use or bank

        public static TargetKind TargetFor(DieFace face)
        {
            if (face == null || face.effects == null || face.effects.Length == 0) return TargetKind.None;
            return face.effects[0].kind switch
            {
                FaceEffectKind.Strike or FaceEffectKind.Jab or FaceEffectKind.Burn or FaceEffectKind.Poison
                    or FaceEffectKind.Weaken or FaceEffectKind.Hex => TargetKind.Enemy,
                FaceEffectKind.Shield or FaceEffectKind.Taunt => TargetKind.Hero,
                FaceEffectKind.Nudge or FaceEffectKind.Flip or FaceEffectKind.RerollAlly or FaceEffectKind.Mirror => TargetKind.Die,
                _ => TargetKind.None,
            };
        }

        public TargetKind TargetFor(int slot) => Die(slot) is { } die ? TargetFor(die.Face) : TargetKind.None;

        public bool CanAct(int slot) =>
            Phase == CombatPhase.Act && PendingAllyReroll < 0 && Die(slot) is { Spent: false } && Hero(slot) is { IsAlive: true };

        /// <summary>
        /// Resolve the die's face. <paramref name="target"/> is an enemy index, a hero slot or a die slot
        /// depending on <see cref="TargetFor(int)"/>; -1 means "self" where that makes sense.
        /// <paramref name="nudgeDelta"/> is +1 or −1 for Nudge.
        /// </summary>
        public bool UseDie(int slot, int target = -1, int nudgeDelta = 1)
        {
            if (!CanAct(slot)) return false;
            var die = Die(slot);
            var hero = Hero(slot);
            var face = die.Face;
            if (face.effects == null || face.effects.Length == 0)
            {
                die.Used = true;
                Say($"{hero.Name}: {face} does nothing.");
                Changed?.Invoke();
                return true;
            }

            // Mirror and Reroll-ally are special: they change dice instead of acting on units.
            var first = face.effects[0].kind;
            if (first == FaceEffectKind.Mirror) return Mirror(die, hero, target);
            if (first == FaceEffectKind.RerollAlly) return RerollAlly(die, hero, target);

            foreach (var entry in face.effects)
            {
                if (!Resolve(hero, entry, target, nudgeDelta, out string reason))
                {
                    Say($"{hero.Name}: cannot use {face}: {reason}.");
                    return false;
                }
            }
            die.Used = true;
            CheckVictory();
            Changed?.Invoke();
            return true;
        }

        private bool Resolve(CombatHero hero, FaceEffectEntry entry, int target, int nudgeDelta, out string reason)
        {
            reason = null;
            switch (entry.kind)
            {
                case FaceEffectKind.Strike:
                case FaceEffectKind.Jab:
                {
                    var enemy = LivingEnemy(target, out reason);
                    if (enemy == null) return false;
                    int dealt = DamageEnemy(enemy, entry.value);
                    Say($"{hero.Name} {entry.kind.ToString().ToLower()}s {enemy.Name} for {dealt}.");
                    return true;
                }
                case FaceEffectKind.Shield:
                {
                    var ally = target < 0 ? hero : Hero(target);
                    if (ally == null || !ally.IsAlive) { reason = "no such ally"; return false; }
                    ally.Shield += entry.value;
                    Say($"{hero.Name} shields {(ally == hero ? "themself" : ally.Name)} for {entry.value}.");
                    return true;
                }
                case FaceEffectKind.Taunt:
                    hero.Statuses.Set(StatusKind.Taunt, 1);
                    Say($"{hero.Name} taunts: enemy attacks go to them this turn.");
                    return true;

                case FaceEffectKind.Burn:
                case FaceEffectKind.Poison:
                {
                    var enemy = LivingEnemy(target, out reason);
                    if (enemy == null) return false;
                    var kind = entry.kind == FaceEffectKind.Burn ? StatusKind.Burn : StatusKind.Poison;
                    enemy.Statuses.Add(kind, entry.value);
                    Say($"{hero.Name} applies {kind} {entry.value} to {enemy.Name} (now {enemy.Statuses.Get(kind)}).");
                    return true;
                }
                case FaceEffectKind.Weaken:
                case FaceEffectKind.Hex:
                {
                    var enemy = LivingEnemy(target, out reason);
                    if (enemy == null) return false;
                    var kind = entry.kind == FaceEffectKind.Weaken ? StatusKind.Weaken : StatusKind.Hex;
                    enemy.Statuses.Set(kind, Math.Max(1, entry.value));
                    Say($"{hero.Name} {(kind == StatusKind.Weaken ? "weakens" : "hexes")} {enemy.Name}.");
                    return true;
                }
                case FaceEffectKind.Nudge:
                case FaceEffectKind.Flip:
                {
                    var targetDie = TargetableDie(target, out reason);
                    if (targetDie == null) return false;
                    IDieModifier modifier = entry.kind == FaceEffectKind.Nudge ? new NudgeModifier(nudgeDelta >= 0 ? 1 : -1) : new FlipModifier();
                    targetDie.Result.Apply(modifier);
                    targetDie.MirroredFace = null;
                    DieModified?.Invoke(targetDie.Slot, modifier);
                    Say($"{hero.Name} uses {modifier.Name} on {Hero(targetDie.Slot).Name}'s die: now {targetDie.Face}.");
                    return true;
                }
                case FaceEffectKind.Fizzle:
                case FaceEffectKind.Number:
                default:
                    Say($"{hero.Name}: {entry} fizzles.");
                    return true;
            }
        }

        private bool Mirror(CombatDie die, CombatHero hero, int target)
        {
            var source = TargetableDie(target, out string reason);
            if (source == null || source.Slot == die.Slot)
            {
                Say($"{hero.Name}: Mirror needs another die.");
                return false;
            }
            die.MirroredFace = source.Face.Clone();
            Say($"{hero.Name} mirrors {Hero(source.Slot).Name}'s {source.Face}.");
            Changed?.Invoke();
            return true; // the die is not spent: it now acts as the copied face
        }

        private bool RerollAlly(CombatDie die, CombatHero hero, int target)
        {
            var ally = TargetableDie(target, out string reason);
            if (ally == null || ally.Slot == die.Slot)
            {
                Say($"{hero.Name}: Reroll ally needs another unspent die.");
                return false;
            }
            die.Used = true;
            PendingAllyReroll = ally.Slot;
            Say($"{hero.Name} re-throws {Hero(ally.Slot).Name}'s die.");
            AllyRerollRequested?.Invoke(ally.Slot);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Bank the die for one mana of its hero's colour.</summary>
        public bool BankDie(int slot)
        {
            if (!CanAct(slot)) return false;
            var die = Die(slot);
            var hero = Hero(slot);
            AddMana(hero.Color, BankValue);
            die.Banked = true;
            Say($"{hero.Name} banks {die.Face} for 1 {hero.Color} mana.");
            Changed?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- signatures

        public bool CanCastSignature(int slot)
        {
            var hero = Hero(slot);
            return Phase == CombatPhase.Act && PendingAllyReroll < 0 && hero is { IsAlive: true }
                   && !hero.SignatureUsedThisTurn
                   && hero.Run.Definition.signatureKind != SignatureKind.None
                   && Mana(hero.Color) >= SignatureCost;
        }

        public static TargetKind SignatureTargetFor(SignatureKind kind) => kind switch
        {
            SignatureKind.Detonate => TargetKind.Enemy,
            SignatureKind.LoadedDice => TargetKind.Die,
            _ => TargetKind.None,
        };

        /// <summary>Cast the hero's signature for 3 mana of its colour. <paramref name="faceIndex"/> is for Loaded Dice.</summary>
        public bool CastSignature(int slot, int target = -1, int faceIndex = 0)
        {
            if (!CanCastSignature(slot)) return false;
            var hero = Hero(slot);
            var kind = hero.Run.Definition.signatureKind;
            string name = hero.Run.Definition.signatureName;

            switch (kind)
            {
                case SignatureKind.Bulwark:
                    foreach (var ally in LivingHeroes) ally.Shield += 6;
                    Say($"{hero.Name} casts {name}: Shield 6 to all allies.");
                    break;

                case SignatureKind.Detonate:
                {
                    var enemy = LivingEnemy(target, out string reason);
                    if (enemy == null) { Say($"{hero.Name}: {name} needs an enemy."); return false; }
                    int stacks = enemy.Statuses.Total;
                    enemy.Statuses.Clear();
                    int dealt = DamageEnemy(enemy, stacks);
                    Say($"{hero.Name} casts {name} on {enemy.Name}: {stacks} stacks consumed, {dealt} damage.");
                    break;
                }
                case SignatureKind.LoadedDice:
                {
                    var die = TargetableDie(target, out string reason);
                    if (die == null || !DieDefinition.IsValidFaceIndex(faceIndex)) { Say($"{hero.Name}: {name} needs an unspent die and a face 1–6."); return false; }
                    var modifier = new ChooseResultModifier(faceIndex);
                    die.Result.Apply(modifier);
                    die.MirroredFace = null;
                    DieModified?.Invoke(die.Slot, modifier);
                    Say($"{hero.Name} casts {name}: {Hero(die.Slot).Name}'s die set to {die.Face}.");
                    break;
                }
                default:
                    return false;
            }

            AddMana(hero.Color, -SignatureCost);
            hero.SignatureUsedThisTurn = true;
            CheckVictory();
            Changed?.Invoke();
            return true;
        }

        // ---------------------------------------------------------------- enemy turn

        /// <summary>End the player's turn: enemies act, statuses tick, then a new turn begins (or the fight ends).</summary>
        public void EndHeroTurn()
        {
            if (Phase != CombatPhase.Act || PendingAllyReroll >= 0) return;
            Phase = CombatPhase.EnemyTurn;
            Say($"— Enemy turn {Turn} —");

            // Burn: start of the enemy's turn, then decay.
            foreach (var enemy in LivingEnemies.ToList())
            {
                int burn = enemy.Statuses.Get(StatusKind.Burn);
                if (burn <= 0) continue;
                int lost = enemy.LoseHealth(burn);
                enemy.Statuses.Set(StatusKind.Burn, burn - 1);
                Say($"{enemy.Name} burns for {lost}.");
            }

            // Intents.
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
                        int dealt = target.TakeDamage(Math.Max(0, intent.value - weaken));
                        Say($"{enemy.Name} attacks {target.Name} for {intent.value - weaken}{(dealt < intent.value - weaken ? $" ({dealt} through shield)" : "")}.{(target.IsAlive ? "" : $" {target.Name} falls!")}");
                        break;
                    }
                    case IntentKind.AttackAll:
                        foreach (var target in LivingHeroes.ToList())
                        {
                            target.TakeDamage(Math.Max(0, intent.value - weaken));
                            if (!target.IsAlive) Say($"{target.Name} falls!");
                        }
                        Say($"{enemy.Name} attacks everyone for {intent.value - weaken}.");
                        break;
                    case IntentKind.Block:
                        enemy.Shield += intent.value;
                        Say($"{enemy.Name} blocks for {intent.value}.");
                        break;
                }
            }

            // Poison: end of the enemy's turn, no decay. One-turn statuses expire.
            foreach (var enemy in LivingEnemies.ToList())
            {
                int poison = enemy.Statuses.Get(StatusKind.Poison);
                if (poison > 0) Say($"{enemy.Name} suffers {enemy.LoseHealth(poison)} poison.");
                enemy.Statuses.Remove(StatusKind.Weaken);
                enemy.Statuses.Remove(StatusKind.Hex);
            }

            // Hero shields and taunts expire with the enemy turn.
            foreach (var hero in m_heroes)
            {
                hero.Shield = 0;
                hero.Statuses.Remove(StatusKind.Taunt);
                hero.SignatureUsedThisTurn = false;
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
                RerollUsed = false;
                m_dice.Clear();
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
            return enemy.TakeDamage(amount);
        }

        private void CheckVictory()
        {
            if (!LivingEnemies.Any() && !IsOver)
            {
                Phase = CombatPhase.Won;
                Say("Victory!");
            }
        }

        private CombatEnemy LivingEnemy(int index, out string reason)
        {
            reason = null;
            var enemy = index >= 0 && index < m_enemies.Count ? m_enemies[index] : null;
            if (enemy == null || !enemy.IsAlive)
            {
                reason = "needs a living enemy";
                return null;
            }
            return enemy;
        }

        private CombatDie TargetableDie(int slot, out string reason)
        {
            reason = null;
            var die = Die(slot);
            if (die == null || die.Spent || Hero(slot) is not { IsAlive: true })
            {
                reason = "needs an unspent die";
                return null;
            }
            return die;
        }

        private void AddMana(DieColor color, int amount)
        {
            switch (color)
            {
                case DieColor.Red: Red = Math.Max(0, Red + amount); break;
                case DieColor.Blue: Blue = Math.Max(0, Blue + amount); break;
                case DieColor.Yellow: Yellow = Math.Max(0, Yellow + amount); break;
            }
        }

        private void Say(string line) => m_log.Add(line);
    }
}
