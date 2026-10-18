package com.openfusion.dualscreen;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.view.View;
import android.widget.Button;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import org.json.JSONArray;
import org.json.JSONObject;

/** Retained Android presentation over InventoryWindow's collection and station operations. */
final class NanoPane extends LinearLayout {
    interface Images { Bitmap get(String path); }
    interface Previews { boolean draw(Canvas canvas,int id,RectF destination,Paint paint); }
    private final Images images;
    private final Previews previews;
    private final TextView notice;
    private final Button close;
    private final LinearLayout columns, details, leftContent, listColumn, equippedRow;
    private final NanoCardView[] equippedCards = new NanoCardView[3];
    private final ScrollView listScroll, detailScroll;
    private final GridLayout grid;
    private final float density;
    private TextView live;
    private LegacyEnergyView liveEnergy;
    private View heroPreview;
    private JSONObject state = new JSONObject(), selected;
    private String gridKey = "", detailKey = "";
    private int selectedId, restoringScroll = -1, confirmTune = -1;
    private boolean applyingScroll, released, collectionApplied;
    private boolean connected, summonAllowed;

    NanoPane(Context c, Images imageSource, Previews previewSource) {
        super(c); images = imageSource; previews=previewSource; density = c.getResources().getDisplayMetrics().density;
        setOrientation(VERTICAL);
        LinearLayout status=new LinearLayout(c); addView(status,new LayoutParams(-1,-2));
        notice = label(10); notice.setMaxLines(2); status.addView(notice, new LayoutParams(0, -2,1));
        close = new Button(c); close.setText("Exit Station"); close.setTextSize(10);
        PanelSkin.button(close);
        close.setOnClickListener(v -> PocketDualScreen.send("closeStation", "", 0, 0)); status.addView(close, new LayoutParams(dp(96), dp(36)));
        columns = new LinearLayout(c); addView(columns, new LayoutParams(-1, 0, 1));
        listScroll = new ScrollView(c); grid = new GridLayout(c); listScroll.addView(grid,new ScrollView.LayoutParams(-1,-2));
        detailScroll = new ScrollView(c); leftContent=new LinearLayout(c); leftContent.setOrientation(VERTICAL);
        TextView equippedTitle=label(11); equippedTitle.setText("EQUIPPED NANOS"); leftContent.addView(equippedTitle);
        equippedRow=new LinearLayout(c); leftContent.addView(equippedRow,new LayoutParams(-1,dp(100)));
        for(int i=0;i<3;i++) { final int slot=i; NanoCardView card=new NanoCardView(c,images,previews,true); equippedCards[i]=card; equippedRow.addView(card,new LayoutParams(0,-1,1));
            card.setOnClickListener(v -> { JSONArray ns=state.optJSONArray("entries"); if(ns!=null) for(int j=0;j<ns.length();j++) { JSONObject n=ns.optJSONObject(j); if(n!=null && n.optInt("slot",-1)==slot) {
                String id=Integer.toString(n.optInt("id")); PocketDualScreen.send("nanoSelect",id,0,0);
                if(connected&&summonAllowed&&!state.optBoolean("busy")) PocketDualScreen.send("nanoCard",id,slot,0); break; } } }); }
        details = new LinearLayout(c); details.setOrientation(VERTICAL); details.setPadding(dp(5),dp(5),dp(5),dp(5)); PanelSkin.cell(details,false);
        detailScroll.addView(details,new ScrollView.LayoutParams(-1,-2)); leftContent.addView(detailScroll,new LayoutParams(-1,0,1));
        listColumn=new LinearLayout(c); listColumn.setOrientation(VERTICAL); TextView allTitle=label(11); allTitle.setText("ALL NANOS"); listColumn.addView(allTitle); listColumn.addView(listScroll,new LayoutParams(-1,0,1));
        columns.setOrientation(HORIZONTAL);
        LayoutParams leftParams=new LayoutParams(0,-1,.48f); leftParams.rightMargin=dp(4);
        columns.addView(leftContent,leftParams); columns.addView(listColumn,new LayoutParams(0,-1,.52f));
        leftContent.addOnLayoutChangeListener((v,l,t,r,b,oldL,oldT,oldR,oldB) -> {
            if(r-l!=oldR-oldL && heroPreview!=null) {
                android.view.ViewGroup.LayoutParams lp=heroPreview.getLayoutParams();
                lp.width=Math.min(dp(70),Math.max(dp(32),(r-l)/3)); heroPreview.setLayoutParams(lp);
            }
        });
        listScroll.addOnLayoutChangeListener((v,l,t,r,b,oldL,oldT,oldR,oldB) -> {
            if(r-l!=oldR-oldL && r>l && !released) { gridKey=""; post(() -> update(state)); }
        });
        listScroll.setOnScrollChangeListener((v,x,y,oldX,oldY) -> {
            if (!applyingScroll && restoringScroll < 0 && !released) PocketDualScreen.send("nanoScroll", "", 0, y);
        });
    }
    private int dp(int n) { return Math.round(n * density); }
    private TextView label(int size) { TextView t = new TextView(getContext()); t.setTextColor(Color.CYAN); t.setTextSize(size); t.setPadding(dp(4), dp(3), dp(4), dp(3)); return t; }
    protected void onSizeChanged(int w,int h,int oldW,int oldH) {
        super.onSizeChanged(w,h,oldW,oldH);
        // Keep the equipped row visible and reserve space for independently scrolling details.
        equippedRow.getLayoutParams().height=Math.round(Math.max(76,Math.min(126,h/density*.36f))*density);
        gridKey = ""; post(() -> update(state));
    }
    void update(JSONObject incoming) {
        if (incoming == null || released) return;
        state = incoming;
        boolean station = state.optBoolean("station"), busy = state.optBoolean("busy");
        notice.setText(state.optString("notice") + (busy ? "\nWaiting for server…" : "")); close.setVisibility(station ? VISIBLE : GONE); close.setEnabled(!busy);
        int id = state.optInt("selected");
        if (selectedId != id) { selectedId = id; confirmTune = -1; detailKey = ""; detailScroll.scrollTo(0,0); }
        JSONArray entries = state.optJSONArray("entries"); selected = null;
        for(int i=0;i<3;i++) { equippedCards[i].setArt(state); equippedCards[i].update(null,i,false); }
        StringBuilder key = new StringBuilder().append(selectedId).append(':').append(listScroll.getWidth());
        if (entries != null) for (int i=0;i<entries.length();i++) {
            JSONObject n = entries.optJSONObject(i); if (n == null) continue;
            key.append('/').append(n.optInt("id")).append(n.optBoolean("owned")).append(n.optInt("slot")).append(n.optString("name")).append(n.optString("image"));
            if (n.optInt("id") == selectedId) selected = n;
            int slot=n.optInt("slot",-1); if(slot>=0 && slot<3) equippedCards[slot].update(n,slot,n.optInt("id")==selectedId);
        }
        if (!key.toString().equals(gridKey)) { gridKey = key.toString(); rebuildGrid(entries, state.optInt("scroll")); }
        // Active state changes independently of collection structure/selection. Refresh retained
        // cells too, without rebuilding the grid or resetting its scroll position.
        int activeSlot=state.optInt("activeSlot",-1);
        for(NanoCardView card:equippedCards) card.setActiveSlot(activeSlot);
        for(int i=0;i<grid.getChildCount();i++) ((NanoCardView)grid.getChildAt(i)).setActiveSlot(activeSlot);
        String next = selected == null ? "none" : selected.optInt("id") + ":" + selected.optBoolean("owned") + ":" + selected.optInt("slot") + ":" + state.optInt("activeSlot",-1) + ":" + station + ":" + busy + ":" + summonAllowed + ":" + selected.optBoolean("active") + ":" + selected.optString("powers") + ":" + selected.optInt("gumballId") + ":" + selected.optInt("gumballCount") + ":" + selected.optBoolean("canGumball");
        if (!next.equals(detailKey)) { detailKey = next; confirmTune = -1; rebuildDetails(); }
        if(liveEnergy!=null && selected!=null) { liveEnergy.update(selected); liveEnergy.setVisibility(selected.optBoolean("energyKnown")?VISIBLE:GONE); }
        if (live != null && selected != null) live.setText(!selected.optBoolean("owned") ? "Locked" : (!selected.optBoolean("energyKnown") ? "Energy unavailable" : "Energy " + selected.optInt("energy") + "/" + selected.optInt("max")) +
            "  •  " + (selected.optBoolean("active") ? "Summoned" : selected.optInt("slot") >= 0 ? "Equipped" : "In collection") +
            (selected.optInt("slot") >= 0 ? "  •  Cooldown " + String.format(java.util.Locale.US,"%.1fs",selected.optDouble("cooldown")) : ""));
    }
    void update(JSONObject incoming, boolean online, boolean canSummon) {
        summonAllowed=canSummon;
        if(connected!=online) { connected=online; detailKey=""; confirmTune=-1; }
        update(incoming);
    }
    private void rebuildGrid(JSONArray entries, int savedScroll) {
        int y = collectionApplied ? restoringScroll >= 0 ? restoringScroll : listScroll.getScrollY() : savedScroll;
        collectionApplied = collectionApplied || entries != null && entries.length() > 0;
        // Measure the catalogue itself: the DS surface can be narrow even on a large display.
        float catalogueWidth=listScroll.getWidth()/density;
        if(catalogueWidth<=0) catalogueWidth=Math.max(1,getWidth()/density*.52f);
        grid.removeAllViews(); int cols = Math.max(2, Math.min(5,(int)(catalogueWidth/73f)));
        grid.setColumnCount(cols);
        if (entries != null) for (int i=0;i<entries.length();i++) {
            JSONObject n = entries.optJSONObject(i); if (n == null) continue;
            int id = n.optInt("id"); boolean owned = n.optBoolean("owned");
            NanoCardView cell = new NanoCardView(getContext(),images,previews,false); cell.setArt(state); cell.update(n,-1,id==selectedId);
            cell.setContentDescription(n.optString("name") + (owned ? ", owned" : ", locked")); cell.setFocusable(true); cell.setClickable(true);
            cell.setOnClickListener(v -> PocketDualScreen.send("nanoSelect", Integer.toString(id), 0, 0));
            GridLayout.LayoutParams lp = new GridLayout.LayoutParams(); lp.columnSpec = GridLayout.spec(i % cols, 1f); lp.width=0; lp.height=dp(86); lp.setMargins(dp(1),dp(1),dp(1),dp(1)); grid.addView(cell,lp);
        }
        restoringScroll = y;
        final int restore = y;
        listScroll.post(() -> { applyingScroll = true; listScroll.scrollTo(0, restore); applyingScroll = false; if(restoringScroll==restore) restoringScroll = -1; });
    }
    private Button action(String label, boolean enabled, Runnable run) {
        return action(details,label,enabled,run);
    }
    private Button action(LinearLayout parent,String label, boolean enabled, Runnable run) {
        Button b = new Button(getContext()); b.setText(label); b.setTextSize(12); PanelSkin.button(b); b.setEnabled(enabled); b.setOnClickListener(v -> run.run());
        LayoutParams lp=new LayoutParams(-1,dp(44)); lp.setMargins(dp(4),dp(4),dp(4),dp(4)); parent.addView(b,lp); return b;
    }
    private void line(String value, int size) { TextView t=label(size); t.setText(value); details.addView(t,new LayoutParams(-1,-2)); }
    private void requirement(LinearLayout parent,String resource,int available,int required,boolean known,String art) {
        LinearLayout row=new LinearLayout(getContext()); row.setGravity(android.view.Gravity.CENTER_VERTICAL);
        LayoutParams rowParams=new LayoutParams(-1,-2); rowParams.setMargins(dp(4),dp(3),dp(4),dp(3)); parent.addView(row,rowParams);
        TextView name=label(10); name.setText(resource); name.setGravity(android.view.Gravity.RIGHT|android.view.Gravity.CENTER_VERTICAL); row.addView(name,new LayoutParams(0,-2,1));
        ResourceGauge amount=new ResourceGauge(resource,available,required,known,art);
        row.addView(amount,new LayoutParams(0,dp(26),1));
    }
    private void rebuildDetails() {
        int y = detailScroll.getScrollY(); details.removeAllViews(); live=null; liveEnergy=null; heroPreview=null;
        if (selected == null) { line("Select a Nano to inspect its powers and equipment.",14); return; }
        final int id=selected.optInt("id"); boolean own=selected.optBoolean("owned"), station=connected && state.optBoolean("station"), busy=state.optBoolean("busy"); int slot=selected.optInt("slot",-1);
        LinearLayout hero=new LinearLayout(getContext()); details.addView(hero,new LayoutParams(-1,dp(72)));
        int portraitWidth=Math.min(dp(70),Math.max(dp(32),leftContent.getWidth()/3));
        heroPreview=new IconView(selected.optString("image"),!own,own?id:-1); hero.addView(heroPreview,new LayoutParams(portraitWidth,-1));
        LinearLayout identity=new LinearLayout(getContext()); identity.setOrientation(VERTICAL); hero.addView(identity,new LayoutParams(0,-1,1));
        TextView title=label(14); title.setText(selected.optString("name")); title.setTextColor(Color.WHITE); identity.addView(title,new LayoutParams(-1,-2));
        TextView style=label(12); style.setText(selected.optString("style")); style.setTextColor(PocketArt.nanoStyleColor(selected.optInt("styleIndex"))); identity.addView(style,new LayoutParams(-1,-2));
        liveEnergy=new LegacyEnergyView(); details.addView(liveEnergy,new LayoutParams(-1,dp(7)));
        line(selected.optString("description"),11);
        if (!own) line("Locked — obtain this Nano through game progression.",13);
        live=label(10); details.addView(live);
        if (own && slot < 0 && state.optBoolean("station")) {
            line("EQUIP IN SLOT",11); LinearLayout targets=new LinearLayout(getContext()); details.addView(targets,new LayoutParams(-1,dp(44)));
            for(int i=0;i<3;i++) { final int target=i; Button b=new Button(getContext()); b.setText("Slot "+(i+1)); b.setTextSize(11); b.setContentDescription("Equip "+selected.optString("name")+" in slot "+(i+1)); PanelSkin.button(b);
                b.setEnabled(station&&!busy&&state.optInt("activeSlot",-1)!=i); b.setOnClickListener(v->PocketDualScreen.send("nanoEquip",Integer.toString(id),target,0)); targets.addView(b,new LayoutParams(0,-1,1)); }
        }
        if (own && slot >= 0) {
            if(state.optBoolean("station")) action("Unequip slot " + (slot+1),station && !busy,() -> PocketDualScreen.send("nanoUnequip",Integer.toString(id),0,0));
            else if(selected.optBoolean("active")) {
                LinearLayout actions=new LinearLayout(getContext());details.addView(actions,new LayoutParams(-1,dp(52)));
                Button dismiss=action(actions,"Dismiss Nano",connected&&summonAllowed&&!busy,() -> PocketDualScreen.send("nano",Integer.toString(slot),0,0));
                Button gum=action(actions,"Gumball ("+selected.optInt("gumballCount")+")",connected&&selected.optBoolean("canGumball")&&!busy,() -> PocketDualScreen.send("nanoGumball",Integer.toString(id),selected.optInt("gumballId"),0));
                dismiss.setLayoutParams(new LayoutParams(0,-1,1));gum.setLayoutParams(new LayoutParams(0,-1,1));
            }
            else line("Tap its equipped card to summon this Nano.",11);
        }
        JSONArray powers=selected.optJSONArray("powers");
        if (powers != null) for(int i=0;i<powers.length();i++) {
            JSONObject p=powers.optJSONObject(i); if(p==null || !station&&!p.optBoolean("current")) continue; final int tune=p.optInt("tune");
            LinearLayout power=new LinearLayout(getContext()); power.setOrientation(VERTICAL); PanelSkin.cell(power,false);
            LayoutParams powerParams=new LayoutParams(-1,-2); powerParams.setMargins(0,dp(6),0,dp(4)); details.addView(power,powerParams);
            if(p.optBoolean("current")) {
                TextView caption=label(10); caption.setText("CURRENT POWER"); caption.setTextColor(PocketArt.INK); caption.setBackgroundColor(PocketArt.CYAN);
                caption.setTypeface(android.graphics.Typeface.DEFAULT,android.graphics.Typeface.BOLD); power.addView(caption,new LayoutParams(-2,-2));
            }
            LinearLayout heading=new LinearLayout(getContext()); heading.setGravity(android.view.Gravity.CENTER_VERTICAL); heading.setPadding(dp(4),dp(4),dp(4),dp(4));
            heading.setBackgroundColor(Color.rgb(0,16,27)); power.addView(heading,new LayoutParams(-1,-2));
            heading.addView(new IconView(p.optString("image"),false),new LayoutParams(dp(36),dp(36)));
            LinearLayout names=new LinearLayout(getContext()); names.setOrientation(VERTICAL); heading.addView(names,new LayoutParams(0,-2,1));
            TextView name=label(13); name.setText(p.optString("name")); name.setTextColor(Color.WHITE); name.setTypeface(android.graphics.Typeface.DEFAULT,android.graphics.Typeface.BOLD); names.addView(name,new LayoutParams(-1,-2));
            TextView type=label(11); type.setText(p.optString("type")); type.setTextColor(PocketArt.LIME); names.addView(type,new LayoutParams(-1,-2));
            TextView description=label(11); description.setText(p.optString("description")); description.setBackgroundColor(Color.rgb(7,58,85));
            LayoutParams descriptionParams=new LayoutParams(-1,-2); descriptionParams.setMargins(dp(1),0,dp(1),dp(1)); power.addView(description,descriptionParams);
            if (!p.optBoolean("current")) {
                LinearLayout requirements=new LinearLayout(getContext()); requirements.setOrientation(VERTICAL); requirements.setPadding(dp(3),dp(6),dp(3),dp(4));
                requirements.setBackgroundColor(Color.rgb(6,43,65)); LayoutParams requirementsParams=new LayoutParams(-1,-2); requirementsParams.setMargins(dp(1),dp(3),dp(1),dp(1)); power.addView(requirements,requirementsParams);
                TextView prompt=label(11); prompt.setText("To activate this power you need:"); prompt.setGravity(android.view.Gravity.CENTER); requirements.addView(prompt,new LayoutParams(-1,-2));
                requirement(requirements,"FUSION MATTER",p.optInt("availableMatter"),p.optInt("matter"),p.optBoolean("resourcesKnown"),state.optString("matterFill"));
                requirement(requirements,p.optString("itemName"),p.optInt("items"),p.optInt("requiredItems"),p.optBoolean("resourcesKnown"),state.optString("itemFill"));
                if (confirmTune == tune) {
                    TextView confirm=label(11); confirm.setText("Confirm spending these resources to change this Nano's power?"); requirements.addView(confirm,new LayoutParams(-1,-2));
                    action(requirements,"Confirm power change",connected && p.optBoolean("canTune"),() -> { confirmTune=-1; PocketDualScreen.send("nanoTune",Integer.toString(id),tune,0); });
                    action(requirements,"Cancel",true,() -> { confirmTune=-1; rebuildDetails(); });
                } else action(requirements,"Change power",connected && p.optBoolean("canTune"),() -> { confirmTune=tune; rebuildDetails(); });
            }
        }
        detailScroll.post(() -> detailScroll.scrollTo(0,y));
    }
    void invalidateImages() { invalidateChildren(grid); invalidateChildren(details); for(NanoCardView card:equippedCards) card.invalidate(); }
    void invalidatePreviews() { for(NanoCardView card:equippedCards) card.invalidate(); if(heroPreview!=null) heroPreview.invalidate(); }
    private void invalidateChildren(android.view.ViewGroup group) { for(int i=0;i<group.getChildCount();i++) { View child=group.getChildAt(i); child.invalidate(); if(child instanceof android.view.ViewGroup) invalidateChildren((android.view.ViewGroup)child); } }
    void release() { released=true; }
    private final class ResourceGauge extends View {
        final int available,required; final boolean known; final String art;
        final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG); final RectF fill=new RectF();
        ResourceGauge(String resource,int have,int need,boolean valid,String path) {
            super(NanoPane.this.getContext()); available=Math.max(0,have); required=Math.max(0,need); known=valid; art=path;
            setContentDescription(resource+": "+(known?available+" available, ":"amount unavailable, ")+required+" required");

        }
        protected void onDraw(Canvas canvas) {
            float ratio=ResourceProgress.ratio(available,required,known);
            paint.setStyle(Paint.Style.FILL); paint.setColor(Color.rgb(0,12,23)); canvas.drawRect(0,0,getWidth(),getHeight(),paint);
            fill.set(dp(1),dp(1),dp(1)+Math.max(0,getWidth()-dp(2))*ratio,getHeight()-dp(1));
            if(ratio>0) {
                boolean matter=art.equals(state.optString("matterFill"));
                paint.setColor(matter?Color.rgb(107,190,22):Color.rgb(239,155,18)); canvas.drawRect(fill,paint);
                Bitmap texture=images.get(art);
                if(texture!=null&&!texture.isRecycled()) { paint.setColor(Color.WHITE);paint.setAlpha(80);canvas.drawBitmap(texture,null,fill,paint);paint.setAlpha(255); }
            }
            paint.setStyle(Paint.Style.STROKE);paint.setStrokeWidth(dp(1));paint.setColor(PocketArt.CYAN);
            canvas.drawRect(dp(1)/2f,dp(1)/2f,getWidth()-dp(1)/2f,getHeight()-dp(1)/2f,paint);paint.setStyle(Paint.Style.FILL);
            String number=(known?Integer.toString(available):"...")+" / "+required;
            paint.setTextSize(dp(10));paint.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
            paint.setColor(known&&available<required?Color.rgb(255,180,180):Color.WHITE);paint.setShadowLayer(dp(1),0,dp(1),Color.BLACK);
            canvas.drawText(number,(getWidth()-paint.measureText(number))/2,(getHeight()-paint.ascent()-paint.descent())/2,paint);paint.clearShadowLayer();
        }
    }

    private final class LegacyEnergyView extends View {
        final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG); final RectF rect=new RectF();
        int energy,max; boolean known;
        LegacyEnergyView() { super(NanoPane.this.getContext()); }
        void update(JSONObject nano) { energy=nano.optInt("energy"); max=nano.optInt("max"); known=nano.optBoolean("energyKnown"); setContentDescription("Nano energy "+energy+"/"+max); invalidate(); }
        protected void onDraw(Canvas canvas) {
            rect.set(0,0,getWidth(),getHeight()); PocketArt.energy(canvas,rect,energy,max,known,paint,density,images.get(state.optString("energyFill")),images.get(state.optString("energyFrame")));
        }
    }
    private final class IconView extends View {
        final String path; final boolean locked; final int nanoId; final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
        IconView(String p, boolean lock) { this(p,lock,-1); }
        IconView(String p, boolean lock,int id) { super(NanoPane.this.getContext()); path=p; locked=lock; nanoId=id; }
        protected void onDraw(Canvas canvas) {
            float size=Math.min(getWidth(),getHeight()); RectF dst=new RectF((getWidth()-size)/2,0,(getWidth()+size)/2,size);
            if(!locked && previews.draw(canvas,nanoId,dst,paint)) return;
            Bitmap icon=images.get(path); if(icon==null || icon.isRecycled()) return;
            paint.setAlpha(locked ? 110 : 255); canvas.drawBitmap(icon,null,dst,paint);
        }
    }
}
