using UnityEngine;
using OpenFusion.Api;

namespace OpenFusion.Screens
{
    public sealed partial class LauncherScreen
    {
        PocketUpdater updater; bool checkedUpdates;
        readonly Scroller scrUpdates = new Scroller();
        MdDoc updateNotes; string notesText;
        void EnsureUpdater()
        {
            if (updater == null) { updater = GetComponent<PocketUpdater>(); if (updater == null) updater = gameObject.AddComponent<PocketUpdater>(); }
            if (!checkedUpdates) { checkedUpdates = true; _ = updater.CheckAsync(); }
        }
        void OpenUpdates()
        {
            EnsureUpdater(); modal = Modal.Updates; modalToken++;
            if (!updater.Checking && !updater.Downloading) _ = updater.CheckAsync();
        }
        void DrawUpdates(Rect a)
        {
            EnsureUpdater();
            var body = ModalFrame(a, Mathf.Min(a.width - 16f, 760f), Mathf.Min(a.height - 16f, 570f), "OpenFusion:Pocket updates");
            float x = body.x + 14, w = body.width - 28, y = body.y + 8;
            Lbl(new Rect(x, y, w, 24), "Installed: " + Application.version + " (build " + updater.InstalledCode + ")", 14, CModalText); y += 26;
            Lbl(new Rect(x, y, w, 48), updater.Status, 14, CModalText, TextAnchor.UpperLeft, false, true); y += 52;
            var release = updater.Latest;
            string notes = release == null ? "Release notes will appear here after checking GitHub." : release.Title + "\n\n" + release.Notes;
            if (notesText != notes) { notesText = notes; updateNotes = ParseMd(notes); updateNotes.baseSize = 14; updateNotes.color = CModalText; scrUpdates.y = 0; }
            var view = new Rect(x, y, w, Mathf.Max(35, body.yMax - y - 98));
            UiKit.Fill(view, new Color(0, 0, 0, .35f)); MdLayout(updateNotes, view.width - 12);
            ScrollBegin(scrUpdates, view, updateNotes.height); MdDraw(updateNotes, view.width - 12); ScrollEnd();
            if (scrUpdates.tap.HasValue)
            {
                string url = MdTap(updateNotes, scrUpdates.tap.Value);
                if (PocketRelease.RepositoryUrl(url, false) || url == PocketRelease.Repository) Application.OpenURL(url);
            }
            float by = body.yMax - 46;
            if (updater.Downloading)
            {
                Lbl(new Rect(x, by - 28, w, 22), updater.Progress > 0 ? "Download: " + Mathf.RoundToInt(updater.Progress * 100) + "%" : "Preparing or verifying download…", 14, CModalText);
                if (Btn(new Rect(x, by, 150, 36), "Cancel download", Kind.Primary, true, 14)) updater.CancelDownload();
            }
            else if (updater.Ready)
            {
                if (Btn(new Rect(x, by, 150, 36), "Install update", Kind.Success, !updater.Checking, 14)) updater.Install();
            }
            else if (updater.Available)
            {
                Lbl(new Rect(x, by - 28, w, 22), "APK: " + (release.Size / 1048576f).ToString("F1") + " MB", 14, CModalText);
                if (Btn(new Rect(x, by, 150, 36), "Download update", Kind.Success, !updater.Checking, 14)) _ = updater.DownloadAsync();
            }
            else if (Btn(new Rect(x, by, 150, 36), "Check again", Kind.Primary, !updater.Checking, 14)) _ = updater.CheckAsync();
            if (Btn(new Rect(x + 158, by, 110, 36), "GitHub", Kind.Primary, true, 14)) Application.OpenURL(release != null ? release.Page : PocketRelease.Repository + "/releases");
            if (Btn(new Rect(body.xMax - 92, by, 78, 36), "Close", Kind.Primary, true, 14)) CloseModal();
        }
    }
}
