package com.openfusion.dualscreen;

public final class PhysicalTouchProofChecks {
    private static void check(boolean ok,String message) { if(!ok) throw new AssertionError(message); }
    public static void main(String[] args) {
        String surface="Screen-2:1080:1240:369", touch="secondary-touch";
        check(PhysicalTouchProof.matches(12,12,4,4,surface,surface,touch,new String[]{"main-touch",touch}),"Reopening app with the verified connection must reuse its real touch proof");
        check(!PhysicalTouchProof.matches(12,13,4,4,surface,surface,touch,new String[]{touch}),"A recycled display id after reboot cannot identify the same screen");
        check(!PhysicalTouchProof.matches(-1,-1,4,4,surface,surface,touch,new String[]{touch}),"Unknown boot identity must fail closed");
        check(!PhysicalTouchProof.matches(12,12,4,5,surface,surface,touch,new String[]{touch}),"Another connection cannot inherit touch verification");
        check(!PhysicalTouchProof.matches(12,12,4,4,surface,"HDMI",touch,new String[]{touch}),"A changed display cannot inherit touch verification");
        check(!PhysicalTouchProof.matches(12,12,4,4,surface,surface,touch,new String[]{"main-touch"}),"Missing secondary touch input must disable remembered DS");
        check(!PhysicalTouchProof.matches(12,12,4,4,surface,surface,touch,new String[]{touch,touch}),"Ambiguous input descriptors cannot prove an association");
        check(!PhysicalTouchProof.matches(12,12,4,4,"","","",new String[]{""}),"An empty profile cannot enable an ordinary display");
        System.out.println("PASS: app restart, reboot, unknown boot, replacement display, changed surface, removed touch, ambiguous touch and empty profile");
    }
}
