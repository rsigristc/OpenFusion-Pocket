package com.openfusion.updater;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;

// A single, read-only APK endpoint. No caller-controlled filesystem paths.
public final class PocketApkProvider extends ContentProvider {
    public static boolean allowed(Context context, File file) throws IOException {
        if (!file.getName().equals("OpenFusion-Pocket.apk")) return false;
        File internal = new File(context.getCacheDir(), "updates/OpenFusion-Pocket.apk").getCanonicalFile();
        return file.equals(internal);
    }
    private File file(Uri uri) throws FileNotFoundException {
        if (!"/apk".equals(uri.getPath()) || uri.getQuery() != null || uri.getFragment() != null || !(getContext().getPackageName()+".updates").equals(uri.getAuthority())) throw new FileNotFoundException("Invalid update URI");
        try {
            File apk = new File(getContext().getCacheDir(), "updates/OpenFusion-Pocket.apk").getCanonicalFile();
            if (!allowed(getContext(), apk) || !apk.isFile()) throw new FileNotFoundException("No verified update available");
            return apk;
        } catch (IOException e) { throw new FileNotFoundException(e.getMessage()); }
    }
    @Override public boolean onCreate() { return true; }
    @Override public String getType(Uri uri) { return "application/vnd.android.package-archive"; }
    @Override public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        if (!"r".equals(mode)) throw new FileNotFoundException("Read-only provider");
        return ParcelFileDescriptor.open(file(uri), ParcelFileDescriptor.MODE_READ_ONLY);
    }
    @Override public Cursor query(Uri uri, String[] projection, String selection, String[] args, String order) {
        try {
            File apk = file(uri);
            String[] columns = projection == null ? new String[]{OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE} : projection;
            MatrixCursor result = new MatrixCursor(columns); Object[] values = new Object[columns.length];
            for(int i=0;i<columns.length;i++) values[i] = OpenableColumns.DISPLAY_NAME.equals(columns[i]) ? apk.getName() : OpenableColumns.SIZE.equals(columns[i]) ? apk.length() : null;
            result.addRow(values); return result;
        } catch(FileNotFoundException e) { return null; }
    }
    @Override public Uri insert(Uri uri, ContentValues values) { throw new UnsupportedOperationException("Read-only"); }
    @Override public int delete(Uri uri, String selection, String[] args) { throw new UnsupportedOperationException("Read-only"); }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] args) { throw new UnsupportedOperationException("Read-only"); }
}
