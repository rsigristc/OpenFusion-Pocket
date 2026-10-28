using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenFusion.Net;
using OpenFusion.World;
using UnityEditor;
using UnityEngine;

namespace OpenFusion.EditorTools
{
    public static class BuddyNameRegressionChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
        static IList Pops(object obj) => (IList)obj.GetType().GetField("pops", Private).GetValue(obj);
        static void I(byte[] b, int o, int n) => BitConverter.GetBytes(n).CopyTo(b, o);
        static void L(byte[] b, int o, long n) => BitConverter.GetBytes(n).CopyTo(b, o);
        static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        static void Dispatch(PacketDispatcher d, uint type, byte[] body) { var p = ClientPackets.New(type, body.Length); body.CopyTo(p, 4); d.Dispatch(p); }
        static byte[] Invite(long uid = 9001, bool approved = true) {
            var p = new byte[64]; System.Text.Encoding.Unicode.GetBytes("Ada").CopyTo(p, 0); System.Text.Encoding.Unicode.GetBytes("Byte").CopyTo(p, 18); L(p, 52, uid); p[60] = approved ? (byte)1 : (byte)0; return p;
        }
        public static void Batch() { try { Run(); EditorApplication.Exit(0); } catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); } }
        public static void Run()
        {
            Check(SocialMenu.TrySplitBuddyName("  Ada   Byte Code  ", out var first, out var last) && first == "Ada" && last == "Byte Code", "Full-name parser must preserve multiword last names while normalizing spaces.");
            foreach (var bad in new[] { "", "Ada", "Ada ", "ABCDEFGHI Byte", "Ada ABCDEFGHIJKLMNOPQ", "Ada\nByte" })
                Check(!SocialMenu.TrySplitBuddyName(bad, out _, out _), "Invalid/overlong names must not be silently truncated: " + bad);
            var host = new GameObject("Buddy name regression"); var sender = NpcUI.Sender;
            bool allowBuddy = GameOptions.Cur.AllowBuddy, allowGroup = GameOptions.Cur.AllowGroup;
            try {
                var d = new PacketDispatcher(); var world = host.AddComponent<WorldEntities>();
                typeof(WorldEntities).GetProperty("SelfId").GetSetMethod(true).Invoke(world, new object[] { 10 });
                var chat = host.AddComponent<ChatHud>(); chat.Init(null, world, d);
                var social = SocialMenu.Ensure(host); social.Init(d, world, null);
                Call(social, "OnEnter", new EnterSucc { Id = 10, Self = new PcAppearance { Uid = 1000 } }); Call(social, "Update");
                var sent = new List<byte[]>(); NpcUI.Sender = p => sent.Add(p); GameOptions.Cur.AllowBuddy = GameOptions.Cur.AllowGroup = true;
                social.OpenAddBuddy(); Check(SocialMenu.Active, "ADD editor must participate in modal input blocking.");
                Check(!social.RequestBuddyByName("Ada") && sent.Count == 0 && SocialMenu.Active, "Invalid input must remain editable and send nothing.");
                Check(social.RequestBuddyByName("Ada Byte") && !SocialMenu.Active && sent.Count == 1, "Valid full name must submit once and close the editor.");
                var request = sent[0];
                Check(request.Length == 56 && BitConverter.ToUInt32(request, 0) == PacketIds.P_CL2FE_REQ_PC_FIND_NAME_MAKE_BUDDY && System.Text.Encoding.Unicode.GetString(request, 4, 18).TrimEnd('\0') == "Ada" && System.Text.Encoding.Unicode.GetString(request, 22, 34).TrimEnd('\0') == "Byte", "Name lookup must match Legacy UTF-16 field offsets and size.");
                Check(!social.RequestBuddyByName("Ada Byte") && sent.Count == 1 && social.VisibleBuddyCount() == 0, "Repeated input must not duplicate requests or invent a buddy before server success."); sent.Clear();
                void Receive(uint type, byte[] body) { Dispatch(d, type, body); Call(social, "Update"); }
                Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC, new byte[63]);
                Check(Pops(social).Count == 0 && sent.Count == 0, "Truncated name invitation must be ignored atomically.");
                Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC, Invite(1000));
                Check(Pops(social).Count == 0, "Self invitation must not open a dialog.");
                Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC, Invite()); Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC, Invite());
                Check(Pops(social).Count == 1 && sent.Count == 0, "One invitation by UID, independent of nearby-avatar streaming; acceptance requires a user choice.");
                var accepted = Pops(social)[0]; Call(social, "Resolve", accepted, true); Call(social, "Resolve", accepted, true);
                Check(sent.Count == 1 && sent[0].Length == 68 && BitConverter.ToUInt32(sent[0], 0) == PacketIds.P_CL2FE_REQ_PC_FIND_NAME_ACCEPT_BUDDY && BitConverter.ToInt32(sent[0], 4) == 1 && BitConverter.ToInt64(sent[0], 8) == 9001 && System.Text.Encoding.Unicode.GetString(sent[0], 16, 18).TrimEnd('\0') == "Ada" && System.Text.Encoding.Unicode.GetString(sent[0], 34, 34).TrimEnd('\0') == "Byte", "Accept must use the named-buddy protocol, UID, strings and Pack=4 offsets.");
                Check(social.VisibleBuddyCount() == 0, "Accepting must not create a buddy until the server confirms."); sent.Clear();
                GameOptions.Cur.AllowBuddy = false; Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC, Invite());
                Check(Pops(social).Count == 0 && sent.Count == 1 && BitConverter.ToInt32(sent[0], 4) == 0, "Disabled buddy requests must automatically decline by the correct protocol."); sent.Clear(); GameOptions.Cur.AllowBuddy = true;
                var buddy = new byte[88]; buddy[13] = 1; I(buddy, 16, 101); L(buddy, 20, 9001); buddy[28] = buddy[30] = 1;
                Receive(PacketIds.P_FE2CL_REP_PC_BUDDYLIST_INFO_SUCC, buddy);
                var whisper = new byte[276]; L(whisper, 0, 9001); L(whisper, 8, 1000); System.Text.Encoding.Unicode.GetBytes("Private test").CopyTo(whisper, 16);
                Call(chat, "Handle", PacketIds.P_FE2CL_REP_SEND_BUDDY_FREECHAT_MESSAGE_SUCC, whisper);
                Check(((IList)typeof(ChatHud).GetField("allBuddyList", Private).GetValue(chat)).Count == 0, "A blocked UID must not enter private chat history.");
                Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC, Invite());
                Check(sent.Count == 1 && BitConverter.ToInt32(sent[0], 4) == 0 && Pops(social).Count == 0, "Blocked UIDs must be declined even without a streamed avatar."); sent.Clear();
                buddy[28] = 0; Receive(PacketIds.P_FE2CL_REP_PC_BUDDYLIST_INFO_SUCC, buddy);
                Call(chat, "Handle", PacketIds.P_FE2CL_REP_SEND_BUDDY_FREECHAT_MESSAGE_SUCC, whisper);
                Check(((IList)typeof(ChatHud).GetField("allBuddyList", Private).GetValue(chat)).Count == 1, "Server-confirmed unblocking must restore private messages.");
                var states = new byte[252]; I(states, 0, 202); states[200] = 1;
                Receive(PacketIds.P_FE2CL_REP_GET_BUDDY_STATE_SUCC, states);
                Check(social.BuddyAt(0).Id == 202 && social.BuddyAt(0).State == 1, "Buddy online status must refresh transient IDs without changing UID.");
                states[200] = 0; Receive(PacketIds.P_FE2CL_REP_GET_BUDDY_STATE_SUCC, states);
                Check(social.BuddyAt(0).State == 0 && social.BuddyAt(0).Uid == 9001, "Offline transition must preserve buddy identity.");
                var malformed = new byte[88]; malformed[13] = 2;
                Receive(PacketIds.P_FE2CL_REP_PC_BUDDYLIST_INFO_SUCC, malformed);
                Check(social.BuddyAt(0)?.Uid == 9001, "Truncated multi-entry buddy snapshot must not overwrite valid state.");
                Receive(PacketIds.P_FE2CL_REP_ACCEPT_MAKE_BUDDY_SUCC, new byte[20]);
                Check(social.BuddyAt(0)?.Uid == 9001, "Truncated accept success must not replace an existing buddy.");
                social.Notice("Older popup"); Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_SUCC, Invite(9002, false));
                var pending = Pops(social)[1]; pending.GetType().GetField("Deadline").SetValue(pending, Time.unscaledTime - 1);
                Call(social, "Update");
                Check(sent.Count == 1 && BitConverter.ToInt32(sent[0], 4) == 0 && System.Text.Encoding.Unicode.GetString(sent[0], 16, 18).TrimEnd('\0') == "Player", "Expired unapproved-name invite must decline behind other dialogs with Legacy's Player UID fallback."); sent.Clear(); Pops(social).Clear();
                var group = new byte[4]; I(group, 0, 88); Receive(PacketIds.P_FE2CL_PC_GROUP_INVITE, group); Receive(PacketIds.P_FE2CL_PC_GROUP_INVITE, group);
                Check(Pops(social).Count == 1, "Repeated group invitation must not create duplicate dialogs.");
                Call(social, "Resolve", Pops(social)[0], false);
                Check(sent.Count == 1 && BitConverter.ToUInt32(sent[0], 0) == PacketIds.P_CL2FE_REQ_PC_GROUP_INVITE_REFUSE, "Declining a group invitation must use group refusal."); sent.Clear();
                Receive(PacketIds.P_FE2CL_PC_GROUP_INVITE, group);
                var groupPrompt = Pops(social)[0]; groupPrompt.GetType().GetField("Deadline").SetValue(groupPrompt, Time.unscaledTime - 1); Call(social, "Update");
                Check(sent.Count == 1 && Pops(social).Count == 0, "Group invitation timeout must decline once."); sent.Clear();
                Receive(PacketIds.P_FE2CL_PC_GROUP_INVITE, group);
                Call(social, "Resolve", Pops(social)[0], true);
                Check(sent.Count == 1 && BitConverter.ToUInt32(sent[0], 0) == PacketIds.P_CL2FE_REQ_PC_GROUP_JOIN && BitConverter.ToInt32(sent[0], 4) == 88, "Group acceptance must identify the inviting leader."); sent.Clear();
                Receive(PacketIds.P_FE2CL_PC_GROUP_INVITE, group);
                Call(social, "OnEnter", new EnterSucc { Id = 10, Self = new PcAppearance { Uid = 2000 } }); Call(social, "Update");
                Check(Pops(social).Count == 0 && social.BuddyAt(0) == null && !social.IsBlockedUid(9001) && sent.Count == 0, "New session/character must discard old invitations and buddy identities without replying to them.");
                for (int error = 1; error <= 5; error++) {
                    var fail = new byte[56]; I(fail, 52, error); Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_MAKE_BUDDY_FAIL, fail);
                    Check(Pops(social).Count == 1, "Known Legacy lookup error must show feedback: " + error); Pops(social).Clear();
                }
                for (int error = 1; error <= 6; error++) {
                    var fail = new byte[68]; L(fail, 52, 9001); I(fail, 64, error); Receive(PacketIds.P_FE2CL_REP_PC_FIND_NAME_ACCEPT_BUDDY_FAIL, fail);
                    Check(Pops(social).Count == 1, "Known Legacy acceptance error must show feedback: " + error); Pops(social).Clear();
                }
                Check(sent.Count == 0, "Server errors must not retry or silently send invitations.");
                var panel = BuddyListPanel.Ensure(host); panel.Init(d);
                var warpBuddy = new SocialMenu.Buddy { Slot = 0, Uid = 9001 };
                Call(panel, "DoWarp", warpBuddy); Call(panel, "DoWarp", warpBuddy);
                Check(sent.Count == 1, "Repeated buddy warp input must send once while waiting."); sent.Clear();
                Dispatch(d, PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_SAME_SHARD_SUCC, Array.Empty<byte>()); Call(panel, "Update");
                Dispatch(d, PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_SAME_SHARD_SUCC, Array.Empty<byte>()); Call(panel, "Update");
                Check(sent.Count == 1 && BitConverter.ToUInt32(sent[0], 0) == PacketIds.P_CL2FE_REQ_PC_GROUP_LEAVE, "A non-group buddy warp may leave the old group once, never on duplicate or unsolicited acknowledgements."); sent.Clear();
                typeof(BuddyListPanel).GetField("warpToGroup", Private).SetValue(panel, true); Call(panel, "DoWarp", warpBuddy); sent.Clear();
                Dispatch(d, PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_SAME_SHARD_SUCC, Array.Empty<byte>()); Call(panel, "Update");
                Check(sent.Count == 0, "Warping to a group member must preserve the group.");
                Call(panel, "DoWarp", warpBuddy); sent.Clear(); Call(panel, "OnEnterRaw", new byte[2564]); Call(panel, "Update");
                Dispatch(d, PacketIds.P_FE2CL_REP_PC_BUDDY_WARP_SAME_SHARD_SUCC, Array.Empty<byte>()); Call(panel, "Update");
                Check(sent.Count == 0 && !SysMessageBox.FocusOut, "A fresh session must discard stale buddy-warp state.");
                var groups = GroupState.Ensure(host); groups.Init(world, d);
                var roster = new byte[236]; I(roster, 4, 2); I(roster, 12, 10); L(roster, 16, 2000); I(roster, 124, 88); L(roster, 128, 9001);
                Dispatch(d, PacketIds.P_FE2CL_PC_GROUP_JOIN_SUCC, roster); Call(groups, "Update");
                var menu = host.AddComponent<GameMenu>();
                Check(menu.CanLeaveGroup && menu.RequestLeaveGroup() && !menu.RequestLeaveGroup() && GroupState.Size == 2 && sent.Count == 1, "Leave Group must send once and preserve server-confirmed membership while waiting."); sent.Clear();
                var leaveFail = new byte[8]; Call(menu, "OnPacket", PacketIds.P_FE2CL_PC_GROUP_LEAVE_FAIL, leaveFail);
                Receive(PacketIds.P_FE2CL_PC_GROUP_LEAVE_FAIL, leaveFail);
                Check(menu.CanLeaveGroup && GroupState.IsMember(88) && menu.RequestLeaveGroup() && sent.Count == 1, "Rejected leave must preserve membership and allow immediate retry."); sent.Clear();
                Dispatch(d, PacketIds.P_FE2CL_PC_GROUP_LEAVE_SUCC, Array.Empty<byte>()); Call(groups, "Update");
                Check(!menu.CanLeaveGroup && !menu.RequestLeaveGroup() && sent.Count == 0, "Only leave success may clear acknowledged membership.");
                foreach (uint type in new[] { PacketIds.P_FE2CL_PC_GROUP_INVITE_FAIL, PacketIds.P_FE2CL_PC_GROUP_JOIN_FAIL, PacketIds.P_FE2CL_PC_GROUP_INVITE_REFUSE }) {
                    Receive(type, new byte[8]); Check(Pops(social).Count == 1, "Legacy group errors/refusal must provide feedback."); Pops(social).Clear();
                }
            } finally { UnityEngine.Object.DestroyImmediate(host); NpcUI.Sender = sender; GameOptions.Cur.AllowBuddy = allowBuddy; GameOptions.Cur.AllowGroup = allowGroup; }
            Directory.CreateDirectory("Logs/legacy-audit");
            File.WriteAllText("Logs/legacy-audit/buddy-name-checks.txt", "PASS: name limits/UTF-16 layouts, editor blocking, duplicate input, server-authoritative addition, UID invitations outside streaming, accept/refuse/disabled/blocked/self/duplicate/expiry, malformed snapshots, group refusal/expiry and session reset. Captured sender only, no real invitations.\n");
            Debug.Log("[buddy-name] regression passed");
        }
    }
}
