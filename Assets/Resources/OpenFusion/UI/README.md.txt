# Assets de interfaz del cliente original (extraídos de main.unity3d)

695 PNG en `Assets/Resources/OpenFusion/UI/Textures/` (nombre original; `__<id>` si el nombre se repite ignorando mayúsculas). 639 están referenciadas por scripts/GUISkin.

Datos en `Resources/OpenFusion/UI/Data/`: `ui_by_script.json` (script -> campo -> textura), `bindings.json` (todos los MonoBehaviour con sus campos), `catalog.json`, `Fonts/*.json` (24 fuentes de mapa de bits).

| Script (GameObject) | Texturas | Ejemplos |
|---|---|---|
| GUISkin#1 (-) | 37 | awaiting_input, blue_button_normal, blue_button_over, chat_box, color_box |
| GUISkin#2 (-) | 77 | CancelNormal, CombinedItemIcon, EnchantLevel, HammerIcon, HammerIconOver |
| GUISkin#3 (-) | 16 | CombiGenderError, CombiIcon2, CombiLookItemBG, CombiStatItemBG, CombiStatItemError |
| GUISkin#4 (-) | 41 | ChatBubbleIcon_buddy, ChatBubbleIcon_group, HasNextMenu_Arrow, blue_button_normal, blue_button_over |
| GUISkin#5 (-) | 12 | button, exit_button, exit_button_push, hscrollbar, hscrollbart |
| GUISkin#6 (-) | 30 | CCCheckboxChecked, CCCheckboxCkeckedOver, CCCheckboxNormal, CCCheckboxOver, NanoMachineHelpButton |
| GUISkin#7 (-) | 5 | TimeMachine_Icon, premABut, premAButUp, timeMBut, timeMButUp |
| GUISkin#8 (-) | 16 | CSFullScreenButton, CSFullToWindow, MessageArea, OverheatBG, OverheatMax |
| GUISkin#9 (-) | 32 | NanoMachineHelpButton, NanoMachineHelpButtonOver, blue_button_normal, blue_button_over, buddy_select |
| GUISkin#10 (-) | 24 | EPRaceBack, EPRaceItemBar, EPStar, EPStarBack, SSBox |
| GUISkin#11 (-) | 13 | backbar, blue_button_normal, blue_button_over, chat_box, ff-closebutton-active |
| GUISkin#12 (-) | 14 | CSFullScreenButton, CSFullToWindow, CSMusicOff, CSMusinOn, LoadingBar |
| GUISkin#13 (-) | 6 | NanoMachineHelpButton, NanoMachineHelpButtonOver, blue_button_normal, blue_button_over, close |
| GUISkin#14 (-) | 12 | CancelNormal, CombinedItemIcon, blue_button_normal, blue_button_over, close |
| GUISkin#15 (-) | 3 | CancelNormal, blue_button_normal, blue_button_over |
| GUISkin#16 (-) | 73 | CCBG, CCBodyshapeDisplay, CCBodyshapeLeftNormal, CCBodyshapeLeftOver, CCBodyshapeRightNormal |
| GUISkin#17 (-) | 11 | advertis, blue_button_normal, blue_button_over, close_normal, close_over |
| GUISkin#18 (-) | 24 | NanoMachineHelpButton, NanoMachineHelpButtonOver, Shadow, blue_button_normal, blue_button_over |
| GUISkin#19 (-) | 11 | HelpButtonNormal, HelpButtonUp, HelpTitle, blue_button_normal, blue_button_over |
| GUISkin#20 (-) | 23 | HP_BAR, bb_009, bbubble, button, close_normal |
| GUISkin#21 (-) | 4 | launchback, launchback__352, launchbar, launchgage |
| GUISkin#22 (-) | 25 | CancelNormal, CombinedItemIcon, NanoMachineBG2, Shadow, Shadow__470 |
| GUISkin#23 (-) | 20 | CancelNormal, NanoMachineHelpButton, NanoMachineHelpButtonOver, blue_button_normal, blue_button_over |
| GUISkin#24 (-) | 28 | NanoMachineHelpButton, NanoMachineHelpButtonOver, RanktabBar__549, ff-closebutton-active, ff-closebutton-hover |
| GUISkin#25 (-) | 15 | CancelNormal, NanoMachineHelpButton, NanoMachineHelpButtonOver, blue_button_normal, blue_button_over |
| GUISkin#26 (-) | 6 | CancelNormal, QuitBack, QuitBackG, QuitButNor, QuitButUp |
| NpcIconMode (NpcIconMode) | 25 | CombiIcon, CombiRulesIcon, back, ben_icon, compu_icon__556 |
| Panel_UserClothes (UserEquip) | 22 | BenIcon, Computeres, Defense, DexIcon, EddIcon |
| Panel_Vendor (Vendor) | 8 | itembar, vendorTab1, vendorTab1Nor, vendorTab1Over, vendorTab2 |
| cnGuideMode (GuideMode) | 21 | Ben, Edd, Edd_confirm, Edd_icon, Mojo |
| cnQuit (QuitMenu) | 1 | back |
| InventoryManagerScript (InvManager) | 8 | BoostIcon, CombiTarosIcon, PostionIcon, Potion, Taros |
| NanoMachineScript (InvManager) | 5 | NanoPopUp, NanoPopUpEquippedBG, NanoPopUpNextNano, NanoPopupFMBar, NanoPopupItemBar |
| UserContainer (UserManager) | 4 | NonSpecRamp1, NonSpecRamp2, SpecRamp1, SpecRamp2 |
| Panel_UserStore (UserStore) | 8 | itembar, vendorTab1, vendorTab1Nor, vendorTab1Over, vendorTab2 |
| ResurrectMode (ResurrectMode) | 4 | back, buff_icon_back, grim, resurrect |
| PrintName (OwnUser) | 5 | target_icon1a, target_icon1b, target_icon2, target_icon3, target_n |
| cnDexterShipEventScene (DexterScene) | 1 | back |
| cnSystemMessageManager (SystemMessageManager) | 7 | CombiIcon2, back, equipbox, messicon_buddy, messicon_group |
| GameFrame (GameModeManager) | 4 | back, combat, vehicle_expire, white |
| cnGuiEnchant (EnchantMode) | 4 | CombiBlackShade, EnchantBG, EnchantWaitProgress, EnchantXmark |
| cnOptionMode (OptionMode) | 8 | color_box, color_box_selected, equipbar, rankmypos, set_check |
| CnGuiNanoFreeTuning (NanoFreeTuningMode) | 2 | NanoPopUpNextNano_1, back |
| cnGuiCombi (CombiMode) | 2 | CombiBlackShade, CombiWndBG |
| CnGuiCharSelection (CharSelectionMode) | 3 | CSDeleteBG, CSDeleteWindow, load |
| CnGuiServerSelection (ServerSelectionMode) | 1 | login_screen_bg_16x10 |
| Panel_EmailList (EmailMode) | 8 | clip, comemaillist, emailback, emailcombut, emailcombutover |
| Panel_NewEmail (EmailMode) | 4 | cacuback, clip, emailbudylist, emailcacu |
| CnGuiCharacter_info (GameHUD) | 8 | DiskFront, HP_BAR, character_info, exp_bar, exp_bonus |
| CnGuiMonster_info (GameHUD) | 17 | DiskFront, HP_BAR, a_icon, b_icon, blue_icon |
| CnGuiNano_info (GameHUD) | 10 | CoolTime_00, HP_BAR, cooltime01, cooltime02, cooltime03 |
| cnGUINanocom (GameHUD) | 60 | EmailNotice, EmailNoticeUp, back, bonus, boxalpha |
| CnGuiGroup_info (GameHUD) | 5 | HP_BAR, group_freechat_icon, group_info, group_nano_hpframe, npc_co_op |
| CnGuiChat (GameHUD) | 5 | CSDeleteBG, CSDeleteWindow, chatAlert, freechat_icon, press_enter |
| cnNanoWheel (GameHUD) | 26 | CoolTime_00, HP_BAR, NanoGumballBlue, NanoGumballRed, NanoGumballYellow |
| CnGuiSkillBuffIcon (GameHUD) | 1 | buff_icon_back |
| cnQuickSlot (GameHUD) | 1 | quickslot_main |
| CnGuiWpnOverheat (GameHUD) | 1 | OverheatNormal |
| CnGuiNameCreation (CharNameCreationMode) | 1 | horizSlider |
| Panel_Cashmall (CashmallMode) | 2 | cash, vendorinfo |
| cnTrans (TransportMode) | 14 | bubble, buddy, line_main, line_main00, line_main01 |
| Panel_BankScript (BankMode) | 1 | vendorinfo |
| AssetLoader (AssetLoader) | 2 | back, load |
| cnGuiFirstUse (FirstUse) | 3 | FirstUseComputress, FirstUseIcon, FirstUseIconSelected |
| cnRaceMode (RaceMode) | 2 | black.dds, fmicon |
| WorldMapMode (WorldMapMode) | 38 | back, line_main, line_main00, line_main01, map_icon_00 |
| cnMissionJournal (Journal) | 24 | ben_banner, buff_icon_back, compu_banner, current_mission, dexter_banner |
| cnDisplayMapName (MainGame) | 7 | boost, crate, fm_count, full_inven, potion__548 |
