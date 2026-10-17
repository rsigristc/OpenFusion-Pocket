package com.openfusion.updater;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import java.io.File;
import java.io.FileInputStream;
import java.security.MessageDigest;
import java.util.HashSet;
import java.util.Set;

public final class PocketInstaller {
    private PocketInstaller() {}
    public static String updatePath(Activity activity) { return new File(activity.getCacheDir(), "updates/OpenFusion-Pocket.apk").getPath(); }
    public static long installedCode(Activity activity) throws Exception {
        return version(activity.getPackageManager().getPackageInfo(activity.getPackageName(), 0));
    }
    private static long version(PackageInfo info) {
        return Build.VERSION.SDK_INT >= 28 ? info.getLongVersionCode() : info.versionCode;
    }
    private static Set<String> certificates(PackageInfo info) throws Exception {
        Set<String> result = new HashSet<>();
        if (info.signatures != null) for (Signature signature : info.signatures) {
            byte[] hash = MessageDigest.getInstance("SHA-256").digest(signature.toByteArray());
            result.add(hex(hash));
        }
        return result;
    }
    private static String hex(byte[] hash) {
        StringBuilder value = new StringBuilder();
        for (byte b : hash) value.append(String.format(java.util.Locale.ROOT, "%02x", b & 255));
        return value.toString();
    }
    public static String install(Activity activity, String path, String expectedHash, long expectedCode) throws Exception {
        File apk = new File(path).getCanonicalFile();
        if (!PocketApkProvider.allowed(activity, apk) || !apk.isFile()) throw new SecurityException("Invalid update location");
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        try (FileInputStream input = new FileInputStream(apk)) {
            byte[] buffer = new byte[65536]; int n;
            while ((n = input.read(buffer)) != -1) digest.update(buffer, 0, n);
        }
        if (!hex(digest.digest()).equalsIgnoreCase(expectedHash)) throw new SecurityException("APK hash changed");
        PackageManager pm = activity.getPackageManager();
        PackageInfo current = pm.getPackageInfo(activity.getPackageName(), PackageManager.GET_SIGNATURES);
        PackageInfo next = pm.getPackageArchiveInfo(apk.getPath(), PackageManager.GET_SIGNATURES);
        Set<String> signing = certificates(current);
        if (next == null || !activity.getPackageName().equals(next.packageName) || version(next) != expectedCode || version(next) <= version(current) || signing.isEmpty() || !signing.equals(certificates(next)))
            throw new SecurityException("APK package, version or signing certificate does not match this app");
        if (Build.VERSION.SDK_INT >= 26 && !pm.canRequestPackageInstalls()) {
            activity.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:" + activity.getPackageName())));
            return "Allow updates from this app in Android settings, then return and tap Install update again.";
        }
        Uri uri = Uri.parse("content://" + activity.getPackageName() + ".updates/apk");
        Intent intent = new Intent(Intent.ACTION_VIEW).setDataAndType(uri, "application/vnd.android.package-archive");
        intent.setClipData(ClipData.newRawUri("OpenFusion:Pocket update", uri));
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        activity.startActivity(intent);
        return "Confirm the update in the Android installer. Your app data will be kept.";
    }
}
