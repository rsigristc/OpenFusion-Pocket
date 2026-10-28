using System;
using UnityEngine;

namespace OpenFusion.Screens
{
    public sealed partial class LauncherScreen
    {
        static string pendingServerLink;
        static bool initialLinkRead;
        void Awake()
        {
            PocketDiagnostics.Initialize();
            Application.deepLinkActivated += ReceiveServerLink;
            if (!initialLinkRead)
            {
                initialLinkRead = true;
                if (!string.IsNullOrEmpty(Application.absoluteURL)) ReceiveServerLink(Application.absoluteURL);
            }
        }
        void ReceiveServerLink(string link) { pendingServerLink = link; }
        void ProcessServerLink()
        {
            if (!Visible || busy || modal != Modal.None || page != Page.Home || string.IsNullOrEmpty(pendingServerLink)) return;
            string link = pendingServerLink; pendingServerLink = null;
            if (!LauncherServerLink.TryParse(link, out var entry, out var error)) { Toast("error", error); return; }
            int existing = servers.FindIndex(s => LauncherServerLink.NormalizeEndpoint(s.endpoint) == entry.endpoint);
            if (existing >= 0)
            {
                sel = existing; SaveSel();
                Toast("success", "Server selected from link. Choose Connect when ready.");
                return;
            }
            OpenAddEdit(null);
            adTab = 1; adName = entry.description; adEndpoint = entry.endpoint;
        }
        void CollectDiagnostics()
        {
            try
            {
                string report = PocketDiagnostics.Report();
                if (PocketPlatform.CanShareDiagnostics) PocketPlatform.ShareDiagnostics(report);
                else
                {
                    string path = System.IO.Path.Combine(Application.persistentDataPath, "OpenFusion-Pocket-Logs.txt");
                    System.IO.File.WriteAllText(path, report);
                    Toast("success", "Diagnostics saved: " + path);
                }
            }
            catch (Exception) { Toast("error", "Could not export logs. Please try again."); }
        }
    }
}
