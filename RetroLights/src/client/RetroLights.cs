using System;
using System.Collections.Generic;
using EccsLogicWorldAPI.Shared.AccessHelper;
using HarmonyLib;
using LogicAPI.Client;
using LogicAPI.Data;
using LogicSettings;
using LogicWorld.ClientCode;
using LogicWorld.Interfaces;
using LogicSettings;

namespace RetroLights {
    public class RetroLights : ClientMod
    {
        private static Action<GenericDisplay<IPanelDisplayData>> queueUpdateForNextFrame;
        
        [Setting_SliderFloat("RetroLights.DisplayEffect.DisplayIntensity")]
        public static float DisplayIntensity {
            get => _displayIntensity;
            set
            {
                _displayIntensity = value;
                updateAllIntensities();
            }
        }
        [Setting_SliderFloat("RetroLights.DisplayEffect.RampOnDivider")]
        public static float RampOnDivider {
            get => _rampOnDivider;
            set => _rampOnDivider = value;
        }
        [Setting_SliderFloat("RetroLights.DisplayEffect.RampOffDivider")]
        public static float RampOffDivider {
            get => _rampOffDivider;
            set => _rampOffDivider = value;
        }
        
        private static float _displayIntensity = 3;
        private static float _rampOnDivider = 6;
        private static float _rampOffDivider = 9;

        protected static void updateAllIntensities(){
            var mainWorld = Instances.MainWorld;
            if (mainWorld == null)
            {
                return;
            }
            var display = mainWorld.ComponentTypes.GetComponentType("MHG.StandingDisplay");
            var panel = mainWorld.ComponentTypes.GetComponentType("MHG.PanelDisplay");
            foreach(var kvp in mainWorld.Data.AllComponents)
            {
                var (address, data) = kvp;
                if (data.Data.Type == display || data.Data.Type == panel)
                {
                    mainWorld.Renderer.Entities.GetClientCode(address).QueueFrameUpdate();
                }
            }
        }
        
        protected override void Initialize()
        {
            var harmony = new Harmony("RetroLights");
            
            var typeGenericDisplay = typeof(GenericDisplay<IPanelDisplayData>);
            var methGetColor = Methods.getPrivate(typeGenericDisplay, "<FrameUpdate>g__GetCurrentColor|2_0");
            var methFrameUpdate = Methods.getPrivate(typeGenericDisplay, "FrameUpdate");
            var methContinueUpdatingForAnotherFrame = Methods.getPrivate(typeGenericDisplay, "ContinueUpdatingForAnotherFrame");
            harmony.Patch(methGetColor, postfix: new HarmonyMethod(Methods.getPublicStatic(GetType(), nameof(patchGetColor))));
            harmony.Patch(methFrameUpdate, postfix: new HarmonyMethod(Methods.getPublicStatic(GetType(), nameof(patchFrameUpdate))));
            
            queueUpdateForNextFrame = (Action<GenericDisplay<IPanelDisplayData>>) methContinueUpdatingForAnotherFrame.CreateDelegate(typeof(Action<GenericDisplay<IPanelDisplayData>>));
            
        }
        
        public static void patchFrameUpdate(GenericDisplay<IPanelDisplayData> __instance){}
        public static void patchGetColor(GenericDisplay<IPanelDisplayData> __instance, IReadOnlyList<IRenderedEntity> ___BlockEntities, ref GpuColor __result)
        {
            var setCol = __result;
            var curCol = ___BlockEntities[0].Color;

            float curR = curCol.r;
            float curG = curCol.g;
            float curB = curCol.b;
            
            curR = removeIntensity(curR, _displayIntensity);
            curG = removeIntensity(curG, _displayIntensity);
            curB = removeIntensity(curB, _displayIntensity);
            
            var newR = calcNewValue(setCol.r, curR, 0.01f, _rampOnDivider, _rampOffDivider);
            var newG = calcNewValue(setCol.g, curG, 0.01f, _rampOnDivider, _rampOffDivider);
            var newB = calcNewValue(setCol.b, curB, 0.01f, _rampOnDivider, _rampOffDivider);
            if (newR != setCol.r || newG != setCol.g || newB != setCol.b) {
                queueUpdateForNextFrame(__instance);
            }
            newR = applyIntensity(newR, _displayIntensity);
            newG = applyIntensity(newG, _displayIntensity);
            newB = applyIntensity(newB, _displayIntensity);
            __result = new GpuColor(newR, newG, newB);
        }

        private static float calcNewValue(float target, float current, float threshold, float divOn, float divOff){
            var delta = target - current;
            if (Math.Abs(delta) < threshold) {
                return target;
            }

            float newValue;
            if (delta > 0) {
                newValue = current + (delta / divOn);
            } else {
                newValue = current + (delta / divOff);
            }
            return newValue;
        }
        private static float removeIntensity(float col, float displayIntensity){
            var newCol = (float)Math.Sqrt(col/displayIntensity);
            return newCol;
        }
        private static float applyIntensity(float col, float displayIntensity){
            var newCol = col*col*displayIntensity;
            return newCol;
        }
    }
}