using System;
using System.Collections.Generic;
using UnityEngine;
using OpenFusion.Net;
using OpenFusion.Screens;
using OpenFusion.Tables.Gen;

namespace OpenFusion.World
{
    // Legacy NanoFreeTuningMode / CnGuiNanoFreeTuning. Presentation never grants a
    // Nano or applies a chosen power: both remain acknowledgements from the server.
    public sealed partial class InventoryWindow
    {
        readonly Queue<int> acquisitionQueue = new Queue<int>();
        readonly HashSet<int> acquisitionQueued = new HashSet<int>();
        readonly HashSet<int> acquisitionCelebration = new HashSet<int>();
        int acquiredNano, acquisitionPhase, acquisitionChoice, acquisitionTune;
        float acquisitionAt, acquisitionRetry, acquisitionSent;
        bool acquisitionWaiting;
        string acquisitionError = "";
        GameObject acquisitionModel;
        Animation acquisitionAnimation;
        Vector3 acquisitionOrigin, acquisitionForward, acquisitionCenter;
        float acquisitionRadius = 0.7f;
        public static bool NanoAcquisitionActive => Instance != null && Instance.acquiredNano > 0;
#if POCKET_INPUT_PREVIEW
        float acquisitionProofAt;
        void AcquisitionProofTick()
        {
            if (!Application.isPlaying || ents?.Self == null || Time.unscaledTime < acquisitionProofAt) return;
            acquisitionProofAt = Time.unscaledTime + 1f;
            // Local preview only: replay an already owned, tuned Nano without a
            // grant, tune request, equipment change or reward packet.
            string path = System.IO.Path.Combine(Application.persistentDataPath, "nano-acquisition-preview.txt");
            if (!System.IO.File.Exists(path)) return;
            string value = System.IO.File.ReadAllText(path); System.IO.File.Delete(path);
            if (int.TryParse(value, out int id) && OwnsNano(id) && SkillOfNano(id) > 0) QueueAcquiredNano(id, true);
        }
#endif

        void QueueAcquiredNano(int id, bool celebrate)
        {
            if (TutorialTraining.Active || id <= 0 || NanoRow(id) == null) return;
            if (celebrate) acquisitionCelebration.Add(id);
            if (id != acquiredNano && acquisitionQueued.Add(id)) acquisitionQueue.Enqueue(id);
        }

        void ResetAcquisition()
        {
            EndAcquisition(); acquisitionQueue.Clear(); acquisitionQueued.Clear(); acquisitionCelebration.Clear();
        }

        void AcquisitionTick()
        {
            if (TutorialTraining.Active) return; // Its practice Nano uses the local training flow.
            if (acquiredNano == 0)
            {
                if (acquisitionQueue.Count == 0 || ents?.Self == null || hud == null || hud.Data.Hp <= 0 ||
                    WorldBootstrap.Travelling || WelcomePopup.Active || NpcUI.Active || GameMenu.Blocking || ChatHud.Typing) return;
                int id = acquisitionQueue.Dequeue(); acquisitionQueued.Remove(id);
                bool celebrate = acquisitionCelebration.Remove(id);
                if (!OwnsNano(id) || (!celebrate && SkillOfNano(id) != 0)) return;
                acquiredNano = id; acquisitionPhase = 0; acquisitionChoice = 0; acquisitionTune = 0;
                acquisitionAt = Time.unscaledTime; acquisitionRetry = 0; acquisitionWaiting = false; acquisitionError = "";
                acquisitionOrigin = ents.Self.transform.position;
                acquisitionForward = ents.Self.transform.forward;
                if (celebrate) {
                    float nearest = 1600f;
                    foreach (var npc in ents.NpcObjects) {
                        var info = npc != null ? npc.GetComponent<NpcInfo>() : null;
                        if (info == null || info.Hp > 0 || info.Type >= 100) continue;
                        float dist = (npc.transform.position - acquisitionOrigin).sqrMagnitude;
                        if (dist < nearest) { nearest = dist; acquisitionOrigin = npc.transform.position; }
                    }
                }
                acquisitionOrigin += acquisitionForward * 2f;
                acquisitionCenter = acquisitionOrigin + Vector3.up * 3.4f;
                Debug.Log("[nano-acquire] begin nano=" + id + " celebration=" + celebrate + " skill=" + SkillOfNano(id));
                Sfx.Ui("Nano_Creation");
                if (celebrate) BulletFx.Instance?.Spawn(705, acquisitionOrigin, Quaternion.identity, 1f, null, 6f);
            }
            if (PlayerLife.Active || WorldBootstrap.Travelling) { int resume = acquiredNano; EndAcquisition(); if (SkillOfNano(resume) == 0) QueueAcquiredNano(resume, false); return; }
            if (acquisitionModel == null && Time.unscaledTime >= acquisitionRetry) {
                acquisitionRetry = Time.unscaledTime + 0.5f;
                BuildAcquisitionModel();
            }
            float age = Time.unscaledTime - acquisitionAt;
            if (acquisitionPhase == 0 && age >= 1.2f) {
                acquisitionPhase = 1;
                if (ents?.Self != null) {
                    BulletFx.Instance?.Fire(76, ents.Self.transform, null, 0, acquisitionOrigin + Vector3.up * 3f);
                    BulletFx.Instance?.Fire(77, ents.Self.transform, null, 0, acquisitionOrigin + Vector3.up * 3f);
                }
            }
            if (acquisitionPhase == 1 && age >= 2f && acquisitionModel != null) {
                acquisitionModel.SetActive(true); acquisitionPhase = 2; PlayAcquisition("call", false);
            }
            // Never block initial selection forever if an optional visual resource is absent.
            if (acquisitionPhase <= 2 && age >= 4f && (acquisitionModel != null || age >= 10f)) {
                acquisitionPhase = 3; PlayAcquisition("stand1", true);
                Debug.Log("[nano-acquire] select nano=" + acquiredNano + " model=" + (acquisitionModel != null));
            }
            if (acquisitionWaiting && Time.unscaledTime - acquisitionSent > 15f) {
                // A timed-out request is not resent automatically: its acknowledgement can still arrive.
                acquisitionError = "Waiting for the server. You may return to the game; an unassigned power will be offered again on login.";
            }
            if (acquisitionPhase == 4 && Time.unscaledTime >= acquisitionAt) EndAcquisition();
            if (acquisitionPhase == 3 && !acquisitionWaiting) {
                if (Input.GetKeyDown(KeyCode.DownArrow)) acquisitionChoice = (acquisitionChoice + 1) % 3;
                if (Input.GetKeyDown(KeyCode.UpArrow)) acquisitionChoice = (acquisitionChoice + 2) % 3;
                // Gamepad A already generates a pointer click; do not consume it twice.
                if (Input.GetKeyDown(KeyCode.Return)) SelectAcquisitionPower(acquisitionChoice);
            }
        }

        void BuildAcquisitionModel()
        {
            var factory = GetComponent<WorldBootstrap>()?.Models;
            if (factory == null || tables == null) return;
            var model = ModelCatalog.From(tables)?.Nano(acquiredNano);
            if (model?.Model == null) return;
            var go = factory.Instantiate(model, transform); if (go == null) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.SetActive(true);
            var main = model.Tex1 != null ? factory.Texture(model.Tex1) : null;
            var sub = model.Tex2 != null ? factory.Texture(model.Tex2) : null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) {
                r.enabled = true;
                foreach (var mat in r.materials) {
                    if (mat.name.Contains("sub") && sub != null) mat.SetTexture("_MainTex", sub);
                    else if (main != null) mat.SetTexture("_MainTex", main);
                }
            }
            go.transform.position = acquisitionOrigin + Vector3.up * 3f;
            // Nano meshes face local +Z. The acquisition camera is in front of
            // the player rather than the normal orbit behind it.
            go.transform.rotation = Quaternion.LookRotation(acquisitionForward, Vector3.up);
            acquisitionModel = go;
            acquisitionAnimation = go.GetComponentInChildren<Animation>(true);
            if (acquisitionAnimation != null) {
                acquisitionAnimation.enabled = true; acquisitionAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
                var sink = acquisitionAnimation.GetComponent<AnimEventSink>(); if (sink != null) sink.IsNano = true;
            }
            bool has = false; var bounds = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) {
                if (!has) { bounds = r.bounds; has = true; } else bounds.Encapsulate(r.bounds);
            }
            if (has) { acquisitionCenter = bounds.center; acquisitionRadius = Mathf.Max(0.5f, bounds.extents.magnitude); }
            go.SetActive(acquisitionPhase >= 2);
            if (acquisitionPhase >= 3) PlayAcquisition("stand1", true);
        }

        float PlayAcquisition(string clip, bool loop)
        {
            if (acquisitionAnimation == null || acquisitionAnimation[clip] == null) return 1f;
            acquisitionAnimation[clip].wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
            acquisitionAnimation.CrossFade(clip, 0.1f); return acquisitionAnimation[clip].length;
        }

        // The normal camera retains its orbit variables; leaving this mode restores it.
        public static bool TryAcquisitionCamera(Camera camera)
        {
            var self = Instance;
            if (!NanoAcquisitionActive || camera == null) return false;
            float distance = self.acquisitionRadius / Mathf.Sin(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            distance = Mathf.Max(2.5f, distance / Mathf.Min(1f, camera.aspect) * 1.4f);
            var focus = self.acquisitionCenter + Quaternion.LookRotation(-self.acquisitionForward, Vector3.up) * Vector3.right * distance * 0.25f;
            camera.transform.position = focus + self.acquisitionForward * distance;
            camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);
            return true;
        }

        NanoTuneElement AcquisitionPower(int choice, out int tune)
        {
            tune = 0; var n = NanoRow(acquiredNano);
            if (n?.m_iTune == null || choice < 0 || choice >= n.m_iTune.Length) return null;
            tune = n.m_iTune[choice]; var data = NT?.m_pNanoTuneData;
            return data != null && tune > 0 && tune < data.Length ? data[tune] : null;
        }

        bool SelectAcquisitionPower(int choice)
        {
            if (acquiredNano <= 0 || acquisitionPhase != 3 || acquisitionWaiting || !OwnsNano(acquiredNano) || SkillOfNano(acquiredNano) != 0 || NpcUI.Sender == null) return false;
            var power = AcquisitionPower(choice, out int tune); if (power == null) return false;
            var packet = ClientPackets.New(PacketIds.P_CL2FE_REQ_NANO_TUNE, 44);
            packet[4] = (byte)acquiredNano; packet[5] = (byte)(acquiredNano >> 8);
            packet[6] = (byte)tune; packet[7] = (byte)(tune >> 8);
            // Explicitly mark every slot unused. Zero means real inventory slot 0:
            // the server can interpret it as paid station tuning and return without
            // an ACK when Fusion Matter is insufficient. Only this untuned-owned
            // Nano flow may send the free initial selection.
            for (int i = 0; i < 10; i++) ClientPackets.PutInt(packet, 8 + 4 * i, -1);
            acquisitionChoice = choice; acquisitionTune = tune; acquisitionWaiting = true; acquisitionSent = Time.unscaledTime; acquisitionError = "";
            Send(packet); Sfx.Ui("Add_Attribute");
            Debug.Log("[nano-acquire] tune requested nano=" + acquiredNano + " tune=" + tune);
            return true;
        }

        void AcquisitionTuneAck(int id, int skill)
        {
            if (id != acquiredNano || !acquisitionWaiting) return;
            var power = AcquisitionPower(acquisitionChoice, out int tune);
            if (power == null || tune != acquisitionTune || power.m_iSkillID != skill) return;
            acquisitionWaiting = false; acquisitionPhase = 4;
            acquisitionAt = Time.unscaledTime + Mathf.Clamp(PlayAcquisition("skill" + (acquisitionChoice + 1), false), 1f, 5f) + 1f;
            Debug.Log("[nano-acquire] tune confirmed nano=" + id + " skill=" + skill);
        }

        void EndAcquisition()
        {
            if (acquisitionModel != null) { if (Application.isPlaying) Destroy(acquisitionModel); else DestroyImmediate(acquisitionModel); }
            acquisitionModel = null; acquisitionAnimation = null; acquiredNano = 0; acquisitionWaiting = false; acquisitionPhase = 0;
        }

        void DrawAcquisition()
        {
            if (!NanoAcquisitionActive || Event.current == null || Screen.height <= 0) return;
            var matrix = GUI.matrix; var color = GUI.color; var enabled = GUI.enabled; int depth = GUI.depth;
            GUI.depth = -2500;
            float scale = Mathf.Min(Screen.width / 1000f, Screen.height / 620f);
            GUI.matrix = Mat(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), scale);
            try {
                float halfW = Screen.width * 0.5f / scale, halfH = Screen.height * 0.5f / scale, bar = Screen.height * 0.15673982f / scale;
                GUI.color = Color.black; GUI.DrawTexture(new Rect(-halfW, -halfH, halfW * 2, bar), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(-halfW, halfH - bar, halfW * 2, bar), Texture2D.whiteTexture); GUI.color = Color.white;
                var blue = Skin.Get(SN, "bigblue"); var yellow = Skin.Get(SN, "bigyellow");
                string lead = Loc.T("YOU HAVE ACQUIRED") + " ", name = NanoStr(NanoRow(acquiredNano), 0), tail = " !";
                float leadW = blue?.Font?.Width(blue.Font.Convert(lead)) ?? 190, nameW = yellow?.Font?.Width(yellow.Font.Convert(name)) ?? 130, tailW = blue?.Font?.Width(blue.Font.Convert(tail)) ?? 20;
                float titleX = -(leadW + nameW + tailW) * 0.5f;
                Skin.Label(new Rect(titleX, -292, leadW, 42), lead, blue);
                Skin.Label(new Rect(titleX + leadW, -292, nameW, 42), name, yellow);
                Skin.Label(new Rect(titleX + leadW + nameW, -292, tailW, 42), tail, blue);
                if (acquisitionPhase < 3) return;
                var light = Skin.Get(SN, "TransparentLightBlue"); var text = Skin.Get(SN, "TransparentBlue");
                var type = Skin.Get(SN, "TransparentYellowSmall"); var button = Skin.Get(SN, "button");
                if (acquisitionModel == null) {
                    var icon = NanoIconTex(NanoRow(acquiredNano), false);
                    if (icon != null) GUI.DrawTexture(new Rect(-420, -160, 340, 340), icon, ScaleMode.ScaleToFit, true);
                    Skin.Label(new Rect(-460, 195, 450, 48), "Nano model is still loading.", text);
                }
                // Original CnGuiNanoFreeTuning geometry, bitmap fonts and background.
                GUI.BeginGroup(new Rect(100, -230, 356, 460));
                // CnGuiNanoFreeTuning binding 1487 uses the transparent variant.
                var background = UiKit.Tex("NanoPopUpNextNano_1");
                if (background != null) GUI.DrawTexture(new Rect(0, 0, 356, 460), background, ScaleMode.StretchToFill, true);
                Skin.Label(new Rect(100, 100, 250, 20), Loc.T(SkillOfNano(acquiredNano) == 0 ? "SELECT A POWER" : "CURRENT POWER"), light);
                for (int k = 0; k < 3; k++) {
                    var power = AcquisitionPower(k, out _); if (power == null) continue;
                    var strings = NT?.m_pNanoTuneStringData;
                    var s = strings != null && power.m_iTuneName >= 0 && power.m_iTuneName < strings.Length ? strings[power.m_iTuneName] : null;
                    float y = k == 0 ? 139 : k == 1 ? 221 : 304;
                    var icon = SkillIconTex(power.m_iSkillID); if (icon != null) GUI.DrawTexture(new Rect(18, y, 35, 35), icon, ScaleMode.ScaleToFit, true);
                    Skin.Label(new Rect(50, y + 4, 200, 15), s?.m_strName ?? "Power " + power.m_iSkillID, light);
                    Skin.Label(new Rect(50, y + 19, 200, 12), s?.m_strComment1 ?? "", type);
                    Skin.Label(new Rect(20, y + 41, 317, 28), s?.m_strComment ?? "", text);
                    if (Skin.Button(new Rect(237, y + (k == 0 ? 6 : 11), 97, 20), Loc.T("SELECT"), button,
                        !acquisitionWaiting && acquisitionPhase == 3 && SkillOfNano(acquiredNano) == 0)) SelectAcquisitionPower(k);
                }
                GUI.EndGroup();
                string status = acquisitionError.Length > 0 ? acquisitionError : acquisitionWaiting ? "Waiting for server confirmation…" : acquisitionPhase == 4 ? "Power selected!" : "Choose your Nano's initial power.";
                Skin.Label(new Rect(-480, 260, 960, 46), status, light);
                // Escape hatch for a rejected/late server request or an already tuned award.
                if ((SkillOfNano(acquiredNano) != 0 && acquisitionPhase == 3) || (acquisitionWaiting && Time.unscaledTime - acquisitionSent > 15f))
                    if (Skin.Button(new Rect(-440, 185, 160, 32), Loc.T("CONTINUE"), button)) EndAcquisition();
                if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel) Event.current.Use();
            } finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; GUI.enabled = enabled; }
        }
    }
}
