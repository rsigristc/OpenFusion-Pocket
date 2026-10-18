package com.openfusion.dualscreen;
public final class ResourceProgressChecks {
    static void check(boolean ok) { if(!ok)throw new AssertionError("Resource progress bounds"); }
    public static void main(String[] args) {
        check(ResourceProgress.ratio(9992,2356,true)==1);
        check(ResourceProgress.ratio(0,5,true)==0);
        check(ResourceProgress.ratio(2,5,true)==.4f);
        check(ResourceProgress.ratio(0,0,true)==1);
        check(ResourceProgress.ratio(100,5,false)==0);
        check(ResourceProgress.ratio(-1,5,true)==0);
        System.out.println("PASS: surplus, empty, partial, zero-cost, unknown and negative resource gauges");
    }
}
