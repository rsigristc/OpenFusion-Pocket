using System;
using OpenFusion.Net;
using UnityEngine;

namespace OpenFusion.World
{
    public sealed partial class SocialMenu
    {
        bool addingBuddy, focusBuddy;
        string buddyName = "", buddyError = "";
        long ownUid;
        float nextBuddyLookup;
        TouchScreenKeyboard buddyKeyboard;

        void ResetSocial(long uid)
        {
            ClearRecall(); CloseAddBuddy(); pops.Clear(); reporting = false; ownUid = uid;
            Array.Clear(bud, 0, bud.Length); Array.Clear(slotUid, 0, slotUid.Length); Array.Clear(slotBlk, 0, slotBlk.Length);
            GetComponent<ChatHud>()?.SetBuddyTarget(0, -1);
            nextBuddyLookup = 0; StateVersion++;
        }

        // Legacy uses fixed UTF-16 strings: 9 and 17 code units including NUL.
        // Reject overlong names instead of silently looking up a different player.
        public static bool TrySplitBuddyName(string text, out string first, out string last)
        {
            first = last = "";
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (char c in text) if (char.IsControl(c)) return false;
            var parts = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;
            first = parts[0]; last = string.Join(" ", parts, 1, parts.Length - 1);
            return first.Length <= 8 && last.Length <= 16;
        }

        public bool RequestBuddyByName(string text)
        {
            if (!TrySplitBuddyName(text, out var first, out var last)) {
                buddyError = Loc.T("Enter a first name (up to 8 characters) and last name (up to 16 characters)."); return false;
            }
            if (NpcUI.Sender == null || Time.unscaledTime < nextBuddyLookup) return false;
            var p = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_FIND_NAME_MAKE_BUDDY, 52);
            ClientPackets.PutStr(p, 4, first, 9); ClientPackets.PutStr(p, 22, last, 17);
            nextBuddyLookup = Time.unscaledTime + 1f;
            Send(p); CloseAddBuddy(); return true;
        }

        public void OpenAddBuddy()
        {
            if (Active || NpcUI.Sender == null) return;
            ChatHud.Instance?.Close(); addingBuddy = focusBuddy = true; buddyName = buddyError = "";
            if (Application.isMobilePlatform) OpenBuddyKeyboard();
        }
        void OpenBuddyKeyboard()
        {
            if (buddyKeyboard == null && Application.isMobilePlatform)
                buddyKeyboard = TouchScreenKeyboard.Open(buddyName, TouchScreenKeyboardType.Default, false, false, false, false, "", 32);
        }
        void StopBuddyKeyboard() { if (buddyKeyboard != null) buddyKeyboard.active = false; buddyKeyboard = null; }
        void CloseAddBuddy() { addingBuddy = focusBuddy = false; StopBuddyKeyboard(); buddyName = buddyError = ""; }
        void OnDisable() { CloseAddBuddy(); }
        void UpdateAddBuddy()
        {
            if (!addingBuddy) return;
            if (pops.Count > 0) { StopBuddyKeyboard(); return; }
            if (buddyKeyboard != null) {
                buddyName = buddyKeyboard.text ?? "";
                if (buddyKeyboard.status == TouchScreenKeyboard.Status.Canceled) { CloseAddBuddy(); return; }
                if (buddyKeyboard.status != TouchScreenKeyboard.Status.Visible) StopBuddyKeyboard();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) CloseAddBuddy();
        }

        void SendNamedBuddyAnswer(Pop p, bool accept)
        {
            var packet = ClientPackets.New(PacketIds.P_CL2FE_REQ_PC_FIND_NAME_ACCEPT_BUDDY, 64);
            ClientPackets.PutInt(packet, 4, accept ? 1 : 0); PutLong(packet, 8, p.FromUid);
            ClientPackets.PutStr(packet, 16, p.First, 9); ClientPackets.PutStr(packet, 34, p.Last, 17);
            Send(packet);
        }
        bool HandleNamedBuddyPacket(uint type, byte[] b)
        {
            if (type == PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC) {
                if (b.Length < 64) return true;
                string first = WStr(b, 0, 9), last = WStr(b, 18, 17); long uid = L(b, 52); bool checkedName = b[60] == 1;
                q.Enqueue(() => {
                    if (uid <= 0 || uid == ownUid) return;
                    var invite = new Pop { Kind = 4, FromUid = uid, First = checkedName ? first : "Player", Last = checkedName ? last : uid.ToString(),
                        Title = Loc.T("BUDDY INVITE RECEIVED"), Deadline = Time.unscaledTime + 30f };
                    if (!GameOptions.Cur.AllowBuddy || IsBlockedUid(uid)) { SendNamedBuddyAnswer(invite, false); return; }
                    if (pops.Exists(p => (p.Kind == 4 || p.Kind == 2) && p.FromUid == uid)) return;
                    invite.Text = Msg(69).Replace("%s", invite.First + " " + invite.Last); pops.Add(invite); Sfx.Ui("Open_Screen");
                }); return true;
            }
            if (type == PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_FAIL) {
                if (b.Length < 56) return true;
                string name = WStr(b, 0, 9) + " " + WStr(b, 18, 17); int error = I(b, 52);
                q.Enqueue(() => { int message = error == 1 ? 143 : error == 2 ? 144 : error == 3 ? 145 : error == 4 ? 146 : error == 5 ? 104 : 0; if (message > 0) Info(message, name, true); });
                return true;
            }
            if (type == PacketIds.P_FE2CL_REP_PC_FIND_NAME_ACCEPT_BUDDY_FAIL) {
                if (b.Length < 68) return true;
                long uid = L(b, 52); string name = b[60] == 1 ? WStr(b, 0, 9) + " " + WStr(b, 18, 17) : "Player " + uid; int error = I(b, 64);
                q.Enqueue(() => {
                    if (uid == ownUid) return;
                    int message = error == 1 ? 144 : error == 2 ? 104 : error == 3 || error == 6 ? 138 : error == 4 ? 145 : error == 5 ? 146 : 0;
                    if (message > 0) Info(message, name, true);
                }); return true;
            }
            return false;
        }

        void DrawAddBuddy(Event e, SkinStyle red, SkinStyle cancel, SkinStyle lab)
        {
            var r = new Rect(-263, -100, 526, 200); Panel(r, e); GUI.BeginGroup(r);
            try {
                Skin.Label(new Rect(176, 12, 230, 24), Loc.T("ADD BUDDY"), lab, Color.yellow);
                Skin.Label(new Rect(40, 38, 446, 30), Loc.T("Please enter a character's full name to add a buddy"), lab, Color.white);
                var input = new Rect(55, 76, 416, 28);
                if (field == null) field = new GUIStyle(GUI.skin.textField) { fontSize = 14 };
                if (Application.isMobilePlatform) {
                    if (GUI.Button(input, string.IsNullOrEmpty(buddyName) ? Loc.T("First name Last name") : buddyName, field)) OpenBuddyKeyboard();
                } else {
                    GUI.SetNextControlName("addbuddyname"); buddyName = GUI.TextField(input, buddyName, 32, field);
                    if (focusBuddy) { GUI.FocusControl("addbuddyname"); focusBuddy = false; }
                }
                Skin.Label(new Rect(40, 108, 446, 40), buddyError, lab, Color.yellow);
                bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
                if (Skin.Button(new Rect(40, 160, 152, 27), Loc.T("CANCEL"), cancel)) { CloseAddBuddy(); return; }
                bool clicked = Skin.Button(new Rect(335, 160, 152, 27), Loc.T("ADD"), red);
                if (clicked || enter) { if (enter) e.Use(); RequestBuddyByName(buddyName); }
            } finally { GUI.EndGroup(); }
        }
    }
}
