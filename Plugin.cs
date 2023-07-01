using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace HaremPOV
{
    [BepInPlugin(GUID, PluginName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "frostedashe.hm.harempov";
        public const string PluginName = "HaremPOV";
        public const string Version = "1.0.0";

        private const string SECTION_GENERAL = "General";
        private const string SECTION_HOTKEYS = "Keyboard shortcuts";

        private const float FOV_DEFAULT = 70.0f;
        private const float FOV_MIN = 20.0f;
        private const float FOV_MAX = 120.0f;

        private static ConfigEntry<float> DefaultFOV { get; set; }
        private static ConfigEntry<float> MouseSensitivity { get; set; }
        private static ConfigEntry<float> CameraSmoothing { get; set; }

        private static ConfigEntry<bool> IncludeMalePOV { get; set; }
        private static ConfigEntry<bool> IncludeFemalePOV { get; set; }
        private static ConfigEntry<bool> UseCameraSmoothing { get; set; }
        private static ConfigEntry<bool> UseMouseLookMode { get; set; }

        private static ConfigEntry<KeyboardShortcut> POVHotkey { get; set; }
        private static ConfigEntry<KeyboardShortcut> PrevCharHotkey { get; set; }
        private static ConfigEntry<KeyboardShortcut> NextCharHotkey { get; set; }
        private static ConfigEntry<KeyboardShortcut> ResetPOVHotkey { get; set; }
        private static ConfigEntry<KeyboardShortcut> CameraSmoothingHotkey { get; set; }
        private static ConfigEntry<KeyboardShortcut> MouseLookModeHotkey { get; set; }
        private static ConfigEntry<KeyboardShortcut> CameraRollHotkey { get; set; }

        private Harmony harmony;
        private static ManualLogSource Log;

        private static bool isPOVEnabled = false;
        private static bool inSettingsScreen = false;

        private static HScene hScene = null;
        private static GameObject mainHPosition = null;
        private static float mainHRotationBackup;
        
        private static List<HScene.HInfo.Human> characters;
        private static HScene.HInfo.Human currentChar;
        private static GameObject currentCharHead;
        private static int currentCharIndex = 0;
        private static bool isBlueMan = false;

        private static Vector3 cameraPositionBackup;
        private static Quaternion cameraRotationBackup;
        private static Dictionary<HScene.HInfo.Human, Quaternion> povHeadRotations;
        private static Dictionary<HScene.HInfo.Human, float> povFOVBackups;
        private static float cameraFovBackup;
        private static float cameraNearClipBackup;

        private static GameObject currentFemaleHairFront;
        private static GameObject currentFemaleHairBack;
        private static GameObject currentFemaleHairSide;
        private static GameObject currentFemaleEyeline;
        private static GameObject currentFemaleHairAcc;
        private static GameObject currentFemaleEarrings;

        private static float povCameraNearClipOffset = -0.075f;
        private static Vector3 malePovCameraOffset = new Vector3(0, 0.05f, 0);
        private static Vector3 femalePovCameraOffset = new Vector3(0, 0.065f, 0);
        private static Vector3 povCameraOffset = malePovCameraOffset;

        private bool wasPOVenabled = false;

        private void Awake()
        {
            Log = base.Logger;

            DefaultFOV = Config.Bind(SECTION_GENERAL, "Default FOV", FOV_DEFAULT, new ConfigDescription("Default Field of View in POV mode.", new AcceptableValueRange<float>(20f, 120f)));
            MouseSensitivity = Config.Bind(SECTION_GENERAL, "Mouse Sensitivity", 3.0f, new ConfigDescription("Mouse look sensitivity.", new AcceptableValueRange<float>(0.1f, 5f)));
            CameraSmoothing = Config.Bind(SECTION_GENERAL, "POV Camera Smoothing", 0.7f, new ConfigDescription("Amount of camera smoothing to apply in POV mode.", new AcceptableValueRange<float>(0.0f, 0.85f)));
            UseMouseLookMode = Config.Bind(SECTION_GENERAL, "Use Mouse Look Mode", false, new ConfigDescription("Whether to use Mouse Look Mode, where the mouse moves the camera without having to click and drag."));
            UseCameraSmoothing = Config.Bind(SECTION_GENERAL, "Use Camera Smoothing", false, new ConfigDescription("Whether to apply camera smoothing in POV mode."));
            IncludeMalePOV = Config.Bind(SECTION_GENERAL, "Include Male POV", true, new ConfigDescription("Whether to include male characters when entering or switching POV."));
            IncludeFemalePOV = Config.Bind(SECTION_GENERAL, "Include Female POV", true, new ConfigDescription("Whether to include female characters when entering or switching POV."));
            
            POVHotkey = Config.Bind(SECTION_HOTKEYS, "Toggle POV", new KeyboardShortcut(KeyCode.F), new ConfigDescription("Enable or disable POV mode."));
            PrevCharHotkey = Config.Bind(SECTION_HOTKEYS, "POV Previous Character", new KeyboardShortcut(KeyCode.C), new ConfigDescription("Switch to previous character's perspective when in POV mode."));
            NextCharHotkey = Config.Bind(SECTION_HOTKEYS, "POV Next Character", new KeyboardShortcut(KeyCode.V), new ConfigDescription("Switch to next character's perspective when in POV mode."));
            ResetPOVHotkey = Config.Bind(SECTION_HOTKEYS, "POV Reset", new KeyboardShortcut(KeyCode.LeftControl), new ConfigDescription("Reset camera rotation and FOV when in POV mode."));
            CameraSmoothingHotkey = Config.Bind(SECTION_HOTKEYS, "Toggle Camera Smoothing", new KeyboardShortcut(KeyCode.G), new ConfigDescription("Enable or disable camera smoothing when in POV mode."));
            MouseLookModeHotkey = Config.Bind(SECTION_HOTKEYS, "Toggle Mouse Look Mode", new KeyboardShortcut(KeyCode.Mouse2), new ConfigDescription("Enable or disable Mouse Look Mode."));
            CameraRollHotkey = Config.Bind(SECTION_HOTKEYS, "Camera Roll Key", new KeyboardShortcut(KeyCode.LeftShift), new ConfigDescription("The camera will roll instead of looking around while this key is held down."));

            characters = new List<HScene.HInfo.Human>();
            povHeadRotations = new Dictionary<HScene.HInfo.Human, Quaternion>();
            povFOVBackups = new Dictionary<HScene.HInfo.Human, float>();

            OnHSceneStart();

            IncludeMalePOV.SettingChanged += 
            (object sender, System.EventArgs e) => 
            {
                FindCharacters();
            };

            IncludeFemalePOV.SettingChanged += 
            (object sender, System.EventArgs e) => 
            {
                FindCharacters();
            };
            
            harmony = Harmony.CreateAndPatchAll(GetType());
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
            if(isPOVEnabled)
            {
                if((currentChar != null) && (currentCharHead != null))
                {
                   DisablePOV();
                }
            }
        }
        
        private void Update()
        {
            if((hScene == null) || inSettingsScreen)
            {
                return;
            }

            wasPOVenabled = isPOVEnabled;
            if(POVHotkey.Value.IsDown())
            {
                if(!isPOVEnabled)
                {
                    EnablePOV();
                }
                else
                {
                    DisablePOV();
                }
            }

            if(MouseLookModeHotkey.Value.IsDown())
            {
                if(isPOVEnabled)
                {
                    UseMouseLookMode.Value = !UseMouseLookMode.Value;
                    if(UseMouseLookMode.Value)
                    {
                        Screen.lockCursor = true;
                    }
                    else
                    {
                        Screen.lockCursor = false;
                    }
                }
                else
                {
                    EnablePOV();
                    if(isPOVEnabled)
                    {
                        UseMouseLookMode.Value = true;
                        Screen.lockCursor = true;
                    }
                }
            }

            if(isPOVEnabled && UseMouseLookMode.Value && !Screen.lockCursor)
            {
                Screen.lockCursor = true;
            }

            if(mainHPosition.transform.rotation.eulerAngles.y != mainHRotationBackup)
            {
                UpdatePOVRotationBackups();
            }
            
            mainHRotationBackup = mainHPosition.transform.rotation.eulerAngles.y;

            // if(isPOVEnabled != wasPOVenabled)
            // {
            //     Log.LogMessage("POV toggled");
            // }
            if(isPOVEnabled)
            {
                UpdatePOVCamera();
            }
        }

        private static void EnablePOV()
        {
            if(characters.Count == 0)
            {
                Log.LogMessage("Unable to enter POV mode: No available targets.");
                return;
            }


            currentChar = characters[currentCharIndex];
            if(!currentChar.isFemale)
            {
                if(((CharMale)(currentChar.chara)).GetBlueMan())
                {
                    isBlueMan = true;
                    ((CharMale)(currentChar.chara)).ChangeBlueMan(false);
                }
            }
            currentCharHead = Traverse.Create(currentChar.chara).Field("objHead").GetValue<GameObject>();
            
            cameraNearClipBackup = Camera.main.nearClipPlane;
            cameraPositionBackup = Camera.main.transform.position;
            cameraRotationBackup = Camera.main.transform.rotation;
            
            cameraFovBackup = Camera.main.fieldOfView;
            if(povFOVBackups.ContainsKey(currentChar))
            {
                Camera.main.fieldOfView = povFOVBackups[currentChar];
            }
            else
            {
                Camera.main.fieldOfView = DefaultFOV.Value;
            }

            povCameraOffset = malePovCameraOffset;

            if(povHeadRotations.ContainsKey(currentChar))
            {
                currentCharHead.transform.rotation = povHeadRotations[currentChar];
            }
            
            Camera.main.transform.position = currentCharHead.transform.position;
            Camera.main.transform.Translate(povCameraOffset);
            Camera.main.transform.rotation = currentCharHead.transform.rotation;
            Camera.main.nearClipPlane = cameraNearClipBackup + povCameraNearClipOffset;
            
            SetObjectRenderersEnabled(currentCharHead, false);

            if(currentChar.isFemale)
            {
                povCameraOffset = femalePovCameraOffset;

                Traverse female = Traverse.Create(currentChar.female);
                currentFemaleEyeline = female.Field("objEyeline").GetValue<GameObject>();
                currentFemaleHairFront = female.Field("objHairF").GetValue<GameObject>();
                currentFemaleHairBack = female.Field("objHairB").GetValue<GameObject>();
                currentFemaleHairSide = female.Field("objHairS").GetValue<GameObject>();
                // currentFemaleHairO = female.Field("objHairO").GetValue<GameObject>();
                currentFemaleHairAcc = FindChildGameObject(currentChar.female.GetTopObj(), "cf_acs_cap");
                currentFemaleEarrings = FindChildGameObject(currentChar.female.GetTopObj(), "cf_acs_earrings");

                SetObjectRenderersEnabled(currentFemaleEyeline, false);
                SetObjectRenderersEnabled(currentFemaleHairFront, false);
                SetObjectRenderersEnabled(currentFemaleHairBack, false);
                SetObjectRenderersEnabled(currentFemaleHairSide, false);
                SetObjectRenderersEnabled(currentFemaleHairAcc, false);
                SetObjectRenderersEnabled(currentFemaleEarrings, false);
            }
            
            isPOVEnabled = true;
        }

        private static void DisablePOV()
        {
            povFOVBackups[currentChar] = Camera.main.fieldOfView;
            Camera.main.fieldOfView = cameraFovBackup;
            Camera.main.nearClipPlane = cameraNearClipBackup;
            Camera.main.transform.position = cameraPositionBackup;
            Camera.main.transform.rotation = cameraRotationBackup;
            
            povHeadRotations[currentChar] = currentCharHead.transform.rotation;
            currentCharHead.transform.rotation = new Quaternion();
            
            SetObjectRenderersEnabled(currentCharHead, true);
            
            if(currentChar.isFemale)
            {
                SetObjectRenderersEnabled(currentFemaleEyeline, true);
                SetObjectRenderersEnabled(currentFemaleHairFront, true);
                SetObjectRenderersEnabled(currentFemaleHairBack, true);
                SetObjectRenderersEnabled(currentFemaleHairSide, true);
                SetObjectRenderersEnabled(currentFemaleHairAcc, true);
                SetObjectRenderersEnabled(currentFemaleEarrings, true);
            }
            else if(isBlueMan)
            {
                ((CharMale)(currentChar.chara)).ChangeBlueMan(true);
            }

            if(UseMouseLookMode.Value)
            {
                Screen.lockCursor = false;
            }
            isPOVEnabled = false;

        }

        private void UpdatePOVCamera()
        {
            if(characters.Count > 1)
            {
                if(NextCharHotkey.Value.IsDown() || ((Input.GetAxis("Mouse ScrollWheel") < 0) && UseMouseLookMode.Value))
                {
                    DisablePOV();
                    currentCharIndex++;
                    if(currentCharIndex > characters.Count - 1)
                    {
                        currentCharIndex = 0;
                    }
                    EnablePOV();
                }
                else if(PrevCharHotkey.Value.IsDown() || ((Input.GetAxis("Mouse ScrollWheel") > 0) && UseMouseLookMode.Value))
                {
                    DisablePOV();
                    currentCharIndex--;
                    if(currentCharIndex < 0)
                    {
                        currentCharIndex = characters.Count - 1;
                    }
                    EnablePOV();
                }
            }

            if(CameraSmoothingHotkey.Value.IsDown())
            {
                UseCameraSmoothing.Value = !UseCameraSmoothing.Value;
            }

            if(ResetPOVHotkey.Value.IsDown() || (!Input.GetMouseButton(1) && Input.GetMouseButtonDown(0) && UseMouseLookMode.Value))
            {
                currentCharHead.transform.rotation = new Quaternion();
                Camera.main.fieldOfView = DefaultFOV.Value;
            }

            Vector3 povHeadRotation = new Vector3();
            float x = Input.GetAxis("Mouse X") * MouseSensitivity.Value;
            float y = Input.GetAxis("Mouse Y") * MouseSensitivity.Value;

            bool shouldLookAround = Input.GetMouseButton(0) || UseMouseLookMode.Value;
            bool shouldChangeFOV = Input.GetMouseButton(1);
            bool shouldRollCamera = (Input.GetKey(CameraRollHotkey.Value.MainKey) && (Input.GetMouseButton(0) || UseMouseLookMode.Value)) || (Input.GetMouseButton(0) && Input.GetMouseButton(1));

            if(shouldRollCamera)
            {
                povHeadRotation = new Vector3(0f, 0f, -x);
            }
            else if(shouldChangeFOV)
            {
                Camera.main.fieldOfView += x;
                Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView, FOV_MIN, FOV_MAX);
            }
            else if(shouldLookAround)
            {
                povHeadRotation = new Vector3(-y, x, 0f);
            }

            currentCharHead.transform.Rotate(povHeadRotation);
            float hScale = mainHPosition.transform.localScale.x;
            if(UseCameraSmoothing.Value)
            {
                float camSmoothing = (1.0f - CameraSmoothing.Value) * 10 * Time.deltaTime;
                Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, currentCharHead.transform.position + (currentCharHead.transform.rotation * (povCameraOffset * hScale)), camSmoothing);
                Camera.main.transform.rotation = Quaternion.Slerp(Camera.main.transform.rotation, currentCharHead.transform.rotation, camSmoothing);
            }
            else
            {
                Camera.main.transform.position = currentCharHead.transform.position;
                Camera.main.transform.Translate((povCameraOffset * hScale));
                Camera.main.transform.rotation = currentCharHead.transform.rotation;
            }
        }

        private static void FindCharacters()
        {
            if(hScene == null)
            {
                return;
            }
            
            HScene.HInfo info = Traverse.Create(hScene).Field("info").GetValue<HScene.HInfo>();
            List<HScene.HInfo.Human> males = new List<HScene.HInfo.Human>();
            List<HScene.HInfo.Human> females = new List<HScene.HInfo.Human>();
            characters.Clear();

            foreach(HScene.HInfo.Human human in info.humanList)
            {
                if(human.isFemale)
                {
                    females.Add(human);
                }
                else
                {
                    males.Add(human);
                }
            }

            if(IncludeMalePOV.Value)
            {   
                foreach (HScene.HInfo.Human male in males)
                {
                    characters.Add(male);
                }
            }

            if(IncludeFemalePOV.Value)
            {   
                foreach (HScene.HInfo.Human female in females)
                {
                    characters.Add(female);
                }
            }

            if(currentChar != null)
            {
                bool shouldResetCurrentChar = (!currentChar.isFemale && !IncludeMalePOV.Value) || (currentChar.isFemale && !IncludeFemalePOV.Value);
                if(shouldResetCurrentChar)
                {
                    currentCharIndex = 0;
                    if(isPOVEnabled)
                    {
                        DisablePOV();
                        EnablePOV();
                    }
                }
            }
        }

        private static void UpdatePOVRotationBackups()
        {
            float hRotation = mainHPosition.transform.rotation.eulerAngles.y - mainHRotationBackup;
            Dictionary<HScene.HInfo.Human, Quaternion>.KeyCollection keys = povHeadRotations.Keys;
            HScene.HInfo.Human[] savedChars = new HScene.HInfo.Human[keys.Count];

            int i = 0;
            foreach(HScene.HInfo.Human human in keys)
            {
                savedChars[i] = human;
                i++;
            }

            for(i = 0; i < savedChars.Length; i++)
            {
                Vector3 currentRot = povHeadRotations[savedChars[i]].eulerAngles;
                currentRot.y += hRotation;
                povHeadRotations[savedChars[i]] = Quaternion.Euler(currentRot);
            }
        }

        private static void SetObjectRenderersEnabled(GameObject go, bool enabled)
        {
            Renderer[] headRenderers = go.GetComponentsInChildren<Renderer>();
            foreach(Renderer renderer in headRenderers)
            {
                renderer.enabled = enabled;
            }
        }
        
        private static GameObject FindChildGameObject(GameObject parent, string name)
        {
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                GameObject child = parent.transform.GetChild(i).gameObject;
                if(child.name == name)
                {
                    return child;
                }

                GameObject result = FindChildGameObject(child, name);
                if(result)
                {
                    return result;
                }
            }
            
            return null;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "Start")]
        private static void OnHSceneStart()
        {
            hScene = FindObjectOfType<HScene>();
            if(hScene)
            {
                FindCharacters();
                mainHPosition = Traverse.Create(hScene).Field("mainPosition").GetValue<GameObject>();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CheckScene), "JudgeProc")]
        private static void OnHSceneEnd(CheckScene __instance, bool __result)
        {
            if(__result && __instance.IsYes && (BaseScene.overlapSceneType == BaseScene.Type.SceneEnd))
            {
                if(isPOVEnabled)
                {
                    DisablePOV();
                }

                povHeadRotations.Clear();
                povFOVBackups.Clear();
                characters.Clear();
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(CameraControl), "LateUpdate")]
        private static bool OnBaseCameraUpdate()
        {
            return !isPOVEnabled;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "ChangeAnimeMenue")]
        private static void OnAnimationChange()
        {
            if(isPOVEnabled)
            {
                DisablePOV();
                povHeadRotations.Clear();
                povFOVBackups.Clear();
                EnablePOV();
            }
            else
            {
                povHeadRotations.Clear();
                povFOVBackups.Clear();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ConfigScene), "ChangeBefore")]
        private static void OnConfigScreenEnter()
        {
            Screen.lockCursor = false;
            inSettingsScreen = true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(ConfigScene), "ChangeAfter")]
        private static void OnConfigScreenExit()
        {
            inSettingsScreen = false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(HSceneMenu), "SubMenu")]
        private static void OnMapPointChangeAfter(HSceneMenu __instance)
        {
            if(!isPOVEnabled)
            {
                return;
            }

            HSceneMenu.E_SUB_MENU subMenu = Traverse.Create(__instance).Field("subMenu").GetValue<HSceneMenu.E_SUB_MENU>();
            if((subMenu == HSceneMenu.E_SUB_MENU.POINT) || (subMenu == HSceneMenu.E_SUB_MENU.MAP))
            {
                YSUI_Select selCtrl = Traverse.Create(__instance).Field("selCtrl").GetValue<YSUI_Select>();
                HSceneMenu.E_COL_ID selectIdFromGroup = (HSceneMenu.E_COL_ID)selCtrl.GetSelectIdFromGroup(4);
                Dictionary<HSceneMenu.E_COL_ID, HSceneMenu.E_SP_ID> colSubPair = Traverse.Create(__instance).Field("colSubPair").GetValue<Dictionary<HSceneMenu.E_COL_ID, HSceneMenu.E_SP_ID>>();

                if(colSubPair.ContainsKey(selectIdFromGroup))
                {
                    Camera.main.transform.position = currentCharHead.transform.position;
                    Camera.main.transform.Translate(povCameraOffset);
                    Camera.main.transform.rotation = currentCharHead.transform.rotation;
                }
            }
        }
    }
}
