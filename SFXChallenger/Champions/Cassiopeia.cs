﻿#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Cassiopeia.cs is part of SFXChallenger.

 SFXChallenger is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXChallenger is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXChallenger. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXChallenger.Abstracts;
using SFXChallenger.Enumerations;
using SFXChallenger.Helpers;
using SFXChallenger.Managers;
using SFXChallenger.Menus;
using SFXChallenger.Wrappers;
using SFXLibrary;
using SFXLibrary.Extensions.NET;
using SFXLibrary.Logger;
using MinionManager = SFXLibrary.MinionManager;
using MinionOrderTypes = SFXLibrary.MinionOrderTypes;
using MinionTeam = SFXLibrary.MinionTeam;
using MinionTypes = SFXLibrary.MinionTypes;
using Orbwalking = SFXChallenger.Wrappers.Orbwalking;
using TargetSelector = SFXChallenger.Wrappers.TargetSelector;
using Utils = SFXChallenger.Helpers.Utils;

#endregion

namespace SFXChallenger.Champions
{
    internal class Cassiopeia : TChampion
    {
        private float _lastEEndTime;
        private float _lastPoisonClearDelay;
        private float _lastQPoisonDelay;
        private Obj_AI_Base _lastQPoisonT;
        public Cassiopeia() : base(1500f) {}

        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override void OnLoad()
        {
            Core.OnPreUpdate += OnCorePreUpdate;
            Core.OnPostUpdate += OnCorePostUpdate;
            Orbwalking.BeforeAttack += OnOrbwalkingBeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget += OnInterruptableTarget;
            CustomEvents.Unit.OnDash += OnUnitDash;
        }

        protected override void OnUnload()
        {
            Core.OnPreUpdate -= OnCorePreUpdate;
            Core.OnPostUpdate -= OnCorePostUpdate;
            Orbwalking.BeforeAttack -= OnOrbwalkingBeforeAttack;
            AntiGapcloser.OnEnemyGapcloser -= OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget -= OnInterruptableTarget;
            CustomEvents.Unit.OnDash -= OnUnitDash;
        }

        protected override void AddToMenu()
        {
            DrawingManager.Add("R " + Global.Lang.Get("G_Flash"), R.Range + SummonerManager.Flash.Range);

            var comboMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Combo"), Menu.Name + ".combo"));
            HitchanceManager.AddToMenu(
                comboMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), comboMenu.Name + ".hitchance")), "combo",
                new Dictionary<string, int> { { "Q", 2 }, { "W", 1 }, { "R", 2 } });
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".w", Global.Lang.Get("G_UseW")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var harassMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Harass"), Menu.Name + ".harass"));
            HitchanceManager.AddToMenu(
                harassMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), harassMenu.Name + ".hitchance")), "harass",
                new Dictionary<string, int> { { "Q", 2 }, { "W", 1 } });
            ManaManager.AddToMenu(
                harassMenu, "harass", ManaCheckType.Minimum, ManaValueType.Total, string.Empty, 70, 0, 750);
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".w", Global.Lang.Get("G_UseW")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var laneclearMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LaneClear"), Menu.Name + ".lane-clear"));
            ManaManager.AddToMenu(
                laneclearMenu, "lane-clear", ManaCheckType.Minimum, ManaValueType.Total, string.Empty, 90, 0, 750);
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".aa", Global.Lang.Get("G_UseAutoAttacks")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".w", Global.Lang.Get("G_UseW")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var lasthitMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LastHit"), Menu.Name + ".lasthit"));
            ManaManager.AddToMenu(
                lasthitMenu, "lasthit", ManaCheckType.Maximum, ManaValueType.Percent, string.Empty, 70);
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));
            lasthitMenu.AddItem(
                new MenuItem(
                    lasthitMenu.Name + ".e-poison", Global.Lang.Get("G_UseE") + " " + Global.Lang.Get("G_Poison"))
                    .SetValue(true));

            var ultimateMenu = UltimateMenu.AddToMenu(Menu, true, true, true, true, true, true, true);

            ultimateMenu.AddItem(
                new MenuItem(ultimateMenu.Name + ".range", Global.Lang.Get("G_Range")).SetValue(
                    new Slider(700, 400, 825))).ValueChanged += delegate(object sender, OnValueChangeEventArgs args)
                    {
                        R.Range = args.GetNewValue<Slider>().Value;
                        DrawingManager.Update(
                            "R " + Global.Lang.Get("G_Flash"),
                            args.GetNewValue<Slider>().Value + SummonerManager.Flash.Range);
                    };

            var killstealMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Killsteal"), Menu.Name + ".killsteal"));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));
            killstealMenu.AddItem(
                new MenuItem(
                    killstealMenu.Name + ".e-poison", Global.Lang.Get("G_UseE") + " " + Global.Lang.Get("G_Poison"))
                    .SetValue(true));

            var fleeMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Flee"), Menu.Name + ".flee"));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".w", Global.Lang.Get("G_UseW")).SetValue(true));

            var miscMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Miscellaneous"), Menu.Name + ".miscellaneous"));

            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("Q " + Global.Lang.Get("G_Dash"), miscMenu.Name + "q-dash")), "q-dash",
                false, false, true, false);
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("Q " + Global.Lang.Get("G_Gapcloser"), miscMenu.Name + "q-gapcloser")),
                "q-gapcloser", false, false, true, false);
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("Q " + Global.Lang.Get("G_Fleeing"), miscMenu.Name + "q-fleeing")),
                "q-fleeing", false, false, true, false);
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("W " + Global.Lang.Get("G_Stunned"), miscMenu.Name + "w-stunned")),
                "w-stunned", false, false, true, false);
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("W " + Global.Lang.Get("G_Dash"), miscMenu.Name + "w-dash")), "w-dash",
                false, false, true, false);
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("W " + Global.Lang.Get("G_Gapcloser"), miscMenu.Name + "w-gapcloser")),
                "w-gapcloser", false, false, true, false);
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("W " + Global.Lang.Get("G_Fleeing"), miscMenu.Name + "w-fleeing")),
                "w-fleeing", false, false, true, false);

            R.Range = Menu.Item(Menu.Name + ".ultimate.range").GetValue<Slider>().Value;
            DrawingManager.Update(
                "R " + Global.Lang.Get("G_Flash"),
                Menu.Item(Menu.Name + ".ultimate.range").GetValue<Slider>().Value + SummonerManager.Flash.Range);

            IndicatorManager.AddToMenu(DrawingManager.GetMenu(), true);
            IndicatorManager.Add(Q);
            IndicatorManager.Add(W);
            IndicatorManager.Add("E", hero => E.GetDamage(hero) * 5);
            IndicatorManager.Add(R);
            IndicatorManager.Finale();

            TargetSelector.AddWeightedItem(
                new WeightedItem(
                    "poison-time", Global.Lang.Get("Cassiopeia_PoisonTime"), 10, true, 333, 500, GetPoisonBuffEndTime));
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 850f);
            Q.SetSkillshot(0.6f, 60f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            W = new Spell(SpellSlot.W, 850f);
            W.SetSkillshot(0.7f, 125f, 2500f, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 700f);
            E.SetTargetted(0.2f, 1700f);
            E.Collision = true;

            R = new Spell(SpellSlot.R, 825f);
            R.SetSkillshot(0.8f, (float) (80 * Math.PI / 180), float.MaxValue, false, SkillshotType.SkillshotCone);
        }

        private void OnCorePreUpdate(EventArgs args)
        {
            try
            {
                if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit && ManaManager.Check("lasthit")) &&
                    E.IsReady())
                {
                    var ePoison = Menu.Item(Menu.Name + ".lasthit.e-poison").GetValue<bool>();
                    var eHit = Menu.Item(Menu.Name + ".lasthit.e").GetValue<bool>();
                    if (eHit || ePoison)
                    {
                        var m =
                            MinionManager.GetMinions(
                                Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.NotAlly)
                                .FirstOrDefault(
                                    e =>
                                        e.Health < E.GetDamage(e) - 5 &&
                                        (ePoison && GetPoisonBuffEndTime(e) > E.GetSpellDelay(e) || eHit));
                        if (m != null)
                        {
                            Casting.TargetSkill(m, E);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnCorePostUpdate(EventArgs args)
        {
            try
            {
                if (Menu.Item(Menu.Name + ".ultimate.flash.enabled").GetValue<bool>() &&
                    Menu.Item(Menu.Name + ".ultimate.flash.hotkey").GetValue<KeyBind>().Active && R.IsReady() &&
                    SummonerManager.Flash.IsReady())
                {
                    if (Menu.Item(Menu.Name + ".ultimate.flash.move-cursor").GetValue<bool>())
                    {
                        Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                    }
                    var targets =
                        Targets.Where(
                            t =>
                                t.Distance(Player) < R.Range + SummonerManager.Flash.Range && !t.IsDashing() &&
                                (t.IsFacing(Player)
                                    ? (t.Distance(Player))
                                    : (Prediction.GetPrediction(t, R.Delay + 0.3f)
                                        .UnitPosition.Distance(Player.Position))) > R.Range * 1.025f);
                    foreach (var target in targets)
                    {
                        var min = Menu.Item(Menu.Name + ".ultimate.flash.min").GetValue<Slider>().Value;
                        var flashPos = Player.Position.Extend(target.Position, SummonerManager.Flash.Range);
                        var pred =
                            Prediction.GetPrediction(
                                new PredictionInput
                                {
                                    Aoe = true,
                                    Collision = false,
                                    CollisionObjects = new[] { CollisionableObjects.YasuoWall },
                                    From = flashPos,
                                    RangeCheckFrom = flashPos,
                                    Delay = R.Delay + 0.3f,
                                    Range = R.Range,
                                    Speed = R.Speed,
                                    Radius = R.Width,
                                    Type = R.Type,
                                    Unit = target
                                });
                        if (pred.Hitchance >= R.GetHitChance("combo"))
                        {
                            R.UpdateSourcePosition(flashPos, flashPos);
                            var hits =
                                GameObjects.EnemyHeroes.Where(enemy => R.WillHit(enemy, pred.CastPosition)).ToList();
                            if (hits.Any(hit => HeroListManager.Check("ultimate-whitelist", hit)) ||
                                hits.Any(hit => HeroListManager.Check("ultimate-force", hit)) &&
                                hits.Count >=
                                (Menu.Item(Menu.Name + ".ultimate.force.additional").GetValue<Slider>().Value + 1))
                            {
                                if (hits.Count >= min)
                                {
                                    R.Cast(
                                        Player.Position.Extend(
                                            pred.CastPosition, -(Player.Position.Distance(pred.CastPosition) * 2)), true);
                                    Utility.DelayAction.Add(300, () => SummonerManager.Flash.Cast(flashPos));
                                }
                                else if (Menu.Item(Menu.Name + ".ultimate.flash.1v1").GetValue<bool>())
                                {
                                    var cDmg = CalcComboDamage(
                                        target, Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady(),
                                        Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() && W.IsReady(),
                                        Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady(), true);
                                    if (cDmg - 20 >= target.Health)
                                    {
                                        R.Cast(
                                            Player.Position.Extend(
                                                pred.CastPosition, -(Player.Position.Distance(pred.CastPosition) * 2)),
                                            true);
                                        Utility.DelayAction.Add(300, () => SummonerManager.Flash.Cast(flashPos));
                                    }
                                }
                            }
                            R.UpdateSourcePosition();
                        }
                    }
                }

                if (Menu.Item(Menu.Name + ".ultimate.assisted.enabled").GetValue<bool>() &&
                    Menu.Item(Menu.Name + ".ultimate.assisted.hotkey").GetValue<KeyBind>().Active && R.IsReady())
                {
                    if (Menu.Item(Menu.Name + ".ultimate.assisted.move-cursor").GetValue<bool>())
                    {
                        Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                    }

                    if (
                        !RLogic(
                            R.GetHitChance("combo"),
                            Menu.Item(Menu.Name + ".ultimate.assisted.min").GetValue<Slider>().Value))
                    {
                        if (Menu.Item(Menu.Name + ".ultimate.assisted.1v1").GetValue<bool>())
                        {
                            RLogic1V1(
                                R.GetHitChance("combo"),
                                Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady(),
                                Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() && W.IsReady(),
                                Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady(), false);
                        }
                    }
                }

                if (Menu.Item(Menu.Name + ".ultimate.auto.enabled").GetValue<bool>() && R.IsReady())
                {
                    if (
                        !RLogic(
                            R.GetHitChance("combo"),
                            Menu.Item(Menu.Name + ".ultimate.auto.min").GetValue<Slider>().Value))
                    {
                        if (Menu.Item(Menu.Name + ".ultimate.auto.1v1").GetValue<bool>())
                        {
                            RLogic1V1(
                                R.GetHitChance("combo"),
                                Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady(),
                                Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() && W.IsReady(),
                                Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady());
                        }
                    }
                }

                if (HeroListManager.Enabled("w-stunned") && W.IsReady())
                {
                    var target = Targets.FirstOrDefault(
                        t => HeroListManager.Check("w-stunned", t) && Utils.IsStunned(t));
                    if (target != null)
                    {
                        Casting.SkillShot(target, W, W.GetHitChance("harass"));
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnOrbwalkingBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            try
            {
                var t = args.Target as Obj_AI_Hero;
                if (t != null &&
                    (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo ||
                     Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed))
                {
                    args.Process = Q.Instance.ManaCost > Player.Mana && W.Instance.ManaCost > Player.Mana &&
                                   (E.Instance.ManaCost > Player.Mana || GetPoisonBuffEndTime(t) < E.GetSpellDelay(t));
                }
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                {
                    args.Process = Menu.Item(Menu.Name + ".lane-clear.aa").GetValue<bool>();
                    if (args.Process == false)
                    {
                        var m = args.Target as Obj_AI_Minion;
                        if (m != null && (_lastEEndTime < Game.Time || E.IsReady()) ||
                            (GetPoisonBuffEndTime(m) < E.GetSpellDelay(m) || E.Instance.ManaCost > Player.Mana) ||
                            !ManaManager.Check("lane-clear"))
                        {
                            args.Process = true;
                        }
                    }
                }

                if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit))
                {
                    var m = args.Target as Obj_AI_Minion;
                    if (m != null && E.CanCast(m))
                    {
                        if (E.Instance.ManaCost < Player.Mana)
                        {
                            args.Process = Menu.Item(Menu.Name + ".lasthit.e").GetValue<bool>() ||
                                           (Menu.Item(Menu.Name + ".lasthit.e-poison").GetValue<bool>() &&
                                            GetPoisonBuffEndTime(m) > E.GetSpellDelay(m)) &&
                                           ManaManager.Check("lasthit");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnUnitDash(Obj_AI_Base sender, Dash.DashItem args)
        {
            try
            {
                var hero = sender as Obj_AI_Hero;
                if (!sender.IsEnemy || hero == null)
                {
                    return;
                }

                var endTick = Game.Time - Game.Ping / 2000f + (args.EndPos.Distance(args.StartPos) / args.Speed);

                var wCasted = false;
                if (HeroListManager.Check("w-dash", hero) && Player.Distance(args.EndPos) <= W.Range && W.IsReady())
                {
                    var delay = (int) (endTick - Game.Time - W.Delay - 0.1f);
                    if (delay > 0)
                    {
                        Utility.DelayAction.Add(delay * 1000, () => W.Cast(args.EndPos));
                    }
                    else
                    {
                        W.Cast(args.EndPos);
                    }
                    wCasted = true;
                }

                if (!wCasted && HeroListManager.Check("q-dash", hero) && Player.Distance(args.EndPos) <= Q.Range &&
                    Q.IsReady())
                {
                    var delay = (int) (endTick - Game.Time - Q.Delay - 0.1f);
                    if (delay > 0)
                    {
                        Utility.DelayAction.Add(delay * 1000, () => Q.Cast(args.EndPos));
                    }
                    else
                    {
                        Q.Cast(args.EndPos);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            try
            {
                if (sender.IsEnemy && args.DangerLevel == Interrupter2.DangerLevel.High &&
                    Menu.Item(Menu.Name + ".ultimate.auto.enabled").GetValue<bool>() &&
                    HeroListManager.Check("ultimate-interrupt", sender) && sender.IsFacing(Player))
                {
                    Casting.SkillShot(sender, R, R.GetHitChance("combo"));
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnEnemyGapcloser(ActiveGapcloser args)
        {
            try
            {
                if (!args.Sender.IsEnemy)
                {
                    return;
                }

                if (Menu.Item(Menu.Name + ".ultimate.auto.enabled").GetValue<bool>() &&
                    HeroListManager.Check("ultimate-gapcloser", args.Sender))
                {
                    if (args.End.Distance(Player.Position) < R.Range)
                    {
                        R.Cast(args.End);
                    }
                }
                var wCasted = false;
                if (HeroListManager.Check("w-gapcloser", args.Sender) && Player.Distance(args.End) <= W.Range &&
                    W.IsReady())
                {
                    W.Cast(args.End);
                    wCasted = true;
                }

                if (!wCasted && HeroListManager.Check("q-gapcloser", args.Sender) &&
                    Player.Distance(args.End) <= Q.Range && Q.IsReady())
                {
                    Q.Cast(args.End);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void Combo()
        {
            var q = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady();
            var w = Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() && W.IsReady();
            var e = Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady();
            var r = Menu.Item(Menu.Name + ".ultimate.combo.enabled").GetValue<bool>() && R.IsReady();

            if (q)
            {
                QLogic(Q.GetHitChance("combo"));
            }
            if (w)
            {
                WLogic(W.GetHitChance("combo"));
            }
            if (e)
            {
                ELogic();
            }
            if (r)
            {
                if (
                    !RLogic(
                        R.GetHitChance("combo"), Menu.Item(Menu.Name + ".ultimate.combo.min").GetValue<Slider>().Value))
                {
                    if (Menu.Item(Menu.Name + ".ultimate.combo.1v1").GetValue<bool>())
                    {
                        RLogic1V1(R.GetHitChance("combo"), q, w, e);
                    }
                }
            }
            var target = Targets.FirstOrDefault(t => t.IsValidTarget(R.Range));
            if (target != null && CalcComboDamage(target, q, w, e, r) > target.Health)
            {
                ItemManager.UseComboItems(target);
                SummonerManager.UseComboSummoners(target);
            }
        }

        private float CalcComboDamage(Obj_AI_Hero target, bool q, bool w, bool e, bool r)
        {
            try
            {
                if (target == null)
                {
                    return 0;
                }
                var manaCost = (w && W.IsReady() ? W.Instance.ManaCost : (q ? Q.Instance.ManaCost : 0)) * 2;
                var damage = (w && W.IsReady() ? W.GetDamage(target) : (q ? Q.GetDamage(target) : 0)) * 2;

                if (e)
                {
                    var eMana = E.Instance.ManaCost;
                    var eDamage = E.GetDamage(target);
                    var count = target.IsNearTurret() && !target.IsFacing(Player) ||
                                target.IsNearTurret() && Player.HealthPercent <= 35 || !R.IsReady()
                        ? 5
                        : 10;
                    for (var i = 0; i < count; i++)
                    {
                        if (manaCost + eMana > Player.Mana)
                        {
                            break;
                        }
                        manaCost += eMana;
                        damage += eDamage;
                    }
                }
                if (r)
                {
                    if (manaCost + R.Instance.ManaCost - 10 > Player.Mana)
                    {
                        return damage;
                    }
                    return damage + (R.IsReady() ? R.GetDamage(target) : 0);
                }
                damage += ItemManager.CalculateComboDamage(target);
                damage += SummonerManager.CalculateComboDamage(target);
                return damage;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        private void RLogic1V1(HitChance hitChance, bool q, bool w, bool e, bool face = true)
        {
            try
            {
                foreach (var t in Targets)
                {
                    if ((!face || t.IsFacing(Player)) && t.HealthPercent > 25 && R.CanCast(t))
                    {
                        var cDmg = CalcComboDamage(t, q, w, e, true);
                        if (cDmg - 10 >= t.Health)
                        {
                            if (
                                GameObjects.EnemyHeroes.Count(
                                    em => !em.IsDead && em.IsVisible && em.Distance(Player) < 3000) == 1)
                            {
                                if (RLogic(hitChance, 1))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private bool RLogic(HitChance hitChance, int min)
        {
            try
            {
                foreach (var target in Targets.Where(t => R.CanCast(t)))
                {
                    var pred = R.GetPrediction(target, true);
                    if (pred.Hitchance >= hitChance)
                    {
                        var hits = GameObjects.EnemyHeroes.Where(enemy => R.WillHit(enemy, pred.CastPosition)).ToList();
                        if (hits.Count >= min &&
                            (hits.Any(h => HeroListManager.Check("ultimate-whitelist", h)) ||
                             hits.Any(h => HeroListManager.Check("ultimate-force", h)) &&
                             hits.Count >=
                             (Menu.Item(Menu.Name + ".ultimate.force.additional").GetValue<Slider>().Value + 1)))
                        {
                            R.Cast(pred.CastPosition);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }

        private void QLogic(HitChance hitChance)
        {
            try
            {
                var ts =
                    Targets.FirstOrDefault(
                        t =>
                            Q.CanCast(t) &&
                            (GetPoisonBuffEndTime(t) < Q.Delay * 1.2f ||
                             (HeroListManager.Check("q-fleeing", t) && !t.IsFacing(Player) && t.IsMoving &&
                              t.Distance(Player) > 150)));
                if (ts != null)
                {
                    _lastQPoisonDelay = Game.Time + Q.Delay;
                    _lastQPoisonT = ts;
                    Casting.SkillShot(ts, Q, hitChance);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void WLogic(HitChance hitChance)
        {
            try
            {
                var ts =
                    Targets.FirstOrDefault(
                        t =>
                            W.CanCast(t) &&
                            ((_lastQPoisonDelay < Game.Time && GetPoisonBuffEndTime(t) < W.Delay * 1.2 ||
                              _lastQPoisonT.NetworkId != t.NetworkId) ||
                             (HeroListManager.Check("w-fleeing", t) && !t.IsFacing(Player) && t.IsMoving &&
                              t.Distance(Player) > 150)));
                if (ts != null)
                {
                    Casting.SkillShot(ts, W, hitChance);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void ELogic()
        {
            try
            {
                var ts = Targets.FirstOrDefault(t => E.CanCast(t) && GetPoisonBuffEndTime(t) > E.GetSpellDelay(t));
                if (ts != null)
                {
                    var pred = E.GetPrediction(ts, false, -1f, new[] { CollisionableObjects.YasuoWall });
                    if (pred.Hitchance != HitChance.Collision)
                    {
                        E.Cast(ts);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void Harass()
        {
            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>())
            {
                QLogic(Q.GetHitChance("harass"));
            }
            if (Menu.Item(Menu.Name + ".harass.w").GetValue<bool>())
            {
                WLogic(W.GetHitChance("harass"));
            }
            if (Menu.Item(Menu.Name + ".harass.e").GetValue<bool>() && ManaManager.Check("harass"))
            {
                ELogic();
            }
        }

        private float GetPoisonBuffEndTime(Obj_AI_Base target)
        {
            try
            {
                var buffEndTime =
                    target.Buffs.Where(buff => buff.Type == BuffType.Poison)
                        .OrderByDescending(buff => buff.EndTime - Game.Time)
                        .Select(buff => buff.EndTime)
                        .FirstOrDefault();
                return buffEndTime;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        protected override void LaneClear()
        {
            var q = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady();
            var w = Menu.Item(Menu.Name + ".lane-clear.w").GetValue<bool>() && W.IsReady();

            if (Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady() &&
                ManaManager.Check("lane-clear"))
            {
                var minion =
                    MinionManager.GetMinions(
                        Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                        .Where(
                            e =>
                                GetPoisonBuffEndTime(e) > E.GetSpellDelay(e) &&
                                (e.Team == GameObjectTeam.Neutral ||
                                 (e.Health > E.GetDamage(e) * 2 || e.Health < E.GetDamage(e) - 5)))
                        .OrderByDescending(
                            m => m.CharData.BaseSkinName.Contains("MinionSiege", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault();
                if (minion != null)
                {
                    _lastEEndTime = Game.Time + E.GetSpellDelay(minion) + 0.1f;
                    Casting.TargetSkill(minion, E);
                }
            }

            if (q || w)
            {
                var minions =
                    MinionManager.GetMinions(Player.ServerPosition, Q.Range + Q.Width)
                        .Where(e => GetPoisonBuffEndTime(e) < Q.Delay * 1.1)
                        .OrderByDescending(
                            m => m.CharData.BaseSkinName.Contains("MinionSiege", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                if (minions.Any())
                {
                    if (q)
                    {
                        var prediction = Q.GetCircularFarmLocation(minions, Q.Width + 30);
                        if (prediction.MinionsHit > 1 && _lastPoisonClearDelay < Game.Time)
                        {
                            _lastPoisonClearDelay = Game.Time + Q.Delay;
                            Q.Cast(prediction.Position);
                        }
                    }
                    if (w)
                    {
                        var prediction = W.GetCircularFarmLocation(minions, W.Width + 50);
                        if (prediction.MinionsHit > 2 && _lastPoisonClearDelay < Game.Time)
                        {
                            _lastPoisonClearDelay = Game.Time + W.Delay;
                            W.Cast(prediction.Position);
                        }
                    }
                }
                else
                {
                    var creep =
                        MinionManager.GetMinions(
                            Player.ServerPosition, Q.Range + Q.Width, MinionTypes.All, MinionTeam.Neutral,
                            MinionOrderTypes.MaxHealth).FirstOrDefault(e => GetPoisonBuffEndTime(e) < Q.Delay * 1.1);
                    if (creep != null)
                    {
                        if (q)
                        {
                            Q.Cast(creep);
                        }
                        if (w)
                        {
                            W.Cast(creep);
                        }
                    }
                }
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.w").GetValue<bool>() && W.IsReady())
            {
                var near = GameObjects.EnemyHeroes.OrderBy(e => e.Distance(Player.Position)).FirstOrDefault();
                if (near != null)
                {
                    var pred = W.GetPrediction(near, true);
                    if (pred.Hitchance >= W.GetHitChance("harass"))
                    {
                        W.Cast(
                            Player.Position.Extend(
                                pred.CastPosition, Player.Position.Distance(pred.CastPosition) * 0.8f));
                    }
                }
            }
        }

        protected override void Killsteal()
        {
            var ePoison = Menu.Item(Menu.Name + ".killsteal.e-poison").GetValue<bool>();
            var eHit = Menu.Item(Menu.Name + ".killsteal.e").GetValue<bool>();
            if (ePoison || eHit && E.IsReady())
            {
                var m =
                    GameObjects.EnemyHeroes.FirstOrDefault(
                        e =>
                            E.CanCast(e) && e.Health < E.GetDamage(e) - 5 &&
                            (ePoison && GetPoisonBuffEndTime(e) > E.GetSpellDelay(e) || eHit));
                if (m != null)
                {
                    Casting.TargetSkill(m, E);
                }
            }
        }
    }
}