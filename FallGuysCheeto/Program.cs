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
        public static ulong lastTime = 0;


        public static Menu RootMenu { get; private set; }

        class Components
        {
            public static readonly MenuKeyBind keepFlying = new MenuKeyBind("keepflying", "Keep staying in air by holding jump key", VirtualKeyCode.Space, KeybindType.Hold, false);
            public static readonly MenuKeyBind keepSpeeding = new MenuKeyBind("keepspeeding", "Keep speeding while holding key", VirtualKeyCode.CapsLock, KeybindType.Hold, false);
            //public static readonly MenuBool superGrab = new MenuBool("superGrab", "Enable SUPER GRAB", true);
            public static readonly MenuSlider normalMaxSpeed = new MenuSlider("normalMaxSpeed", "normalMaxSpeed", 500, 95, 500);
            public static readonly MenuSlider gravityScale = new MenuSlider("gravityScale", "gravityScale", -1, -1, 15);
            //public static readonly MenuSeperator sep1 = new MenuSeperator("sep1");
            //public static readonly MenuSlider getUpMaxSpeed = new MenuSlider("getUpMaxSpeed", "getUpMaxSpeed", 95, 95, 500);
            //public static readonly MenuSlider rollingMaxSpeed = new MenuSlider("rollingMaxSpeed", "rollingMaxSpeed", 70, 70, 500);
            //public static readonly MenuSlider rollingInAirMaxSpeed = new MenuSlider("rollingInAirMaxSpeed", "rollingInAirMaxSpeed", 2, 2, 500);
            //public static readonly MenuSlider grabbingMaxSpeed = new MenuSlider("grabbingMaxSpeed", "grabbingMaxSpeed", 50, 50, 500);
            //public static readonly MenuSlider grabbingAttemptMaxSpeed = new MenuSlider("grabbingAttemptMaxSpeed", "grabbingAttemptMaxSpeed", 70, 70, 500);
            //public static readonly MenuSlider carryMaxSpeed = new MenuSlider("carryMaxSpeed", "carryMaxSpeed", 80, 80, 500);
            //public static readonly MenuSlider normalTurnSpeed = new MenuSlider("normalTurnSpeed", "normalTurnSpeed", 80, 80, 500);
            //public static readonly MenuSlider aerialTurnSpeed = new MenuSlider("aerialTurnSpeed", "aerialTurnSpeed", 40, 40, 500);
            //public static readonly MenuSlider diveTurnSpeed = new MenuSlider("diveTurnSpeed", "diveTurnSpeed", 11, 11, 500);
            //public static readonly MenuSlider rollingTurnSpeed = new MenuSlider("rollingTurnSpeed", "rollingTurnSpeed", 30, 30, 500);
            //public static readonly MenuSlider rollingInAirTurnSpeed = new MenuSlider("rollingInAirTurnSpeed", "rollingInAirTurnSpeed", 3, 3, 500);
            //public static readonly MenuSlider grabbingTurnSpeed = new MenuSlider("grabbingTurnSpeed", "grabbingTurnSpeed", 40, 40, 500);
            //public static readonly MenuSlider grabbingAttemptTurnSpeed = new MenuSlider("grabbingAttemptTurnSpeed", "grabbingAttemptTurnSpeed", 60, 60, 500);
            //public static readonly MenuSlider grabbedTurnSpeed = new MenuSlider("grabbedTurnSpeed", "grabbedTurnSpeed", 5, 5, 500);
            //public static readonly MenuSlider maxSlopeIncline = new MenuSlider("maxSlopeIncline", "maxSlopeIncline", 480, 480, 5000);
            //public static readonly MenuSlider maxSlopeInclineForAnim = new MenuSlider("maxSlopeInclineForAnim", "maxSlopeInclineForAnim", 300, 300, 3000);
            //public static readonly MenuSlider maxGravityVelocity = new MenuSlider("maxGravityVelocity", "maxGravityVelocity", 400, 400, 5000);
            //public static readonly MenuSlider unintentionalMoveSpeedThreshold = new MenuSlider("unintentionalMoveSpeedThreshold", "unintentionalMoveSpeedThreshold", 75, 75, 5000);
            //public static readonly MenuSlider unintentionalMoveSpeedThresholdDuringEmote = new MenuSlider("unintentionalMoveSpeedThresholdDuringEmote", "unintentionalMoveSpeedThresholdDuringEmote", 15, 15, 500);
        }

        public static void InitializeMenu()
        {
            RootMenu = new Menu("fallguystrainer", "WeScript.app FallGuys Cheeto Trainer 2.0", true)
            {
                Components.keepFlying,
                Components.keepSpeeding,
                //Components.superGrab,
                Components.normalMaxSpeed,
                Components.gravityScale,
                //Components.sep1,
                //Components.getUpMaxSpeed,
                //Components.rollingMaxSpeed,
                //Components.rollingInAirMaxSpeed,
                //Components.grabbingMaxSpeed,
                //Components.grabbingAttemptMaxSpeed,
                //Components.carryMaxSpeed,
                //Components.normalTurnSpeed,
                //Components.diveTurnSpeed,
                //Components.rollingTurnSpeed,
                //Components.rollingInAirTurnSpeed,
                //Components.grabbingTurnSpeed,
                //Components.grabbingAttemptTurnSpeed,
                //Components.grabbedTurnSpeed,
                //Components.maxSlopeIncline,
                //Components.maxSlopeInclineForAnim,
                //Components.maxGravityVelocity,
                //Components.unintentionalMoveSpeedThreshold,
                //Components.unintentionalMoveSpeedThresholdDuringEmote,
            };
            RootMenu.Attach();
        }

        static void Main(string[] args)
        {
            Console.WriteLine("FallGuys Cheeto for season 2 loaded! (13.10.2020)");
            InitializeMenu();
            if (!Memory.InitDriver(DriverName.frost_64))
            {
                Console.WriteLine("[ERROR] Failed to initialize driver for some reason...");
            }
            Memory.OnTick += OnTick;
        }

        public static void writeCheatos(IntPtr processHandle, IntPtr characterPtr, bool reset)
        {
            if (Components.keepFlying.Enabled)
            {
                Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x18), (Components.normalMaxSpeed.Value * 0.1f));
                Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xC0), (Components.gravityScale.Value * 0.1f));
            }
            else
            {
                Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x18), 9.5f);
                Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xC0), 1.5f);
                if (Components.keepSpeeding.Enabled)
                {
                    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x18), (Components.normalMaxSpeed.Value * 0.1f));
                }
                else
                {
                    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x18), 9.5f);
                }
            }

            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x18), reset ? 9.5f : (Components.normalMaxSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1C), reset ? 9.5f : (Components.getUpMaxSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x20), reset ? 7.0f : (Components.rollingMaxSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x24), reset ? 0.2f : (Components.rollingInAirMaxSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x28), reset ? 5.0f : (Components.grabbingMaxSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x2C), reset ? 7.0f : (Components.grabbingAttemptMaxSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x30), reset ? 8.0f : (Components.carryMaxSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x34), reset ? 8.0f : (Components.normalTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x38), reset ? 4.0f : (Components.aerialTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x3C), reset ? 1.1f : (Components.diveTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x40), reset ? 3.0f : (Components.rollingTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x44), reset ? 0.3f : (Components.rollingInAirTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x48), reset ? 4.0f : (Components.grabbingTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x4C), reset ? 6.0f : (Components.grabbingAttemptTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x50), reset ? 0.5f : (Components.grabbedTurnSpeed.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xB0), reset ? 48.0f : (Components.maxSlopeIncline.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xB4), reset ? 30.0f : (Components.maxSlopeInclineForAnim.Value * 0.1f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xB8), reset ? 1.5f : (Components.gravityScale.Value * 0.1f));
            //if (Components.keepFlying.Enabled)
            //{
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xBC), reset ? 40.0f : -0.1f);
            //}
            //else
            //{
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xBC), reset ? 40.0f : (Components.maxGravityVelocity.Value * 0.1f));
            //}

            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xC0), reset ? 0.75f : (Components.unintentionalMoveSpeedThreshold.Value * 0.01f));
            //Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0xC4), reset ? 1.5f : (Components.unintentionalMoveSpeedThresholdDuringEmote.Value * 0.1f));
            //if (Components.superGrab.Enabled)
            //{
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1A8), reset ? 6.0f : 100000000.0f); //playerGrabDetectRadius
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1AC), reset ? 2.0f : 100000000.0f); //playerGrabCheckDistance
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x20C), reset ? 6.0f : 100000000.0f); //playerGrabberMaxForce
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1C4), reset ? 1.2f : 100000000.0f); //playerGrabBreakTime
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x134), reset ? 1.0f : 100000000.0f); //armLength
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1B0), reset ? 1.0f : 100000000.0f); //playerGrabCheckPredictionBase
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x218), reset ? 0.5f : 1.0f); //playerGrabImmediateVelocityReduction
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x214), reset ? 0.5f : 1.0f); //playerGrabberDragDirectionContribution
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x17C), reset ? 0.5f : 0.0f); //grabCooldown
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1E4), reset ? 2.0f : 0.0f); //playerGrabRegrabDelay
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1C8), reset ? 0.01999999955f : 0.0f); //playerGrabBreakTimeJumpInfluence
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x170), reset ? 1.0f : 0.0f); //forceReleaseRegrabCooldown
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x130), reset ? 75.0f : 360.0f); //breakGrabAngle
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x210), reset ? 1.0f : 0.0f); //playerGrabbeeMaxForce
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1E0), reset ? 7.0f : 0.0f); //playerGrabBreakSeparationForce
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1E8), reset ? 1.5f : 0.0f); //playerGrabbeeInvulnerabilityWindow
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x180), reset ? 10.0f : 100000000.0f); //objectGrabAdditionalForceScale
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x184), reset ? 3.0f : 100000000.0f); //objectGrabAdditionalPushForceScale
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x188), reset ? 1.0f : 0.0f); //carryPickupDuration
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x1A4), reset ? 1.0f : 0.0f); //carryAlwaysLoseTussleWhenGrabbed
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x204), reset ? 0.1f : 0.7f); //playerGrabberVelocityComponent
            //    Memory.ZwWriteFloat(processHandle, (IntPtr)(characterPtr.ToInt64() + 0x208), reset ? 0.2f : 1.0f); //playerGrabbeeVelocityComponent
            //}
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
                        processHandle = Memory.ZwOpenProcess(PROCESS_ALL_ACCESS, calcPid); //get full access to the process so we can use it later
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
                        GameAssembly_dll = Memory.ZwGetModule(processHandle, "GameAssembly.dll", isWow64Process);
                        Console.WriteLine($"Found GameAssembly.dll: {GameAssembly_dll.ToString("X")}");
                    }
                    //since we're not drawing, the rest of the code can be put here.
                    if (characterPtr == IntPtr.Zero)
                    {
                        //characterPtr = Memory.ZwFindSignatureBase(processHandle, IntPtr.Zero, IntPtr.Zero, "00 00 18 41 00 00 18 41 00 00 E0 40 CD CC 4C 3E 00 00 A0 40 00 00 E0 40 00 00 00 41 00 00 00 41 00"); //just to get the base of the character allocation data
                        //9.50 9.50 7.00 0.20 5.00 7.00 8.00 or ... 41180000 41180000 40E00000 3E4CCCCD 40A00000 40E00000 41000000
                        characterPtr = SDKUtil.ZwReadPointerChain(processHandle, (IntPtr)(GameAssembly_dll.ToInt64() + 0x028F1F40), isWow64Process, 0xB58, 0x88);
                        if (characterPtr != IntPtr.Zero)
                        {
                            Console.WriteLine($"FOUND Character Base Ptr: {characterPtr.ToString("X")}");
                        }
                        else
                        {
                            if (lastTime + 1000 < Memory.TickCount)
                            {
                                lastTime = Memory.TickCount;
                                Console.WriteLine("FAILED TO FIND Character Base Ptr :(");
                            }
                        }
                    }
                    else
                    {
                        //we are in game and got char ptr ... continue with menu modifications every tick

                        IntPtr timeToRunNextCharacterControllerDataCheckPTR = SDKUtil.ZwReadPointerChain(processHandle, (IntPtr)(GameAssembly_dll.ToInt64() + 0x2C2AD28), isWow64Process, 0xB8, 0x0, 0xC8, 0x10, 0x30, 0x1D8);
                        //Console.WriteLine(timeToRunNextCharacterControllerDataCheck.ToString("X"));
                        if (timeToRunNextCharacterControllerDataCheckPTR != IntPtr.Zero)
                        {
                            var timeToRunNextCharacterControllerDataCheck = Memory.ZwReadFloat(processHandle, (IntPtr)timeToRunNextCharacterControllerDataCheckPTR.ToInt64() + 0x10);
                            if ((timeToRunNextCharacterControllerDataCheck > 0) && (timeToRunNextCharacterControllerDataCheck < 100000000.0f))
                            {
                                Memory.ZwWriteFloat(processHandle, (IntPtr)(timeToRunNextCharacterControllerDataCheckPTR.ToInt64() + 0x10), 100000000.0f);
                            }
                            else
                            {
                                var doublecheck = Memory.ZwReadFloat(processHandle, (IntPtr)timeToRunNextCharacterControllerDataCheckPTR.ToInt64() + 0x10);
                                if (doublecheck == 100000000.0f)
                                {
                                    //DelayAction.Queue(() => writeCheatosz(processHandle, characterPtr), 5000.0f);
                                    writeCheatos(processHandle, characterPtr, false);
                                }
                            }
                        }
                        else
                        {
                            //writeCheatos(processHandle, characterPtr, true);
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
