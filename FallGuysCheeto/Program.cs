using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics;
using SharpDX.XInput;
using WeScriptWrapper;
using WeScript.SDK.UI;
using WeScript.SDK.UI.Components;
using WeScript.SDK.Utils;

//full credits to Apin for his Cheat Table https://pastebin.com/VFdECgCE

namespace FallGuysCheeto
{
    class Program
    {

        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static IntPtr GameAssembly_dll = IntPtr.Zero;
        public static IntPtr characterPtr = IntPtr.Zero;
       

        public static Menu RootMenu { get; private set; }

        class Components
        {
            public static readonly MenuSlider normalMaxSpeed = new MenuSlider("normalMaxSpeed", "normalMaxSpeed", 95, 95, 500);
            public static readonly MenuSlider getUpMaxSpeed = new MenuSlider("getUpMaxSpeed", "getUpMaxSpeed", 95, 95, 500);
            public static readonly MenuSlider rollingMaxSpeed = new MenuSlider("rollingMaxSpeed", "rollingMaxSpeed", 70, 70, 500);
            public static readonly MenuSlider rollingInAirMaxSpeed = new MenuSlider("rollingInAirMaxSpeed", "rollingInAirMaxSpeed", 2, 2, 500);
            public static readonly MenuSlider grabbingMaxSpeed = new MenuSlider("grabbingMaxSpeed", "grabbingMaxSpeed", 50, 50, 500);
            public static readonly MenuSlider grabbingAttemptMaxSpeed = new MenuSlider("grabbingAttemptMaxSpeed", "grabbingAttemptMaxSpeed", 70, 70, 500);
            public static readonly MenuSlider carryMaxSpeed = new MenuSlider("carryMaxSpeed", "carryMaxSpeed", 80, 80, 500);
            public static readonly MenuSlider normalTurnSpeed = new MenuSlider("normalTurnSpeed", "normalTurnSpeed", 80, 80, 500);
            public static readonly MenuSlider aerialTurnSpeed = new MenuSlider("aerialTurnSpeed", "aerialTurnSpeed", 40, 40, 500);
            public static readonly MenuSlider diveTurnSpeed = new MenuSlider("diveTurnSpeed", "diveTurnSpeed", 11, 11, 500);
            public static readonly MenuSlider rollingTurnSpeed = new MenuSlider("rollingTurnSpeed", "rollingTurnSpeed", 30, 30, 500);
            public static readonly MenuSlider rollingInAirTurnSpeed = new MenuSlider("rollingInAirTurnSpeed", "rollingInAirTurnSpeed", 3, 3, 500);
            public static readonly MenuSlider grabbingTurnSpeed = new MenuSlider("grabbingTurnSpeed", "grabbingTurnSpeed", 40, 40, 500);
            public static readonly MenuSlider grabbingAttemptTurnSpeed = new MenuSlider("grabbingAttemptTurnSpeed", "grabbingAttemptTurnSpeed", 60, 60, 500);
            public static readonly MenuSlider grabbedTurnSpeed = new MenuSlider("grabbedTurnSpeed", "grabbedTurnSpeed", 5, 5, 500);
            public static readonly MenuSlider maxSlopeIncline = new MenuSlider("maxSlopeIncline", "maxSlopeIncline", 480, 480, 5000);
            public static readonly MenuSlider maxSlopeInclineForAnim = new MenuSlider("maxSlopeInclineForAnim", "maxSlopeInclineForAnim", 300, 300, 3000);
            public static readonly MenuSlider gravityScale = new MenuSlider("gravityScale", "gravityScale", 15, 1, 15);
            public static readonly MenuSlider maxGravityVelocity = new MenuSlider("maxGravityVelocity", "maxGravityVelocity", 400, 400, 5000);
            public static readonly MenuSlider unintentionalMoveSpeedThreshold = new MenuSlider("unintentionalMoveSpeedThreshold", "unintentionalMoveSpeedThreshold", 75, 75, 5000);
            public static readonly MenuSlider unintentionalMoveSpeedThresholdDuringEmote = new MenuSlider("unintentionalMoveSpeedThresholdDuringEmote", "unintentionalMoveSpeedThresholdDuringEmote", 15, 15, 500);
        }

        public static void InitializeMenu()
        {
            RootMenu = new Menu("fallguystrainer", "WeScript.app FallGuys Cheeto Trainer", true)
            {
                Components.normalMaxSpeed,
                Components.getUpMaxSpeed,
                Components.rollingMaxSpeed,
                Components.rollingInAirMaxSpeed,
                Components.grabbingMaxSpeed,
                Components.grabbingAttemptMaxSpeed,
                Components.carryMaxSpeed,
                Components.normalTurnSpeed,
                Components.diveTurnSpeed,
                Components.rollingTurnSpeed,
                Components.rollingInAirTurnSpeed,
                Components.grabbingTurnSpeed,
                Components.grabbingAttemptTurnSpeed,
                Components.grabbedTurnSpeed,
                Components.maxSlopeIncline,
                Components.maxSlopeInclineForAnim,
                Components.gravityScale,
                Components.maxGravityVelocity,
                Components.unintentionalMoveSpeedThreshold,
                Components.unintentionalMoveSpeedThresholdDuringEmote,
            };
            RootMenu.Attach();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("FallGuys Cheeto assembly loaded! (Simple trainer updated 25 August 2020)");
            InitializeMenu();
            Memory.OnTick += OnTick;
        }

        public static void writeCheatos(IntPtr processHandle, IntPtr characterPtr, bool reset)
        {
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x18), reset ? 9.5f : (Components.normalMaxSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x1C), reset ? 9.5f : (Components.getUpMaxSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x20), reset ? 7.0f : (Components.rollingMaxSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x24), reset ? 0.2f : (Components.rollingInAirMaxSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x28), reset ? 5.0f : (Components.grabbingMaxSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x2C), reset ? 7.0f : (Components.grabbingAttemptMaxSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x30), reset ? 8.0f : (Components.carryMaxSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x34), reset ? 8.0f : (Components.normalTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x38), reset ? 4.0f : (Components.aerialTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x3C), reset ? 1.1f : (Components.diveTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x40), reset ? 3.0f : (Components.rollingTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x44), reset ? 0.3f : (Components.rollingInAirTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x48), reset ? 4.0f : (Components.grabbingTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x4C), reset ? 6.0f : (Components.grabbingAttemptTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0x50), reset ? 0.5f : (Components.grabbedTurnSpeed.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0xB0), reset ? 48.0f : (Components.maxSlopeIncline.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0xB4), reset ? 30.0f : (Components.maxSlopeInclineForAnim.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0xB8), reset ? 1.5f : (Components.gravityScale.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0xBC), reset ? 40.0f : (Components.maxGravityVelocity.Value * 0.1f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0xC0), reset ? 0.75f : (Components.unintentionalMoveSpeedThreshold.Value * 0.01f));
            Memory.WriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() - 0x18 + 0xC4), reset ? 1.5f : (Components.unintentionalMoveSpeedThresholdDuringEmote.Value * 0.1f));
        }

        private static void OnTick(int counter, EventArgs args)
        {
            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = Memory.FindWindowClassName("UnityWndClass"); //try finding the window of the process (check if it's spawned and loaded)
                if (wndHnd != IntPtr.Zero) //if it exists
                {
                    var calcPid = Memory.GetPIDFromHWND(wndHnd); //get the PID of that same process
                    if (calcPid > 0) //if we got the PID
                    {
                        processHandle = Memory.OpenProcess(PROCESS_ALL_ACCESS, calcPid); //get full access to the process so we can use it later
                        if (processHandle != IntPtr.Zero)
                        {
                            //if we got access to the game, check if it's x64 bit, this is needed when reading pointers, since their size is 4 for x86 and 8 for x64
                            isWow64Process = Memory.IsProcess64Bit(processHandle);
                            //here you can scan for signatures and stuff, it happens only once on "attach"
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowClassName("UnityWndClass");
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    isOverlayOnTop = Overlay.IsOnTop();

                    if (GameAssembly_dll == IntPtr.Zero)
                    {
                        GameAssembly_dll = Memory.GetModule(processHandle, "GameAssembly.dll");
                    }
                    //since we're not drawing, the rest of the code can be put here.
                    if (characterPtr == IntPtr.Zero)
                    {
                        characterPtr = Memory.FindSignatureBase(processHandle, IntPtr.Zero, IntPtr.Zero, "00 00 18 41 00 00 18 41 00 00 E0 40 CD CC 4C 3E 00 00 A0 40 00 00 E0 40 00 00 00 41 00 00 00 41 00"); //just to get the base of the character allocation data
                        //9.50 9.50 7.00 0.20 5.00 7.00 8.00 or ... 41180000 41180000 40E00000 3E4CCCCD 40A00000 40E00000 41000000
                        if (characterPtr != IntPtr.Zero)
                        {
                            Console.WriteLine($"FOUND Character Base Ptr: {characterPtr.ToString("X")}");
                        }
                        else
                        {
                            Console.WriteLine("FAILED TO FIND Character Base Ptr :(");
                        }
                    }
                    else
                    {
                        //we are in game and got char ptr ... continue with menu modifications every tick

                        IntPtr timeToRunNextCharacterControllerDataCheckPTR = SDKUtil.ReadPointerChain(processHandle, (IntPtr)(GameAssembly_dll.ToInt64() + 0x02BC6108), isWow64Process, 0xB8, 0x0, 0xB8, 0x10, 0x30, 0x1C8);
                        //Console.WriteLine(timeToRunNextCharacterControllerDataCheck.ToString("X"));
                        if (timeToRunNextCharacterControllerDataCheckPTR != IntPtr.Zero)
                        {
                            var timeToRunNextCharacterControllerDataCheck = Memory.ReadFloat(processHandle, (IntPtr)timeToRunNextCharacterControllerDataCheckPTR.ToInt64() + 0x10);
                            if ((timeToRunNextCharacterControllerDataCheck > 0) && (timeToRunNextCharacterControllerDataCheck < 100000000.0f))
                            {                                                                                             
                                Memory.WriteFloat(processHandle, (IntPtr)(timeToRunNextCharacterControllerDataCheckPTR.ToInt64() + 0x10), 100000000.0f);
                            }
                            else
                            {
                                var doublecheck = Memory.ReadFloat(processHandle, (IntPtr)timeToRunNextCharacterControllerDataCheckPTR.ToInt64() + 0x10);
                                if (doublecheck == 100000000.0f)
                                {
                                    //DelayAction.Queue(() => writeCheatosz(processHandle, characterPtr), 5000.0f);
                                    writeCheatos(processHandle, characterPtr, false);
                                }
                            }
                        }
                        else
                        {
                            writeCheatos(processHandle, characterPtr, true);
                        }
                    }

                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(processHandle); //close the handle to avoid leaks
                    processHandle = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;
                    characterPtr = IntPtr.Zero;
                    GameAssembly_dll = IntPtr.Zero;
                }
            }
        }

    }
}
