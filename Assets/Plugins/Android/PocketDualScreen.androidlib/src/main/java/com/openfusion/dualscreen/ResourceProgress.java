package com.openfusion.dualscreen;
final class ResourceProgress {
    static float ratio(int available,int required,boolean known) {
        if(!known)return 0;
        if(required<=0)return 1;
        return Math.max(0,Math.min(1,(float)available/required));
    }
}
