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
using System.Runtime.InteropServices; //for StructLayout

namespace CS16
{
    class Program
    {

        [StructLayout(LayoutKind.Explicit)]
        public struct GameEntityStruct
        {
            [FieldOffset(0x2BC)]
            public Int32 messagenum;
            //[FieldOffset(0x2C0)]
            //public Vector3 Origin; //this is laggy, don't use
            [FieldOffset(0x2CC)]
            public Vector3 Angles; //not really used but whatever
            [FieldOffset(0x378)]
            public Int32 isDucking;
            [FieldOffset(0xB48)]
            public Vector3 Origin;
            [FieldOffset(0xB94)]
            public IntPtr modelPtr; //loop from 32 to 1024 ents to get weapon names on the ground
        } //cl_entity_t;//Size=0x0BB8

        public static float M_PI_F = (180.0f / Convert.ToSingle(System.Math.PI));
        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw
        public static Vector2 GameCenterPos = new Vector2(0, 0); //for crosshair and aim
        public static Vector2 AimTarg2D = new Vector2(0, 0); //for aimbot
        public static Vector3 AimTarg3D = new Vector3(0, 0, 0);
        public static IntPtr client_dll = IntPtr.Zero;
        public static IntPtr client_dll_size = IntPtr.Zero;
        public static IntPtr hw_dll = IntPtr.Zero;
        public static IntPtr hw_dll_size = IntPtr.Zero;
        public static IntPtr dwViewMatrix_Offs = IntPtr.Zero;
        public static IntPtr cl_entity_pointer = IntPtr.Zero;
        public static IntPtr hud_player_info_pointer = IntPtr.Zero;
        public static IntPtr myPungAng_Offs = IntPtr.Zero; //+0xA0 punchangles
        public static IntPtr myAimAng_Offs = IntPtr.Zero; //+4 readable ang +0x34 = writable ang
        public static IntPtr myEyePos_Offs = IntPtr.Zero;
        public static Int32 highest_messagenum = 0;
        public static Int32 myIndex = -1;
        public static byte myTeam = 0;
        public static bool isNoSteam = false;


        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }
        public static Menu AimbotMenu { get; private set; }


        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuColor CTColor = new MenuColor("ctcolor", "CT ESP Color", new SharpDX.Color(0, 0, 255));
                public static readonly MenuBool DrawAlliesEsp = new MenuBool("drawalbox", "Draw Allies ESP", true);
                public static readonly MenuColor TRColor = new MenuColor("tercolor", "Terrorist ESP Color", new SharpDX.Color(255, 0, 0));
                public static readonly MenuBool DrawBox = new MenuBool("drawbox", "Draw Box ESP", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);
                public static readonly MenuSliderBool DrawTextSize = new MenuSliderBool("drawtextsize", "Text Size", false, 14, 4, 72);
                public static readonly MenuBool DrawTextDist = new MenuBool("drawtextdist", "Draw Distance", true);
                public static readonly MenuBool DrawPlayerNames = new MenuBool("draplayername", "Draw Names", true);
            }
            public static class AimbotComponent
            {
                public static readonly MenuBool AimGlobalBool = new MenuBool("enableaim", "Enable Aimbot Features", true);
                public static readonly MenuKeyBind AimKey = new MenuKeyBind("aimkey", "Aimbot HotKey (HOLD)", VirtualKeyCode.LeftMouse, KeybindType.Hold, false);
                public static readonly MenuList AimType = new MenuList("aimtype", "Aimbot Type", new List<string>() { "Direct Engine ViewAngles", "Real Mouse Movement" }, 0);
                public static readonly MenuList AimSpot = new MenuList("aimspot", "Aimbot Spot", new List<string>() { "Aim at their Head", "Aim at their Body" }, 0);
                public static readonly MenuBool AIMRC = new MenuBool("aimrecoilcompens", "Compensate weapon recoil while aimbotting?", true);
                public static readonly MenuSlider AIMRCSPD = new MenuSlider("aimrcspeed", "Weapon Recoil Compensation Multiplier", 18, 1, 20);
                public static readonly MenuBool DrawRecoil = new MenuBool("drawrecoilpattern", "Draw Weapon Recoil Crosshair", true);
                public static readonly MenuColor RecoilColor = new MenuColor("recoilcolor", "Recoil Compensation Color", new SharpDX.Color(255, 0, 0, 255));
                public static readonly MenuBool AimAtEveryone = new MenuBool("aimeveryone", "Aim At Everyone (even teammates)", false);
                public static readonly MenuSlider AimSpeed = new MenuSlider("aimspeed", "Aimbot Speed %", 12, 1, 100);
                public static readonly MenuBool DrawAimSpot = new MenuBool("drawaimspot", "Draw Aimbot Spot", true);
                public static readonly MenuBool DrawAimTarget = new MenuBool("drawaimtarget", "Draw Aimbot Current Target", true);
                public static readonly MenuColor AimTargetColor = new MenuColor("aimtargetcolor", "Target Color", new SharpDX.Color(0x1F, 0xBE, 0xD6, 255));
                public static readonly MenuBool DrawAimFov = new MenuBool("drawaimfov", "Draw Aimbot FOV Circle", true);
                public static readonly MenuColor AimFovColor = new MenuColor("aimfovcolor", "FOV Color", new SharpDX.Color(255, 255, 255, 30));
                public static readonly MenuSlider AimFov = new MenuSlider("aimfov", "Aimbot FOV", 100, 4, 1000);
            }

        }

        public static void InitializeMenu()
        {
            VisualsMenu = new Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.CTColor,
                Components.VisualsComponent.DrawAlliesEsp.SetToolTip("Really great feature to increase performance by the way!"),
                Components.VisualsComponent.TRColor,
                Components.VisualsComponent.DrawBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
                Components.VisualsComponent.DrawTextSize,
                Components.VisualsComponent.DrawTextDist,
                Components.VisualsComponent.DrawPlayerNames,
            };

            AimbotMenu = new Menu("aimbotmenu", "Aimbot Menu")
            {
                Components.AimbotComponent.AimGlobalBool,
                Components.AimbotComponent.AimKey,
                Components.AimbotComponent.AimType,
                Components.AimbotComponent.AimSpot,
                Components.AimbotComponent.AIMRC,
                Components.AimbotComponent.AIMRCSPD,
                Components.AimbotComponent.DrawRecoil,
                Components.AimbotComponent.RecoilColor,
                Components.AimbotComponent.AimAtEveryone,
                Components.AimbotComponent.AimSpeed,
                Components.AimbotComponent.DrawAimSpot,
                Components.AimbotComponent.DrawAimTarget,
                Components.AimbotComponent.DrawAimFov,
                Components.AimbotComponent.AimFovColor,
                Components.AimbotComponent.AimFov,
            };


            RootMenu = new Menu("cs16example", "WeScript.app CS 1.6 Example Assembly", true)
            {
                Components.MainAssemblyToggle.SetToolTip("The magical boolean which completely disables/enables the assembly!"),
                VisualsMenu,
                AimbotMenu,
            };
            RootMenu.Attach();
        }

        private static byte isTerror(string modelName)
        {
            if (modelName.Contains("arc") || modelName.Contains("gue") || modelName.Contains("lee") || modelName.Contains("ter"))
            {
                return 1;
            }
            else
            {
                return 2;
            }
        }

        private static double GetDistance3D(Vector3 myPos, Vector3 enemyPos)
        {
            Vector3 vector = new Vector3(myPos.X - enemyPos.X, myPos.Y - enemyPos.Y, myPos.Z - enemyPos.Z);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        }

        private static double GetDistance2D(Vector2 pos1, Vector2 pos2)
        {
            Vector2 vector = new Vector2(pos1.X - pos2.X, pos1.Y - pos2.Y);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }


        public static Vector3 ClampAngle(Vector3 angle)
        {
            while (angle.Y > 180) angle.Y -= 360;
            while (angle.Y < -180) angle.Y += 360;

            if (angle.X > 89.0f) angle.X = 89.0f;
            if (angle.X < -89.0f) angle.X = -89.0f;

            angle.Z = 0f;

            return angle;
        }

        public static Vector3 NormalizeAngle(Vector3 angle)
        {
            while (angle.X < -180.0f) angle.X += 360.0f;
            while (angle.X > 180.0f) angle.X -= 360.0f;

            while (angle.Y < -180.0f) angle.Y += 360.0f;
            while (angle.Y > 180.0f) angle.Y -= 360.0f;

            while (angle.Z < -180.0f) angle.Z += 360.0f;
            while (angle.Z > 180.0f) angle.Z -= 360.0f;

            return angle;
        }

        public static Vector3 CalcAngle(Vector3 playerPosition, Vector3 enemyPosition, Vector3 aimPunch, Vector3 vecView, float yawRecoilReductionFactory, float pitchRecoilReductionFactor)
        {
            Vector3 delta = new Vector3(playerPosition.X - enemyPosition.X, playerPosition.Y - enemyPosition.Y, (playerPosition.Z + vecView.Z) - enemyPosition.Z);

            Vector3 tmp = Vector3.Zero;
            tmp.X = Convert.ToSingle(System.Math.Atan(delta.Z / System.Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y))) * 57.295779513082f - aimPunch.X * yawRecoilReductionFactory;
            tmp.Y = Convert.ToSingle(System.Math.Atan(delta.Y / delta.X)) * M_PI_F - aimPunch.Y * pitchRecoilReductionFactor;
            tmp.Z = 0;

            if (delta.X >= 0.0) tmp.Y += 180f;

            tmp = NormalizeAngle(tmp);
            tmp = ClampAngle(tmp);

            return tmp;
        }


        static void Main(string[] args)
        {
            Console.WriteLine("WeScript.app CS1.6 Example Assembly Loaded! (Support STEAM & NONSTEAM)");
            InitializeMenu();
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }

        public static IntPtr GetSteamNonSteamWnd()
        {
            var wndHnd = Memory.FindWindowClassName("SDL_app");
            if (wndHnd != IntPtr.Zero)
            {
                return wndHnd;
            }
            else
            {
                wndHnd = Memory.FindWindowClassName("Valve001");
                if (wndHnd != IntPtr.Zero)
                {
                    return wndHnd;
                }
            }
            return IntPtr.Zero;
        }

        private static void OnTick(int counter, EventArgs args)
        {
            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = GetSteamNonSteamWnd(); //try finding the window of the process (check if it's spawned and loaded)
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
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = GetSteamNonSteamWnd();
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    GameCenterPos = new Vector2(wndSize.X / 2 + wndMargins.X, wndSize.Y / 2 + wndMargins.Y); //even if the game is windowed, calculate perfectly it's "center" for aim or crosshair
                    isOverlayOnTop = Overlay.IsOnTop();

                    if (client_dll == IntPtr.Zero) //if the dll is still null
                    {
                        client_dll = Memory.GetModule(processHandle, "client.dll", isWow64Process); //attempt to find the module (if it's loaded)
                        if (client_dll == IntPtr.Zero) //dll is still zero?! is this some ancient NonSteam? Scan the whole game mem..
                        {
                            if (hud_player_info_pointer == IntPtr.Zero) //if offset is zero... find it
                            {
                                hud_player_info_pointer = Memory.FindSignature(processHandle, IntPtr.Zero, IntPtr.Zero, "53 55 8B 11 FF 52 10 BF", 0x8);
                                if (hud_player_info_pointer != IntPtr.Zero)
                                {
                                    isNoSteam = true; //yeah we found it in "nonSteam"
                                    Console.WriteLine($"hud_player_info_pointer: {hud_player_info_pointer.ToString("X")}");
                                }
                            }
                        }
                    }
                    else
                    {
                        if (client_dll_size == IntPtr.Zero) //dll got loaded, check if size is zero
                        {
                            client_dll_size = Memory.GetModuleSize(processHandle, "client.dll", isWow64Process); //get module size
                        }
                        else
                        {
                            if (hud_player_info_pointer == IntPtr.Zero) //if offset is zero... find it
                            {
                                hud_player_info_pointer = Memory.FindSignature(processHandle, client_dll, client_dll_size, "53 55 8B 11 FF 52 10 BF", 0x8);
                                if (hud_player_info_pointer != IntPtr.Zero) Console.WriteLine($"hud_player_info_pointer: {hud_player_info_pointer.ToString("X")}");
                            }
                        }
                    }
                    if (hw_dll == IntPtr.Zero)
                    {
                        hw_dll = Memory.GetModule(processHandle, "hw.dll", isWow64Process);
                        if (hw_dll == IntPtr.Zero) //is this ancient NonSteam? Scan the whole game mem..
                        {
                            if (dwViewMatrix_Offs == IntPtr.Zero) //if offset is zero... find it
                            {
                                dwViewMatrix_Offs = Memory.FindSignature(processHandle, IntPtr.Zero, IntPtr.Zero, "D9 05 ? ? ? ? D8 08 D9 05 ? ? ? ? D8 48 08 DE C1", 0x2);
                                if (dwViewMatrix_Offs != IntPtr.Zero) Console.WriteLine($"dwViewMatrix_Offs: {dwViewMatrix_Offs.ToString("X")}");
                            }
                            if (cl_entity_pointer == IntPtr.Zero)
                            {
                                cl_entity_pointer = Memory.FindSignature(processHandle, IntPtr.Zero, IntPtr.Zero, "B9 55 00 00 00 8D 04 80 8D 14 80 A1", 0xC);
                                if (cl_entity_pointer != IntPtr.Zero) Console.WriteLine($"cl_entity_pointer: {cl_entity_pointer.ToString("X")}");
                            }
                            if (myPungAng_Offs == IntPtr.Zero)
                            {
                                myPungAng_Offs = Memory.FindSignature(processHandle, IntPtr.Zero, IntPtr.Zero, "0F 85 ? ? ? ? 53 53 C7 05", 0xE);
                                if (myPungAng_Offs != IntPtr.Zero) Console.WriteLine($"myPungAng_Offs: {myPungAng_Offs.ToString("X")}");
                            }
                            if (myAimAng_Offs == IntPtr.Zero)
                            {
                                myAimAng_Offs = Memory.FindSignature(processHandle, IntPtr.Zero, IntPtr.Zero, "E8 ? ? ? ? B9 0D 00 00 00 BF ? ? ? ? 88 46 02", 0xB);
                                if (myAimAng_Offs != IntPtr.Zero) Console.WriteLine($"myAimAng_Offs: {myAimAng_Offs.ToString("X")}");
                            }
                            if (myEyePos_Offs == IntPtr.Zero)
                            {
                                myEyePos_Offs = Memory.FindSignature(processHandle, IntPtr.Zero, IntPtr.Zero, "51 68 CD CC 8C 3F 52 E8", 0xE);
                                if (myEyePos_Offs != IntPtr.Zero) Console.WriteLine($"myEyePos_Offs: {myEyePos_Offs.ToString("X")}");
                            }
                        }
                    }
                    else
                    {
                        if (hw_dll_size == IntPtr.Zero)
                        {
                            hw_dll_size = Memory.GetModuleSize(processHandle, "hw.dll", isWow64Process);
                        }
                        else
                        {
                            if (dwViewMatrix_Offs == IntPtr.Zero) //if offset is zero... find it
                            {
                                dwViewMatrix_Offs = Memory.FindSignature(processHandle, hw_dll, hw_dll_size, "D9 05 ? ? ? ? D8 08 D9 05 ? ? ? ? D8 48 08 DE C1", 0x2);
                                if (dwViewMatrix_Offs != IntPtr.Zero) Console.WriteLine($"dwViewMatrix_Offs: {dwViewMatrix_Offs.ToString("X")}");
                            }
                            if (cl_entity_pointer == IntPtr.Zero)
                            {
                                cl_entity_pointer = Memory.FindSignature(processHandle, hw_dll, hw_dll_size, "B9 55 00 00 00 8D 04 80 8D 14 80 A1", 0xC);
                                if (cl_entity_pointer != IntPtr.Zero) Console.WriteLine($"cl_entity_pointer: {cl_entity_pointer.ToString("X")}");
                            }
                            if (myPungAng_Offs == IntPtr.Zero)
                            {
                                myPungAng_Offs = Memory.FindSignature(processHandle, hw_dll, hw_dll_size, "0F 85 ? ? ? ? 53 53 C7 05", 0xE);
                                if (myPungAng_Offs != IntPtr.Zero) Console.WriteLine($"myPungAng_Offs: {myPungAng_Offs.ToString("X")}");
                            }
                            if (myAimAng_Offs == IntPtr.Zero)
                            {
                                myAimAng_Offs = Memory.FindSignature(processHandle, hw_dll, hw_dll_size, "E8 ? ? ? ? B9 0D 00 00 00 BF ? ? ? ? 88 46 02", 0xB);
                                if (myAimAng_Offs != IntPtr.Zero) Console.WriteLine($"myAimAng_Offs: {myAimAng_Offs.ToString("X")}");
                            }
                            if (myEyePos_Offs == IntPtr.Zero)
                            {
                                myEyePos_Offs = Memory.FindSignature(processHandle, hw_dll, hw_dll_size, "51 68 CD CC 8C 3F 52 E8", 0xE);
                                if (myEyePos_Offs != IntPtr.Zero) Console.WriteLine($"myEyePos_Offs: {myEyePos_Offs.ToString("X")}");
                            }
                        }
                    }
                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(processHandle); //close the handle to avoid leaks
                    processHandle = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;

                    //clear your offsets, modules
                    client_dll = IntPtr.Zero;
                    hw_dll = IntPtr.Zero;
                    client_dll_size = IntPtr.Zero;
                    hw_dll_size = IntPtr.Zero;
                    dwViewMatrix_Offs = IntPtr.Zero;
                    cl_entity_pointer = IntPtr.Zero;
                    myPungAng_Offs = IntPtr.Zero;
                    myAimAng_Offs = IntPtr.Zero;
                    myEyePos_Offs = IntPtr.Zero;
                    hud_player_info_pointer = IntPtr.Zero;
                    highest_messagenum = 0;
                    myIndex = -1;
                    myTeam = 0;
                    isNoSteam = false;
                }
            }
        }


        private static void OnRenderer(int fps, EventArgs args) //good sig for punch >> 0F 85 ? ? ? ? 53 53 C7 05
        {
            if (!gameProcessExists) return; //process is dead, don't bother drawing
            if ((!isGameOnTop) && (!isOverlayOnTop)) return; //if game and overlay are not on top, don't draw
            if (!Components.MainAssemblyToggle.Enabled) return; //main menu boolean to toggle the cheat on or off

            double fClosestPos = 999999;
            AimTarg2D = new Vector2(0, 0);
            AimTarg3D = new Vector3(0, 0, 0);

            if (dwViewMatrix_Offs != IntPtr.Zero)
            {
                var matrix = Memory.ReadMatrix(processHandle, (IntPtr)(dwViewMatrix_Offs.ToInt64() + 0x0));
                if (cl_entity_pointer != IntPtr.Zero)
                {
                    var myPunchAng = Memory.ReadVector3(processHandle, (IntPtr)(myPungAng_Offs.ToInt64() + 0xA0));
                    var myEyePos = Memory.ReadVector3(processHandle, (IntPtr)(myEyePos_Offs.ToInt64() + 0x0));
                    var myViewAng = Memory.ReadVector3(processHandle, (IntPtr)(myAimAng_Offs.ToInt64() + 0x4)); //+0x34 for writing
                    var cl_entity_data = Memory.ReadPointer(processHandle, cl_entity_pointer, isWow64Process);
                    if (cl_entity_data != IntPtr.Zero)
                    {
                        if (hud_player_info_pointer != IntPtr.Zero)
                        {
                            for (uint i = 1; i <= 32; i++)
                            {
                                var entityData = SDKUtil.ReadStructure<GameEntityStruct>(processHandle, (IntPtr)(cl_entity_data.ToInt64() + i * 0x0BB8));
                                if (entityData.messagenum > highest_messagenum)
                                {
                                    highest_messagenum = entityData.messagenum;
                                }
                                if (entityData.messagenum + 10 > highest_messagenum)
                                {
                                    if ((myEyePos.X + 15.0f > entityData.Origin.X) && (myEyePos.X - 15.0f < entityData.Origin.X) && (myEyePos.Y + 15.0f > entityData.Origin.Y) && (myEyePos.Y - 15.0f < entityData.Origin.Y))
                                    {
                                        myIndex = (int)i;
                                        //Console.WriteLine(myIndex);
                                    }
                                    var playerNamePtr = Memory.ReadPointer(processHandle, (IntPtr)(hud_player_info_pointer.ToInt64() + (i - 1) * (isNoSteam ? 0x014 : 0x020)), isWow64Process);
                                    var playerModelPtr = Memory.ReadPointer(processHandle, (IntPtr)(hud_player_info_pointer.ToInt64() + ((i - 1) * (isNoSteam ? 0x014 : 0x020)) + 0xC), isWow64Process);
                                    if (playerModelPtr != IntPtr.Zero)
                                    {
                                        var playerName = Memory.ReadString(processHandle, playerNamePtr, false);
                                        var playerModel = Memory.ReadString(processHandle, playerModelPtr, false);
                                        if ((int)i == myIndex)
                                        {
                                            myTeam = isTerror(playerModel);
                                            continue;
                                        }
                                        var m_iTeamNum = isTerror(playerModel);
                                        var isenemybool = (myTeam != m_iTeamNum);
                                        Vector2 vScreen_head = new Vector2(0, 0);
                                        Vector2 vScreen_feet = new Vector2(0, 0);
                                        if (Renderer.WorldToScreen(new Vector3(entityData.Origin.X, entityData.Origin.Y, entityData.Origin.Z + 26), out vScreen_head, matrix, wndMargins, wndSize, W2SType.TypeOGL))
                                        {
                                            Renderer.WorldToScreen(new Vector3(entityData.Origin.X, entityData.Origin.Y, entityData.Origin.Z - 22), out vScreen_feet, matrix, wndMargins, wndSize, W2SType.TypeOGL);
                                            //Renderer.DrawFPSBox(vScreen_head, vScreen_feet, Color.White, (entityData.isDucking == 1) ? BoxStance.crouching : BoxStance.standing, 1.0f, true, true, 0, 0, 0, 0, 12, playerName, playerModel, "", "", "");
                                            string dist_str = "";
                                            if (Components.VisualsComponent.DrawTextDist.Enabled)
                                            {
                                                double playerDist = GetDistance3D(myEyePos, entityData.Origin) / 22.0f;
                                                dist_str = $"[{playerDist.ToString("0.0")}]"; //only 1 demical number after the dot
                                            }
                                            if (Components.VisualsComponent.DrawTheVisuals.Enabled)
                                            {
                                                if ((!Components.VisualsComponent.DrawAlliesEsp.Enabled) && (!isenemybool)) continue; //skil allies
                                                Renderer.DrawFPSBox(vScreen_head, vScreen_feet, (m_iTeamNum == 2) ? Components.VisualsComponent.CTColor.Color : Components.VisualsComponent.TRColor.Color, (entityData.isDucking == 1) ? BoxStance.crouching : BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled, Components.VisualsComponent.DrawBox.Enabled, 0, 0, 0, 0, Components.VisualsComponent.DrawTextSize.Enabled ? Components.VisualsComponent.DrawTextSize.Value : 0, dist_str, Components.VisualsComponent.DrawPlayerNames.Enabled ? playerName : string.Empty, string.Empty, string.Empty, string.Empty);
                                            }
                                        }
                                        if (Components.AimbotComponent.AimGlobalBool.Enabled)
                                        {
                                            if (!Components.AimbotComponent.AimAtEveryone.Enabled)
                                            {
                                                if (!isenemybool) continue; //skip allies
                                            }
                                            Vector3 targetVec = new Vector3(0, 0, 0);
                                            switch (Components.AimbotComponent.AimSpot.Value)
                                            {
                                                case 0: //head
                                                    {
                                                        targetVec = new Vector3(entityData.Origin.X, entityData.Origin.Y, entityData.Origin.Z + 18.0f);
                                                    }
                                                    break;
                                                case 1: //body
                                                    {
                                                        targetVec = entityData.Origin;
                                                    }
                                                    break;
                                                default: //ignore default case, should never occur
                                                    break;
                                            }
                                            Vector2 vScreen_aim = new Vector2(0, 0);
                                            if (Renderer.WorldToScreen(targetVec, out vScreen_aim, matrix, wndMargins, wndSize, W2SType.TypeOGL)) //our aimpoint is on screen
                                            {
                                                if (Components.AimbotComponent.DrawAimSpot.Enabled)
                                                {
                                                    Renderer.DrawFilledRect(vScreen_aim.X - 1, vScreen_aim.Y - 1, 2, 2, new Color(255, 255, 255)); //lazy to implement aimspotcolor
                                                }
                                                var AimDist2D = GetDistance2D(vScreen_aim, GameCenterPos);
                                                if (Components.AimbotComponent.AimFov.Value < AimDist2D) continue; //ignore anything outside our fov
                                                if (AimDist2D < fClosestPos)
                                                {
                                                    fClosestPos = AimDist2D;
                                                    AimTarg2D = vScreen_aim;
                                                    AimTarg3D = targetVec;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            if (Components.AimbotComponent.AimGlobalBool.Enabled)
                            {
                                if (Components.AimbotComponent.DrawAimFov.Enabled) //draw fov circle
                                {
                                    Renderer.DrawCircle(GameCenterPos, Components.AimbotComponent.AimFov.Value, Components.AimbotComponent.AimFovColor.Color);
                                }

                                var dx = (GameCenterPos.X * 1.8f) / 90;
                                var dy = (GameCenterPos.Y * 1.8f) / 90;

                                var rx = GameCenterPos.X - (dx * ((myPunchAng.Y)));
                                var ry = GameCenterPos.Y + (dy * ((myPunchAng.X)));
                                if (Components.AimbotComponent.DrawRecoil.Enabled)
                                {
                                    Renderer.DrawFilledRect(rx - 1, ry - 1, 2, 2, Components.AimbotComponent.RecoilColor.Color);
                                }

                                if ((AimTarg2D.X != 0) && (AimTarg2D.Y != 0))//check just in case if we have aimtarg
                                {
                                    if (Components.AimbotComponent.DrawAimTarget.Enabled) //draw aim target
                                    {
                                        Renderer.DrawRect(AimTarg2D.X - 3, AimTarg2D.Y - 3, 6, 6, Components.AimbotComponent.AimTargetColor.Color);
                                    }
                                    if (Components.AimbotComponent.AimKey.Enabled)
                                    {

                                        switch (Components.AimbotComponent.AimType.Value)
                                        {
                                            case 0: //engine viewangles
                                                {
                                                    if (myAimAng_Offs != IntPtr.Zero)
                                                    {
                                                        var myAngles = Memory.ReadVector3(processHandle, (IntPtr)(myAimAng_Offs.ToInt64() + 0x4));
                                                        float recoilCompens = Components.AimbotComponent.AIMRC.Enabled ? (Components.AimbotComponent.AIMRCSPD.Value * 0.1f) : 0.0f;
                                                        var newAng = CalcAngle(myEyePos, AimTarg3D, myPunchAng, myAngles, recoilCompens, recoilCompens);
                                                        if (Components.AimbotComponent.AimSpeed.Value < 100) //smoothing only below 100%
                                                        {
                                                            float aimsmooth_ = Components.AimbotComponent.AimSpeed.Value * 0.01f;
                                                            var diff = newAng - myAngles;
                                                            diff = NormalizeAngle(diff);
                                                            diff = ClampAngle(diff);
                                                            if (diff.X > aimsmooth_)
                                                            {
                                                                newAng.X = myAngles.X + aimsmooth_;
                                                            }
                                                            if (diff.X < -aimsmooth_)
                                                            {
                                                                newAng.X = myAngles.X - aimsmooth_;
                                                            }
                                                            if (diff.Y > aimsmooth_)
                                                            {
                                                                newAng.Y = myAngles.Y + aimsmooth_;
                                                            }
                                                            if (diff.Y < -aimsmooth_)
                                                            {
                                                                newAng.Y = myAngles.Y - aimsmooth_;
                                                            }
                                                            newAng = ClampAngle(newAng); //just in case?
                                                        }
                                                        Memory.WriteVector3(processHandle, (IntPtr)(myAimAng_Offs.ToInt64() + 0x34), newAng);
                                                    }
                                                }
                                                break;
                                            case 1: //mouse event
                                                {

                                                    double DistX = 0;
                                                    double DistY = 0;
                                                    if (Components.AimbotComponent.AIMRC.Enabled)
                                                    {
                                                        DistX = (AimTarg2D.X) - rx;
                                                        DistY = (AimTarg2D.Y) - ry;
                                                    }
                                                    else
                                                    {
                                                        DistX = (AimTarg2D.X) - GameCenterPos.X;
                                                        DistY = (AimTarg2D.Y) - GameCenterPos.Y;
                                                    }
                                                    double slowDistX = DistX / (1.0f + (Math.Abs(DistX) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    double slowDistY = DistY / (1.0f + (Math.Abs(DistY) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    Input.mouse_eventWS(MouseEventFlags.MOVE, (int)slowDistX, (int)slowDistY, MouseEventDataXButtons.NONE, IntPtr.Zero);
                                                }
                                                break;
                                            default: //ignore default case, should never occur
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        highest_messagenum = 0;
                        myIndex = -1;
                        myTeam = 0;
                    }
                }
            }
        }
    }
}
