#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 SpellQueue.cs is part of SFXSivir.

 SFXSivir is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXSivir is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXSivir. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using LeagueSharp;
using LeagueSharp.Common;
using SFXLibrary.Logger;

#endregion

namespace SFXSivir.Helpers
{
    public class SpellQueue
    {
        private static float _sendTime;
        public static bool Enabled { get; set; }

        public static bool IsBusy
        {
            get
            {
                var busy = _sendTime > 0 && _sendTime + (Game.Ping / 2000f) - Game.Time > 0 ||
                           ObjectManager.Player.Spellbook.IsCastingSpell || ObjectManager.Player.Spellbook.IsChanneling ||
                           ObjectManager.Player.Spellbook.IsCharging;

                IsBusy = busy;

                return busy;
            }
            private set
            {
                if (!value)
                {
                    _sendTime = 0;
                }
            }
        }

        public static bool IsReady
        {
            get { return !IsBusy; }
        }

        public static void Initialize()
        {
            Spellbook.OnCastSpell += OnSpellbookCastSpell;
            Obj_AI_Base.OnProcessSpellCast += OnObjAiBaseProcessSpellCast;
            Spellbook.OnStopCast += OnSpellbookStopCast;
        }

        private static void OnSpellbookCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (!Enabled)
            {
                return;
            }
            try
            {
                if (sender.Owner.IsMe)
                {
                    switch (args.Slot)
                    {
                        case SpellSlot.Q:
                        case SpellSlot.W:
                        case SpellSlot.E:
                        case SpellSlot.R:
                            if (IsReady)
                            {
                                _sendTime = Game.Time;
                            }
                            else
                            {
                                args.Process = false;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private static void OnObjAiBaseProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!Enabled)
            {
                return;
            }
            try
            {
                if (sender.IsMe && !args.SData.IsAutoAttack())
                {
                    IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private static void OnSpellbookStopCast(Spellbook sender, SpellbookStopCastEventArgs args)
        {
            if (!Enabled)
            {
                return;
            }
            try
            {
                if (sender.Owner.IsMe)
                {
                    IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}